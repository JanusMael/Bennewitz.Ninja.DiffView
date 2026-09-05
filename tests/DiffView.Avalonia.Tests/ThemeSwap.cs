using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Semi.Avalonia;
using ThemeVariant = Avalonia.Styling.ThemeVariant;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>The ten theme targets of plan 00001: Fluent and Simple in two variants, Semi in six.</summary>
internal static class ThemeTargets
{
    public static TheoryData<string, string> All => new()
    {
        { "Fluent", "Light" }, { "Fluent", "Dark" },
        { "Simple", "Light" }, { "Simple", "Dark" },
        { "Semi", "Light" }, { "Semi", "Dark" }, { "Semi", "Aquatic" }, { "Semi", "Desert" }, { "Semi", "Dusk" }, { "Semi", "NightSky" },
    };

    public static TheoryData<string> SemiVariants => ["Light", "Dark", "Aquatic", "Desert", "Dusk", "NightSky"];
}

/// <summary>
/// Puts the named theme in place of the test application's Semi and requests a variant;
/// disposing restores Semi and the previous variant, so the snapshot tests keep their host.
/// </summary>
internal sealed class ThemeSwap : IDisposable
{
    private readonly IStyle _original;
    private readonly IStyle _replacement;
    private readonly ThemeVariant? _previousVariant;

    private ThemeSwap(IStyle original, IStyle replacement, ThemeVariant? previousVariant, ThemeVariant variant)
    {
        _original = original;
        _replacement = replacement;
        _previousVariant = previousVariant;
        Variant = variant;
    }

    public ThemeVariant Variant { get; }

    public static ThemeSwap To(string theme, string variant)
    {
        Application app = Application.Current!;
        IStyle original = app.Styles[0];
        Assert.IsType<SemiTheme>(original);

        IStyle replacement = theme switch
        {
            "Fluent" => new FluentTheme(),
            "Simple" => new SimpleTheme(),
            "Semi" => original,
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "unknown theme"),
        };
        if (!ReferenceEquals(replacement, original))
        {
            app.Styles.RemoveAt(0);
            app.Styles.Insert(0, replacement);
        }

        ThemeVariant requested = variant switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            "Aquatic" => SemiTheme.Aquatic,
            "Desert" => SemiTheme.Desert,
            "Dusk" => SemiTheme.Dusk,
            "NightSky" => SemiTheme.NightSky,
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "unknown variant"),
        };
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = requested;
        return new ThemeSwap(original, replacement, previous, requested);
    }

    public void Dispose()
    {
        Application app = Application.Current!;
        if (!ReferenceEquals(_replacement, _original))
        {
            app.Styles.RemoveAt(0);
            app.Styles.Insert(0, _original);
        }

        app.RequestedThemeVariant = _previousVariant;
    }
}
