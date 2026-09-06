using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The compiled form of <c>Themes/SideBySideDiffView.axaml</c>: the control themes of
/// <see cref="SideBySideDiffView"/>, <see cref="DiffPaneHeader"/> and <see cref="DiffStatusStrip"/>.
/// Each of the three merges a fresh instance into its own resources, the trim-safe way
/// <see cref="DiffPanePresenterTheme"/> established.
/// </summary>
public sealed partial class SideBySideDiffViewTheme : ResourceDictionary
{
    /// <summary>Loads the compiled dictionary.</summary>
    public SideBySideDiffViewTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
