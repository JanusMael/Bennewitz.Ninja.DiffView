using System.Reflection;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests;

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
        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Resolver = (key, _) =>
            {
                asked.Add(key);
                return null;
            },
        }))
        {
            foreach ((_, string key) in DeclaredKeys())
            {
                DiffViewStrings.Get(key);
            }
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

        // Only the two bare side words are redirected; every other key keeps its English. A
        // string that pastes one of them shows the sentinel where the word would have been.
        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Resolver = (key, _) => key is DiffViewStrings.SideLeft or DiffViewStrings.SideRight ? Sentinel : null,
        }))
        {
            offenders.AddRange(await SideBySideTooltips());
            offenders.AddRange(await UnifiedTooltips());
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

    /// <summary>
    /// Plan 00016 §Phase 1: every key's <c>&lt;summary&gt;</c> accounts for every placeholder its
    /// string takes. The packet a native reviewer reads pairs each translation with that summary as
    /// its only context, one row at a time, so a summary that omits a placeholder — or claims one
    /// the string does not take — tells the reviewer something false about what they are reading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nine of 143 failed when this was first measured, in two kinds. Five never documented their
    /// placeholder anywhere: <c>Save.Failed</c> said only <em>"Reported when the write itself
    /// failed"</em> and never that <c>{0}</c> is the side's header title and <c>{1}</c> the reason.
    /// Four said <em>"The same, counterpart on the right"</em>, which is true and readable in this
    /// file, where the left sibling two lines above spells the placeholders out, and resolves to
    /// nothing in a row read on its own.
    /// </para>
    /// <para>
    /// Read from the XML documentation file rather than the source, because that is what the
    /// generator reads: a summary that survives the compiler and not the doc build would pass a
    /// source-reading test and still leave the packet's context column empty. Absence of the file
    /// is a failure, not a skip — the quiet version of this defect is a document that looks
    /// complete.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_summary_accounts_for_the_placeholders_its_string_takes()
    {
        IReadOnlyDictionary<string, string> summaries = Summaries.ByField();
        List<string> wrong = [];

        foreach ((string name, string key) in DeclaredKeys())
        {
            if (!summaries.TryGetValue(name, out string? summary))
            {
                wrong.Add($"{name} (\"{key}\") has no summary in the documentation file");
                continue;
            }

            // English rather than Get(key): the comparison is about what the string is made of,
            // which is the same sentence in every culture, so this stays inside the de-DE leg.
            HashSet<int> taken = LocaleParity.IndicesIn(DiffViewStrings.EnglishDefaults[key]);
            HashSet<int> accounted = LocaleParity.IndicesIn(summary);

            if (!taken.SetEquals(accounted))
            {
                wrong.Add($"{name} (\"{key}\"): the string takes {Describe(taken)}, the summary accounts for {Describe(accounted)}");
            }
        }

        Assert.True(wrong.Count == 0, "These summaries would mislead a reviewer about their own string: " + string.Join("; ", wrong));
    }

    /// <summary>
    /// Plan 00016 §Phase 3: no summary leans on the one above it.
    /// </summary>
    /// <remarks>
    /// The sibling of the placeholder test, and the reason that one is not enough. A reviewer meets
    /// each key as a single row in a table, so <em>"The same, rightwards."</em> is context that
    /// resolves to nothing — and it accounts for every one of its zero placeholders, so the
    /// placeholder gate passes it. The goal is that a row can be judged without reading the row
    /// above it; accounting for the placeholders was only ever a proxy for it.
    /// </remarks>
    [Fact]
    public void No_summary_leans_on_the_key_above_it()
    {
        string[] leaning = ["the same", "same ", "as above", "likewise", "ditto"];
        IReadOnlyDictionary<string, string> summaries = Summaries.ByField();
        List<string> dependent = [];

        foreach ((string name, string key) in DeclaredKeys())
        {
            if (summaries.TryGetValue(name, out string? summary)
                && leaning.Any(opening => summary.StartsWith(opening, StringComparison.OrdinalIgnoreCase)))
            {
                dependent.Add($"{name} (\"{key}\"): \"{summary}\"");
            }
        }

        Assert.True(
            dependent.Count == 0,
            "These summaries only make sense beside the one above them, and a review document shows a key on its own: "
            + string.Join("; ", dependent));
    }
    private static string Describe(HashSet<int> indices)
    {
        return indices.Count == 0
            ? "no placeholder"
            : string.Join(", ", indices.Order().Select(i => "{" + i.ToString(provider: null) + "}"));
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
