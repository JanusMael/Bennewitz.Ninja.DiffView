namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 2: the parse, with Markdig as a locator and the text from our own line array.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Every test here asserts an <b>expected heading set</b> rather than a count or a shape, which
/// is what step 2's verification asks for. A count passes on the wrong headings; the set does not.
/// </para>
/// <para>
/// Fixtures are joined with <c>"\n"</c> explicitly rather than written as raw string literals,
/// because a literal's line endings are whatever this source file happens to carry — and these
/// documents are CRLF on the Windows boxes and LF on JanusHalo. A fixture whose line endings depend
/// on a checkout cannot test line numbering.
/// </para>
/// </remarks>
public sealed class MarkdownOutlineTests
{
    private static string Doc(params string[] lines) => string.Join("\n", lines);

    /// <summary>The outline as level and title, which is the set these tests are about.</summary>
    private static string[] Headings(string text) =>
        [.. MarkdownOutline.Read(text).Sections.Select(s => $"{s.Level} {s.Title}")];

    /// <summary>The outline with the lines each section governs, for the tests about position.</summary>
    private static string[] HeadingsWithLines(string text) =>
        [.. MarkdownOutline.Read(text).Sections.Select(s => $"{s.Level} {s.Title} [{s.FirstLine}..{s.LastLine}]")];

    /// <summary>
    /// The whole of every section, text included, for the tests comparing one source against
    /// another. ⛔ Positions alone are too weak for those: Markdig handles <c>\r\n</c> natively, so
    /// the line numbers agree even with normalisation removed, and only the text carries the stray
    /// carriage returns. Measured — a mutation removing normalisation left the position-only
    /// comparison green.
    /// </summary>
    private static string[] FullOutline(string text) =>
        [.. MarkdownOutline.Read(text).Sections.Select(
            s => $"{s.Level} {s.Title} [{s.FirstLine}..{s.LastLine}] {s.HeadingPath}\n{s.Text}")];

    /// <summary>
    /// The scan this parse replaces, kept as a positive control: a fixture that does not actually
    /// have the awkward shape would let the interesting tests below pass for the wrong reason.
    /// </summary>
    private static int NaiveHashScan(string text) =>
        text.Split('\n').Count(l => l.StartsWith('#'));

    [Fact]
    public void Atx_headings_carry_their_level_and_lose_any_closing_sequence()
    {
        string[] headings = Headings(Doc(
            "# Title",
            "",
            "## Closing ##",
            "",
            "###### Six",
            "",
            "body"));

        // `## Closing ##` is a level-2 heading whose title is "Closing": CommonMark strips a closing
        // run of hashes. A parser that kept it would put "Closing ##" in a section's identity, and
        // decision 6 makes that identity the thing a later run matches on.
        Assert.Equal(["1 Title", "2 Closing", "6 Six"], headings);
    }

    [Fact]
    public void A_hash_with_no_space_after_it_is_not_a_heading()
    {
        string text = Doc(
            "# Real",
            "",
            "#NoSpace",
            "",
            "####### Seven hashes",
            "",
            "## Also real");

        // CommonMark requires a space (or end of line) after the hashes, and caps a heading at six.
        // Both of those lines begin with '#' at column 0, so the scan this replaces reads four
        // headings where there are two.
        //
        // ⚠ This pins the DEPENDENCY, not our code: no mutation of MarkdownOutline can break it,
        // because nothing in MarkdownOutline decides it — measured, the mutation run left this one
        // green throughout. It is kept because it is what makes the parse worth taking, and it would
        // fail if an upgrade changed Markdig's heading recognition. Marked rather than claimed.
        // inert-at-pin: Markdig 1.4.1
        Assert.Equal(["1 Real", "2 Also real"], Headings(text));
        Assert.Equal(4, NaiveHashScan(text));
    }

