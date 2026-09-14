namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Which edge of the panes the overview map is docked against. Both are real arrangements: which
/// one reads better depends on the window, the screen and the reader, not on anything the control
/// can know.
/// </summary>
/// <remarks>
/// The map's <em>lanes</em> do not follow this — the left lane is the left file wherever the map
/// sits, because a lane names a file and not an edge. What follows it is the pair of things that
/// hug an edge: the current-block marker, which points into the panes, and the find ticks, which
/// stay out of the lanes' way on the other side. See <see cref="DiffMinimap.MirrorEdges"/>.
/// </remarks>
public enum MinimapPlacement
{
    /// <summary>Outside the right pane, where the map has always been.</summary>
    Right,

    /// <summary>Outside the left pane.</summary>
    Left,
}
