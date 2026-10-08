using System.Text.RegularExpressions;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The documents name tests as evidence; this checks they are tests that exist. `PROGRESS.md` once
/// recorded a *passing* verification for a test plan 00004 had deleted, and nothing noticed for six
/// plans, because nothing checks prose against the tests it names.
/// </summary>
/// <remarks>
/// A citation may legitimately name a test that is gone — saying so is how the record stays honest
/// about what was retired. Those lines say so in words, or strike themselves through, and are
/// skipped; a citation that reads as a live pass must resolve.
/// </remarks>
public sealed class DocumentationCitationTests
{
    private static readonly string[] Documents =
    [
        "PROGRESS.md", "AGENTS.md", "DECISIONS.md", "CHANGELOG.md", "README.md",
        // The hosting guide is read by people who cannot check it. Its API names are held to a
        // stricter gate than this one — HostingGuideTests resolves them against the shipped
        // surface — but a test name it cites is this file's business, like every other document's.
        "docs/hosting-diffview.md",
    ];

    /// <summary>
    /// Snake_case with at least four words: a test name, not a type or a member. The documents cite
    /// a test both bare and qualified, so the dotted prefix — optional, and repeating for a nested
    /// type — is matched but deliberately not captured: what has to resolve is the name after it.
    /// No example is spelled out here, because <see cref="AllTestSource"/> is a substring search
    /// over this file too, and a name written in a comment would resolve itself.
    /// </summary>
    /// <remarks>
    /// The prefix was not admitted at first, and a character class holding no <c>.</c> cannot match
    /// across one, so every qualified citation — 155 of them — was silently skipped. One of them had
    /// named a deleted test for four commits by the time this was widened.
    /// </remarks>
    private static readonly Regex Citation = new(@"`(?:[A-Z][A-Za-z0-9]*\.)*([A-Z][A-Za-z0-9]*(?:_[A-Za-z0-9]+){3,})`", RegexOptions.Compiled);

    /// <summary>Wording that marks a citation as history rather than as evidence.</summary>
    private static readonly string[] Retired =
    [
        "~~",
        "no longer exists",
        "is deleted",
        "was deleted",
        "retired",
        "went with",
        "had deleted",
    ];