    [Fact]
    public void A_hash_at_column_zero_inside_a_fence_is_not_a_heading()
    {
        string text = Doc(
            "# Real",
            "",
            "```",
            "# not a heading, backtick fence",
            "```",
            "",
            "~~~",
            "# not a heading, tilde fence",
            "~~~",
            "",
            "## Also real");

        // Both fence spellings, because `~~~` is the one a hand-rolled scanner is likelier to miss.
        //
        // ⚠ A dependency pin like the one above, and inert for the same reason: our walk asks for
        // HeadingBlocks and Markdig is what decides that a fenced line is not one.
        // inert-at-pin: Markdig 1.4.1
        Assert.Equal(["1 Real", "2 Also real"], Headings(text));
        Assert.Equal(4, NaiveHashScan(text));
    }

    [Fact]
    public void Front_matter_is_detected_and_never_read_as_a_setext_heading()
    {
        string text = Doc(
            "---",
            "name: a-note",
            "description: something",
            "---",
            "",
            "# After the front matter",
            "",
            "body");

        MarkdownOutline outline = MarkdownOutline.Read(text);

        // ⛔ This is the measured trap, not a hypothetical: without UseYamlFrontMatter the closing
        // `---` reads as a setext underline and the YAML body becomes a level-2 heading titled
        // "name: a-note description: something". Every memory-shaped file would gain a heading
        // nobody wrote, and it would look like real data.
        Assert.Equal(["1 After the front matter"], Headings(text));
        Assert.True(outline.HasFrontMatter);
    }

    [Fact]
    public void A_document_without_front_matter_does_not_claim_to_have_it()
    {
        // The negative half: HasFrontMatter asserted only on the positive side would pass for a
        // property hardcoded true.
        Assert.False(MarkdownOutline.Read(Doc("# Title", "", "body")).HasFrontMatter);
    }

    [Fact]
    public void A_setext_heading_begins_at_its_text_rather_than_at_its_underline()
    {
        string[] headings = HeadingsWithLines(Doc(
            "Alpha",          // 1
            "=====",          // 2
            "",               // 3
            "body",           // 4
            "",               // 5
            "Beta",           // 6
            "-----",          // 7
            "",               // 8
            "# Atx",          // 9
            "",               // 10
            "more"));         // 11

        // ⛔ Markdig reports a setext heading's `Line` as the UNDERLINE row. A section starting
        // there would begin at "=====" and silently lose its own heading text, so the first line
        // comes from `Span.Start` instead. Asserting the ranges is what catches it: the titles alone
        // are identical either way.
        Assert.Equal(["1 Alpha [1..5]", "2 Beta [6..8]", "1 Atx [9..11]"], headings);
    }

    [Fact]
    public void A_setext_heading_whose_text_runs_several_lines_begins_at_the_first_of_them()
    {
        string[] headings = HeadingsWithLines(Doc(
            "Alpha",          // 1
            "Beta",           // 2
            "Gamma",          // 3
            "=====",          // 4
            "",               // 5
            "body"));         // 6

        // ⛔ And the text may run several lines, so `Line - 1` is wrong too — measured, three lines
        // above the underline. The title collapses to one line because it is half of an identity.
        Assert.Equal(["1 Alpha Beta Gamma [1..6]"], headings);
    }

    [Fact]
    public void A_document_wholly_nested_in_a_list_item_yields_its_headings()
    {
        string text = Doc(
            "- # Nested Title",
            "",
            "  Some body text.",
            "",
            "  ## Nested Section",
            "",
            "  More body.",
            "",
            "  ### Deeper",
            "",
            "  ```",
            "  # fenced inside the list item",
            "  ```");

        // ⭐ JanusHalo's real shape, which is why this plan takes Markdig at all: measured, a `^#`
        // scan finds 0 of that file's 18 headings and so does a walk of the document's top-level
        // blocks, because every heading is inside one ListItemBlock. The naive count below is the
        // positive control — it proves the fixture really has the shape, so that the assertion above
        // it is not passing on an ordinary document.
        Assert.Equal(["1 Nested Title", "2 Nested Section", "3 Deeper"], Headings(text));
        Assert.Equal(0, NaiveHashScan(text));
    }

