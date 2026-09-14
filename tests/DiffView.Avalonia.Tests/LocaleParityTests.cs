namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00014 phase 2: the gate every shipped locale has to pass, landed before any translation
/// exists.
/// </summary>
/// <remarks>
/// The order is deliberate. A gate written after the data it judges gets written until it passes;
/// a gate written first has to be aimed at something known-wrong to show it works at all. So every
/// check here is exercised twice — once against an input carrying exactly the defect it hunts, and
/// once against a clean one, because a check that fires on everything is no better than one that
/// fires on nothing.
/// </remarks>
public sealed class LocaleParityTests
{
    private const string LocalizationDirectory = "src/DiffView.Avalonia/Localization";
    private const string FixtureDirectory = "fixtures/locales";

    /// <summary>
    /// The cultures the library ships — ClaudeForge's set, so a host localised for one is localised
    /// for both. A locale file appearing without a line here fails
    /// <see cref="The_shipped_locales_are_exactly_the_declared_set"/>, so shipping a translation
    /// stays a decision rather than a side effect of adding a file.
    /// </summary>
    private static readonly string[] DeclaredLocales =
        ["de-DE", "es-ES", "fr-FR", "ja-JP", "ko-KR", "pt-BR", "ru-RU", "zh-CN"];

    private static Dictionary<string, string> English => new(StringComparer.Ordinal)
    {
        ["Greeting"] = "Ready",
        ["Count"] = "{0} changes",
        ["Pair"] = "Line {0} · {1}",
        ["Technical"] = "UTF-8",
    };

    private static Dictionary<string, string> GoodTranslation => new(StringComparer.Ordinal)
    {
        ["Greeting"] = "Bereit",
        ["Count"] = "{0} Änderungen",
        ["Pair"] = "Zeile {0} · {1}",
        ["Technical"] = "UTF-8",
    };

    [Fact]
    public void A_clean_translation_trips_nothing()
    {
        // The guard on every other test in this class: a check that fires on a correct locale
        // would make all of them pass for the wrong reason.
        Assert.Empty(LocaleParity.MissingKeys("de-DE", English, GoodTranslation));
        Assert.Empty(LocaleParity.UnknownKeys("de-DE", English, GoodTranslation));
        Assert.Empty(LocaleParity.PlaceholderMismatches("de-DE", English, GoodTranslation));
        Assert.True(LocaleParity.UntranslatedShare(English, GoodTranslation) <= LocaleParity.MaxUntranslatedShare);
    }

    [Fact]
    public void A_locale_missing_a_key_is_caught()
    {
        Dictionary<string, string> locale = GoodTranslation;
        locale.Remove("Technical");

        LocaleParity.Violation violation = Assert.Single(LocaleParity.MissingKeys("de-DE", English, locale));
        Assert.Equal("Technical", violation.Key);
    }

    [Fact]
    public void A_locale_carrying_an_undeclared_key_is_caught()
    {
        Dictionary<string, string> locale = GoodTranslation;
        locale["Renamed.Long.Ago"] = "übrig";

        LocaleParity.Violation violation = Assert.Single(LocaleParity.UnknownKeys("de-DE", English, locale));
        Assert.Equal("Renamed.Long.Ago", violation.Key);
    }

    [Fact]
    public void A_locale_that_drops_a_placeholder_is_caught()
    {
        Dictionary<string, string> locale = GoodTranslation;
        locale["Count"] = "Änderungen";

        LocaleParity.Violation violation = Assert.Single(LocaleParity.PlaceholderMismatches("de-DE", English, locale));
        Assert.Equal("Count", violation.Key);
    }

    [Fact]
    public void A_locale_that_renumbers_a_placeholder_is_caught()
    {
        // The one that throws FormatException rather than rendering wrongly: {2} has no argument.
        Dictionary<string, string> locale = GoodTranslation;
        locale["Pair"] = "Zeile {0} · {2}";

        LocaleParity.Violation violation = Assert.Single(LocaleParity.PlaceholderMismatches("de-DE", English, locale));
        Assert.Equal("Pair", violation.Key);
    }

    [Fact]
    public void A_locale_that_adds_a_placeholder_is_caught()
    {
        // Plan 00010's rule arriving through a translator: a sentence English authors whole comes
        // back with a hole for a side word to be pasted into.
        Dictionary<string, string> locale = GoodTranslation;
        locale["Greeting"] = "{0} bereit";

        LocaleParity.Violation violation = Assert.Single(LocaleParity.PlaceholderMismatches("de-DE", English, locale));
        Assert.Equal("Greeting", violation.Key);
    }

