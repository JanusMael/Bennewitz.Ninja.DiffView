using System.Reflection;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// The localization contract, adapted from ClaudeForge's <c>LocalizationParityTests</c>: every key
/// the library declares has an English default, and every key is reachable through the resolver a
/// host swaps in. Without the first, a key added without a default renders as its own name — the
/// user sees <c>Menu.CopySelection.Left</c> where a label should be, and nothing else notices.
/// </summary>
public sealed class StringCatalogueTests
{
    [Fact]
    public void Every_declared_key_has_an_English_default()
    {
        List<string> missing = [];
        foreach ((string name, string key) in DeclaredKeys())
        {
            if (DiffViewStrings.Get(key) == key)
            {
                missing.Add($"{name} (\"{key}\")");
            }
        }

        Assert.True(missing.Count == 0, "These keys have no English text, so they would render as their own key name: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_declared_key_goes_through_the_resolver()
    {
        List<string> asked = [];
        try
        {
            DiffViewStrings.Resolver = key =>
            {
                asked.Add(key);
                return null;
            };

            foreach ((_, string key) in DeclaredKeys())
            {
                DiffViewStrings.Get(key);
            }
        }
        finally
        {
            DiffViewStrings.Resolver = null;
        }

        // A host's translation reaches every one of them, so no string is quietly English-only.
        Assert.Equal(DeclaredKeys().Count, asked.Distinct().Count());
    }

    [Fact]
    public void No_two_keys_share_a_name()
    {
        List<(string Name, string Key)> declared = DeclaredKeys();
        List<string> duplicates = declared
            .GroupBy(k => k.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key + " on " + string.Join(" and ", g.Select(k => k.Name)))
            .ToList();

        // Two constants on one key is one of them silently winning; the English dictionary would
        // hold a single entry and the parity test above would still pass.
        Assert.True(duplicates.Count == 0, "Duplicate keys: " + string.Join("; ", duplicates));
    }

    /// <summary>Every <c>public const string</c> key <see cref="DiffViewStrings"/> declares.</summary>
    private static List<(string Name, string Key)> DeclaredKeys()
    {
        return typeof(DiffViewStrings)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!))
            .ToList();
    }
}