    [Fact]
    public void A_nested_sections_text_keeps_the_indent_the_source_had()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc(
            "- # Nested Title",
            "",
            "  ## Nested Section",
            "",
            "  More body."));

        // Unwrapping is ingest normalisation's job — decision 8, step 3 — so the parse reports what
        // the source really was. A parse that quietly dedented here would make step 3 untestable.
        Assert.Equal(Doc("- # Nested Title", ""), outline.Sections[0].Text);
        Assert.Equal(Doc("  ## Nested Section", "", "  More body."), outline.Sections[1].Text);
    }

    [Fact]
    public void The_first_line_of_a_document_is_line_one()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc("# First", "", "body"));

        // The one line-base boundary: Markdig counts from 0 and everything downstream of here counts
        // from 1, as AvaloniaEdit's TextDocument does.
        Assert.Equal(1, outline.Sections[0].FirstLine);
        Assert.Equal(3, outline.Sections[0].LastLine);
        Assert.Equal(3, outline.Sections[0].LineCount);
    }

    [Fact]
    public void A_sections_text_is_its_own_source_lines()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc(
            "# One",
            "",
            "```",
            "# fenced",
            "```",
            "",
            "## Two",
            "",
            "tail"));

        // Decision 3: the text comes from our line array, so a fenced block survives verbatim
        // rather than being re-rendered out of the parse tree.
        Assert.Equal(Doc("# One", "", "```", "# fenced", "```", ""), outline.Sections[0].Text);
        Assert.Equal(Doc("## Two", "", "tail"), outline.Sections[1].Text);
    }

    [Fact]
    public void The_heading_path_names_the_ancestry_even_where_a_level_is_skipped()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc(
            "# Top",
            "",
            "### Skipped to three",
            "",
            "## Back to two",
            "",
            "#### Under two",
            "",
            "# Second top"));

        // Decision 6 makes the heading path half of a section's identity, so it has to survive the
        // level skipping these documents really contain. Two headings at the top, so neither is a
        // title and every path shows its whole ancestry; the title has tests of its own below.
        Assert.Equal(
            [
                "Top",
                "Top / Skipped to three",
                "Top / Back to two",
                "Top / Back to two / Under two",
                "Second top",
            ],
            outline.Sections.Select(s => s.HeadingPath));
    }

    [Fact]
    public void A_documents_title_is_in_no_heading_path_so_its_sections_keep_theirs_whatever_it_is_called()
    {
        string[] body = ["", "Lead.", "", "## 1. Hard invariants", "", "Rule.", "", "### Detail", "", "## 2. Tests", ""];
        MarkdownOutline ours = MarkdownOutline.Read(Doc(["# AGENTS.md — cross-file contracts for DiffView", .. body]));
        MarkdownOutline theirs = MarkdownOutline.Read(Doc(["# AGENTS.md — operational rules for LLM contributors", .. body]));

        // ⭐ Open item 22, measured on C:\c\cl: three projects' AGENTS.md open on `## 1. Hard invariants`
        // under three titles, and with the title in the path that was three rules. The title's own path
        // is empty, so the two titles are versions of one section as well.
        string[] expected = ["", "1. Hard invariants", "1. Hard invariants / Detail", "2. Tests"];
        Assert.Equal(expected, ours.Sections.Select(s => s.HeadingPath));
        Assert.Equal(expected, theirs.Sections.Select(s => s.HeadingPath));
    }

    [Fact]
    public void Two_headings_at_the_top_of_the_outline_are_sections_and_neither_is_a_title()
    {
        // The global CLAUDE.md's shape: two top-level headings, each a section with rules under it.
        MarkdownOutline claude = MarkdownOutline.Read(Doc(
            "# Working preferences", "", "## Deliverables are files", "", "# Fable family", "", "## The loop", ""));

        // And a level-1 heading that is the only one but not the first: the heading above it is at the
        // top of the outline as well, so neither is the document's title.
        MarkdownOutline late = MarkdownOutline.Read(Doc("## Intro", "", "# Main", "", "## Under main", ""));

        Assert.Equal(
            ["Working preferences", "Working preferences / Deliverables are files", "Fable family", "Fable family / The loop"],
            claude.Sections.Select(s => s.HeadingPath));
        Assert.Equal(["Intro", "Main", "Main / Under main"], late.Sections.Select(s => s.HeadingPath));
    }

    [Fact]
    public void A_title_is_the_top_of_the_outline_whatever_its_level()
    {
        // A path is built from what sits under what, so `## Notes` over `###` leaves its title out as
        // `#` over `##` does; read as level 1 instead, these would keep "Notes" in every path.
        Assert.Equal(
            ["", "One", "One / Detail"],
            MarkdownOutline.Read(Doc("## Notes", "", "### One", "", "#### Detail", "")).Sections.Select(s => s.HeadingPath));

        // A document of one heading is all title: nothing after it is anything but deeper.
        Assert.Equal([""], MarkdownOutline.Read(Doc("# Only", "", "Body.")).Sections.Select(s => s.HeadingPath));
    }

    [Fact]
    public void Everything_above_the_first_heading_is_kept_as_the_preamble()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc(
            "Some preamble.",
            "",
            "# One",
            "",
            "body"));

        // Non-lossy: a preamble belongs to no section, so without this it would vanish between the
        // outline and a saved baseline.
        Assert.Equal(Doc("Some preamble.", ""), outline.Preamble);
        Assert.Equal(["1 One"], outline.Sections.Select(s => $"{s.Level} {s.Title}"));
    }

    [Fact]
    public void A_document_with_no_heading_at_all_is_all_preamble_and_no_sections()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc("Just text.", "", "More text."));

        Assert.Empty(outline.Sections);
        Assert.Equal(Doc("Just text.", "", "More text."), outline.Preamble);
    }

    [Fact]
    public void The_sections_and_the_preamble_cover_every_line_once()
    {
        MarkdownOutline outline = MarkdownOutline.Read(Doc(
            "intro",
            "",
            "# One",
            "",
            "body",
            "",
            "## Two",
            "",
            "## Three",
            "",
            "tail"));

        // No line is in two sections or in none: each section begins where the one before it ended,
        // the last reaches the final line, and the preamble is exactly what sits above the first.
        for (int i = 1; i < outline.Sections.Count; i++)
        {
            Assert.Equal(outline.Sections[i - 1].LastLine + 1, outline.Sections[i].FirstLine);
        }

        Assert.Equal(outline.Lines.Count, outline.Sections[^1].LastLine);
        Assert.Equal(
            string.Join("\n", outline.Lines.Take(outline.Sections[0].FirstLine - 1)),
            outline.Preamble);
    }

    [Fact]
    public void Line_endings_do_not_move_a_heading()
    {
        string[] lines = ["# One", "", "body", "", "Setext", "------", "", "tail"];

        // Compared as whole sections, text included, and that is the load-bearing part: Markdig
        // counts '\n' either way, so the POSITIONS agree even with normalisation removed, and what
        // a CRLF source leaves behind is a stray '\r' at the end of every line of every section's
        // text. These documents are CRLF on the Windows boxes and LF on JanusHalo, so a baseline
        // assembled from both is exactly where that would surface.
        Assert.Equal(
            FullOutline(string.Join("\n", lines)),
            FullOutline(string.Join("\r\n", lines)));
    }

    [Fact]
    public void A_lone_carriage_return_is_a_line_ending_too()
    {
        string[] lines = ["# One", "", "## Two", "", "tail"];

        Assert.Equal(
            FullOutline(string.Join("\n", lines)),
            FullOutline(string.Join("\r", lines)));
    }
}
