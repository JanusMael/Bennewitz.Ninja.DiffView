using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

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

    /// <summary>Ink a marker must put down to be scannable; a single digit lays down about 29.</summary>
    private const int InkFloor = 20;

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
        Color gutter = PresenterHost.Token("DiffView.GutterBackgroundBrush");

        // One block of each kind, so every glyph is on screen: the left owns the deletion, the
        // right owns the insertion, and both own the modified row.
        Assert.Equal(
            [DiffLineKind.Deleted, DiffLineKind.Modified, DiffLineKind.Inserted],
            host.View.Document!.Blocks.Select(b => b.Kind));

        Dictionary<DiffLineKind, int> ink = [];
        foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
        {
            foreach ((int line, DiffLineKind kind) in pane.ChangeMarkerMargin.LastRendered)
            {
                if (ChangeMarkerMargin.GlyphFor(kind) is null)
                {
                    continue;
                }

                ink[kind] = Math.Max(ink.GetValueOrDefault(kind), Ink(host, frame, pane, line, gutter));
            }
        }

        Assert.Equal(3, ink.Count);
        foreach ((DiffLineKind kind, int painted) in ink)
        {
            Assert.True(
                painted >= InkFloor,
                $"the {kind} marker '{ChangeMarkerMargin.GlyphFor(kind)}' painted {painted} pixels, under the {InkFloor} floor");
        }
    }

    /// <summary>Pixels the marker cell of <paramref name="line"/> paints over the gutter background.</summary>
    private static int Ink(CompositeHost host, WriteableBitmap frame, DiffPanePresenter pane, int line, Color background)
    {
        ChangeMarkerMargin margin = pane.ChangeMarkerMargin;
        double rowHeight = pane.TextArea.TextView.DefaultLineHeight;
        double top = pane.TextArea.TextView.GetVisualLine(line)!.VisualTop
                     - pane.TextArea.TextView.VerticalOffset
                     + (pane.Metadata.PaddingBefore(line) * rowHeight);
        Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;

        // The bar lives at the inner edge and is not a glyph, so the probe stops short of it.
        PixelRect cell = PixelProbe.Inside(origin.X, origin.Y + top, origin.X + margin.Bounds.Width - 3, origin.Y + top + rowHeight, inset: 0);
        return PixelProbe.Count(frame, cell, c => !PresenterHost.Near(c, background, tolerance: 24));
    }
}
