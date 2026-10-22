using System.Text;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 decision 6, as step 6 first needs it: a section is its heading path and a hash of its
/// normalised text, so one rule on two machines is one identity however each machine stores it.
/// </summary>
public sealed class SectionIdentityTests
{
    private const string Plain = "# Rules\n\n## One\n\nAlpha.\n\n## Two\n\nBeta.  \nwith a hard break.\n";

    private static SectionIdentity IdentityOf(IngestedDocument document, string path) =>
        SectionIdentity.Of(document.Outline.Sections.Single(s => s.HeadingPath == path));

    [Fact]
    public void Line_endings_and_a_list_nesting_do_not_change_a_sections_identity()
    {
        // ⭐ The two shapes plan 00034 §2 measured on real machines: CRLF, and a whole document nested in
        // one list item with every line indented two spaces. Hashed raw, neither matches the plain file.
        IngestedDocument plain = IngestedDocument.FromBytes(Encoding.UTF8.GetBytes(Plain));
        IngestedDocument crlf = IngestedDocument.FromBytes(Encoding.UTF8.GetBytes(Plain.Replace("\n", "\r\n", StringComparison.Ordinal)));
        IngestedDocument nested = IngestedDocument.FromBytes(Encoding.UTF8.GetBytes(
            "- # Rules\n  \n  ## One\n  \n  Alpha.\n  \n  ## Two\n  \n  Beta.  \n  with a hard break.\n"));

        SectionIdentity expected = IdentityOf(plain, "Two");

        Assert.Equal(expected, IdentityOf(crlf, "Two"));
        Assert.Equal(expected, IdentityOf(nested, "Two"));
        Assert.Equal(64, expected.Hash.Length);
    }

    [Fact]
    public void One_rule_in_two_files_titled_differently_is_one_identity()
    {
        // ⭐ Open item 22: each project's AGENTS.md has a title of its own and the same first rule under
        // it. The title is in no path, so the rule is one identity; the two titles share the empty path
        // and differ in their text, so they are two versions of one section.
        MarkdownOutline ours = MarkdownOutline.Read("# AGENTS.md — cross-file contracts for DiffView\n\n## 1. Hard invariants\n\nRule.\n");
        MarkdownOutline theirs = MarkdownOutline.Read("# AGENTS.md — operational rules for LLM contributors\n\n## 1. Hard invariants\n\nRule.\n");

        Assert.Equal(SectionIdentity.Of(ours.Sections[1]), SectionIdentity.Of(theirs.Sections[1]));
        Assert.Equal("1. Hard invariants", SectionIdentity.Of(ours.Sections[1]).HeadingPath);

        SectionIdentity ourTitle = SectionIdentity.Of(ours.Sections[0]);
        SectionIdentity theirTitle = SectionIdentity.Of(theirs.Sections[0]);
        Assert.Equal(string.Empty, ourTitle.HeadingPath);
        Assert.Equal(ourTitle.HeadingPath, theirTitle.HeadingPath);
        Assert.NotEqual(ourTitle.Hash, theirTitle.Hash);
    }

    [Fact]
    public void Blank_lines_after_a_section_leave_its_version_alone_and_a_changed_word_does_not()
    {
        // Each document has the same title, so One's path is the same in all of them: a heading alone in
        // its file would be that file's title, with a path of its own.
        MarkdownSection middle = MarkdownOutline.Read("# Rules\n\n## One\n\nAlpha.\n\n## Two\n").Sections[1];
        MarkdownSection last = MarkdownOutline.Read("# Rules\n\n## One\n\nAlpha.").Sections[1];
        MarkdownSection spaced = MarkdownOutline.Read("# Rules\n\n## One\n\nAlpha.\n\n\n\n## Two\n").Sections[1];
        MarkdownSection reworded = MarkdownOutline.Read("# Rules\n\n## One\n\nAlpha, reworded.\n").Sections[1];
        MarkdownSection hardBreak = MarkdownOutline.Read("# Rules\n\n## One\n\nAlpha.  \n").Sections[1];

        Assert.Equal(SectionIdentity.Of(middle), SectionIdentity.Of(last));
        Assert.Equal(SectionIdentity.Of(middle), SectionIdentity.Of(spaced));
        Assert.NotEqual(SectionIdentity.Of(middle).Hash, SectionIdentity.Of(reworded).Hash);

        // Two trailing spaces are a markdown hard line break, so they are part of what the rule says.
        Assert.NotEqual(SectionIdentity.Of(middle).Hash, SectionIdentity.Of(hardBreak).Hash);
    }
}
