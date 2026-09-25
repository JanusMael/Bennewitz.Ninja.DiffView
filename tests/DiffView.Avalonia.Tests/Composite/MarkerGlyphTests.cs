using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// The change-marker vocabulary and the weight it is drawn at. The glyph is what carries a row's
/// kind when colour cannot, so how much ink it puts on the page is the property under test, not a
/// detail of it: the ASCII pair it replaced failed exactly there.
/// </summary>
public sealed class MarkerGlyphTests
{
    private const string Left = """
        namespace Sample;

        public sealed class Greeter
        {
            private void Legacy()
            {
            }

            public string Greet(string name)
            {
                return "Hello, " + name;
            }

            public void Log(string name)
            {
            }
        }
        """;

    private const string Right = """
        namespace Sample;

        public sealed class Greeter
        {
            public string Greet(string name)
            {
                return "Hi there, " + name;
            }

            public void Log(string name)
            {
                Console.WriteLine(name);
            }
        }
        """;

    /// <summary>
    /// Ink a marker must lay down to be scannable, read as coverage — see <see cref="PixelProbe.Coverage"/>.
    /// What CI measured, Linux / Windows / macOS: '−' 9.96 / 10.71 / 7.38, '+' 18.72 / 20.08 / 17.97,
    /// '≠' 28.49 / 29.38 / 26.71, and a line-number digit 17.5 / 16.6 / 22.4 for scale. The floor sits
    /// midway between the lightest of them, macOS's '−' at 7.38, and a middle dot '·', which lays down
    /// 4.79 here — room either side for another rasterizer.
    /// </summary>
    private const double InkFloor = 6.0;

    [AvaloniaFact]
    public void The_vocabulary_is_one_family_of_operators()
    {
        // A minus sign and a not-equal, not a hyphen and a tilde.
        Assert.Equal("+", ChangeMarkerMargin.GlyphFor(DiffLineKind.Inserted));
        Assert.Equal("−", ChangeMarkerMargin.GlyphFor(DiffLineKind.Deleted));
        Assert.Equal("≠", ChangeMarkerMargin.GlyphFor(DiffLineKind.Modified));
        Assert.Null(ChangeMarkerMargin.GlyphFor(DiffLineKind.Unchanged));
    }

    [AvaloniaFact]
    public async Task Every_marker_is_heavy_enough_to_scan()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(Left, Right);

        using WriteableBitmap frame = host.Capture();

        // One block of each kind, so every glyph is on screen: the left owns the deletion, the
        // right owns the insertion, and both own the modified row.
        Assert.Equal(
            [DiffLineKind.Deleted, DiffLineKind.Modified, DiffLineKind.Inserted],
            host.View.Document!.Blocks.Select(b => b.Kind));

        Dictionary<DiffLineKind, double> ink = [];
        foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
        {
            foreach ((int line, DiffLineKind kind) in pane.ChangeMarkerMargin.LastRendered)
            {
                if (ChangeMarkerMargin.GlyphFor(kind) is null)
                {
                    continue;
                }

                ink[kind] = Math.Max(ink.GetValueOrDefault(kind), Ink(host, frame, pane, line, kind));
            }
        }

        Assert.Equal(3, ink.Count);
        foreach ((DiffLineKind kind, double painted) in ink)
        {
            Assert.True(
                painted >= InkFloor,
                FormattableString.Invariant($"the {kind} marker '{ChangeMarkerMargin.GlyphFor(kind)}' laid down {painted:0.00} of ink, under the {InkFloor} floor"));
        }
    }

    /// <summary>The ink the marker in <paramref name="line"/>'s cell lays over its chip, read as coverage.</summary>
    private static double Ink(CompositeHost host, WriteableBitmap frame, DiffPanePresenter pane, int line, DiffLineKind kind)
    {
        ChangeMarkerMargin margin = pane.ChangeMarkerMargin;
        double rowHeight = pane.TextArea.TextView.DefaultLineHeight;
        double top = pane.TextArea.TextView.GetVisualLine(line)!.VisualTop
                     - pane.TextArea.TextView.VerticalOffset
                     + (pane.Metadata.PaddingBefore(line) * rowHeight);
        Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;

        // The bar lives at the inner edge and is not a glyph, so the probe stops short of it. ⛔ Coverage,
        // not a count of pixels past a threshold, which measures how one rasterizer rounds a thin stroke:
        // it read the same '−' as 25 pixels on Linux, 18 on Windows and 16 on macOS.
        PixelRect cell = PixelProbe.Inside(origin.X, origin.Y + top, origin.X + margin.Bounds.Width - 3, origin.Y + top + rowHeight, inset: 0);
        return PixelProbe.Coverage(
            frame, cell, PresenterHost.Token($"DiffView.MarkerChip{kind}Brush"), PresenterHost.Token($"DiffView.Marker{kind}Brush")).Mass;
    }
}
