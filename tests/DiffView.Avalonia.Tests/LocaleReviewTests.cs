using Bennewitz.Ninja.DiffView;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00016 §Phase 3: the committed review documents describe the strings they claim to.
/// </summary>
/// <remarks>
/// Each check is first shown catching a document broken in exactly that one way, built in memory
/// beside a clean one, the way <c>LocaleParityTests</c> proves its own gate. Then the whole set of
/// real documents goes through all of them at once — which is the test that fails the day somebody
/// corrects a translation and does not regenerate.
/// </remarks>
public sealed class LocaleReviewTests
{
    private const string Key = "Build.Completed";

    private static Dictionary<string, string> English => new(StringComparer.Ordinal)
    {
        [Key] = "Compared {0} rows in {1} ms",
    };

    private static Dictionary<string, string> Translated => new(StringComparer.Ordinal)
    {
        [Key] = "{0} Zeilen in {1} ms verglichen",
    };

    private static Dictionary<string, string> Context => new(StringComparer.Ordinal)
    {
        [Key] = "The success message after a build: {0} rows, {1} milliseconds.",
    };

    private static string Document(string? english = null, string? translation = null, string? context = null, string? holds = null, string? key = null)
    {
        return "| Key | What it is | Holds | English | de-DE |\n"
               + "|---|---|---|---|---|\n"
               + $"| `{key ?? Key}` | {context ?? Context[Key]} | {holds ?? "`{0}` `{1}`"} | {english ?? English[Key]} | {translation ?? Translated[Key]} |\n";
    }

    private static List<LocaleReview.Violation> Check(string markdown)
    {
        return LocaleReview.Check("de-DE", LocaleReview.Parse(markdown), English, Translated, Context);
    }

    [Fact]
    public void A_document_that_describes_its_strings_trips_nothing()
    {
        Assert.Empty(Check(Document()));
    }

