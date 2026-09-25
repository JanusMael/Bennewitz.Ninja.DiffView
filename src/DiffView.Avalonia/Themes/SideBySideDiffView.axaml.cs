using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The compiled form of <c>Themes/SideBySideDiffView.axaml</c>: the control theme of
/// <see cref="SideBySideDiffView"/>, which the view merges into its own resources, the trim-safe way
/// <see cref="DiffPanePresenterTheme"/> established. The find bar, the headers and the status strip
/// it hosts merge their own — <see cref="DiffFindBarTheme"/>, <see cref="DiffPaneHeaderTheme"/> and
/// <see cref="DiffStatusStripTheme"/> — so no theme is found twice on the way up from any of them.
/// </summary>
public sealed partial class SideBySideDiffViewTheme : ResourceDictionary
{
    /// <summary>Loads the compiled dictionary.</summary>
    public SideBySideDiffViewTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