    [Fact]
    public void Every_test_a_document_cites_as_evidence_exists()
    {
        string tests = AllTestSource();
        List<string> stale = [];

        foreach (string document in Documents)
        {
            string path = RepoPaths.Source(document);
            if (!File.Exists(path))
            {
                continue;
            }

            int number = 0;
            foreach (string line in File.ReadLines(path))
            {
                number++;
                if (Retired.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                foreach (Match match in Citation.Matches(line))
                {
                    string name = match.Groups[1].Value;
                    if (!tests.Contains(name, StringComparison.Ordinal))
                    {
                        stale.Add($"{document}:{number} cites {match.Value.Trim('`')}");
                    }
                }
            }
        }

        Assert.True(stale.Count == 0, "These documents name tests that do not exist: " + string.Join("; ", stale));
    }

    // ---------------------------------------------------------------------------------------------
    // Section references. `AGENTS.md` is the one document here with numbered sections, and the rest
    // of the prose points at them by number — 153 times, across two dozen files.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A section heading of <c>AGENTS.md</c>: <c>## 7. The unified view</c>.</summary>
    private static readonly Regex SectionHeading = new(@"^##\s+(\d+)\.\s", RegexOptions.Compiled);

    /// <summary>A reference to a numbered section: <c>§7</c>, and the two ends of <c>§1–§6</c>.</summary>
    private static readonly Regex SectionReference = new(@"§\s*(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// A markdown filename, which is how a reference says which document's sections it means. Matched
    /// without its directory, so <c>docs/UI-STYLE-GUIDE.md</c> is recognised by its last segment.
    /// </summary>
    private static readonly Regex MarkdownFile = new(@"[A-Za-z0-9_.\-]+\.md", RegexOptions.Compiled);

    private const string Sectioned = "AGENTS.md";

    /// <summary>
    /// Floors, not counts: what has to hold is that the reading found the bulk of the prose, so a
    /// regex that stopped matching is red rather than vacuously green. Measured at 153 references
    /// over 24 documents, and the document floor is what requires <c>plans/</c> to still be scanned —
    /// 19 of those 24 are plans. Citations only accumulate, so these do not need revisiting; if a
    /// change ever takes the reading below them, that is itself worth looking at.
    /// </summary>
    private const int ReferenceFloor = 100;
    private const int DocumentFloor = 15;

    /// <summary>The directories whose every authored document may cite a section.</summary>
    private static readonly string[] SectionCitingDirectories = ["docs", "plans"];

    /// <summary>
    /// Every <c>§N</c> that names a section of <c>AGENTS.md</c> names one that exists. Plan 00023
    /// phase 5 rewrote §9 wholesale — 75 lines restructured and the section retitled — and nothing
    /// in the suite checked that `DECISIONS.md`'s five citations of it still landed anywhere. Four of
    /// those five are covered here; the fifth writes §9 bare, which <b>Scope</b> below explains is not
    /// attributable to this document by reading one line.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>This checks that a section exists, never that it still says what the citing sentence
    /// claims.</b> Nothing here reads meaning, and a keyword grep would be the wrong instrument
    /// rather than a weaker one: §9's rewrite kept every trap it had and reworded them, so a grep
    /// for <c>IsViewable</c> reports a live finding as lost while <c>map state</c>, <c>BadMatch</c>
    /// and <c>error_code</c> carry it. Two sessions nearly filed that as a regression. A gate that
    /// looked like it checked meaning would be worse than this one, because it would be trusted.
    /// <para>
    /// <b>Scope.</b> A reference is checked when the nearest markdown filename before it on its own
    /// line is <c>AGENTS.md</c>, and — inside <c>AGENTS.md</c> — when no filename precedes it at all,
    /// which is a self-reference. Everything else is skipped, because <c>§N</c> is not a repo-wide
    /// synonym for an <c>AGENTS.md</c> section: plan 00026's bare <c>§4</c> and <c>§5</c> mean
    /// sections of XamlQuality's <c>docs/ai-drivable-ui.md</c>, and plan 00001 cites ClaudeForge's
    /// <c>docs/UI-STYLE-GUIDE.md</c> §2. Resolving those here would read as coverage and would later
    /// blame a frozen plan for a section it never cited.
    /// </para>
    /// <para>
    /// One attribution this cannot make is between this repository's <c>AGENTS.md</c> and another's:
    /// a line reading <i>ClaudeForge's <c>AGENTS.md</c> §3</i> would be resolved here. No line does
    /// today — both mentions of a sibling's <c>AGENTS.md</c> cite no section — and the honest remedy
    /// if one appears is to name the repository outside the reference, not to teach a regex which
    /// checkout is meant.
    /// </para>
    /// <para>
    /// <b>Why <c>plans/</c> is in scope, where the test-name gate above excludes it.</b> The two
    /// differ in where a failure can be repaired. A plan naming a retired test cannot be repaired
    /// anywhere — the test is gone and the plan may not be edited — but a section number can always
    /// be repaired in <c>AGENTS.md</c>, which is a living document. So a section number is an
    /// <i>anchor</i>: retitle and rewrite a section freely, as §9 was, but do not remove or renumber
    /// one. The failure below therefore names the section and not the citation, and sends the repair
    /// to the one file that is allowed to change.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_section_of_AGENTS_a_document_cites_by_number_exists()
    {
        HashSet<int> sections = [];
        foreach (string line in File.ReadLines(RepoPaths.Source(Sectioned)))
        {
            Match heading = SectionHeading.Match(line);
            if (heading.Success)
            {
                sections.Add(int.Parse(heading.Groups[1].ValueSpan));
            }
        }

        Assert.True(sections.Count > 0, $"{Sectioned} declares no numbered sections, so nothing could be resolved against it.");

        SortedDictionary<int, List<string>> missing = [];
        HashSet<string> documentsRead = [];
        int read = 0;

        foreach (string document in SectionCitingDocuments())
        {
            bool isSectioned = document == Sectioned;
            int number = 0;

            foreach (string line in File.ReadLines(RepoPaths.Source(document)))
            {
                number++;
                MatchCollection references = SectionReference.Matches(line);
                if (references.Count == 0)
                {
                    continue;
                }

                MatchCollection files = MarkdownFile.Matches(line);
                foreach (Match reference in references)
                {
                    string? nearest = null;
                    foreach (Match file in files)
                    {
                        if (file.Index < reference.Index)
                        {
                            nearest = file.Value;
                        }
                    }

                    bool ours = nearest is null ? isSectioned : nearest == Sectioned;
                    if (!ours)
                    {
                        continue;
                    }

                    read++;
                    documentsRead.Add(document);

                    int cited = int.Parse(reference.Groups[1].ValueSpan);
                    if (!sections.Contains(cited))
                    {
                        if (!missing.TryGetValue(cited, out List<string>? where))
                        {
                            missing[cited] = where = [];
                        }

                        where.Add($"{document}:{number}");
                    }
                }
            }
        }

        // The positive control. Without it a reference regex that matched nothing would pass, which
        // is how this file's test-name citation regex silently skipped 155 citations for four commits.
        Assert.True(
            read >= ReferenceFloor && documentsRead.Count >= DocumentFloor,
            $"This reading found only {read} section references over {documentsRead.Count} documents, under its floor of "
                + $"{ReferenceFloor} over {DocumentFloor}. It is not reading the prose it is supposed to be reading, so it "
                + "proves nothing about the citations — repair the reading before trusting a green here.");

        Assert.True(
            missing.Count == 0,
            $"{Sectioned} has no such section, and these citations point at it. A section number is an anchor: retitle and "
                + "rewrite a section freely, but keep its number — approved plans cite these numbers and are never edited, so "
                + "the repair belongs in "
                + Sectioned
                + " as a heading that stays and says where its content went. "
                + string.Join("; ", missing.Select(entry => $"§{entry.Key} ← {Sample(entry.Value)}")));
    }

    /// <summary>
    /// Enough citations to see the scale of what a missing section strands, and no more. Which lines
    /// they are does not change the repair — the number is the anchor, and the citations are not what
    /// gets edited — so a complete list would only make a failure over several sections unreadable.
    /// </summary>
    private static string Sample(List<string> where)
    {
        const int Shown = 8;
        return where.Count <= Shown
            ? string.Join(", ", where)
            : string.Join(", ", where.Take(Shown)) + $" (+{where.Count - Shown} more)";
    }

    /// <summary>
    /// Every document that points at a numbered section: the top-level record, the authored documents
    /// under <c>docs/</c>, and every plan. <c>docs/locale-review/</c> is left out because it is
    /// generated from the string table and cites nothing — a reference appearing there would be the
    /// generator's to fix, not a citation's.
    /// </summary>
    private static IEnumerable<string> SectionCitingDocuments()
    {
        foreach (string document in Documents)
        {
            if (File.Exists(RepoPaths.Source(document)))
            {
                yield return document;
            }
        }

        foreach (string directory in SectionCitingDirectories)
        {
            string path = RepoPaths.Source(directory);
            if (!Directory.Exists(path))
            {
                continue;
            }

            IEnumerable<string> found = Directory
                .EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly)
                .Select(file => $"{directory}/{Path.GetFileName(file)}")
                .Where(relative => !Documents.Contains(relative, StringComparer.Ordinal))
                .OrderBy(relative => relative, StringComparer.Ordinal);

            foreach (string relative in found)
            {
                yield return relative;
            }
        }
    }

    private static string AllTestSource()
    {
        string root = RepoPaths.Source("tests");
        IEnumerable<string> files = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        return string.Join("\n", files.Select(File.ReadAllText));
    }
}
