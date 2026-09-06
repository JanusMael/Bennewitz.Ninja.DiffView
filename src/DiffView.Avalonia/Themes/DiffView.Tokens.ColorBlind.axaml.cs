using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The compiled form of <c>Themes/DiffView.Tokens.ColorBlind.axaml</c>, the colour-blind-safe
/// palette. A host that switches palettes at runtime merges an instance into its application
/// resources after the theme include and removes it to switch back; the class keeps that
/// trim-safe where a runtime <c>ResourceInclude</c> is not. <see cref="DiffViewResources.ColorBlindTokensUri"/>
/// is the same dictionary for a XAML include.
/// </summary>
public sealed partial class DiffViewColorBlindPalette : ResourceDictionary
{
    /// <summary>Loads the compiled dictionary.</summary>
    public DiffViewColorBlindPalette()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
