using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Inline;

/// <summary>
/// Plan 00001 §Phase 11, find: the same engine over the same two sides as the side-by-side view,
/// its matches addressed by the line of the unified document they are on, and the scope control
/// gone — there being one pane to search.
/// </summary>
public sealed class InlineFindTests
{
    private const string Needle = "Greeter";

    [AvaloniaFact]
    public async Task The_scope_control_is_gone_and_the_scope_stays_both()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        DiffFindBar bar = host.View.FindBar ?? throw new InvalidOperationException("no find bar");

        Assert.False(bar.ShowScope);
        Assert.Equal(FindScope.Both, host.View.FindOptions.Scope);

        // A host that asks for one side gets both anyway: the pane shows lines of each.
        host.View.FindOptions = host.View.FindOptions with { Scope = FindScope.Left, MatchCase = true };
        Assert.Equal(FindScope.Both, host.View.FindOptions.Scope);
        Assert.True(host.View.FindOptions.MatchCase);
        Assert.Equal(FindScope.Both, bar.Scope);

        // The strip's find lane carries the count alone; there is no scope to name.
        host.View.OpenFind();
        await host.FindAsync(Needle);
        Assert.Equal("find 3 matches", host.View.StatusStrip!.FindText);
    }

    [AvaloniaFact]
    public async Task The_matches_are_the_side_by_side_view_own_minus_the_context_lines_it_shows_twice()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost inline = new(width: 900, height: 400);
        using CompositeHost sideBySide = new(width: 900, height: 400);
        inline.Show();
        sideBySide.Show();
        await inline.LoadAsync(left, right);
        await sideBySide.LoadAsync(left, right);

        inline.View.OpenFind();
        sideBySide.View.OpenFind();
        await inline.FindAsync(Needle);
        await sideBySide.FindAsync(Needle);

        // The side-by-side view finds the needle on both lines of an unchanged row; the unified
        // view shows that row once, as the left line, so the right twin has nowhere to land.
        InlineDocument table = inline.View.Inline!;
        List<FindMatch> expected = [];
        foreach (FindMatch match in sideBySide.View.FindResult!.Matches)
        {
            if (table.LineOf(match.Side, match.Line) is { } line)
            {
                expected.Add(match with { Line = line });
            }
        }

        expected.Sort(static (a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.Column.CompareTo(b.Column));
        Assert.Equal(4, sideBySide.View.FindResult.Matches.Count);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, inline.View.FindResult!.Matches);
        Assert.Equal(2, inline.View.FindResult.LeftCount);
        Assert.Equal(1, inline.View.FindResult.RightCount);
        Assert.Equal(inline.View.FindResult.Matches, inline.Pane.SearchMatches);

        // Every match is on the line it says, and the text under it is the query.
        foreach (FindMatch match in inline.View.FindResult.Matches)
        {
            string line = inline.View.PaneDocument.GetText(inline.View.PaneDocument.GetLineByNumber(match.Line + 1));
            Assert.Equal(Needle, line.Substring(match.Column, match.Length));
            Assert.Equal(match.Side, table.Lines[match.Line].Side);
        }
    }

    [AvaloniaFact]
    public async Task Over_changed_rows_only_the_two_views_find_exactly_the_same_matches()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost inline = new(width: 900, height: 400);
        using CompositeHost sideBySide = new(width: 900, height: 400);
        inline.Show();
        sideBySide.Show();
        await inline.LoadAsync(left, right);
        await sideBySide.LoadAsync(left, right);

        inline.View.FindOptions = inline.View.FindOptions with { ChangedRowsOnly = true };
        sideBySide.View.FindOptions = sideBySide.View.FindOptions with { ChangedRowsOnly = true };
        inline.View.OpenFind();
        sideBySide.View.OpenFind();
        await inline.FindAsync(Needle);
        await sideBySide.FindAsync(Needle);

        // No context row is searched, so nothing is dropped: the same count, the same sides.
        Assert.Equal(sideBySide.View.FindResult!.Matches.Count, inline.View.FindResult!.Matches.Count);
        Assert.Equal(sideBySide.View.FindResult.LeftCount, inline.View.FindResult.LeftCount);
        Assert.Equal(sideBySide.View.FindResult.RightCount, inline.View.FindResult.RightCount);
        Assert.Equal(
            sideBySide.View.FindResult.Matches.Select(m => (m.Side, m.Column, m.Length)).Order(),
            inline.View.FindResult.Matches.Select(m => (m.Side, m.Column, m.Length)).Order());
    }

    [AvaloniaFact]
    public async Task F3_walks_the_matches_down_the_pane_selecting_each_in_turn()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        await host.FindAsync(Needle);

        IReadOnlyList<FindMatch> matches = host.View.FindResult!.Matches;
        Assert.Equal(3, matches.Count);
        Assert.Equal(-1, host.View.CurrentFindMatchIndex);
        Assert.Null(host.Pane.CurrentSearchMatch);

        int previousLine = -1;
        for (int i = 0; i < matches.Count; i++)
        {
            Press(host, Key.F3, PhysicalKey.F3);
            Assert.Equal(i, host.View.CurrentFindMatchIndex);
            Assert.Equal(matches[i], host.Pane.CurrentSearchMatch);
            Assert.Equal(Needle, host.Pane.SelectedText);
            Assert.True(host.Pane.TextArea.IsFocused);
            Assert.Equal($"match {i + 1} of 3 (L 2 · R 1)", host.View.FindBar!.CountText);

            // The walk never goes backwards on screen.
            Assert.True(matches[i].Line > previousLine, "the matches are in line order");
            previousLine = matches[i].Line;
        }

        // Past the last one it wraps, as it does in the side-by-side view.
        Press(host, Key.F3, PhysicalKey.F3);
        Assert.Equal(0, host.View.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public async Task An_invalid_regular_expression_shows_the_error_inline_and_the_state_stays_ready()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        host.View.FindOptions = host.View.FindOptions with { UseRegex = true };
        await host.FindAsync("([unclosed");

        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.NotNull(host.View.FindResult!.Error);
        Assert.Equal(host.View.FindResult.Error, host.View.FindBar!.ErrorText);
        Assert.Empty(host.Pane.SearchMatches);
        Assert.Null(host.View.FindBar.CountText);
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_bar_drops_the_highlights_and_returns_focus_to_the_pane()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        // The bindings are the control's, and a key reaches them through the focused pane.
        host.Pane.TextArea.Focus();
        InlineHost.Layout();

        Press(host, Key.F, PhysicalKey.F, RawInputModifiers.Control);
        Assert.True(host.View.IsFindBarOpen);
        Assert.True(host.View.FindBar!.QueryBox!.IsFocused);
        await host.FindAsync(Needle);
        Assert.NotEmpty(host.Pane.SearchMatches);

        Press(host, Key.Escape, PhysicalKey.Escape);
        Assert.False(host.View.IsFindBarOpen);
        Assert.False(host.View.FindBar.IsVisible);
        Assert.True(host.Pane.TextArea.IsFocused);
        Assert.Empty(host.Pane.SearchMatches);
        Assert.Null(host.View.FindResult);
        Assert.Null(host.View.StatusStrip!.FindText);
    }

    private static void Press(InlineHost host, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        host.Window.KeyPress(key, modifiers, physical, null);
        host.Window.KeyRelease(key, modifiers, physical, null);
        InlineHost.Layout();
    }
}
