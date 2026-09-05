namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Locations of the resources a host includes to style the DiffView controls.
/// </summary>
public static class DiffViewResources
{
    /// <summary>
    /// The one style include every host adds:
    /// <c>&lt;StyleInclude Source="avares://DiffView.Avalonia/Themes/DiffView.axaml" /&gt;</c>.
    /// It defines every <c>DiffView.*</c> token the controls use, for every theme variant, and
    /// never references a host theme's own keys.
    /// </summary>
    public static Uri ThemeUri { get; } = new("avares://DiffView.Avalonia/Themes/DiffView.axaml");
}
