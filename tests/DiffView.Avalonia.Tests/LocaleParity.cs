using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// The checks a shipped locale has to pass, as pure functions over the neutral and locale
/// dictionaries.
/// </summary>
/// <remarks>
/// Pure on purpose. The gate lands in plan 00014 phase 2, <em>before</em> any translation exists,
/// so there is nothing real to run it against yet — and a gate first exercised by the data it is
/// meant to judge is a gate nobody has tested. Taking dictionaries rather than paths lets every
/// check be aimed at a deliberately broken input and seen to fail.
/// </remarks>
internal static class LocaleParity
{
    /// <summary>One thing wrong with one locale.</summary>
    internal sealed record Violation(string Culture, string Key, string Detail)
    {
        public override string ToString()
        {
            return $"{Culture}: {Key} — {Detail}";
        }
    }

    /// <summary>
    /// A <c>string.Format</c> placeholder: <c>{0}</c>, <c>{0,-8}</c>, <c>{0:N0}</c> and the two
    /// together. The doubled-brace alternatives come first so an escaped <c>{{</c> is consumed as
    /// itself rather than read as the start of a placeholder.
    /// </summary>
    private static readonly Regex Placeholder = new(
        @"\{\{|\}\}|\{(\d+)(?:,-?\d+)?(?::[^}]*)?\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Above this share of values byte-identical to English, a locale is a copy rather than a
    /// translation.
    /// </summary>
    /// <remarks>
    /// Not zero, because some values are the same in every language: <c>UTF-8</c>, <c>LF</c>,
    /// <c>CRLF</c>, <c>C#</c>. Those are a named non-goal for translation and there are about ten of
    /// them in 143 keys, so a real translation sits near 0.07 and a copied file at 1.00. The
    /// threshold only has to separate those two. Borrowed from ClaudeForge's
    /// <c>LocalizationParityTests</c>, which catches the resx duplicated and never translated —
    /// exactly the failure mode of a machine-generated locale, where a model that declines to
    /// translate a term returns the English one and neither key nor placeholder parity notices.
    /// </remarks>
    internal const double MaxUntranslatedShare = 0.8;

    /// <summary>Keys the neutral set declares that this locale does not answer.</summary>
    internal static IReadOnlyList<Violation> MissingKeys(
        string culture,
        IReadOnlyDictionary<string, string> neutral,
        IReadOnlyDictionary<string, string> locale)
    {
        return neutral.Keys
            .Where(key => !locale.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new Violation(culture, key, "missing; the key would fall back to English mid-sentence"))
            .ToList();
    }

    /// <summary>Keys this locale carries that the neutral set does not declare.</summary>
    /// <remarks>
    /// A key nothing asks for is dead weight, but it is more often a renamed key left behind, which
    /// is the same defect as a missing one wearing a different face.
    /// </remarks>
    internal static IReadOnlyList<Violation> UnknownKeys(
        string culture,
        IReadOnlyDictionary<string, string> neutral,
        IReadOnlyDictionary<string, string> locale)
    {
        return locale.Keys
            .Where(key => !neutral.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new Violation(culture, key, "not a declared key; a rename probably left it behind"))
            .ToList();
    }

    /// <summary>
    /// Keys whose placeholder set differs from English — a dropped, added or renumbered
    /// <c>{N}</c>.
    /// </summary>
    /// <remarks>
    /// The failure this catches renders as literal braces to a user, or throws
    /// <see cref="FormatException"/> where an index has no argument, and no other check sees it.
    /// An <em>added</em> placeholder is also how a side word gets pasted back into a sentence the
    /// English authors as a whole — plan 00010's rule, arriving through a translator instead.
    /// </remarks>
    internal static IReadOnlyList<Violation> PlaceholderMismatches(
        string culture,
        IReadOnlyDictionary<string, string> neutral,
        IReadOnlyDictionary<string, string> locale)
    {
        List<Violation> violations = [];

        foreach ((string key, string english) in neutral.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!locale.TryGetValue(key, out string? translated))
            {
                continue;   // MissingKeys owns that one; reporting it twice helps nobody.
            }

            HashSet<int> expected = IndicesIn(english);
            HashSet<int> actual = IndicesIn(translated);

            if (!expected.SetEquals(actual))
            {
                violations.Add(new Violation(
                    culture,
                    key,
                    $"placeholders {Describe(expected)} in English, {Describe(actual)} here"));
            }
        }

        return violations;
    }

    /// <summary>The share of shared keys whose translation is byte-identical to the English.</summary>
    internal static double UntranslatedShare(
        IReadOnlyDictionary<string, string> neutral,
        IReadOnlyDictionary<string, string> locale)
    {
        List<string> shared = neutral.Keys.Where(locale.ContainsKey).ToList();
        if (shared.Count == 0)
        {
            return 0;
        }

        int identical = shared.Count(key => string.Equals(neutral[key], locale[key], StringComparison.Ordinal));
        return (double)identical / shared.Count;
    }

    /// <summary>The <c>name</c> / <c>value</c> pairs of a resx, ignoring the schema and headers.</summary>
    internal static Dictionary<string, string> ReadResx(string path)
    {
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    /// <summary>The locale files beside a neutral <c>Strings.resx</c>, keyed by culture name.</summary>
    internal static IReadOnlyDictionary<string, string> DiscoverLocales(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return Directory
            .EnumerateFiles(directory, "Strings.*.resx")
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path)["Strings.".Length..],
                path => path,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// The placeholder indices <paramref name="text"/> takes. Internal rather than private because
    /// <see cref="StringCatalogueTests"/> asks the same question of a documentation comment, and a
    /// second regex for it would be a second regex to get <c>{{</c> wrong in.
    /// </summary>
    internal static HashSet<int> IndicesIn(string text)
    {
        HashSet<int> indices = [];
        foreach (Match match in Placeholder.Matches(text))
        {
            if (match.Groups[1].Success)
            {
                indices.Add(int.Parse(match.Groups[1].ValueSpan, provider: null));
            }
        }

        return indices;
    }

    private static string Describe(HashSet<int> indices)
    {
        return indices.Count == 0
            ? "none"
            : "{" + string.Join(",", indices.OrderBy(i => i)) + "}";
    }
}
