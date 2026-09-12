using System.Globalization;
using System.Resources;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// The localisation seam: the one-value state, the scope that restores it, and the generated
/// neutral resource the satellite assemblies hang off.
/// </summary>
/// <remarks>
/// The resource tests matter more than they look. Every string in the library now passes through
/// <c>ResourceManager</c>, and a wrong base name or an unembedded resx makes every lookup throw
/// <see cref="MissingManifestResourceException"/> and fall back to the compiled table — which is
/// the same rendered text, so the whole suite stays green over a mechanism that never runs.
/// </remarks>
public sealed class LocalizationTests
{
    private const string BaseName = "Bennewitz.Ninja.DiffView.Avalonia.Localization.Strings";
    private const string CommittedResx = "src/DiffView.Avalonia/Localization/Strings.resx";

    [Fact]
    public void The_neutral_resource_is_embedded_and_resolves()
    {
        ResourceManager resources = new(BaseName, typeof(DiffViewStrings).Assembly);

        // Not Assert.NotNull on one key: a base name that is wrong throws rather than returning
        // null, and a resx that is present but empty returns null for everything.
        string? ready = resources.GetString(DiffViewStrings.StateReady, CultureInfo.InvariantCulture);

        Assert.Equal(DiffViewStrings.EnglishDefaults[DiffViewStrings.StateReady], ready);
    }

    [Fact]
    public void The_committed_neutral_resx_carries_every_English_default_and_no_others()
    {
        Dictionary<string, string> committed = ReadResx(RepoPaths.Source(CommittedResx));

        Assert.Equal(
            DiffViewStrings.EnglishDefaults.OrderBy(e => e.Key, StringComparer.Ordinal).ToList(),
            committed.OrderBy(e => e.Key, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void A_resolver_that_returns_null_falls_through_to_English()
    {
        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Culture = CultureInfo.InvariantCulture,
            Resolver = (key, _) => key == DiffViewStrings.StateReady ? "Bereit" : null,
        }))
        {
            Assert.Equal("Bereit", DiffViewStrings.Get(DiffViewStrings.StateReady));
            Assert.Equal(
                DiffViewStrings.EnglishDefaults[DiffViewStrings.LeftPaneName],
                DiffViewStrings.Get(DiffViewStrings.LeftPaneName));
        }
    }

    [Fact]
    public void The_resolver_is_told_the_culture_being_resolved_for()
    {
        List<CultureInfo> seen = [];
        CultureInfo pinned = CultureInfo.GetCultureInfo("de-DE");

        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Culture = pinned,
            Resolver = (_, culture) =>
            {
                seen.Add(culture);
                return null;
            },
        }))
        {
            DiffViewStrings.Get(DiffViewStrings.StateReady);
        }

        Assert.Equal([pinned], seen);
    }

    [Fact]
    public void A_null_culture_follows_the_current_ui_culture()
    {
        List<CultureInfo> seen = [];

        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Resolver = (_, culture) =>
            {
                seen.Add(culture);
                return null;
            },
        }))
        {
            DiffViewStrings.Get(DiffViewStrings.StateReady);
        }

        Assert.Equal([CultureInfo.CurrentUICulture], seen);
    }

    [Fact]
    public void Override_restores_the_previous_state_when_the_body_throws()
    {
        DiffViewLocalization before = new() { Culture = CultureInfo.GetCultureInfo("fr-FR") };
        DiffViewStrings.Localization = before;

        try
        {
            // Cast: a lambda whose body only throws infers no return type, and xUnit's Func<Task>
            // overload wins the resolution — which would assert nothing here.
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using (DiffViewStrings.Override(new DiffViewLocalization { Culture = CultureInfo.InvariantCulture }))
                {
                    throw new InvalidOperationException("the body of a scope that must still be unwound");
                }
            }));

            Assert.Same(before, DiffViewStrings.Localization);
        }
        finally
        {
            DiffViewStrings.ResetForTesting();
        }
    }

    [Fact]
    public void Resetting_restores_both_the_resolver_and_the_culture()
    {
        DiffViewStrings.Localization = new DiffViewLocalization
        {
            Culture = CultureInfo.GetCultureInfo("ja-JP"),
            Resolver = (key, _) => key,
        };

        DiffViewStrings.ResetForTesting();

        // Both, in one assertion: the two-property version of this seam could restore one and
        // leave the other, and the failure would be a resolver answering in the wrong language.
        Assert.Same(DiffViewLocalization.Default, DiffViewStrings.Localization);
        Assert.Null(DiffViewStrings.Localization.Resolver);
        Assert.Null(DiffViewStrings.Localization.Culture);
    }

    [Fact]
    public void Nested_scopes_unwind_in_order()
    {
        CultureInfo outer = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo inner = CultureInfo.GetCultureInfo("ja-JP");

        using (DiffViewStrings.Override(new DiffViewLocalization { Culture = outer }))
        {
            Assert.Equal(outer, DiffViewStrings.Localization.Culture);

            using (DiffViewStrings.Override(new DiffViewLocalization { Culture = inner }))
            {
                Assert.Equal(inner, DiffViewStrings.Localization.Culture);
            }

            Assert.Equal(outer, DiffViewStrings.Localization.Culture);
        }

        Assert.Null(DiffViewStrings.Localization.Culture);
    }

    /// <summary>The <c>name</c> / <c>value</c> pairs of a resx, ignoring the schema and headers.</summary>
    private static Dictionary<string, string> ReadResx(string path)
    {
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                d => d.Attribute("name")!.Value,
                d => d.Element("value")!.Value,
                StringComparer.Ordinal);
    }
}
