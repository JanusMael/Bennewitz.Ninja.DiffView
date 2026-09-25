using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The compiled form of <c>Themes/DiffStatusStrip.axaml</c>: the status strip's control theme, which
/// <see cref="DiffStatusStrip"/> merges into its own resources. A class rather than a runtime
/// <c>ResourceInclude</c> because the include's loader resolves the resource by reflection and the
/// trimmer refuses it (IL2026); the XAML compiler resolves this class at build time, which trims
/// clean.
/// </summary>
public sealed partial class DiffStatusStripTheme : ResourceDictionary
{
    /// <summary>Loads the compiled dictionary.</summary>
    public DiffStatusStripTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
