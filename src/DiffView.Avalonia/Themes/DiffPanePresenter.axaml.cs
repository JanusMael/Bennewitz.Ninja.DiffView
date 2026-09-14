using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The compiled form of <c>Themes/DiffPanePresenter.axaml</c>: the presenter's control theme and
/// the text-area theme it applies by <see cref="DiffPanePresenter.TextAreaThemeKey"/>. A class
/// rather than a runtime <c>ResourceInclude</c> because the include's loader resolves the
/// resource by reflection and the trimmer refuses it (IL2026); the XAML compiler resolves this
/// class at build time, which trims clean.
/// </summary>
public sealed partial class DiffPanePresenterTheme : ResourceDictionary
{
    /// <summary>Loads the compiled dictionary.</summary>
    public DiffPanePresenterTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
