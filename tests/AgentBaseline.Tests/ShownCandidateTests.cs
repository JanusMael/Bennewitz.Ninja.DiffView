using System.Text;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00036 steps 3 and 4: the shown candidate. A candidate's paragraph holding the words of one of the accumulator's
/// is shown in that one's lines, a paragraph that differs from its counterpart in words is shown in the counterpart's
/// lines with each break moved to the matching word, and a re-break that would change what the text is, is not made.
/// </summary>
/// <remarks>
/// Fixtures are joined with <c>"\n"</c> explicitly, as <see cref="ParagraphBreaksTests"/>' are, so no fixture's line
/// endings depend on a checkout.
/// </remarks>
public sealed class ShownCandidateTests
{
    /// <summary>The seed as JANUSSER6 writes its rules: wrapped near 100 columns.</summary>
    private static readonly string Seed = Doc(
        "# Working rules",
        "",
        "## Commits",
        "",
        "Every commit is one reviewable change, and its message says why the change was made rather than",
        "what the diff already shows. A summary that needs an and usually wants splitting in two.",
        "",
        "- Branch before committing when the checkout is on the default branch, and never commit straight",
        "  to it.",
        "- Leave unrelated changes out of the commit.",
        "",
        "## Scripts",
        "",
        "A script that a repository keeps is written to a file and run from there, never inlined into a",
        "shell command, because every layer a command passes through claims its own quoting.",
        "",
        "## Reviews",
        "",
        "A review reads the whole change before it comments on any line of it, and says what it would do",
        "instead wherever it objects.",
        "");

    /// <summary>
    /// The same rules as JanusHalo writes them, before ingest unwraps them: one list item holding the whole document,
    /// and a paragraph a line. Its Scripts sit under another heading, and one word of its Reviews is its own.
    /// </summary>
    private static readonly string Candidate = InOneListItem(
        "# Working rules",
        "",
        "## Commits",
        "",
        "Every commit is one reviewable change, and its message says why the change was made rather than what the diff already shows. A summary that needs an and usually wants splitting in two.",
        "",
        "- Branch before committing when the checkout is on the default branch, and never commit straight to it.",
        "- Leave unrelated changes out of the commit.",
        "",
        "## Shell scripts",
        "",
        "A script that a repository keeps is written to a file and run from there, never inlined into a shell command, because every layer a command passes through claims its own quoting.",
        "",
        "## Reviews",
        "",
        "A review reads the whole change before it comments on any part of it, and says what it would do instead wherever it objects.",
        "");

    private static string Doc(params string[] lines) => string.Join("\n", lines);

    /// <summary>
    /// JanusHalo's shape, as <see cref="CorpusTests"/> builds it: a marker on the first line, and two spaces on every
    /// other but the empty one the last line ending leaves.
    /// </summary>
    private static string InOneListItem(params string[] lines) =>
        string.Join(
            "\n",
            lines.Select((line, i) => i == 0 ? "- " + line : i == lines.Length - 1 && line.Length == 0 ? line : "  " + line));

    [Fact]
    public void Every_block_equal_in_words_to_one_of_the_accumulators_shows_in_its_lines()
    {
        IngestedDocument candidate = IngestedDocument.FromBytes(Encoding.UTF8.GetBytes(Candidate));

        // ⛔ The premise: ingest took the list item off, so what is re-broken is the text the diff compares.
        Assert.Equal(1, candidate.UnwrappedLevels);

        ShownCandidate shown = ShownCandidate.Of(candidate.Text, Seed);

        // Every block holding the seed's words is in the seed's lines, the list item's second line indented to its
        // text and the Scripts paragraph under its other heading too; and so is the Reviews paragraph, one word of
        // which is its own, in the lines of the seed's, its counterpart. The heading, whose words are its own, is as
        // the candidate wrote it.
        string expected = Seed
            .Replace("## Scripts", "## Shell scripts", StringComparison.Ordinal)
            .Replace("on any line of it", "on any part of it", StringComparison.Ordinal);
        Assert.Equal(expected, shown.Text);
    }

