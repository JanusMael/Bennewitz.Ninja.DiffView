namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00036 step 1: the fold. A soft line break inside a paragraph is one space, and every other line break is kept.
/// </summary>
/// <remarks>
/// <para>
/// Fixtures are joined with <c>"\n"</c> explicitly, as <see cref="MarkdownOutlineTests"/>' are, so no fixture's line
/// endings depend on a checkout.
/// </para>
/// <para>
/// ⚠ Two tests ask Markdig rather than the fold whether a code block and an HTML block are paragraphs, and carry the
/// marker that says so: the fold reads only a paragraph's soft breaks, so no mutation of it reaches either. Each asserts
/// besides that its fixture holds lines a fold knowing no blocks would join, without which it would pass for nothing.
/// </para>
/// </remarks>
public sealed class ParagraphBreaksTests
{
    private static string Doc(params string[] lines) => string.Join("\n", lines);

    /// <summary>
    /// The line endings a fold that knew no blocks would join: each one between two lines that are not blank.
    /// </summary>
    private static int NaiveJoins(string text)
    {
        string[] lines = text.Split('\n');
        return Enumerable.Range(0, lines.Length - 1)
            .Count(i => !string.IsNullOrWhiteSpace(lines[i]) && !string.IsNullOrWhiteSpace(lines[i + 1]));
    }

    [Fact]
    public void One_paragraph_wrapped_three_ways_folds_to_one_text()
    {
        const string OneLine =
            "In CommonMark a line break inside a paragraph is a soft break, rendered as whitespace: where a " +
            "paragraph breaks its lines is layout, not content.";

        // As an editor wraps near a width.
        string wrapped = Doc(
            "In CommonMark a line break inside a paragraph is a soft break, rendered as",
            "whitespace: where a paragraph breaks its lines is layout, not content.");

        // Narrower, with a space left at the end of one line and a tab at the end of another, which the break takes.
        string ragged = Doc(
            "In CommonMark a line break inside a paragraph ",
            "is a soft break, rendered as whitespace:\t",
            "where a paragraph breaks its lines",
            "is layout, not content.");

        // Its continuation lines indented, by spaces and by a tab, as a paragraph's continuation may be.
        string indented = Doc(
            "In CommonMark a line break inside a paragraph is a soft break,",
            "    rendered as whitespace: where a paragraph",
            "\tbreaks its lines is layout, not content.");

        Assert.Equal(OneLine, ParagraphBreaks.Fold(wrapped));
        Assert.Equal(OneLine, ParagraphBreaks.Fold(ragged));
        Assert.Equal(OneLine, ParagraphBreaks.Fold(indented));
    }

    [Fact]
    public void A_hard_break_of_each_kind_keeps_its_line_break()
    {
        // Two trailing spaces and a backslash each end a line where the paragraph is rendered, so each is part of
        // what it says. The soft break after them folds, which shows the fold ran over this paragraph at all.
        string text = Doc(
            "Two spaces end this line,  ",
            "a backslash the next,\\",
            "and this one",
            "ends softly.");

        Assert.Equal(
            Doc(
                "Two spaces end this line,  ",
                "a backslash the next,\\",
                "and this one ends softly."),
            ParagraphBreaks.Fold(text));
    }

    [Fact]
    public void A_fenced_and_an_indented_code_block_keep_their_lines()
    {
        string text = Doc(
            "```",
            "dotnet test --project",
            "  tests/AgentBaseline.Tests",
            "```",
            "",
            "~~~",
            "a tilde fence",
            "keeps its lines",
            "~~~",
            "",
            "    an indented block",
            "    keeps its lines too");

        // ⚠ This pins the DEPENDENCY, not our code: the fold reads only a paragraph's soft breaks, and Markdig is what
        // decides that a code block is not a paragraph, so no mutation of the fold reaches this. It is kept because it
        // is what decision 1 promises, and it would fail if an upgrade changed how Markdig reads a code block.
        // inert-at-pin: Markdig 1.4.1
        Assert.Equal(text, ParagraphBreaks.Fold(text));
        Assert.Equal(7, NaiveJoins(text));
    }

    [Fact]
    public void A_table_keeps_its_rows()
    {
        // ⭐ Read with the outline's pipeline, which has no pipe tables, these rows are one paragraph with a soft break
        // between each two, and a fold over that parse joins them into one line: the fold parses with pipe tables for
        // that reason, decision 1, and the seed of the 2026-11-10 corpus holds a table.
        string text = Doc(
            "| Package | Pin |",
            "|---|---|",
            "| Markdig | 1.4.1 |",
            "| DiffPlex | 1.9.0 |");

        Assert.Equal(text, ParagraphBreaks.Fold(text));
    }

