using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00012 phase 5, the rendered evidence: one frame per surface the plan gave a menu, plus
/// the icon column in both variants.
/// </summary>
/// <remarks>
/// <para>
/// A context menu is a popup with its own top level, and no snapshot in this repository had ever
/// captured one — plan 00010 shipped the pane menu with no frame at all. A window's captured
/// frame does contain it: opening one moves about 8% of the pixels, which is sixteen times the
/// comparer's half-a-percent tolerance, so unlike a chip or an arrow these frames really can fail
/// on their own. The assertions beside each capture are still the guard for what the comparer
/// cannot see — a label, an accelerator, an icon of the wrong kind.
/// </para>
/// <para>
/// Each capture opens the menu with a real right-click rather than by asking for the items,
/// because what is being evidenced is the menu on screen: its placement against the surface that
/// raised it, its column alignment, and the popup actually appearing.
/// </para>
/// </remarks>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
public sealed class MenuSnapshotTests
{
    private const string Left = """
        alpha
        one
        BETA
        gamma
        gone
        delta
        epsilon
        """;

    private const string Right = """
        alpha
        one
        beta
        gamma
        delta
        added
        epsilon
        """;

    [AvaloniaFact]
    public Task The_line_number_margins_menu()
    {
        return Capture("Light", (host, _) =>
        {
            RightClick(host, host.Left.LineNumberMargin, new Point(8, RowY(host, 3)));
            List<string?> headers = Headers(host);

            // The copy items, then the go-to the arrow in this gutter is about, then navigate and
            // find. No save and no revert: those are the file's verbs and a gutter is a position.
            Assert.Contains(DiffViewStrings.Get(DiffViewStrings.MenuGoToChange), headers);
            Assert.DoesNotContain(DiffViewStrings.MenuSave(DiffSide.Left), headers);
        });
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task The_change_marker_margins_menu(string variant)
    {
        return Capture(variant, (host, _) =>
        {
            RightClick(host, host.Left.ChangeMarkerMargin, new Point(4, RowY(host, 3)));
            List<string?> headers = Headers(host);

            // The gutter carrying both change verbs, so this is the frame where the icon column
            // holds both kinds at once: an operator for the change and an arrow for each copy.
            // Captured in both variants because the icons follow the inherited foreground rather
            // than DiffBrushes, and a swap that did not carry them would show here.
            Assert.Contains(DiffViewStrings.Get(DiffViewStrings.MenuGoToChange), headers);
            Assert.Contains(DiffViewStrings.Get(DiffViewStrings.MenuSelectChange), headers);
            Assert.Contains(DiffViewStrings.MenuCopyChange(DiffSide.Right), headers);

            // Four: an operator on each of the two change verbs and an arrow on each of the two
            // copies. Line 3 is a modified pair, so there is a kind to show — on an unchanged
            // line the two operators are null and this reads 2, which is how the first draft of
            // this capture was pointed at the wrong row.
            Assert.Equal(4, Icons(host));
        });
    }

    [AvaloniaFact]
    public Task The_connector_gutters_menu()
    {
        return Capture("Light", (host, document) =>
        {
            ChangeConnectorGutter gutter = host.View.Gutter!;
            ChangeBlock block = document.Blocks.MaxBy(b => b.RowCount)!;
            ConnectorPolygon polygon = gutter.LastPolygons[block.Index];
            double bottom = Math.Max(polygon.LeftBottom.Y, polygon.RightBottom.Y);
            RightClick(host, gutter, new Point(gutter.Bounds.Width / 2, (polygon.LeftTop.Y + bottom) / 2));

            // Both copy directions, because the column belongs to neither side, and the two verbs
            // about the block the polygon is — then plan 00013's folding group behind a
            // separator, because the column is where a run's absence is most visible.
            Assert.Equal(
                [
                    DiffViewStrings.MenuCopyChange(DiffSide.Left),
                    DiffViewStrings.MenuCopyChange(DiffSide.Right),
                    DiffViewStrings.Get(DiffViewStrings.MenuGoToChange),
                    DiffViewStrings.Get(DiffViewStrings.MenuSelectChange),
                    null,
                    DiffViewStrings.Get(DiffViewStrings.MenuShowAllRows),
                    DiffViewStrings.Get(DiffViewStrings.MenuShowDifferencesOnly),
                    DiffViewStrings.Get(DiffViewStrings.MenuShowContext),
                    DiffViewStrings.Get(DiffViewStrings.MenuExpandFold),
                ],
                Headers(host));
        });
    }

    [AvaloniaFact]
    public Task The_overview_maps_menu()
    {
        return Capture("Light", (host, _) =>
        {
            DiffMinimap map = host.View.Minimap!;
            double y = map.Bounds.Height * 0.4;
            double x = Enumerable.Range(0, (int)map.Bounds.Width)
                .Select(i => i + 0.5)
                .First(candidate => map.LaneAt(candidate) == DiffSide.Left);

            RightClick(host, map, new Point(x, y));

            // Short by nature and kept anyway: the row, the change at it, and the map's own way
            // off screen. The separator is the null header.
            Assert.Equal(
                [
                    DiffViewStrings.Get(DiffViewStrings.MenuGoToRow),
                    DiffViewStrings.Get(DiffViewStrings.MenuGoToChange),
                    null,
                    DiffViewStrings.Get(DiffViewStrings.MenuHideOverviewMap),
                ],
                Headers(host));
        });
    }

    [AvaloniaFact]
    public Task The_headers_menu()
    {
        return Capture("Light", (host, _) =>
        {
            DiffPaneHeader header = host.View.LeftHeader!;
            RightClick(host, header, new Point(header.Bounds.Width / 2, header.Bounds.Height / 2));

            // The file's two verbs and nothing else — no copy and no navigate, a header not being
            // a position — and no icons, because neither has a mark in the gutter's vocabulary.
            Assert.Equal(
                [
                    DiffViewStrings.MenuSave(DiffSide.Left),
                    DiffViewStrings.MenuRevert(DiffSide.Left),
                ],
                Headers(host));
            Assert.Equal(0, Icons(host));
        });
    }

    /// <summary>
    /// Loads the pair, runs <paramref name="open"/> to open a menu, and verifies the frame.
    /// </summary>
    /// <remarks>
    /// The window is captured after two layout passes: the click opens the popup on the first and
    /// the popup lays itself out on the second, and a frame taken between them shows an empty one.
    /// </remarks>
    private static async Task Capture(string variant, Action<CompositeHost, SideBySideDocument> open)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 760, height: 300);
            host.Show();
            await host.LoadAsync(Left, Right);

