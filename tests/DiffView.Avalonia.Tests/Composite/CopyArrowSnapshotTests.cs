using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

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
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
[LinuxBaseline]
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
            // The arrow is outlined and filled since plan 00006, so its ink is both colours: a
            // count of the outline alone is the 1 px edge and almost nothing.
            Color outline = PresenterHost.Token("DiffView.GutterArrowBrush");
            Color fill = PresenterHost.Token("DiffView.GutterArrowFillBrush");
            Color gutter = PresenterHost.Token("DiffView.GutterBackgroundBrush");

            foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
            {
                DiffLineNumberMargin margin = pane.LineNumberMargin;
                Point origin = OriginOf(margin, host);
                Assert.NotEmpty(margin.LastCopyArrows);

                int painted = 0;
                foreach ((Rect zone, _, _) in margin.LastCopyArrows)
                {
                    // Flush with the numbers' own right edge, which the arrow's reported bounds
                    // cannot show on their own.
                    Assert.Equal(margin.LastColumnRight, zone.Right, 0.5);

                    int ink = Glyph(frame, origin, zone, gutter);
                    Assert.True(ink > 30, $"{pane.Side}: the arrow painted {ink} pixels.");

                    // The head's back edge is the widest part of the glyph, so the column carrying
                    // the most ink is on the side the arrow points at, whichever way it faces.
                    // Weighing the two halves stopped working when the arrow gained an outline:
                    // the shaft's long edges put ink on the tail side and the head's interior is
                    // eaten by its own border, which reversed the comparison.
                    int heaviest = HeaviestColumn(frame, origin, zone, fill);
                    if (pane.Side == DiffSide.Left)
                    {
                        Assert.True(heaviest > zone.Width / 2, $"the left pane's arrow should point right; its heaviest column is {heaviest} of {zone.Width}.");
                    }
                    else
                    {
                        Assert.True(heaviest < zone.Width / 2, $"the right pane's arrow should point left; its heaviest column is {heaviest} of {zone.Width}.");
                    }

                    painted += PaintedWith(frame, origin, zone, outline);
                }

                // Nothing else in the margin is drawn in the arrow brush, so every arrow pixel
                // belongs to a zone the margin reported.
                Assert.Equal(painted, PaintedWith(frame, origin, new Rect(margin.Bounds.Size), outline));
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

            Assert.True(
                Glyph(frame, rightOrigin, rightZone, PresenterHost.Token("DiffView.GutterBackgroundBrush")) > 30,
                "the padding arrow should be painted.");
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

    /// <summary>
    /// The column of <paramref name="zone"/> carrying the most of the arrow's <em>fill</em>, which
    /// is on the side the arrow points at.
    /// </summary>
    /// <remarks>
    /// The fill and not the whole glyph: the silhouette is symmetric — a tip inset one pixel plus
    /// a five-pixel head puts the head's back edge six from either end of a twelve-pixel zone — so
    /// the widest column of the outline is the same whichever way the arrow faces. The pen offsets
    /// the fill towards the head's interior, and that is the asymmetry worth measuring.
    /// </remarks>
    private static int HeaviestColumn(WriteableBitmap frame, Point origin, Rect zone, Color fill)
    {
        int heaviest = -1;
        int most = -1;
        for (int dx = 0; dx < (int)zone.Width; dx++)
        {
            int ink = PaintedWith(frame, origin, new Rect(zone.Left + dx, zone.Top, 1, zone.Height), fill);
            if (ink > most)
            {
                most = ink;
                heaviest = dx;
            }
        }

        return heaviest;
    }

    /// <summary>
    /// Everything painted over the gutter inside <paramref name="area"/>. An outlined glyph is
    /// mostly neither of its two colours exactly — the edge between them is anti-aliased — so
    /// "not the background" is what measures the shape, and matching a token measures a colour.
    /// </summary>
    private static int Glyph(WriteableBitmap frame, Point origin, Rect area, Color gutter)
    {
        PixelRect probe = PixelProbe.Inside(
            origin.X + area.Left, origin.Y + area.Top, origin.X + area.Right, origin.Y + area.Bottom, inset: 0);
        return PixelProbe.Count(frame, probe, c => !PresenterHost.Near(c, gutter, tolerance: 24));
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