    [Fact]
    public void An_html_block_keeps_its_lines()
    {
        string text = Doc(
            "<details>",
            "<summary>Why</summary>",
            "the lines of an HTML block",
            "are the block's own",
            "</details>");

        // ⚠ A pin of the dependency, as the code blocks' is: Markdig decides that these lines are an HTML block.
        // inert-at-pin: Markdig 1.4.1
        Assert.Equal(text, ParagraphBreaks.Fold(text));
        Assert.Equal(4, NaiveJoins(text));
    }

    [Fact]
    public void A_lists_structure_keeps_its_lines_and_each_items_wrapped_paragraph_folds()
    {
        string text = Doc(
            "- One item on one line",
            "- An item whose paragraph",
            "  is wrapped",
            "  - a nested item,",
            "    wrapped too",
            "  - and one that is not",
            "1. An ordered item",
            "   wrapped once",
            "2. and its sibling");

        Assert.Equal(
            Doc(
                "- One item on one line",
                "- An item whose paragraph is wrapped",
                "  - a nested item, wrapped too",
                "  - and one that is not",
                "1. An ordered item wrapped once",
                "2. and its sibling"),
            ParagraphBreaks.Fold(text));
    }

    [Fact]
    public void A_quotes_wrapped_paragraphs_fold_and_a_lazy_line_with_them()
    {
        // The second line's marker goes with the break before it, the folded line being in the same quote; a lazy line
        // has none, and is the paragraph's all the same.
        string text = Doc(
            "> A quoted paragraph",
            "> wrapped once,",
            "and a lazy line, which continues it.",
            ">",
            "> A second paragraph",
            ">   indented after its marker.");

        Assert.Equal(
            Doc(
                "> A quoted paragraph wrapped once, and a lazy line, which continues it.",
                ">",
                "> A second paragraph indented after its marker."),
            ParagraphBreaks.Fold(text));
    }

    [Fact]
    public void A_soft_break_with_nothing_after_it_in_its_paragraph_is_kept()
    {
        // Both measured: pipe tables take a table out of the paragraph above it and leave that paragraph its break
        // before the first row, and a link whose text ends at a line ending keeps that break as its last child, the next
        // line opening with the link's own syntax. Joining either puts a row, or a link's tail, in the middle of a line.
        string aboveATable = Doc(
            "The pins, as they stand",
            "today:",
            "| Package | Pin |",
            "|---|---|",
            "| Markdig | 1.4.1 |");
        string linkText = Doc(
            "See [the hosting guide",
            "](docs/hosting-diffview.md) for the ids.");

        Assert.Equal(
            Doc(
                "The pins, as they stand today:",
                "| Package | Pin |",
                "|---|---|",
                "| Markdig | 1.4.1 |"),
            ParagraphBreaks.Fold(aboveATable));
        Assert.Equal(linkText, ParagraphBreaks.Fold(linkText));
    }

    [Fact]
    public void Text_with_no_soft_break_comes_out_byte_for_byte()
    {
        // ⭐ Decision 2 rests on this: a section whose text has no soft break hashes as it did before the fold existed.
        // Every block here breaks its lines for a reason of its own, front matter among them, two of whose lines would
        // be a paragraph of their own if the parse did not know front matter.
        string text = Doc(
            "---",
            "name: rules",
            "description: the house rules",
            "",
            "scope: global",
            "---",
            "",
            "# Rules — the title",
            "",
            "A setext heading",
            "over two lines",
            "---------------",
            "",
            "One paragraph, one line ending in a hard break:  ",
            "and the line after it.",
            "",
            "```",
            "fenced",
            "code",
            "```",
            "",
            "    indented",
            "    code",
            "",
            "<div>",
            "html",
            "</div>",
            "",
            "| a | b |",
            "|---|---|",
            "| 1 | 2 |",
            "",
            "- a list",
            "  - nested",
            "1. ordered",
            "",
            "> a quote on one line",
            "",
            "  ",
            "A last paragraph, a trailing space after it. ",
            "");

        Assert.Equal(text, ParagraphBreaks.Fold(text));
    }

    [Fact]
    public void A_document_with_crlf_line_endings_folds_to_what_its_lf_form_folds_to()
    {
        string lf = Doc("# Rules", "", "A paragraph", "wrapped once.", "", "Another.");
        string crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(Doc("# Rules", "", "A paragraph wrapped once.", "", "Another."), ParagraphBreaks.Fold(lf));
        Assert.Equal(ParagraphBreaks.Fold(lf), ParagraphBreaks.Fold(crlf));
    }
}
