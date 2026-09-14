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
    private static readonly string[] Documents = ["PROGRESS.md", "AGENTS.md", "DECISIONS.md", "CHANGELOG.md", "README.md"];

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
