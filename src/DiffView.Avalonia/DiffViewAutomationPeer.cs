using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of the three views — <see cref="SideBySideDiffView"/>, <see cref="DiffViewer"/>
/// and <see cref="InlineDiffView"/>: a <c>Group</c>, named by the <c>AutomationProperties.Name</c> the
/// host gives the view.
/// </summary>
/// <remarks>
/// ⛔ <b>Without it a view is not a control element.</b> Avalonia gives a <c>TemplatedControl</c> the
/// <c>NoneAutomationPeer</c>, and a search of the control view never finds such a control, its name or
/// its id — which is how every control of this library stood until plan 00026. It advertises no
/// pattern: UIA requires none of a <c>Group</c>, and the patterns a harness could drive the view through
/// are deferred to the plan that has such a harness.
/// </remarks>
internal sealed class DiffViewAutomationPeer(Control owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
}
