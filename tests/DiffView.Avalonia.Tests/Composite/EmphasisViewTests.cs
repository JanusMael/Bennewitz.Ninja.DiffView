using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Viewer;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00035 step 3: <see cref="SideBySideDiffView.LeftEmphasis"/> and <see cref="SideBySideDiffView.RightEmphasis"/>,
/// and the same two on <see cref="DiffViewer"/>, reach each side's own pane on both of <c>AGENTS.md</c> §6's paths:
/// set after load, and set before the template applies, which is the path a host setting them in XAML takes. The
/// viewer's are held here as well, because none of the editor's coverage reaches a second control.
/// </summary>
public sealed class EmphasisViewTests
{
    private static readonly LineRange Marked = new(6, 3);
    private static readonly LineRange Other = new(0, 2);

    [AvaloniaFact]
    public async Task The_editors_ranges_set_after_load_reach_their_own_panes_and_are_shaded()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        host.View.LeftEmphasis = Marked;
        AssertPanes(host.Left, host.Right, Marked, null, "the editor, after load");
        using (WriteableBitmap _ = host.Capture())
        {
        }

        AssertShaded(host.Left, [7, 8, 9], "the editor's left pane");
        AssertShaded(host.Right, [], "the editor's right pane");

        host.View.RightEmphasis = Other;
        AssertPanes(host.Left, host.Right, Marked, Other, "the editor, after load");
        host.View.LeftEmphasis = null;
        AssertPanes(host.Left, host.Right, null, Other, "the editor, after load");
    }

    [AvaloniaFact]
    public async Task The_editors_ranges_set_before_the_template_applies_reach_their_own_panes()
    {
        using CompositeHost host = new();
        host.View.LeftEmphasis = Marked;
        host.View.RightEmphasis = Other;
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        AssertPanes(host.Left, host.Right, Marked, Other, "the editor, before the template");
    }

    [AvaloniaFact]
    public async Task The_viewers_ranges_set_after_load_reach_their_own_panes_and_are_shaded()
    {
        using ViewerHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        host.View.RightEmphasis = Other;
        AssertPanes(host.Left, host.Right, null, Other, "the viewer, after load");
        using (WriteableBitmap _ = host.Capture())
        {
        }

        AssertShaded(host.Left, [], "the viewer's left pane");
        AssertShaded(host.Right, [1, 2], "the viewer's right pane");

        host.View.LeftEmphasis = Marked;
        AssertPanes(host.Left, host.Right, Marked, Other, "the viewer, after load");
        host.View.RightEmphasis = null;
        AssertPanes(host.Left, host.Right, Marked, null, "the viewer, after load");
    }

    [AvaloniaFact]
    public async Task The_viewers_ranges_set_before_the_template_applies_reach_their_own_panes()
    {
        using ViewerHost host = new();
        host.View.LeftEmphasis = Marked;
        host.View.RightEmphasis = Other;
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        AssertPanes(host.Left, host.Right, Marked, Other, "the viewer, before the template");
    }

    [AvaloniaFact]
    public async Task The_range_is_the_hosts_so_an_edit_and_a_new_source_leave_it_where_it_was()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);
        host.View.LeftEmphasis = Marked;

        // Two lines inserted above the range move its lines down, and the range stays at the lines it named.
        host.Left.Document.Insert(0, "first\nsecond\n");
        CompositeHost.Layout();
        AssertKept(host, "an edit");

        // A new source replaces the left's document, and the range is still the host's to change.
        host.View.LeftSource = new PaneSource(right);
        await host.WaitForBuildAsync();
        AssertKept(host, "a new source");
        using (WriteableBitmap _ = host.Capture())
        {
        }

        AssertShaded(host.Left, [7, 8, 9], "the editor's left pane, after a new source");
    }

    private static void AssertKept(CompositeHost host, string after)
    {
        Assert.True(host.View.LeftEmphasis == Marked, $"after {after} the view's range is {Describe(host.View.LeftEmphasis)}");
        Assert.True(host.Left.Emphasis == Marked, $"after {after} the pane's range is {Describe(host.Left.Emphasis)}");
    }

    private static void AssertPanes(DiffPanePresenter left, DiffPanePresenter right, LineRange? expectedLeft, LineRange? expectedRight, string what)
    {
        Assert.True(
            left.Emphasis == expectedLeft && right.Emphasis == expectedRight,
            $"{what}: the left pane holds {Describe(left.Emphasis)} and the right {Describe(right.Emphasis)}, "
                + $"where the view holds {Describe(expectedLeft)} and {Describe(expectedRight)}");
    }

    /// <summary>The first lines of the visual lines the pane's last frame shaded are exactly <paramref name="expected"/>.</summary>
    private static void AssertShaded(DiffPanePresenter pane, int[] expected, string what)
    {
        int[] shaded = [.. pane.EmphasisRenderer.LastDrawn.Select(r => r.FirstLineNumber)];
        Assert.True(
            shaded.SequenceEqual(expected),
            $"{what} shaded lines [{string.Join(", ", shaded)}] where it should shade [{string.Join(", ", expected)}]");
    }

    private static string Describe(LineRange? range) => range is { } r ? $"lines {r.Start} + {r.Count}" : "nothing";
}
