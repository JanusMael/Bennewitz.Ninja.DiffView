using Avalonia.Automation.Peers;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a <see cref="DiffPaneHeader"/>: a <c>Header</c>, named by the
/// <c>AutomationProperties.Name</c> the view gives it — <c>LeftHeaderName</c> or <c>RightHeaderName</c>.
/// </summary>
/// <remarks>
/// A header without it is not a control element, so a harness cannot find the header it would
/// right-click for the file's menu. UIA requires no pattern of a <c>Header</c>, and it advertises none.
/// </remarks>
internal sealed class DiffPaneHeaderAutomationPeer(DiffPaneHeader owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Header;
}