    [Fact]
    public void A_row_whose_English_drifted_is_caught()
    {
        LocaleReview.Violation violation = Assert.Single(Check(Document(english: "Compared {0} rows in {1} milliseconds")));
        Assert.Contains("the library says", violation.What, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_whose_translation_drifted_is_caught()
    {
        LocaleReview.Violation violation = Assert.Single(Check(Document(translation: "{0} Zeilen verglichen")));
        Assert.Contains("the locale says", violation.What, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_whose_context_drifted_is_caught()
    {
        LocaleReview.Violation violation = Assert.Single(Check(Document(context: "The same, but after a build.")));
        Assert.Contains("summary", violation.What, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Holds_column_that_does_not_match_the_string_is_caught()
    {
        // The column a reviewer checks a translation's placeholders against. Wrong here, every
        // translation of this key looks wrong or looks fine for the wrong reason.
        LocaleReview.Violation violation = Assert.Single(Check(Document(holds: "`{0}`")));
        Assert.Contains("the string takes", violation.What, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_for_a_key_the_library_no_longer_declares_is_caught()
    {
        List<LocaleReview.Violation> violations = Check(Document(key: "Build.Retired"));

        // Two: the row describes nothing, and the key it displaced now has no row at all.
        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.What.Contains("not a key the library declares", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.What.Contains("has no row", StringComparison.Ordinal));
    }

    [Fact]
    public void A_key_with_no_row_at_all_is_caught()
    {
        LocaleReview.Violation violation = Assert.Single(Check("| Key | What it is | Holds | English | de-DE |\n|---|---|---|---|---|\n"));
        Assert.Contains("has no row", violation.What, StringComparison.Ordinal);
    }

    [Fact]
    public void An_escaped_pipe_survives_the_round_trip()
    {
        // No shipped string contains a pipe, so this is the only place the escaping and the reading
        // of it are ever exercised against each other. A parser that split on every pipe would tear
        // this row into six cells and report it as unreadable rather than as equal.
        Dictionary<string, string> english = new(StringComparer.Ordinal) { [Key] = "a | b" };
        Dictionary<string, string> translated = new(StringComparer.Ordinal) { [Key] = "a | b" };
        Dictionary<string, string> context = new(StringComparer.Ordinal) { [Key] = "Two things." };

        string markdown = "| Key | What it is | Holds | English | de-DE |\n"
                          + "|---|---|---|---|---|\n"
                          + $"| `{Key}` | Two things. | — | a \\| b | a \\| b |\n";

        Assert.Empty(LocaleReview.Check("de-DE", LocaleReview.Parse(markdown), english, translated, context));
    }

    [Fact]
    public void A_string_padded_with_spaces_survives_the_round_trip()
    {
        // Fold.Placeholder is " ⋯ {0} matching rows hidden " and the padding is the space around a
        // pill drawn in the gutter. Every markdown renderer trims a cell, so without the ␣ marker
        // the reviewer reads a string that is not the string, and returns a translation with the
        // padding helpfully removed. This is the test that says the marker is load-bearing.
        const string Padded = " ⋯ 1 matching row hidden ";
        Dictionary<string, string> english = new(StringComparer.Ordinal) { [Key] = Padded };
        Dictionary<string, string> translated = new(StringComparer.Ordinal) { [Key] = Padded };
        Dictionary<string, string> context = new(StringComparer.Ordinal) { [Key] = "The fold placeholder." };

        string markdown = "| Key | What it is | Holds | English | de-DE |\n"
                          + "|---|---|---|---|---|\n"
                          + $"| `{Key}` | The fold placeholder. | — | {LocaleReview.Space}⋯ 1 matching row hidden{LocaleReview.Space} "
                          + $"| {LocaleReview.Space}⋯ 1 matching row hidden{LocaleReview.Space} |\n";

        Assert.Empty(LocaleReview.Check("de-DE", LocaleReview.Parse(markdown), english, translated, context));
    }

    [Fact]
    public void A_padded_string_shown_without_its_padding_is_caught()
    {
        // The failure the marker exists to prevent, asserted rather than assumed: a document that
        // shows the trimmed string does not describe the string the library holds.
        Dictionary<string, string> english = new(StringComparer.Ordinal) { [Key] = " ⋯ 1 matching row hidden " };
        Dictionary<string, string> translated = new(StringComparer.Ordinal) { [Key] = " ⋯ 1 matching row hidden " };
        Dictionary<string, string> context = new(StringComparer.Ordinal) { [Key] = "The fold placeholder." };

        string markdown = "| Key | What it is | Holds | English | de-DE |\n"
                          + "|---|---|---|---|---|\n"
                          + $"| `{Key}` | The fold placeholder. | — | ⋯ 1 matching row hidden | ⋯ 1 matching row hidden |\n";

        Assert.Equal(2, LocaleReview.Check("de-DE", LocaleReview.Parse(markdown), english, translated, context).Count);
    }

    [Fact]
    public void Every_shipped_locale_has_a_review_document()
    {
        string directory = RepoPaths.Source(LocaleReview.Directory);
        List<string> cultures = Directory
            .GetFiles(RepoPaths.Source("src/DiffView.Avalonia/Localization"), "Strings.*.resx")
            .Select(path => Path.GetFileNameWithoutExtension(path)["Strings.".Length..])
            .OrderBy(culture => culture, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(cultures);
        foreach (string culture in cultures)
        {
            Assert.True(
                File.Exists(Path.Combine(directory, culture + ".md")),
                $"{culture} ships and has no review document, so nobody can review it. Run scripts/gen-locale-review.sh.");
        }
    }

    [Fact]
    public void Every_committed_review_document_describes_the_strings_it_claims_to()
    {
        IReadOnlyDictionary<string, string> summaries = Summaries.ByKey();
        List<LocaleReview.Violation> violations = [];

        foreach (string path in Directory.GetFiles(RepoPaths.Source(LocaleReview.Directory), "*.md").OrderBy(p => p, StringComparer.Ordinal))
        {
            string culture = Path.GetFileNameWithoutExtension(path);
            Dictionary<string, string> translated = ReadResx(
                RepoPaths.Source($"src/DiffView.Avalonia/Localization/Strings.{culture}.resx"));

            violations.AddRange(LocaleReview.Check(
                culture,
                LocaleReview.Parse(File.ReadAllText(path)),
                DiffViewStrings.EnglishDefaults,
                translated,
                summaries));
        }

        Assert.True(
            violations.Count == 0,
            "These review documents no longer describe the strings they were generated from — run "
            + "scripts/gen-locale-review.sh:\n  " + string.Join("\n  ", violations.Select(v => v.ToString())));
    }

    private static Dictionary<string, string> ReadResx(string path)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (System.Xml.Linq.XElement data in System.Xml.Linq.XDocument.Load(path).Root!.Elements("data"))
        {
            string? name = data.Attribute("name")?.Value;
            if (name is not null)
            {
                values[name] = data.Element("value")?.Value ?? string.Empty;
            }
        }

        return values;
    }
}
