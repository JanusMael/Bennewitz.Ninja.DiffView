using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00004 §Phase 3, the rendered evidence: the copy arrows in the panes' number cells, the
/// same pair read-only where every number is shown instead, and a one-sided block whose arrow
/// falls in padding — in both theme variants.
/// </summary>
/// <remarks>
/// A 12 px glyph is 0.03 % of a 900×600 frame and the comparer tolerates 0.5 %, so a redraw can
/// pass a snapshot unchanged; plan 00003 proved that the hard way. The pixel assertions beside
/// each capture are the guard, and the PNG is what a reviewer looks at.
/// </remarks>
public sealed class CopyArrowSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_copy_arrows_are_painted_in_the_panes(string variant)
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
            host.View.RightReadOnly = false;
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Color arrowColour = PresenterHost.Token("DiffView.GutterArrowBrush");

            foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
            {
                DiffLineNumberMargin margin = pane.LineNumberMargin;
                Point origin = OriginOf(margin, host);
                Assert.NotEmpty(margin.LastCopyArrows);

                int painted = 0;
                foreach ((Rect zone, _, _) in margin.LastCopyArrows)
                {
                    // The head carries about twice the shaft's area, so the heavier half of the
                    // zone is the half the arrow points at. No threshold to tune: the two halves
                    // simply cannot be equal, and flipping the glyph swaps them.
                    // Flush with the numbers' own right edge, which the arrow's reported bounds
                    // cannot show on their own.
                    Assert.Equal(margin.LastColumnRight, zone.Right, 0.5);

                    int towardsLeft = PaintedWith(frame, origin, new Rect(zone.Left, zone.Top, zone.Width / 2, zone.Height), arrowColour);
                    int towardsRight = PaintedWith(frame, origin, new Rect(zone.Center.X, zone.Top, zone.Width / 2, zone.Height), arrowColour);
                    Assert.True(towardsLeft > 0 && towardsRight > 0, $"{pane.Side}: the arrow painted {towardsLeft} and {towardsRight} pixels.");
                    if (pane.Side == DiffSide.Left)
                    {
                        Assert.True(towardsRight > towardsLeft, $"the left pane's arrow should point right; halves were {towardsLeft} and {towardsRight}.");
                    }
                    else
                    {
                        Assert.True(towardsLeft > towardsRight, $"the right pane's arrow should point left; halves were {towardsLeft} and {towardsRight}.");
                    }

                    painted += towardsLeft + towardsRight;
                }

                // Nothing else in the margin is drawn in the arrow brush, so every arrow pixel
                // belongs to a zone the margin reported.
                Assert.Equal(painted, PaintedWith(frame, origin, new Rect(margin.Bounds.Size), arrowColour));
            }
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
    public async Task A_read_only_pair_shows_every_number_instead(string variant)
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

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Color arrowColour = PresenterHost.Token("DiffView.GutterArrowBrush");

            // The same pair as the frame above, with nothing editable: not one arrow anywhere,
            // and every line the pane shows still carries its number.
            foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
            {
                DiffLineNumberMargin margin = pane.LineNumberMargin;
                Assert.Empty(margin.LastCopyArrows);
                Assert.Equal(0, PaintedWith(frame, OriginOf(margin, host), new Rect(margin.Bounds.Size), arrowColour));
                Assert.Equal(margin.LastRendered.Count, margin.LastSourceNumbers.Count(n => n.Left is not null));
            }
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
    public async Task A_one_sided_block_puts_its_arrow_in_the_padding(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 700, height: 260);
            host.Show();
            await host.LoadAsync("alpha\nbravo\ncharlie\n", "alpha\ncharlie\n");
            host.View.LeftReadOnly = false;
            host.View.RightReadOnly = false;
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            // The left owns the deleted line, so its arrow takes that line's cell; the right has
            // no line there at all, so its arrow sits in the padding and costs no number.
            (Rect leftZone, _, int? overLine) = Assert.Single(host.Left.LineNumberMargin.LastCopyArrows);
            (Rect rightZone, _, int? none) = Assert.Single(host.Right.LineNumberMargin.LastCopyArrows);
            Assert.Equal(2, overLine);
            Assert.Null(none);

            // Both are on the same row of the composed view, which is what the padding is for.
            Point leftOrigin = OriginOf(host.Left.LineNumberMargin, host);
            Point rightOrigin = OriginOf(host.Right.LineNumberMargin, host);
            Assert.Equal(leftOrigin.Y + leftZone.Top, rightOrigin.Y + rightZone.Top, 0.5);
            Assert.Equal(host.Left.LineNumberMargin.LastColumnRight, leftZone.Right, 0.5);
            Assert.Equal(host.Right.LineNumberMargin.LastColumnRight, rightZone.Right, 0.5);

            Color arrowColour = PresenterHost.Token("DiffView.GutterArrowBrush");
            Assert.True(PaintedWith(frame, rightOrigin, rightZone, arrowColour) > 20, "the padding arrow should be painted.");
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    private static Point OriginOf(Visual control, CompositeHost host)
    {
        return control.TranslatePoint(new Point(0, 0), host.Window)
               ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
    }

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
}
