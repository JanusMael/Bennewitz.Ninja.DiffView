using Avalonia.Automation.Peers;
using AvaloniaEdit.Editing;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The automation peer of a <see cref="DiffPanePresenter"/>: an <c>Edit</c>, named by the pane's own
/// <c>AutomationProperties.Name</c>, that answers for the keyboard focus of the pane's text area.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The keyboard focus is the text area's, never the pane's.</b> AvaloniaEdit's <c>TextEditor</c> is
/// not focusable — its <c>TextArea</c> takes the keyboard — while Avalonia's
/// <c>ControlAutomationPeer</c> reads its owner's own <c>IsFocused</c> and <c>Focusable</c> and focuses the
/// owner itself. Left to those, a pane being typed into reports that it has no focus, cannot take one,
/// and ignores <c>SetFocus</c>. All three questions go to the text area instead.
/// </para>
/// <para>
/// <c>Edit</c>, not <c>Document</c>: UIA requires the <c>Text</c> pattern of a <c>Document</c>, which
/// Avalonia 12 has no provider for, and requires nothing of an <c>Edit</c>. It advertises no pattern;
/// the pane's <c>Value</c> and <c>Scroll</c> wait for a harness that drives through patterns.
/// </para>
/// </remarks>
internal sealed class DiffPanePresenterAutomationPeer(DiffPanePresenter owner) : ControlAutomationPeer(owner)
{
    private TextArea TextArea => ((DiffPanePresenter)Owner).TextArea;

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    /// <inheritdoc/>
    protected override bool HasKeyboardFocusCore() => TextArea.IsFocused;

    /// <inheritdoc/>
    protected override bool IsKeyboardFocusableCore() => TextArea.Focusable;

    /// <inheritdoc/>
    protected override void SetFocusCore() => TextArea.Focus();
}