    [Fact]
    public void A_paragraph_whose_re_break_would_open_a_list_is_shown_as_written_and_the_others_are_re_broken()
    {
        // ⭐ The accumulator breaks before a dash that its indent keeps in the paragraph. Above the first heading and
        // under one, the two places the check reads.
        string accumulator = Doc(
            "Pass the flags",
            "    - v and q when the tool should run quietly.",
            "",
            "## Flags",
            "",
            "Every other paragraph is broken",
            "where the accumulator breaks it.",
            "",
            "Pass the flags",
            "    - v and q when the tool should run quietly.",
            "",
            "And so is this one,",
            "broken here.",
            "");
        string written = "Pass the flags - v and q when the tool should run quietly.";
        string candidate = Doc(
            written,
            "",
            "## Flags",
            "",
            "Every other paragraph is broken where the accumulator breaks it.",
            "",
            written,
            "",
            "And so is this one, broken here.",
            "");

        // ⛔ The premise: broken there with nothing before the dash, the paragraph opens a list, which a fold keeps.
        Assert.NotEqual(ParagraphBreaks.Fold(written), ParagraphBreaks.Fold("Pass the flags\n- v and q when the tool should run quietly."));

        // Each re-break is tried over the ones kept before it, so both that pass are made, the one before the failing
        // one and the one after it.
        Assert.Equal(
            Doc(
                written,
                "",
                "## Flags",
                "",
                "Every other paragraph is broken",
                "where the accumulator breaks it.",
                "",
                written,
                "",
                "And so is this one,",
                "broken here.",
                ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_paragraph_the_accumulator_holds_twice_is_shown_in_the_lines_of_the_one_under_its_own_heading()
    {
        string accumulator = Doc(
            "## One", "", "Both sections hold these words,", "broken one way.", "",
            "## Two", "", "Both sections hold", "these words, broken one way.", "");

        // In the other order, so the one the candidate's paragraph takes its lines from is told by the heading alone.
        string candidate = Doc(
            "## Two", "", "Both sections hold these words, broken one way.", "",
            "## One", "", "Both sections hold these words, broken one way.", "");

        Assert.Equal(
            Doc(
                "## Two", "", "Both sections hold", "these words, broken one way.", "",
                "## One", "", "Both sections hold these words,", "broken one way.", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_quotes_lines_keep_its_marker_and_a_list_items_are_indented_to_its_text()
    {
        string accumulator = Doc(
            "## Notes",
            "",
            "> A quoted rule, wrapped",
            "> over two lines.",
            "",
            "10. An ordered item, wrapped",
            "    over two lines.",
            "");

        // Broken otherwise, rather than written on one line, so a paragraph is matched by its words and not its lines.
        string candidate = Doc(
            "## Notes",
            "",
            "> A quoted rule,",
            "> wrapped over two lines.",
            "",
            "10. An ordered item,",
            "    wrapped over two lines.",
            "");

        Assert.Equal(accumulator, ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_paragraph_ending_in_spaces_is_re_broken_and_keeps_them()
    {
        // ⚠ Measured: a paragraph's block takes in the spaces ending its last line, and its inlines do not. Under a
        // heading the accumulator does not have, so the paragraph is shown in other lines only if its words, taken
        // without those spaces, are the accumulator's. Each document has a title, which is in no heading path, so
        // the two sections are told apart by their own headings.
        string accumulator = Doc("# Rules", "", "## Notes", "", "A paragraph whose lines", "end without spaces.", "");
        string candidate = Doc("# Rules", "", "## Remarks", "", "A paragraph whose lines end without spaces.   ", "");

        Assert.Equal(
            Doc("# Rules", "", "## Remarks", "", "A paragraph whose lines", "end without spaces.   ", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_paragraph_differing_in_one_word_shows_every_other_line_as_the_accumulator_does_and_that_word_in_a_line_pair_of_its_own()
    {
        string accumulator = Doc(
            "## Pushing",
            "",
            "Keep the build green on every",
            "platform the project ships to.",
            "",
            "A change is pushed only once the",
            "whole suite has passed on this",
            "machine and on the runners, and",
            "never with a test skipped.",
            "");

        // The paragraph before it holds the accumulator's words, so the one that differs is the second of its section
        // on both sides, and its counterpart is told by the diff that pairs them.
        string candidate = Doc(
            "## Pushing",
            "",
            "Keep the build green on every platform the project ships to.",
            "",
            "A change is pushed only once the entire suite has passed on this machine and on the runners, and never with a test skipped.",
            "");

        string shown = ShownCandidate.Of(candidate, accumulator).Text;

        Assert.Equal(accumulator.Replace("whole suite", "entire suite", StringComparison.Ordinal), shown);

        // As the window shows the two: the changed word's line beside the accumulator's, and every other line equal.
        AlignedRow pair = Assert.Single(
            DiffDocumentBuilder.Build(accumulator, shown, TestContext.Current.CancellationToken).Document.Rows,
            row => row.Kind != DiffLineKind.Unchanged);
        Assert.Equal(new AlignedRow(6, 6, DiffLineKind.Modified), pair);
    }

    [Fact]
    public void A_word_added_where_two_lines_meet_ends_the_first_so_the_second_starts_at_the_accumulators_word()
    {
        string accumulator = Doc("## Formatting", "", "Run the formatter before", "every commit you make.", "");
        string candidate = Doc("## Formatting", "", "Run the formatter before nearly every commit you make.", "");

        Assert.Equal(
            Doc("## Formatting", "", "Run the formatter before nearly", "every commit you make.", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_line_whose_first_word_the_candidate_dropped_starts_where_that_word_was()
    {
        string accumulator = Doc("## Formatting", "", "Run the formatter before", "each and every commit.", "");
        string candidate = Doc("## Formatting", "", "Run the formatter before every commit.", "");

        Assert.Equal(
            Doc("## Formatting", "", "Run the formatter before", "every commit.", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_word_added_at_a_paragraphs_start_or_end_is_shown_on_its_first_or_last_line()
    {
        string accumulator = Doc("## Merging", "", "CI runs the formatter", "before every merge.", "");
        string candidate = Doc("## Merging", "", "Our CI runs the formatter before every merge. Always.", "");

        Assert.Equal(
            Doc("## Merging", "", "Our CI runs the formatter", "before every merge. Always.", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_paragraph_twice_its_counterparts_length_is_broken_where_the_counterpart_is()
    {
        string accumulator = Doc("## Pull requests", "", "Keep each pull request small", "and focused on one change.", "");
        string candidate = Doc(
            "## Pull requests",
            "",
            "Keep each pull request small enough to review in one sitting, and focused on one change and the tests that prove it.",
            "");

        // Each added run ends the line before the break it sits at, so the second line starts where the accumulator's
        // does.
        Assert.Equal(
            Doc(
                "## Pull requests",
                "",
                "Keep each pull request small enough to review in one sitting,",
                "and focused on one change and the tests that prove it.",
                ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_break_never_lands_in_a_run_of_spaces_and_the_others_are_still_made()
    {
        string accumulator = Doc("## Testing", "", "Write the test first.", "Then make it pass, and", "only then refactor.", "");

        // ⭐ Two spaces after a sentence, where the accumulator breaks: a fold reads a break as one space, so a break
        // there would change the paragraph's words and the check would refuse every break in it.
        string candidate = Doc("## Testing", "", "Write the test first.  Then make it pass, and only then refactor.", "");

        Assert.Equal(
            Doc("## Testing", "", "Write the test first.  Then make it pass, and", "only then refactor.", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_hard_break_inside_a_paragraph_leaves_it_one_line_of_the_diff_that_pairs_the_paragraphs()
    {
        // ⚠ A hard break is no soft break, so the line ending it keeps is inside the paragraph's words.
        string accumulator = Doc(
            "## Commits",
            "",
            "One change a commit,\\",
            "one reason a change.",
            "",
            "Keep the build green on every",
            "platform the project ships to.",
            "");
        string candidate = Doc(
            "## Commits",
            "",
            "One change a commit,\\",
            "one reason a change.",
            "",
            "Keep the build green on every platform the project ships to, always.",
            "");

        Assert.Equal(
            Doc(
                "## Commits",
                "",
                "One change a commit,\\",
                "one reason a change.",
                "",
                "Keep the build green on every",
                "platform the project ships to, always.",
                ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }

    [Fact]
    public void A_paragraph_under_a_heading_the_accumulator_does_not_have_is_shown_as_written()
    {
        string accumulator = Doc("# Rules", "", "## Building", "", "Keep the build green on every", "platform the project ships to.", "");
        string candidate = Doc(
            "# Rules",
            "",
            "## Building",
            "",
            "Keep the build green on every platform the project ships to.",
            "",
            "## Notes",
            "",
            "Keep the notes short and",
            "to the point, always.",
            "");

        Assert.Equal(
            Doc(
                "# Rules",
                "",
                "## Building",
                "",
                "Keep the build green on every",
                "platform the project ships to.",
                "",
                "## Notes",
                "",
                "Keep the notes short and",
                "to the point, always.",
                ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }
}
