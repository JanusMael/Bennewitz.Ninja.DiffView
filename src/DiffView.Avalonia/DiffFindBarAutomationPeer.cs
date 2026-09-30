using Avalonia.Automation.Peers;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a <see cref="DiffFindBar"/>: a <c>ToolBar</c>, named by the name the bar gives
/// itself in its constructor.
/// </summary>
/// <remarks>
/// The query box, the toggles and the buttons inside it are framework controls with peers of their own,
/// reachable before this one existed; what was not reachable is the bar that contains them. UIA requires
/// no pattern of a <c>ToolBar</c>, and it advertises none.
/// </remarks>
internal sealed class DiffFindBarAutomationPeer(DiffFindBar owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;
}
