using Avalonia.Automation.Peers;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a <see cref="DiffStatusStrip"/>: a <c>StatusBar</c>, named by the
/// <c>AutomationProperties.Name</c> the view gives it — <c>StatusStripName</c>.
/// </summary>
/// <remarks>
/// UIA requires no pattern of a <c>StatusBar</c>, and it advertises none. It carries no live setting
/// either: Avalonia's Windows layer announces a live region only when an element's name changes, and the
/// strip's name is its own, not its message's.
/// </remarks>
internal sealed class DiffStatusStripAutomationPeer(DiffStatusStrip owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.StatusBar;
}
