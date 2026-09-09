using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00003, the rendered evidence: the copy arrows on both edges of the connector gutter, and
/// the marks an edit leaves — the modified-since-load bar down the marker margin, the header's
/// "Unsaved" word and the strip's lane — in both theme variants.
/// </summary>
/// <remarks>
/// Phases 4 and 5 assert what the gutter and the margin <em>recorded</em>; nothing asserted that
/// either was painted, or where. The snapshot comparer tolerates half a percent of differing
/// pixels and neither a 12 px arrow nor a 2 px bar is that much of a 900×600 frame, so the pixel
/// assertions beside each capture are what holds the geometry — the PNG is the thing a reviewer
/// can look at without running the demo.
/// </remarks>
public sealed class EditingSnapshotTests
{
    /// <summary>Enough of a pixel to matter, in layout units.</summary>
    private const double Tolerance = 0.5;

    /// <summary>A line of the left fixture far from the edited one, which should carry no bar.</summary>
    private const int CleanLine = 5;

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_copy_arrows_are_painted_on_both_edges_of_the_gutter(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            (string left, string right) = CompositeHost.SmallFixture();
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();
            await host.LoadAsync(left, right);

            // Both sides editable, so every visible block offers both directions at once.
            host.View.LeftReadOnly = false;
            host.View.RightReadOnly = false;
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            ChangeConnectorGutter gutter = host.View.Gutter!;
            Point origin = OriginOf(gutter, host.Window);
            Color arrowColour = PresenterHost.Token("DiffView.GutterArrowBrush");
            Assert.NotEmpty(gutter.LastArrows);

            int inZones = 0;
            foreach (IGrouping<int, (Rect Bounds, int BlockIndex, DiffSide ToSide)> block in gutter.LastArrows.GroupBy(a => a.BlockIndex))
            {
                Rect toLeft = Assert.Single(block, a => a.ToSide == DiffSide.Left).Bounds;
                Rect toRight = Assert.Single(block, a => a.ToSide == DiffSide.Right).Bounds;

                // Each arrow sits on its own edge of the column, level with the other, and neither
                // runs into it: at a 12 px arrow in a 24 px column the two exactly fill it and
                // touch, which is the thing the snapshot is here to show.
                Assert.Equal(0d, toLeft.Left, Tolerance);
                Assert.Equal(gutter.Bounds.Width, toRight.Right, Tolerance);
                Assert.Equal(toLeft.Center.Y, toRight.Center.Y, Tolerance);
                Assert.True(toLeft.Right <= toRight.Left + Tolerance, $"block {block.Key}: the arrows overlap, {toLeft} and {toRight}.");

                int leftPixels = PaintedWith(frame, origin, toLeft, arrowColour);
                int rightPixels = PaintedWith(frame, origin, toRight, arrowColour);
                Assert.True(leftPixels > 20, $"block {block.Key}: the leftward arrow painted {leftPixels} pixels.");
                Assert.True(rightPixels > 20, $"block {block.Key}: the rightward arrow painted {rightPixels} pixels.");
                inZones += leftPixels + rightPixels;
            }

            // Nothing else in the column is drawn in the arrow brush, so every arrow pixel in the
            // frame belongs to a zone the gutter reported — an arrow drawn somewhere else would
            // move too few pixels for the comparer to notice.
            Assert.Equal(inZones, PaintedWith(frame, origin, new Rect(gutter.Bounds.Size), arrowColour));
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_marks_an_edit_leaves_are_painted_and_named(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            (string left, string right) = CompositeHost.SmallFixture();
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();
            await host.LoadAsync(left, right);

            host.View.LeftReadOnly = false;
            CompositeHost.Layout();
            host.Left.TextArea.Focus();
            host.Left.TextArea.Caret.Offset = 0;
            CompositeHost.Layout();
            host.Window.KeyTextInput("X");
            CompositeHost.Layout();

            // The precondition first: a test that asserts the marks without asserting the edit
            // landed cannot fail.
            Assert.StartsWith("Xusing System;", host.Left.Document.Text, StringComparison.Ordinal);
            await host.WaitForReDiffAsync();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
            Assert.Contains(1, margin.LastModified);
            Assert.Empty(host.Right.ChangeMarkerMargin.LastModified);

            Point origin = OriginOf(margin, host.Window);
            Color barColour = PresenterHost.Token("DiffView.ModifiedSinceLoadBrush");
            double width = margin.Bounds.Width;
            (double top, double bottom) = TextBandOf(host.Left, 1);
            (double cleanTop, double cleanBottom) = TextBandOf(host.Left, CleanLine);

            // The bar runs down the margin's inner edge, beside the diff's own glyph rather than
            // over it, on the line this session edited and on no other.
            int barPixels = PaintedWith(frame, origin, new Rect(width - 3, top, 3, bottom - top), barColour);
            Assert.True(barPixels > 0, "the edited line should carry the bar at the margin's inner edge.");
            Assert.Equal(0, PaintedWith(frame, origin, new Rect(0, top, width / 2, bottom - top), barColour));
            Assert.Equal(0, PaintedWith(frame, origin, new Rect(0, cleanTop, width, cleanBottom - cleanTop), barColour));

            // The edited side says so in its header, the other does not, and the strip names it.
            DiffPaneHeader header = host.View.LeftHeader!;
            Assert.True(header.IsDirty);
            Assert.False(host.View.RightHeader!.IsDirty);
            Assert.Contains("Left", host.View.StatusStrip!.DirtyText!, StringComparison.Ordinal);

            TextBlock word = header.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Dirty");
            Assert.True(word.IsVisible, "the header's dirty word should be visible while the side is dirty.");
            Assert.Equal(DiffViewStrings.Get(DiffViewStrings.HeaderDirty), word.Text);

            int wordPixels = PaintedWith(frame, OriginOf(word, host.Window), new Rect(word.Bounds.Size), PresenterHost.Token("DiffView.StatusWarningForegroundBrush"));
            Assert.True(wordPixels > 5, $"the header's dirty word painted {wordPixels} pixels.");
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    /// <summary>A control's top-left corner in the window's coordinates, for pixel sampling.</summary>
    private static Point OriginOf(Visual control, Window window)
    {
        return control.TranslatePoint(new Point(0, 0), window)
               ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
    }

    /// <summary>Pixels inside <paramref name="area"/> — control coordinates — that carry <paramref name="colour"/>.</summary>
    private static int PaintedWith(WriteableBitmap frame, Point origin, Rect area, Color colour)
    {
        PixelRect probe = PixelProbe.Inside(
            origin.X + area.Left,
            origin.Y + area.Top,
            origin.X + area.Right,
            origin.Y + area.Bottom,
            inset: 0);
        return PixelProbe.Count(frame, probe, c => PresenterHost.Near(c, colour));
    }

    /// <summary>A line's own text band in its pane's text-view coordinates: past any padding above it.</summary>
    private static (double Top, double Bottom) TextBandOf(DiffPanePresenter pane, int lineNumber)
    {
        TextView view = pane.TextArea.TextView;
        VisualLine line = view.GetVisualLine(lineNumber)
                          ?? throw new InvalidOperationException($"Line {lineNumber} is not visible.");
        TextLine text = line.TextLines[0];
        double top = line.GetTextLineVisualYPosition(text, VisualYPosition.TextTop) - view.VerticalOffset;
        return (top, top + text.Height);
    }
}
