namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Which part of a pane a <see cref="DiffPaneContext"/> describes. Only
/// <see cref="Text"/> raises a menu in v1; the margins are named here so that giving them one
/// later adds a case rather than changing the type every consumer already reads.
/// </summary>
public enum DiffPaneRegion
{
    /// <summary>The text itself.</summary>
    Text,

    /// <summary>The line-number gutter, which also carries the copy arrows.</summary>
    LineNumberMargin,

    /// <summary>The change-marker gutter, which carries the chips and the modified-since-load bar.</summary>
    ChangeMarkerMargin,
}