            // The connector's polygons are a frame's product, so one is rendered before a test
            // may hit-test them.
            using (WriteableBitmap _ = host.Capture())
            {
            }

            open(host, host.View.Document!);
            CompositeHost.Layout();

            Assert.NotNull(host.View.LastMenu);
            Assert.True(host.View.LastMenu.IsOpen, "the menu should be open in the frame being captured");

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            host.View.LastMenu.Close();
            CompositeHost.Layout();
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    /// <summary>The headers of the open menu's rows, a separator reading as <c>null</c>.</summary>
    private static List<string?> Headers(CompositeHost host)
    {
        Assert.NotNull(host.View.LastMenu);
        return [.. host.View.LastMenu.Items.Select(i => i is MenuItem row ? Text(row) : null)];
    }

    /// <summary>How many of the open menu's rows carry an icon.</summary>
    private static int Icons(CompositeHost host)
    {
        Assert.NotNull(host.View.LastMenu);
        return host.View.LastMenu.Items.OfType<MenuItem>().Count(i => i.Icon is not null);
    }

    /// <summary>A row's label, which is a TextBlock so it can carry the accelerator gap.</summary>
    private static string? Text(MenuItem row)
    {
        return row.Header is TextBlock block ? block.Text : row.Header?.ToString();
    }

    /// <summary>The y at the middle of the pane's line <paramref name="line"/>.</summary>
    private static double RowY(CompositeHost host, int line)
    {
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        return ((line - 0.5) * lineHeight) - host.Left.VerticalOffset;
    }

    private static void RightClick(CompositeHost host, Control control, Point inControl)
    {
        Point inWindow = control.TranslatePoint(inControl, host.Window) ?? inControl;
        host.Window.MouseDown(inWindow, MouseButton.Right);
        host.Window.MouseUp(inWindow, MouseButton.Right);
        CompositeHost.Layout();
    }
}
