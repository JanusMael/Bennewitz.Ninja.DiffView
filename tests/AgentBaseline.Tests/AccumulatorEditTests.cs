namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 6, where a take or a drop lands: positioned by the accumulator's own parse
/// (decision 5), and refused, with nothing written, when it would change any section it is not about.
/// </summary>
/// <remarks>
/// Every assertion on a result is on the whole text, not on the lines a decision added: <i>"the left
/// gained these lines"</i> passes while the left silently loses a rule, which is the defect step 6's
/// verification is written against.
/// </remarks>
public sealed class AccumulatorEditTests
{
    private static string Doc(params string[] lines) => string.Join("\n", lines);

    /// <summary>Takes the candidate's section at <paramref name="path"/> into <paramref name="accumulator"/>.</summary>
    private static (string After, TakeResult Result) Take(string accumulator, string candidate, string path)
    {
        MarkdownOutline outline = MarkdownOutline.Read(candidate);
        (TextEdit edit, TakeResult result) = AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf(path).Single());
        return (edit.ApplyTo(accumulator), result);
    }

    private static string Drop(string accumulator, string path) =>
        AccumulatorEdits.Drop(accumulator, path).Edit.ApplyTo(accumulator);

    [Fact]
    public void A_section_the_accumulator_holds_is_replaced_and_the_blank_lines_after_it_stay_the_accumulators()
    {
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");
        string candidate = Doc("# Rules", "", "## One", "", "Alpha, rewritten.", "", "", "## Two", "", "Beta.", "");

        (string after, TakeResult result) = Take(accumulator, candidate, "Rules / One");

        Assert.Equal(TakeResult.Replaced, result);
        Assert.Equal(Doc("# Rules", "", "## One", "", "Alpha, rewritten.", "", "## Two", "", "Beta.", ""), after);
    }

    [Fact]
    public void Taking_the_version_the_accumulator_already_holds_writes_nothing()
    {
        // The same content set off by more blank lines is the same version: the blank lines after a
        // section say where it sits, not what it says.
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");
        string candidate = Doc("# Rules", "", "## One", "", "Alpha.", "", "", "", "## Two", "", "Beta, other.", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        (TextEdit edit, TakeResult result) = AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf("Rules / One").Single());

        Assert.Equal(TakeResult.Unchanged, result);
        Assert.Equal(accumulator, edit.ApplyTo(accumulator));
    }

    [Fact]
    public void A_section_the_accumulator_lacks_goes_after_its_nearest_preceding_sibling_and_everything_under_that()
    {
        // Two levels under One, so "everything under it" is read through a grandchild, not one level down.
        string accumulator = Doc(
            "# Rules", "", "## One", "", "Alpha.", "", "### Detail", "", "Fine print.", "", "#### Finer", "", "Finer print.", "",
            "## Three", "", "Gamma.", "");
        string candidate = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "", "## Three", "", "Gamma.", "");

        (string after, TakeResult result) = Take(accumulator, candidate, "Rules / Two");

        Assert.Equal(TakeResult.Inserted, result);
        Assert.Equal(
            Doc(
                "# Rules", "", "## One", "", "Alpha.", "", "### Detail", "", "Fine print.", "", "#### Finer", "", "Finer print.", "",
                "## Two", "", "Beta.", "", "## Three", "", "Gamma.", ""),
            after);
    }

    [Fact]
    public void With_no_preceding_sibling_held_a_section_goes_first_under_its_parent()
    {
        string accumulator = Doc("# Rules", "", "Intro.", "", "## Two", "", "Beta.", "");
        string candidate = Doc("# Rules", "", "Intro.", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");

        (string after, TakeResult result) = Take(accumulator, candidate, "Rules / One");

        Assert.Equal(TakeResult.Inserted, result);
        Assert.Equal(Doc("# Rules", "", "Intro.", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", ""), after);
    }

    [Fact]
    public void With_no_parent_and_no_preceding_sibling_a_section_goes_first_of_all_below_the_preamble()
    {
        string accumulator = Doc("Above every heading.", "", "# Second", "", "Body.", "");
        string candidate = Doc("# First", "", "Lead.", "", "# Second", "", "Body.", "");

        (string after, TakeResult result) = Take(accumulator, candidate, "First");

        Assert.Equal(TakeResult.Inserted, result);
        Assert.Equal(Doc("Above every heading.", "", "# First", "", "Lead.", "", "# Second", "", "Body.", ""), after);
    }

    [Fact]
    public void At_the_end_a_take_leaves_the_last_line_as_it_was_with_a_line_ending_or_without()
    {
        string candidate = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");

        // Ending in a line ending: a blank line before the new section, and the ending kept.
        (string ended, _) = Take(Doc("# Rules", "", "## One", "", "Alpha.", ""), candidate, "Rules / Two");
        Assert.Equal(Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", ""), ended);

        // Ending without one: the section above keeps its text exactly, so it gains no blank line, and
        // the document still ends without one.
        (string unended, _) = Take(Doc("# Rules", "", "## One", "", "Alpha."), candidate, "Rules / Two");
        Assert.Equal(Doc("# Rules", "", "## One", "", "Alpha.", "## Two", "", "Beta."), unended);
    }

    [Fact]
    public void A_take_whose_parent_the_accumulator_lacks_is_refused()
    {
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "");
        string candidate = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "", "### Detail", "", "Fine print.", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf("Rules / Two / Detail").Single()));

        Assert.Contains("no \"Rules / Two\" to put \"Detail\" under", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_take_is_refused_where_the_accumulator_holds_its_path_or_its_parents_twice()
    {
        string twice = Doc("# Rules", "", "## One", "", "Alpha.", "", "## One", "", "Alpha, again.", "");
        string candidate = Doc("# Rules", "", "## One", "", "Alpha, rewritten.", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        DecisionRefusedException replace = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(twice, outline, outline.IndicesOf("Rules / One").Single()));
        Assert.Contains("holds 2 sections at \"Rules / One\"", replace.Message, StringComparison.Ordinal);

        string parentTwice = Doc("# Rules", "", "## One", "", "Alpha.", "", "# Rules", "", "Again.", "");
        MarkdownOutline withChild = MarkdownOutline.Read(Doc("# Rules", "", "## Two", "", "Beta.", ""));
        DecisionRefusedException insert = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(parentTwice, withChild, withChild.IndicesOf("Rules / Two").Single()));
        Assert.Contains("holds 2 sections at \"Rules\"", insert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_take_whose_fence_never_closes_is_refused_because_it_would_swallow_the_headings_after_it()
    {
        // In its own file the open fence runs to the end and swallows nothing; set in the middle of the
        // accumulator it would turn every heading below it into code.
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Three", "", "Gamma.", "");
        string candidate = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "```", "an open fence", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf("Rules / Two").Single()));

        Assert.Contains("\"Rules / Three\" would no longer be a heading", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_take_that_would_move_the_sections_under_another_is_refused()
    {
        // The candidate's One is a level deeper, so set in place it would make the accumulator's Detail
        // its sibling: same text, another path.
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "### Detail", "", "Fine print.", "");
        string candidate = Doc("# Rules", "", "### One", "", "Alpha, a level down.", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf("Rules / One").Single()));

        Assert.Contains("\"Rules / One / Detail\", which would read as \"Rules / Detail\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_setext_heading_set_against_the_text_above_it_is_refused_rather_than_taking_that_text_into_its_title()
    {
        // The accumulator's preamble runs straight into its first heading, so a setext heading put there
        // would read "Above it." as the first line of its own title.
        string accumulator = Doc("Above it.", "# Second", "", "Body.", "");
        string candidate = Doc("First", "=====", "", "Lead.", "", "# Second", "", "Body.", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf("First").Single()));

        Assert.Contains("the text above the first heading", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_section_whose_title_reads_otherwise_in_the_accumulator_is_refused()
    {
        // The candidate defines the link its heading names; the accumulator does not, so there the same
        // line reads "[Foo] rules" — another path, and nothing else in the accumulator moves.
        string accumulator = Doc("# Rules", "", "## Links", "", "None here.", "");
        string candidate = Doc("# Rules", "", "## [Foo] rules", "", "Body.", "", "## Links", "", "[Foo]: https://example.com/", "");
        MarkdownOutline outline = MarkdownOutline.Read(candidate);

        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(
            () => AccumulatorEdits.Take(accumulator, outline, outline.IndicesOf("Rules / Foo rules").Single()));

        Assert.Contains("the section taken, which would read as \"Rules / [Foo] rules\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dropping_a_section_removes_its_lines_and_the_blank_lines_after_it()
    {
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "", "## Three", "", "Gamma.", "");

        (TextEdit edit, MarkdownSection dropped) = AccumulatorEdits.Drop(accumulator, "Rules / Two");

        Assert.Equal("Rules / Two", dropped.HeadingPath);
        Assert.Equal(Doc("# Rules", "", "## One", "", "Alpha.", "", "## Three", "", "Gamma.", ""), edit.ApplyTo(accumulator));
    }

    [Fact]
    public void Dropping_the_last_section_leaves_the_one_above_with_exactly_the_text_it_had()
    {
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "## Two", "", "Beta.", "");

        Assert.Equal(Doc("# Rules", "", "## One", "", "Alpha.", ""), Drop(accumulator, "Rules / Two"));
    }

    [Fact]
    public void Dropping_a_documents_only_section_from_its_first_line_leaves_nothing()
    {
        Assert.Equal(string.Empty, Drop(Doc("## Only", "", "Text.", ""), "Only"));
    }

    [Fact]
    public void A_drop_is_refused_where_the_accumulator_holds_the_path_never_or_twice()
    {
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "## One", "", "Alpha, again.", "");

        DecisionRefusedException never = Assert.Throws<DecisionRefusedException>(() => AccumulatorEdits.Drop(accumulator, "Rules / Two"));
        Assert.Contains("holds no section at \"Rules / Two\"", never.Message, StringComparison.Ordinal);

        DecisionRefusedException twice = Assert.Throws<DecisionRefusedException>(() => AccumulatorEdits.Drop(accumulator, "Rules / One"));
        Assert.Contains("holds 2 sections at \"Rules / One\"", twice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_drop_that_would_move_the_sections_under_it_is_refused()
    {
        string accumulator = Doc("# Rules", "", "## One", "", "Alpha.", "", "### Detail", "", "Fine print.", "", "## Two", "", "Beta.", "");

        DecisionRefusedException refused = Assert.Throws<DecisionRefusedException>(() => AccumulatorEdits.Drop(accumulator, "Rules / One"));

        Assert.Contains("\"Rules / One / Detail\", which would read as \"Rules / Detail\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_ending_of_any_of_the_three_kinds_counts_one_line_as_the_outline_does()
    {
        // The accumulator is LF by construction; the edits are placed by a line table of their own, and
        // a table that counted CRLF as two lines would put every edit after the first one off by a line.
        string accumulator = "# Rules\r\n\r\n## One\r\n\r\nAlpha.\r\n\r\n## Two\r\n\r\nBeta.\r\n";

        Assert.Equal("# Rules\r\n\r\n## One\r\n\r\nAlpha.\r\n", Drop(accumulator, "Rules / Two"));
    }
}
