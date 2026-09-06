using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 8, find: the bar's keys and focus, the walk over the matches in row-then-side
/// order, the scope, a bad pattern, the cap, and the worker's isolation from the live documents.
/// </summary>
public sealed class FindTests
{
    private const string Needle = "Greeter";

    [AvaloniaFact]
    public async Task Ctrl_F_opens_the_bar_with_focus_in_the_query_box_and_Escape_closes_it_and_returns_focus()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        DiffFindBar bar = host.View.FindBar ?? throw new InvalidOperationException("no find bar");

        Assert.False(host.View.IsFindBarOpen);
        Assert.False(bar.IsVisible);
        Assert.Null(host.View.StatusStrip!.FindText);

        // A one-line selection in the focused pane pre-fills the query.
        host.Right.TextArea.Focus();
        CompositeHost.Layout();
        DocumentLine classLine = LineContaining(host.Right.Document, Needle);
        host.Right.Select(classLine.Offset + host.Right.Document.GetText(classLine).IndexOf(Needle, StringComparison.Ordinal), Needle.Length);
        Assert.Equal(DiffSide.Right, host.View.FocusedSide);

        Press(host, Key.F, RawInputModifiers.Control, PhysicalKey.F);
        Assert.True(host.View.IsFindBarOpen);
        Assert.True(bar.IsVisible);
        Assert.True(bar.QueryBox!.IsFocused);
        Assert.Equal(Needle, host.View.FindQuery);
        Assert.Equal(Needle, bar.Query);
        Assert.Equal("find · both", host.View.StatusStrip.FindText);

        await host.WaitForFindAsync();
        Assert.Equal(4, host.View.FindResult!.Matches.Count);
        Assert.Equal("4 matches (L 2 · R 2)", bar.CountText);
        Assert.Equal("find 4 matches · both", host.View.StatusStrip.FindText);
        Assert.Equal(2, host.Left.SearchMatches.Count);
        Assert.Equal(2, host.Right.SearchMatches.Count);
        Assert.NotNull(host.View.Minimap!.MatchRows);

