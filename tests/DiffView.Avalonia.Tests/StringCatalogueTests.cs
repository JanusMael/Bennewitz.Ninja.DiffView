using System.Reflection;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Inline;
using Bennewitz.Ninja.DiffView.Core;

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

    /// <summary>
    /// The rule the side-naming strings were rebuilt around: a side word is part of the sentence,
    /// not a runtime value, so every string that names one is a whole sentence per direction and
    /// nothing ever asks for the bare word and pastes it in. A translator cannot inflect a word
    /// dropped into someone else's sentence — German wants <em>linke Zeile</em> beside <em>nach
    /// links</em>, and one pasted word cannot be both.
    /// </summary>
    [AvaloniaFact]
    public async Task No_string_of_the_library_is_built_by_pasting_a_side_word_into_it()
    {
        const string Sentinel = "«PASTED»";
        List<string> offenders = [];

        try
        {
            // Only the two bare side words are redirected; every other key keeps its English. A
            // string that pastes one of them shows the sentinel where the word would have been.
            DiffViewStrings.Resolver = key =>
                key is DiffViewStrings.SideLeft or DiffViewStrings.SideRight ? Sentinel : null;

            offenders.AddRange(await SideBySideTooltips());
            offenders.AddRange(await UnifiedTooltips());
        }
        finally
        {
            DiffViewStrings.Resolver = null;
        }

        Assert.True(offenders.Count == 0, "These are built by pasting a side word in: " + string.Join(" | ", offenders.Distinct()));
    }

    private static async Task<List<string>> SideBySideTooltips()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nfour\n");

        // Editable, so the copy arrows exist; selected, so the selection arrow does too. Both put
        // a line of their own on the tooltip and both name the side receiving the copy.
        host.View.LeftReadOnly = false;
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Left.Select(4, 3);
        host.Capture().Dispose();

        List<string> found = [];
        foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
        {
            for (int line = 1; line <= pane.Document.LineCount; line++)
            {
                Collect(found, pane.LineNumberMargin.TooltipFor(line));
                Collect(found, pane.ChangeMarkerMargin.TooltipFor(line));
            }
        }

        // The overview map's lane tooltips name a side outright.
        if (host.View.Minimap is { } minimap)
        {
            for (double y = 1; y < minimap.Bounds.Height; y += 4)
            {
                Collect(found, minimap.TooltipFor(2, y));
                Collect(found, minimap.TooltipFor(minimap.Bounds.Width - 2, y));
            }
        }

        return found;
    }

    private static async Task<List<string>> UnifiedTooltips()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        // The unified gutter names the line's own side, which is the other half of the rule.
        List<string> found = [];
        for (int line = 1; line <= host.Pane.Document.LineCount; line++)
        {
            Collect(found, host.Pane.LineNumberMargin.TooltipFor(line));
            Collect(found, host.Pane.ChangeMarkerMargin.TooltipFor(line));
        }

        return found;
    }

    private static void Collect(List<string> found, string? tooltip)
    {
        if (tooltip is not null && tooltip.Contains("«PASTED»", StringComparison.Ordinal))
        {
            found.Add(tooltip);
        }
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
