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

        SectionIdentity expected = IdentityOf(plain, "Rules / Two");

        Assert.Equal(expected, IdentityOf(crlf, "Rules / Two"));
        Assert.Equal(expected, IdentityOf(nested, "Rules / Two"));
        Assert.Equal(64, expected.Hash.Length);
    }

    [Fact]
    public void Blank_lines_after_a_section_leave_its_version_alone_and_a_changed_word_does_not()
    {
        MarkdownSection middle = MarkdownOutline.Read("## One\n\nAlpha.\n\n## Two\n").Sections[0];
        MarkdownSection last = MarkdownOutline.Read("## One\n\nAlpha.").Sections[0];
        MarkdownSection spaced = MarkdownOutline.Read("## One\n\nAlpha.\n\n\n\n## Two\n").Sections[0];
        MarkdownSection reworded = MarkdownOutline.Read("## One\n\nAlpha, reworded.\n").Sections[0];
        MarkdownSection hardBreak = MarkdownOutline.Read("## One\n\nAlpha.  \n").Sections[0];

        Assert.Equal(SectionIdentity.Of(middle), SectionIdentity.Of(last));
        Assert.Equal(SectionIdentity.Of(middle), SectionIdentity.Of(spaced));
        Assert.NotEqual(SectionIdentity.Of(middle).Hash, SectionIdentity.Of(reworded).Hash);

        // Two trailing spaces are a markdown hard line break, so they are part of what the rule says.
        Assert.NotEqual(SectionIdentity.Of(middle).Hash, SectionIdentity.Of(hardBreak).Hash);
    }
}
