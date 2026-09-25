using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The compiled form of <c>Themes/DiffFindBar.axaml</c>: the find bar's control theme, which
/// <see cref="DiffFindBar"/> merges into its own resources, so only a view with a find bar carries
/// it. A class rather than a runtime <c>ResourceInclude</c> because the include's loader resolves the
/// resource by reflection and the trimmer refuses it (IL2026); the XAML compiler resolves this class
/// at build time, which trims clean.
/// </summary>
public sealed partial class DiffFindBarTheme : ResourceDictionary
{
    /// <summary>Loads the compiled dictionary.</summary>
    public DiffFindBarTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