        // Escape closes it, drops the highlights, and hands focus back to the pane that had it.
        Press(host, Key.Escape, RawInputModifiers.None, PhysicalKey.Escape);
        Assert.False(host.View.IsFindBarOpen);
        Assert.False(bar.IsVisible);
        Assert.True(host.Right.TextArea.IsFocused);
        Assert.Empty(host.Left.SearchMatches);
        Assert.Empty(host.Right.SearchMatches);
        Assert.Null(host.View.Minimap.MatchRows);
        Assert.Null(host.View.StatusStrip.FindText);
        Assert.Null(host.View.FindResult);
    }

    [AvaloniaFact]
    public async Task In_both_scope_F3_walks_the_matches_in_row_then_side_order_and_the_pane_holding_one_has_it_selected()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        await host.FindAsync(Needle);

        // Independent expectation: walk the alignment table, left line before right line in a row.
        List<FindMatch> expected = Expected(host.View.Document!, left, right, Needle);
        Assert.Equal([
            new FindMatch(DiffSide.Left, 4, 24, 7),   // the class line, an unchanged row
            new FindMatch(DiffSide.Right, 5, 24, 7),
            new FindMatch(DiffSide.Left, 8, 15, 7),   // the constructor, a modified row
            new FindMatch(DiffSide.Right, 10, 15, 7),
        ], expected);
        Assert.Equal(expected, host.View.FindResult!.Matches);
        Assert.Equal(-1, host.View.CurrentFindMatchIndex);
        Assert.Null(host.Left.CurrentSearchMatch);

        for (int i = 0; i < expected.Count; i++)
        {
            Press(host, Key.F3, RawInputModifiers.None, PhysicalKey.F3);
            FindMatch match = expected[i];
            DiffPanePresenter pane = match.Side == DiffSide.Left ? host.Left : host.Right;
            DiffPanePresenter other = match.Side == DiffSide.Left ? host.Right : host.Left;
            Assert.Equal(i, host.View.CurrentFindMatchIndex);
            Assert.True(pane.TextArea.IsFocused, $"match {i}: the {match.Side} pane should have focus");
            Assert.Equal(Needle, pane.SelectedText);
            Assert.Equal(match, pane.CurrentSearchMatch);
            Assert.Null(other.CurrentSearchMatch);
            Assert.Equal($"match {i + 1} of 4 (L 2 · R 2)", host.View.FindBar!.CountText);
            Assert.Equal($"find {i + 1} of 4 · both", host.View.StatusStrip!.FindText);

            // Both panes are scrolled together: the rows are aligned.
            Assert.Equal(host.Left.VerticalOffset, host.Right.VerticalOffset, 1e-6);
        }

        // The walk wraps at the end, and backwards from the first.
        Press(host, Key.F3, RawInputModifiers.None, PhysicalKey.F3);
        Assert.Equal(0, host.View.CurrentFindMatchIndex);
        Press(host, Key.F3, RawInputModifiers.Shift, PhysicalKey.F3);
        Assert.Equal(3, host.View.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public async Task Switching_the_scope_re_runs_the_search_and_the_counts_and_highlights_follow()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        await host.FindAsync(Needle);
        DiffFindBar bar = host.View.FindBar!;
        Assert.Equal(FindScope.Both, bar.Scope);
        Assert.Equal(2, host.View.FindResult!.LeftCount);
        Assert.Equal(2, host.View.FindResult.RightCount);

        bar.Scope = FindScope.Left;
        await host.WaitForFindAsync();
        Assert.Equal(FindScope.Left, host.View.FindOptions.Scope);
        Assert.Equal(2, host.View.FindResult!.LeftCount);
        Assert.Equal(0, host.View.FindResult.RightCount);
        Assert.Equal(2, host.Left.SearchMatches.Count);
        Assert.Empty(host.Right.SearchMatches);
        Assert.Equal("2 matches (L 2 · R 0)", bar.CountText);
        Assert.Equal("find 2 matches · left", host.View.StatusStrip!.FindText);
        using (WriteableBitmap _ = host.Capture())
        {
            Assert.NotEmpty(host.Left.SearchRenderer.LastRectangles);
            Assert.Empty(host.Right.SearchRenderer.LastRectangles);
        }

        bar.Scope = FindScope.Right;
        await host.WaitForFindAsync();
        Assert.Empty(host.Left.SearchMatches);
        Assert.Equal(2, host.Right.SearchMatches.Count);
        Assert.Equal("find 2 matches · right", host.View.StatusStrip.FindText);
        using (WriteableBitmap _ = host.Capture())
        {
            Assert.Empty(host.Left.SearchRenderer.LastRectangles);
            Assert.NotEmpty(host.Right.SearchRenderer.LastRectangles);
        }

        bar.Scope = FindScope.Both;
        await host.WaitForFindAsync();
        Assert.Equal(4, host.View.FindResult!.Matches.Count);
        Assert.Equal(2, host.Left.SearchMatches.Count);
        Assert.Equal(2, host.Right.SearchMatches.Count);

        // A toggle takes the same route: the option reaches the search.
        bar.MatchCase = true;
        await host.WaitForFindAsync();
        Assert.True(host.View.FindOptions.MatchCase);
        bar.Query = "greeter";
        await host.WaitForFindAsync();
        Assert.Empty(host.View.FindResult!.Matches);
        Assert.Equal("no matches", bar.CountText);
        Assert.Equal("find no matches · both", host.View.StatusStrip.FindText);
    }

    [AvaloniaFact]
    public async Task An_invalid_regular_expression_shows_the_error_inline_leaves_no_highlights_and_the_state_stays_ready()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        await host.FindAsync(Needle);
        Assert.NotEmpty(host.Left.SearchMatches);

        host.View.FindBar!.UseRegex = true;
        await host.FindAsync("Greet(er");

        Assert.NotNull(host.View.FindResult);
        Assert.StartsWith("Invalid regular expression", host.View.FindResult!.Error);
        Assert.Equal(host.View.FindResult.Error, host.View.FindBar.ErrorText);
        Assert.Null(host.View.FindBar.CountText);
        Assert.Empty(host.Left.SearchMatches);
        Assert.Empty(host.Right.SearchMatches);
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.Equal(-1, host.View.CurrentFindMatchIndex);

        // The failure is logged under the Find category, and the pattern is not: it may be a
        // piece of the document, because Ctrl+F pre-fills the query from the selection.
        Assert.Contains(host.Logs.Records, r => r.Category == DiffViewLogCategories.Find && r.Level == LogLevel.Warning);
        Assert.DoesNotContain(host.Logs.Records, r => r.Everything.Contains("Greet(er", StringComparison.Ordinal));

        // A pattern that compiles clears the error and the matches come back.
        await host.FindAsync("Greet(er)?");
        Assert.Null(host.View.FindBar.ErrorText);
        Assert.NotEmpty(host.Left.SearchMatches);
    }

    [AvaloniaFact]
    public async Task More_hits_than_the_cap_truncate_with_a_notice_and_the_search_runs_off_the_UI_thread()
    {
        // 10,000 lines a side, every one holding the needle: 20,000 hits against a 10,000 cap.
        (string left, string right) = LargeFixture(10_000);
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.True(host.View.Document!.Rows.Count > host.View.FindWorkerRowThreshold);

        bool? ranOnUiThread = null;
        Func<SideBySideDocument, IPaneText, IPaneText, string, FindOptions, CancellationToken, FindResult> real = host.View.Searcher;
        host.View.Searcher = (document, paneLeft, paneRight, query, options, token) =>
        {
            ranOnUiThread = Dispatcher.UIThread.CheckAccess();
            return real(document, paneLeft, paneRight, query, options, token);
        };

        host.View.OpenFind();
        await host.FindAsync("alpha");

        Assert.False(ranOnUiThread);
        Assert.True(host.View.FindResult!.Truncated);
        Assert.Equal(host.View.FindOptions.MaxMatches, host.View.FindResult.Matches.Count);
        Assert.Equal("Showing the first 10,000 matches", host.View.FindBar!.NoticeText);
        Assert.Equal(StatusKind.Warning, host.View.Status.Kind);
        Assert.Equal("Showing the first 10,000 matches", host.View.Status.Text);

        // The control still lays out and renders, and the walk works over the capped matches.
        using WriteableBitmap frame = host.Capture();
        Assert.NotEmpty(host.Left.SearchRenderer.LastRectangles);
        host.View.FindNext();
        CompositeHost.Layout();
        Assert.Equal(0, host.View.CurrentFindMatchIndex);
    }

    [AvaloniaFact]
    public async Task The_search_completes_while_the_UI_thread_holds_a_document_in_an_update()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        using ManualResetEventSlim gate = new();
        Func<SideBySideDocument, IPaneText, IPaneText, string, FindOptions, CancellationToken, FindResult> real = host.View.Searcher;
        host.View.FindWorkerRowThreshold = 0;
        host.View.Searcher = (document, paneLeft, paneRight, query, options, token) =>
        {
            gate.Wait(token);
            return real(document, paneLeft, paneRight, query, options, token);
        };

        host.View.OpenFind();
        host.View.FindQuery = Needle;
        host.Time.Advance(SideBySideDiffView.FindDebounce);
        CompositeHost.Layout();
        Task find = host.View.CurrentFind ?? throw new InvalidOperationException("the search did not start");
        Assert.False(find.IsCompleted);

        // The worker reads its own snapshot: the live document is held open on this thread, and
        // touching it from the worker would throw from TextDocument.VerifyAccess.
        using (host.Left.Document.RunUpdate())
        {
            gate.Set();
            await find;
        }

        CompositeHost.Layout();
        Assert.Null(host.View.FindResult!.Error);
        Assert.Equal(4, host.View.FindResult.Matches.Count);
        Assert.Equal(2, host.Left.SearchMatches.Count);
    }

    [AvaloniaFact]
    public async Task Match_highlights_sit_above_the_row_fill_and_below_the_selection()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        await host.FindAsync(Needle);

        Color background = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        Color match = PresenterHost.Token("DiffView.FindMatchBrush");
        Color current = PresenterHost.Token("DiffView.FindCurrentMatchBrush");
        Color selection = PresenterHost.Token("DiffView.SelectionBrush");

        // The class line is an unchanged row, so the only fill under the match is the pane's own.
        using (WriteableBitmap frame = host.Capture())
        {
            SearchRectangle drawn = Assert.Single(host.Left.SearchRenderer.LastRectangles, r => r.LineNumber == 5);
            Assert.False(drawn.IsCurrent);
            Assert.True(Count(host, frame, drawn.Rect, PresenterHost.Composite(match, background)) > 0, "the match should carry the match brush over the pane background");
        }

        // The current match is drawn in its own brush, with the selection over it.
        host.View.CurrentFindMatchIndex = 0;
        using (WriteableBitmap frame = host.Capture())
        {
            SearchRectangle drawn = Assert.Single(host.Left.SearchRenderer.LastRectangles, r => r.LineNumber == 5);
            Assert.True(drawn.IsCurrent);
            Assert.Equal(Needle, host.Left.SelectedText);
            Color expected = PresenterHost.Composite(selection, PresenterHost.Composite(current, background));
            Assert.True(Count(host, frame, drawn.Rect, expected) > 0, "the current match should carry the selection over the current-match brush");
        }
    }

    /// <summary>Pixels inside a text-view rectangle that carry <paramref name="expected"/>.</summary>
    private static int Count(CompositeHost host, WriteableBitmap frame, Rect rect, Color expected)
    {
        Point origin = host.Left.TextArea.TextView.TranslatePoint(new Point(0, 0), host.Window)
                       ?? throw new InvalidOperationException("the text view is not in the window");
        PixelRect area = PixelProbe.Inside(origin.X + rect.Left, origin.Y + rect.Top, origin.X + rect.Right, origin.Y + rect.Bottom);
        return PixelProbe.Count(frame, area, c => PresenterHost.Near(c, expected));
    }

    /// <summary>The matches a row-then-side walk over the model finds, computed without the search engine.</summary>
    private static List<FindMatch> Expected(SideBySideDocument document, string left, string right, string needle)
    {
        string[] leftLines = LineSplitter.Split(left);
        string[] rightLines = LineSplitter.Split(right);
        List<FindMatch> expected = [];
        foreach (AlignedRow row in document.Rows)
        {
            Collect(DiffSide.Left, row.LeftLine, leftLines);
            Collect(DiffSide.Right, row.RightLine, rightLines);
        }

        return expected;

        void Collect(DiffSide side, int? line, string[] lines)
        {
            if (line is not { } index)
            {
                return;
            }

            for (int at = lines[index].IndexOf(needle, StringComparison.Ordinal); at >= 0; at = lines[index].IndexOf(needle, at + 1, StringComparison.Ordinal))
            {
                expected.Add(new FindMatch(side, index, at, needle.Length));
            }
        }
    }

    private static DocumentLine LineContaining(TextDocument document, string needle)
    {
        foreach (DocumentLine line in document.Lines)
        {
            if (document.GetText(line).Contains(needle, StringComparison.Ordinal))
            {
                return line;
            }
        }

        throw new InvalidOperationException($"no line holds {needle}");
    }

    /// <summary>A pair of <paramref name="lines"/>-line sides, every line holding "alpha", a few differing.</summary>
    private static (string Left, string Right) LargeFixture(int lines)
    {
        StringBuilder left = new();
        StringBuilder right = new();
        for (int i = 0; i < lines; i++)
        {
            left.Append(CultureInfo.InvariantCulture, $"alpha {i} left\n");
            right.Append(CultureInfo.InvariantCulture, $"alpha {i} {(i % 500 == 0 ? "changed" : "left")}\n");
        }

        return (left.ToString(), right.ToString());
    }

    private static void Press(CompositeHost host, Key key, RawInputModifiers modifiers, PhysicalKey physical)
    {
        host.Window.KeyPress(key, modifiers, physical, null);
        host.Window.KeyRelease(key, modifiers, physical, null);
        CompositeHost.Layout();
    }
}
