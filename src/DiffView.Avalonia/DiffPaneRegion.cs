namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Which surface a <see cref="DiffPaneContext"/> describes. Plan 00010 shaped this as an enum
/// with room rather than a <c>bool IsText</c> so that giving another surface a menu would be a
/// case rather than a redesign; plans 00012's first two phases are that bet paying off.
/// </summary>
/// <remarks>
/// There is deliberately no <c>Header</c> member. A header's subject is a side and its file, with
/// no line at all, so <see cref="DiffPaneContext"/> cannot describe one — and an enum value its
/// own context type cannot describe is how an enum starts lying. The header carries its own
/// sibling record instead.
/// </remarks>
public enum DiffPaneRegion
{
    /// <summary>The text itself.</summary>
    Text,

    /// <summary>The line-number gutter, which also carries the copy arrows.</summary>
    LineNumberMargin,

    /// <summary>The change-marker gutter, which carries the chips and the modified-since-load bar.</summary>
    ChangeMarkerMargin,

    /// <summary>
    /// The column between the panes, whose polygons are change blocks. Side-by-side only: the
    /// unified view has one pane and nothing to draw a connector between.
    /// </summary>
    ConnectorGutter,

    /// <summary>
    /// The overview map beside the panes, whose subject is a row. Side-by-side only, for the
    /// same reason.
    /// </summary>
    OverviewMap,
}
