using Avalonia.Automation.Peers;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a pane's two margins, <see cref="DiffLineNumberMargin"/> and
/// <see cref="ChangeMarkerMargin"/>: a <c>Custom</c> element, named by the name the margin gives itself —
/// <i>Line numbers</i> or <i>Change markers</i>.
/// </summary>
/// <remarks>
/// ⛔ <b>Custom-drawn is not decorative.</b> The number margin carries the copy arrows and the marker
/// margin the change markers, both drawn; a harness acts on them by position within the margin's bounds,
/// which it can find only if the margin is a control element. The margins' host in the text area's
/// template is marked raw, so in the control view a margin's parent is its pane. UIA requires nothing of
/// a <c>Custom</c> element, and it advertises no pattern.
/// </remarks>
internal sealed class DiffMarginAutomationPeer(DiffMargin owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
}
