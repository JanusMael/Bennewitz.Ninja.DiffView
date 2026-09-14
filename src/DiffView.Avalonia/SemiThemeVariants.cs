using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Semi.Avalonia's four high-contrast theme variants by name, without a reference to Semi.
/// <see cref="ThemeVariant"/> equality is by key, and Semi declares each as
/// <c>new ThemeVariant("Aquatic", ThemeVariant.Dark)</c> and so on, so a dictionary keyed by
/// these matches the variant Semi requests at runtime. The generated <c>Themes/Compat</c>
/// dictionaries use them for the per-variant entries Avalonia's <c>ThemeVariant</c> converter
/// cannot express as a string, and a host can use them to request a Semi variant by name.
/// </summary>
public static class SemiThemeVariants
{
    /// <summary>Semi's Aquatic variant: high contrast, inherits Dark.</summary>
    public static ThemeVariant Aquatic { get; } = new("Aquatic", ThemeVariant.Dark);

    /// <summary>Semi's Desert variant: high contrast, inherits Light.</summary>
    public static ThemeVariant Desert { get; } = new("Desert", ThemeVariant.Light);

    /// <summary>Semi's Dusk variant: high contrast, inherits Dark.</summary>
    public static ThemeVariant Dusk { get; } = new("Dusk", ThemeVariant.Dark);

    /// <summary>Semi's NightSky variant: high contrast, inherits Dark.</summary>
    public static ThemeVariant NightSky { get; } = new("NightSky", ThemeVariant.Dark);
}
