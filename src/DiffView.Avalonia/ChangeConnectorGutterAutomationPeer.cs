using Avalonia.Automation.Peers;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a <see cref="ChangeConnectorGutter"/>: a <c>Custom</c> element, named by the
/// <c>AutomationProperties.Name</c> the view gives it — <c>GutterName</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Custom-drawn is not decorative.</b> The gutter is the splitter a person drags and the connectors
/// a person clicks to select a block, all drawn, with no template for <c>BNXQ1006</c> to see.
/// </para>
/// <para>
/// <c>Custom</c>, not <c>Thumb</c>: UIA's own guidance calls a separator that can be moved a
/// <c>Thumb</c>, and requires the <c>Transform</c> pattern of one, which Avalonia 12 has no provider
/// for. UIA requires nothing of a <c>Custom</c> element, and it advertises no pattern.
/// </para>
/// </remarks>
internal sealed class ChangeConnectorGutterAutomationPeer(ChangeConnectorGutter owner) : ControlAutomationPeer(owner)
{
    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
}
