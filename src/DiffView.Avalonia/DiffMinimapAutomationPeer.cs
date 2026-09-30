using Avalonia.Automation.Peers;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a <see cref="DiffMinimap"/>: a <c>ScrollBar</c>, named by the
/// <c>AutomationProperties.Name</c> the view gives it — <c>MinimapName</c>.
/// </summary>
/// <remarks>
/// ⛔ <b>Custom-drawn is not decorative.</b> The overview map draws everything it shows and has no
/// template, so it is invisible to <c>BNXQ1006</c>, which checks themed controls — and it is one of the
/// surfaces a person acts on most. UIA's control-pattern mapping makes <c>RangeValue</c> conditional on
/// a <c>ScrollBar</c>, and it advertises none: a value in rows, the unit a drag moves in, waits for a
/// harness that drives through patterns.
/// </remarks>
internal sealed class DiffMinimapAutomationPeer(DiffMinimap owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ScrollBar;
}
