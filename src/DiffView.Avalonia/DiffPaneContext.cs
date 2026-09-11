using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// What was under the pointer — or the caret — when a pane's context menu was asked for. This is
/// the type that makes a host-written menu possible at all: everything it answers otherwise lives
/// in <c>PaneMetadata</c>, which is internal.
/// </summary>
/// <remarks>
/// A snapshot, taken as the menu opens, and deliberately not a live view: a model rebuilt while
/// the menu is open would renumber the line the reader is pointing at. A consumer that acts on a
/// stale context gets what the stale context describes — which is why the control's own copy
/// commands re-read the selection when they run rather than taking it from here, the rule plan
/// 00006 set for <c>CopySelectionRequested</c>.
/// </remarks>
/// <param name="Region">Which surface this describes.</param>
/// <param name="Side">
/// The side this surface belongs to, or <c>null</c> where it belongs to neither: the unified
/// view, whose pane shows both files, and the connector gutter, which sits between them. On the
/// overview map it is the lane under the pointer, and <c>null</c> over the marker column the two
/// lanes share. Null rather than a stand-in: a consumer that read <see cref="DiffSide.Left"/>
/// there and acted on it would be wrong for half the lines.
/// </param>
/// <param name="LineNumber">
/// The 1-based line in the pane's own document. The connector and the map are not panes and a
/// row can carry a line on each side, so there they name the row's line on
/// <paramref name="SourceSide"/> — the lane's own side on the map, and the left's then the
/// right's where the surface names no side. It is never a stand-in for "no line": every surface
/// that raises a <see cref="DiffPaneContext"/> has at least one.
/// </param>
/// <param name="SourceSide">
/// The side the line belongs to. The same as <paramref name="Side"/> in the side-by-side view's
/// panes; in the unified view it is the half of the composed document this line came from, and on
/// the connector and the map it is the side <paramref name="LineNumber"/> was read from.
/// </param>
/// <param name="SourceLine">
/// The 1-based line on <paramref name="SourceSide"/>'s own file — the only numbering the unified
/// view's gutter shows. <c>null</c> where the model does not know the line.
/// </param>
/// <param name="Row">The model's row, or <c>null</c> for a line the model does not know.</param>
/// <param name="Block">The change block the line is in, or <c>null</c> outside one.</param>
/// <param name="Kind">The line's kind; <see cref="DiffLineKind.Unchanged"/> where unknown.</param>
/// <param name="SelectedLines">
/// The whole lines the pane's selection covers, 0-based as the model counts them, or <c>null</c>
/// with no selection. The same reading the selection arrow uses, so the menu and the gutter cannot
/// offer two different operations.
/// </param>
/// <param name="IsUnified">Whether this pane shows the composed unified document.</param>
/// <param name="IsReadOnly">Whether this pane refuses typing.</param>
public sealed record DiffPaneContext(
    DiffPaneRegion Region,
    DiffSide? Side,
    int LineNumber,
    DiffSide? SourceSide,
    int? SourceLine,
    int? Row,
    ChangeBlock? Block,
    DiffLineKind Kind,
    LineRange? SelectedLines,
    bool IsUnified,
    bool IsReadOnly)
{
    /// <summary>Whether the pane holds a selection; <see cref="SelectedLines"/> says which lines.</summary>
    public bool HasSelection => SelectedLines is not null;

    /// <summary>Whether the line sits inside a change block.</summary>
    public bool IsInChange => Block is not null;
}