    [Fact]
    public void An_escaped_brace_is_not_a_placeholder()
    {
        // Asymmetric on purpose, and the first draft of this test was not. A reader that does not
        // understand `{{` finds a placeholder in BOTH halves of a matched pair and compares them
        // equal, so a fixture where English and the locale escape the same braces cannot tell a
        // correct reader from a broken one — the defect cancels out. The mutation harness caught
        // that: blinding the regex to escapes left the symmetric version green.
        Dictionary<string, string> english = new(StringComparer.Ordinal) { ["Brace"] = "use {{0}} to escape" };
        Dictionary<string, string> escaped = new(StringComparer.Ordinal) { ["Brace"] = "mit {{0}} maskieren" };
        Dictionary<string, string> live = new(StringComparer.Ordinal) { ["Brace"] = "mit {0} maskieren" };

        // English has no placeholder here at all: both braces are escaped.
        Assert.Empty(LocaleParity.PlaceholderMismatches("de-DE", english, escaped));

        // The locale turned escaped text into a live placeholder, which is a defect, and is the
        // case a reader blind to escapes reports as identical.
        LocaleParity.Violation violation = Assert.Single(LocaleParity.PlaceholderMismatches("de-DE", english, live));
        Assert.Equal("Brace", violation.Key);
    }

    [Fact]
    public void A_locale_copied_from_English_and_never_translated_is_caught()
    {
        Assert.True(LocaleParity.UntranslatedShare(English, English) > LocaleParity.MaxUntranslatedShare);
    }

    [Fact]
    public void A_real_translation_keeping_the_technical_terms_is_not_flagged_as_a_copy()
    {
        // UTF-8 is the same word everywhere and translating it would be wrong. The threshold has to
        // clear that without clearing a copied file, which is the only thing it is for.
        double share = LocaleParity.UntranslatedShare(English, GoodTranslation);

        Assert.Equal(0.25, share, precision: 3);
        Assert.True(share <= LocaleParity.MaxUntranslatedShare);
    }

    [Fact]
    public void The_broken_fixture_locale_is_caught_reading_real_files()
    {
        // End to end over resx on disk: parsing, discovery and all three checks, against a file
        // carrying one of each defect. The pure checks above cannot prove the reader works.
        string directory = RepoPaths.Source(FixtureDirectory);
        IReadOnlyDictionary<string, string> locales = LocaleParity.DiscoverLocales(directory);

        string path = Assert.Contains("zz-ZZ", locales);
        Dictionary<string, string> neutral = LocaleParity.ReadResx(Path.Combine(directory, "Strings.resx"));
        Dictionary<string, string> broken = LocaleParity.ReadResx(path);

        Assert.Equal("Fixture.Technical", Assert.Single(LocaleParity.MissingKeys("zz-ZZ", neutral, broken)).Key);
        Assert.Equal("Fixture.Leftover", Assert.Single(LocaleParity.UnknownKeys("zz-ZZ", neutral, broken)).Key);

        List<string> mismatched = LocaleParity.PlaceholderMismatches("zz-ZZ", neutral, broken)
            .Select(v => v.Key)
            .ToList();
        Assert.Equal(["Fixture.Count", "Fixture.Pair"], mismatched);
    }

    [Fact]
    public void Every_shipped_locale_passes_every_check()
    {
        string directory = RepoPaths.Source(LocalizationDirectory);
        Dictionary<string, string> neutral = LocaleParity.ReadResx(Path.Combine(directory, "Strings.resx"));
        List<LocaleParity.Violation> violations = [];
        List<string> copies = [];

        foreach ((string culture, string path) in LocaleParity.DiscoverLocales(directory).OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            Dictionary<string, string> locale = LocaleParity.ReadResx(path);
            violations.AddRange(LocaleParity.MissingKeys(culture, neutral, locale));
            violations.AddRange(LocaleParity.UnknownKeys(culture, neutral, locale));
            violations.AddRange(LocaleParity.PlaceholderMismatches(culture, neutral, locale));

            double share = LocaleParity.UntranslatedShare(neutral, locale);
            if (share > LocaleParity.MaxUntranslatedShare)
            {
                copies.Add($"{culture} is {share:P0} identical to English");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
        Assert.True(copies.Count == 0, string.Join("\n", copies));
    }

    [Fact]
    public void The_shipped_locales_are_exactly_the_declared_set()
    {
        // Until phase 3 both sides are empty, and this test is a tripwire rather than a
        // measurement: it fails the moment a locale file appears that nobody declared.
        string[] found = LocaleParity.DiscoverLocales(RepoPaths.Source(LocalizationDirectory))
            .Keys
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(DeclaredLocales.OrderBy(c => c, StringComparer.Ordinal).ToArray(), found);
    }

    [Fact]
    public void Discovery_finds_locale_files_where_there_are_some()
    {
        // The tripwire above passes today by finding nothing, which would also be what a broken
        // discovery returns. This is the same function against a directory that does have one.
        IReadOnlyDictionary<string, string> found = LocaleParity.DiscoverLocales(RepoPaths.Source(FixtureDirectory));

        Assert.Equal(["zz-ZZ"], found.Keys.OrderBy(c => c, StringComparer.Ordinal).ToArray());
    }
}
