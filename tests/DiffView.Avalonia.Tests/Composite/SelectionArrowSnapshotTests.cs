using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00006 §Phase 3, the rendered evidence: both arrows in one frame, a selection whose rows
/// are padding on the other side, and the cell both arrows want — in both theme variants and both
/// palettes, because the shape cue is the colour-blind palette's and appears in no other frame.
/// </summary>
/// <remarks>
/// The comparer tolerates 0.5 % of a frame and a 12 px glyph is 0.03 % of one, so a PNG cannot be
/// the guard for anything at this scale; the pixel assertions beside each capture are, and the PNG
/// is what a reviewer looks at. See <c>AGENTS.md</c> §5.
/// </remarks>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
public sealed class SelectionArrowSnapshotTests
{
    private static readonly Uri BaseUri = new("avares://DiffView.Avalonia.Tests/");

    [AvaloniaTheory]
    [InlineData("Light", "Default")]
    [InlineData("Dark", "Default")]
    [InlineData("Light", "ColorBlind")]
    [InlineData("Dark", "ColorBlind")]
    public async Task Both_arrows_are_painted_in_one_frame(string variant, string palette)
    {
        MemoryStream png = await RenderAsync(
            variant,
            palette,
            async host =>
            {
                await host.LoadAsync("alpha\nBRAVO\ncharlie\ndelta\necho\nFOXTROT\ngolf\n", "alpha\nbravo\ncharlie\ndelta\necho\nfoxtrot\ngolf\n");
                host.View.LeftReadOnly = false;
                host.View.RightReadOnly = false;
                CompositeHost.Layout();

                // Lines 3 and 4, which are unchanged and so are nobody's anchor row: the
                // selection's arrow and both block arrows are then on three different rows.
                host.Left.Select(12, 13);
            },
            (host, frame) =>
            {
                DiffLineNumberMargin left = host.Left.LineNumberMargin;
                AssertTheTwoArrowsAreToldApart(host, frame);

                // Two blocks, both still offering their own arrows on the rows they anchor,
                // and the selection's on the row it starts on — three arrows, three rows.
                Assert.NotNull(left.LastSelectionArrow);
                Assert.Equal(3, left.LastSelectionArrow.Value.OverLine);
                Assert.Equal([2, 6], left.LastCopyArrows.Select(a => a.OverLine).OfType<int>().Order());
                Assert.DoesNotContain(left.LastCopyArrows, a => a.Bounds.Top == left.LastSelectionArrow!.Value.Bounds.Top);

                // The selection is the left pane's, so the right pane has block arrows and no
                // selection arrow: a selection has one owner.
                Assert.Equal(2, host.Right.LineNumberMargin.LastCopyArrows.Count);
                Assert.Null(host.Right.LineNumberMargin.LastSelectionArrow);
            });

        await Verifier.Verify(png, "png").UseParameters(variant, palette);
    }

    [AvaloniaTheory]
    [InlineData("Light", "Default")]
    [InlineData("Dark", "Default")]
    [InlineData("Light", "ColorBlind")]
    [InlineData("Dark", "ColorBlind")]
    public async Task A_selection_spanning_padding_is_painted(string variant, string palette)
    {
        MemoryStream png = await RenderAsync(
            variant,
            palette,
            async host =>
            {
                await host.LoadAsync("alpha\nbravo\ncharlie\ndelta\necho\n", "alpha\nbravo\necho\n");
                host.View.LeftReadOnly = false;
                host.View.RightReadOnly = false;
                CompositeHost.Layout();

                // Lines 2 to 4: it starts on an unchanged line and runs over the two the right
                // side has no lines for at all, so most of what it covers is padding over there.
                host.Left.Select(6, 19);
            },
            (host, frame) =>
            {
                AssertTheTwoArrowsAreToldApart(host, frame);

                // The selection starts an unchanged line above the block, so both of the left
                // pane's arrows are on screen and neither is standing in for the other.
                DiffLineNumberMargin left = host.Left.LineNumberMargin;
                Assert.NotNull(left.LastSelectionArrow);
                Assert.Equal(2, left.LastSelectionArrow.Value.OverLine);
                (_, _, int? anchored) = Assert.Single(left.LastCopyArrows);
                Assert.Equal(3, anchored);

                // The block's rows are padding on the right, so its arrow costs no number there,
                // and the selection runs over those same rows.
                (_, _, int? none) = Assert.Single(host.Right.LineNumberMargin.LastCopyArrows);
                Assert.Null(none);

                SideBySideDocument model = host.View.Document!;
                ChangeBlock block = Assert.Single(model.Blocks);
                Assert.True(block.LinesFor(DiffSide.Right).IsEmpty, "the right side should have no lines in the block");
                Assert.Equal(new LineRange(2, 2), block.LinesFor(DiffSide.Left));
                Assert.Equal(new LineRange(1, 3), host.Left.SelectedLines);
            });

        await Verifier.Verify(png, "png").UseParameters(variant, palette);
    }

