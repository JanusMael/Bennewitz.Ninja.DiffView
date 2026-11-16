using System.Text;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00036 step 3: the shown candidate, for exact matches. A candidate's paragraph holding the words of one of the
/// accumulator's is shown in that one's lines, and a re-break that would change what the text is, is not made.
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
        // text and the Scripts paragraph under its other heading too. The heading and the Reviews paragraph, whose
        // words are their own, are as the candidate wrote them.
        string expected = Seed
            .Replace("## Scripts", "## Shell scripts", StringComparison.Ordinal)
            .Replace(
                "on any line of it, and says what it would do\ninstead wherever it objects.",
                "on any part of it, and says what it would do instead wherever it objects.",
                StringComparison.Ordinal);
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
        // ⚠ Measured: a paragraph's block takes in the spaces ending its last line, and its inlines do not.
        string accumulator = Doc("## Notes", "", "A paragraph whose lines", "end without spaces.", "");
        string candidate = Doc("## Notes", "", "A paragraph whose lines end without spaces.   ", "");

        Assert.Equal(
            Doc("## Notes", "", "A paragraph whose lines", "end without spaces.   ", ""),
            ShownCandidate.Of(candidate, accumulator).Text);
    }
}
