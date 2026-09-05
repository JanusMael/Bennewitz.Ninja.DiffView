using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

public sealed class DiffSearchTests
{
    // Left:            Right:
    // 0 foo bar        0 foo bar          unchanged
    // 1 the Foo        1 the fooBar       modified
    // 2 gone foo       -                  deleted
    // 3 last           2 last             unchanged  (keeps the deleted and inserted rows apart)
    // -                3 xfoo foo         inserted
    // 4 tail           4 tail             unchanged
    private const string Left = "foo bar\nthe Foo\ngone foo\nlast\ntail";
    private const string Right = "foo bar\nthe fooBar\nlast\nxfoo foo\ntail";

    private static readonly SideBySideDocument Document = DiffDocumentBuilder.Build(Left, Right).Document;
    private static readonly StringPaneText LeftText = new(Left);
    private static readonly StringPaneText RightText = new(Right);

    private static FindResult Find(string query, FindOptions? options = null, CancellationToken cancellationToken = default)
    {
        return DiffSearch.Find(Document, LeftText, RightText, query, options, cancellationToken);
    }

    [Fact]
    public void The_fixture_aligns_as_drawn()
    {
        Assert.Equal(
            [
                new AlignedRow(0, 0, DiffLineKind.Unchanged),
                new AlignedRow(1, 1, DiffLineKind.Modified),
                new AlignedRow(2, null, DiffLineKind.Deleted),
                new AlignedRow(3, 2, DiffLineKind.Unchanged),
                new AlignedRow(null, 3, DiffLineKind.Inserted),
                new AlignedRow(4, 4, DiffLineKind.Unchanged),
            ],
            Document.Rows);
    }

    [Fact]
    public void Both_scope_orders_by_row_then_left_before_right_then_column()
    {
        FindResult result = Find("foo");

        Assert.Null(result.Error);
        Assert.False(result.Truncated);
        Assert.Equal(
            [
                new FindMatch(DiffSide.Left, 0, 0, 3),
                new FindMatch(DiffSide.Right, 0, 0, 3),
                new FindMatch(DiffSide.Left, 1, 4, 3),   // "Foo", case-insensitive by default
                new FindMatch(DiffSide.Right, 1, 4, 3),
                new FindMatch(DiffSide.Left, 2, 5, 3),   // the deleted row: left only
                new FindMatch(DiffSide.Right, 3, 1, 3),  // the inserted row: right only, two hits
                new FindMatch(DiffSide.Right, 3, 5, 3),
            ],
            result.Matches);
        Assert.Equal(3, result.LeftCount);
        Assert.Equal(4, result.RightCount);
    }

    [Fact]
    public void Scope_filters_to_one_side()
    {
        FindResult left = Find("foo", new FindOptions { Scope = FindScope.Left });
        Assert.All(left.Matches, m => Assert.Equal(DiffSide.Left, m.Side));
        Assert.Equal(3, left.LeftCount);
        Assert.Equal(0, left.RightCount);

        FindResult right = Find("foo", new FindOptions { Scope = FindScope.Right });
        Assert.All(right.Matches, m => Assert.Equal(DiffSide.Right, m.Side));
        Assert.Equal(4, right.RightCount);
    }

    [Fact]
    public void Match_case_and_whole_word_apply_at_line_boundaries()
    {
        FindResult matchCase = Find("foo", new FindOptions { MatchCase = true });
        Assert.DoesNotContain(matchCase.Matches, m => m.Side == DiffSide.Left && m.Line == 1);

        FindResult wholeWord = Find("foo", new FindOptions { WholeWord = true });
        Assert.Equal(
            [
                new FindMatch(DiffSide.Left, 0, 0, 3),    // at the start of a line
                new FindMatch(DiffSide.Right, 0, 0, 3),
                new FindMatch(DiffSide.Left, 1, 4, 3),    // at the end of a line
                new FindMatch(DiffSide.Left, 2, 5, 3),
                new FindMatch(DiffSide.Right, 3, 5, 3),   // "xfoo" and "fooBar" are not whole words
            ],
            wholeWord.Matches);
    }

    [Fact]
    public void Changed_rows_only_skips_unchanged_rows()
    {
        FindResult result = Find("foo", new FindOptions { ChangedRowsOnly = true });

        Assert.DoesNotContain(result.Matches, m => m.Line == 0);
        Assert.Equal(5, result.Matches.Count); // rows 1, 2 and 4; the unchanged rows 0, 3 and 5 are skipped
    }

    [Fact]
    public void Regex_search_supports_whole_word_and_case_and_reports_a_bad_pattern()
    {
        FindResult regex = Find("fo+", new FindOptions { UseRegex = true, MatchCase = true, WholeWord = true });
        Assert.Null(regex.Error);
        Assert.Equal([new FindMatch(DiffSide.Left, 0, 0, 3), new FindMatch(DiffSide.Right, 0, 0, 3), new FindMatch(DiffSide.Left, 2, 5, 3), new FindMatch(DiffSide.Right, 3, 5, 3)], regex.Matches);

        FindResult bad = Find("fo(o", new FindOptions { UseRegex = true });
        Assert.StartsWith("Invalid regular expression", bad.Error);
        Assert.Empty(bad.Matches);

        // A pattern with a backreference cannot run non-backtracking; it still works with the timeout engine.
        FindResult backreference = Find(@"(o)\1", new FindOptions { UseRegex = true });
        Assert.Null(backreference.Error);
        Assert.Equal(7, backreference.Matches.Count);
    }

    [Fact]
    public void A_pattern_that_forces_catastrophic_backtracking_hits_the_timeout()
    {
        // The lookahead keeps the pattern off the non-backtracking engine; (a+)+$ on a run of a's
        // followed by b is exponential in the backtracking one.
        string line = new string('a', 40) + "b";
        SideBySideDocument document = DiffDocumentBuilder.Build(line, line).Document;
        FindOptions options = new() { UseRegex = true, MatchTimeout = TimeSpan.FromMilliseconds(50) };

        FindResult result = DiffSearch.Find(document, new StringPaneText(line), new StringPaneText(line), "(?=a)(a+)+$", options);

        Assert.NotNull(result.Error);
        Assert.Contains("took longer than 50 ms", result.Error);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void More_hits_than_the_cap_truncates_in_order()
    {
        FindResult result = Find("foo", new FindOptions { MaxMatches = 3 });

        Assert.True(result.Truncated);
        Assert.Equal(3, result.Matches.Count);
        Assert.Equal([new FindMatch(DiffSide.Left, 0, 0, 3), new FindMatch(DiffSide.Right, 0, 0, 3), new FindMatch(DiffSide.Left, 1, 4, 3)], result.Matches);
        Assert.Equal(2, result.LeftCount);
        Assert.Equal(1, result.RightCount);
    }

    [Fact]
    public void An_empty_query_is_an_empty_result_and_cancellation_throws()
    {
        Assert.Same(FindResult.Empty, Find(string.Empty));
        Assert.Same(FindResult.Empty, Find(null!));

        using CancellationTokenSource cts = new();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Find("foo", null, cts.Token));
    }

    [Fact]
    public void A_pane_text_shorter_than_the_document_is_tolerated()
    {
        // The editor's snapshot may lag a rebuild by a keystroke; a line past its end is skipped, never thrown on.
        FindResult result = DiffSearch.Find(Document, new StringPaneText("foo"), new StringPaneText("foo"), "foo");

        Assert.Equal([new FindMatch(DiffSide.Left, 0, 0, 3), new FindMatch(DiffSide.Right, 0, 0, 3)], result.Matches);
    }
}