    [AvaloniaTheory]
    [InlineData("Light", "Default")]
    [InlineData("Dark", "Default")]
    [InlineData("Light", "ColorBlind")]
    [InlineData("Dark", "ColorBlind")]
    public async Task The_selection_arrow_takes_the_block_s_cell(string variant, string palette)
    {
        MemoryStream png = await RenderAsync(
            variant,
            palette,
            async host =>
            {
                await host.LoadAsync("alpha\nBRAVO\ncharlie\nDELTA\necho\n", "alpha\nbravo\ncharlie\ndelta\necho\n");
                host.View.LeftReadOnly = false;
                host.View.RightReadOnly = false;
                CompositeHost.Layout();

                // Line 2, which is the first block's anchor row: the two arrows want one cell.
                host.Left.Select(6, 5);
            },
            (host, frame) =>
            {
                AssertTheTwoArrowsAreToldApart(host, frame);

                // The right pane is the control frame: two blocks, two block arrows. The left has
                // one, because the selection took the other's cell — and its own arrow is level
                // with the right pane's on that row, which is what makes the swap legible.
                DiffLineNumberMargin left = host.Left.LineNumberMargin;
                DiffLineNumberMargin right = host.Right.LineNumberMargin;
                Assert.Equal(2, right.LastCopyArrows.Count);
                (_, int index, int? anchored) = Assert.Single(left.LastCopyArrows);
                Assert.Equal(1, index);
                Assert.Equal(4, anchored);

                Assert.NotNull(left.LastSelectionArrow);
                (Rect selection, int overLine) = left.LastSelectionArrow.Value;
                Assert.Equal(2, overLine);
                (Rect counterpart, _, _) = Assert.Single(right.LastCopyArrows, a => a.BlockIndex == 0);
                Assert.Equal(
                    OriginOf(left, host).Y + selection.Top,
                    OriginOf(right, host).Y + counterpart.Top,
                    0.5);
            });

        await Verifier.Verify(png, "png").UseParameters(variant, palette);
    }

    /// <summary>
    /// What every frame here has to show: one selection arrow in the left margin, painted in its
    /// own fill, carrying none of the block arrows' and leaving none of its own in theirs — and no
    /// pixel of its fill anywhere else in the margin, which its own reported bounds cannot say.
    /// </summary>
    private static void AssertTheTwoArrowsAreToldApart(CompositeHost host, WriteableBitmap frame)
    {
        DiffLineNumberMargin margin = host.Left.LineNumberMargin;
        Point origin = OriginOf(margin, host);
        Assert.NotNull(margin.LastSelectionArrow);
        Rect selection = margin.LastSelectionArrow.Value.Bounds;

        Color selectionFill = PresenterHost.Token("DiffView.SelectionArrowFillBrush");
        Color blockFill = PresenterHost.Token("DiffView.GutterArrowFillBrush");
        Color gutter = PresenterHost.Token("DiffView.GutterBackgroundBrush");

        // Where a number would be, and painted.
        Assert.Equal(margin.LastColumnRight, selection.Right, 0.5);
        int ink = Glyph(frame, origin, selection, gutter);
        Assert.True(ink > 30, $"the selection arrow painted {ink} pixels.");

        int inZone = PaintedWith(frame, origin, selection, selectionFill);
        Assert.True(inZone > 5, $"the selection arrow painted {inZone} pixels of its own fill.");
        Assert.Equal(0, PaintedWith(frame, origin, selection, blockFill));

        foreach ((Rect zone, int index, _) in margin.LastCopyArrows)
        {
            int block = PaintedWith(frame, origin, zone, blockFill);
            Assert.True(block > 5, $"block {index}'s arrow painted {block} pixels of its own fill.");
            Assert.Equal(0, PaintedWith(frame, origin, zone, selectionFill));
        }

        // One arrow's worth of selection fill in the whole margin: a glyph drawn anywhere else
        // would report the place it strayed to, so the margin is measured rather than the zone.
        Assert.Equal(inZone, PaintedWith(frame, origin, new Rect(margin.Bounds.Size), selectionFill));
    }

    private static async Task<MemoryStream> RenderAsync(
        string variant,
        string palette,
        Func<CompositeHost, Task> arrange,
        Action<CompositeHost, WriteableBitmap> inspect)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        ResourceInclude? colourBlind = palette == "ColorBlind"
            ? new ResourceInclude(BaseUri) { Source = DiffViewResources.ColorBlindTokensUri }
            : null;
        if (colourBlind is not null)
        {
            app.Resources.MergedDictionaries.Add(colourBlind);
        }

        MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 700, height: 320);
            host.Show();
            await arrange(host);

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            inspect(host, frame);
        }
        finally
        {
            // Restored before the caller awaits Verify: that continuation may run off the UI thread.
            if (colourBlind is not null)
            {
                app.Resources.MergedDictionaries.Remove(colourBlind);
            }

            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        return png;
    }

    private static Point OriginOf(Visual control, CompositeHost host)
    {
        return control.TranslatePoint(new Point(0, 0), host.Window)
               ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
    }

    /// <summary>
    /// Everything painted over the gutter inside <paramref name="area"/>. An outlined glyph is
    /// mostly neither of its colours exactly — the edge between them is anti-aliased — so "not the
    /// background" is what measures the shape, and matching a token measures a colour.
    /// </summary>
    private static int Glyph(WriteableBitmap frame, Point origin, Rect area, Color gutter)
    {
        return Count(frame, origin, area, c => !PresenterHost.Near(c, gutter, tolerance: 24));
    }

    private static int PaintedWith(WriteableBitmap frame, Point origin, Rect area, Color colour)
    {
        return Count(frame, origin, area, c => PresenterHost.Near(c, colour));
    }

    private static int Count(WriteableBitmap frame, Point origin, Rect area, Func<Color, bool> predicate)
    {
        PixelRect probe = PixelProbe.Inside(
            origin.X + area.Left, origin.Y + area.Top, origin.X + area.Right, origin.Y + area.Bottom, inset: 0);
        return PixelProbe.Count(frame, probe, predicate);
    }
}
