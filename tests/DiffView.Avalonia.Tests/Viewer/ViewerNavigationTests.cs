using System.Windows.Input;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// Plan 00021: what the viewer keeps of navigation, none of which touches content — the four walks
/// and their commands, the overview map's jump, the connector's block click, and folding with the
/// placeholder that opens a run again.
/// </summary>
public sealed class ViewerNavigationTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaFact]
    public async Task The_walks_move_the_current_change_and_their_commands_follow_the_change_count()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using ViewerHost host = new(width: 900, height: 300);
        host.Show();

        // A bound button learns availability from CanExecuteChanged, not by polling, so the event is
        // what is counted — CanExecute alone would read correctly with the event never raised.
        ICommand[] walks = [host.View.NextChangeCommand, host.View.PreviousChangeCommand, host.View.FirstChangeCommand, host.View.LastChangeCommand];
        int[] raised = new int[walks.Length];
        for (int i = 0; i < walks.Length; i++)
        {
            int index = i;
            walks[i].CanExecuteChanged += (_, _) => raised[index]++;
        }

        Assert.All(walks, w => Assert.False(w.CanExecute(null)));
        await host.LoadAsync(left, right);
        Assert.Equal(5, host.View.ChangeCount);
        Assert.All(walks, w => Assert.True(w.CanExecute(null)));
        Assert.All(raised, count => Assert.True(count > 0, "a walk's availability moved without CanExecuteChanged"));

        SideBySideDocument document = host.View.Document!;
        host.View.NextChangeCommand.Execute(null);
        ViewerHost.Layout();
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Assert.Same(document.Blocks[0], host.Left.CurrentBlock);
        Assert.Same(document.Blocks[0], host.Right.CurrentBlock);

        host.View.LastChange();
        Assert.Equal(4, host.View.CurrentChangeIndex);
        host.View.PreviousChange();
        Assert.Equal(3, host.View.CurrentChangeIndex);
        host.View.FirstChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);
        host.View.GoToChange(2);
        Assert.Equal(2, host.View.CurrentChangeIndex);

        // Nothing to walk: every command says so, and says it through the event.
        Array.Clear(raised);
        await host.LoadAsync(left, left);
        Assert.Equal(0, host.View.ChangeCount);
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.All(walks, w => Assert.False(w.CanExecute(null)));
        Assert.All(raised, count => Assert.True(count > 0, "a walk's availability moved without CanExecuteChanged"));
    }

    [AvaloniaFact]
    public async Task A_click_on_the_map_jumps_both_panes_and_a_click_on_a_polygon_makes_its_block_current()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using ViewerHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.NotNull(host.View.Minimap);
        Assert.NotNull(host.View.Gutter);
        DiffMinimap map = host.View.Minimap;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.Equal(0, host.Left.VerticalOffset, Tolerance);
        double y = map.Bounds.Height * 0.8;
        int row = map.RowForClick(y);
        Click(host, map, new Point(map.Bounds.Width / 2, y));
        Assert.True(host.Left.VerticalOffset > 0, "the click should have scrolled the panes");
        Assert.Equal(host.Left.VerticalOffset, host.Right.VerticalOffset, Tolerance);
        double centre = host.Left.VerticalOffset + (host.Left.ViewportHeight / 2);
        double bottom = host.Left.ExtentHeight - host.Left.ViewportHeight;
        Assert.True(
            host.Left.VerticalOffset >= bottom - Tolerance || Math.Abs(centre - ((row + 0.5) * lineHeight)) <= lineHeight,
            $"row {row} should be centred, or the panes at the bottom");

        // Back at the top, where the connector draws the blocks it culls once they scroll away.
        host.View.ScrollToRow(0);
        host.Capture().Dispose();
        ChangeConnectorGutter gutter = host.View.Gutter;
        ConnectorPolygon polygon = gutter.LastPolygons.MaxBy(p => Math.Max(p.LeftBottom.Y, p.RightBottom.Y) - p.LeftTop.Y)
                                   ?? throw new InvalidOperationException("The connector drew no polygon.");
        Point inside = new(gutter.Bounds.Width / 2, (polygon.LeftTop.Y + Math.Max(polygon.LeftBottom.Y, polygon.RightBottom.Y)) / 2);
        Assert.True(polygon.Contains(inside));
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Click(host, gutter, inside);
        Assert.Equal(polygon.BlockIndex, host.View.CurrentChangeIndex);
        Assert.Equal(polygon.BlockIndex, gutter.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task The_option_folds_both_panes_alike_and_a_placeholder_click_gives_a_run_back()
    {
        using ViewerHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        double unfolded = host.Left.ExtentHeight;
        host.View.UnchangedContextRows = 0;
        ViewerHost.Layout();
        int folds = host.Left.CollapsedSectionCount;
        Assert.True(folds > 1, "the fixture should fold more than one run");
        Assert.True(host.Left.ExtentHeight < unfolded);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);

        // A real press on the first placeholder, at its own visual column.
        TextView textView = host.Left.TextArea.TextView;
        VisualLine visualLine = textView.VisualLines.First(v => v.Elements.OfType<FoldPlaceholderElement>().Any());
        FoldPlaceholderElement element = visualLine.Elements.OfType<FoldPlaceholderElement>().Single();
        Point inText = visualLine.GetVisualPosition(element.VisualColumn, VisualYPosition.TextMiddle) - textView.ScrollOffset;
        Point inWindow = textView.TranslatePoint(inText + new Vector(4, 0), host.Window)
                         ?? throw new InvalidOperationException("The text view is not in the window.");
        host.Window.MouseDown(inWindow, MouseButton.Left);
        host.Window.MouseUp(inWindow, MouseButton.Left);
        ViewerHost.Layout();

        Assert.Equal(folds - 1, host.Left.CollapsedSectionCount);
        Assert.Equal(folds - 1, host.Right.CollapsedSectionCount);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);

        host.View.UnchangedContextRows = null;
        ViewerHost.Layout();
        Assert.Equal(0, host.Left.CollapsedSectionCount);
        Assert.Equal(unfolded, host.Left.ExtentHeight, Tolerance);
    }

    private static void Click(ViewerHost host, Visual control, Point inControl)
    {
        Point inWindow = control.TranslatePoint(inControl, host.Window)
                         ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
        host.Window.MouseDown(inWindow, MouseButton.Left);
        host.Window.MouseUp(inWindow, MouseButton.Left);
        ViewerHost.Layout();
    }
}
