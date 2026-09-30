using Avalonia.Automation.Peers;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// What a pane's automation peer answers about the keyboard. AvaloniaEdit's <c>TextEditor</c> is not
/// focusable and its <c>TextArea</c> takes the keyboard, so every focus question a harness asks of a pane
/// belongs to the text area — and Avalonia's <c>ControlAutomationPeer</c>, which reads its owner, would
/// answer each of them wrongly.
/// </summary>
public sealed class PanePeerFocusTests
{
    [AvaloniaFact]
    public async Task SetFocus_on_a_panes_peer_puts_the_keyboard_in_its_text_area_and_the_peer_says_so()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);
        CompositeHost.Layout();

        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(host.Left);

        // The other pane holds the keyboard first, so neither answer below can be true by default.
        host.Right.TextArea.Focus();
        Assert.False(
            peer.HasKeyboardFocus(),
            "The left pane's peer claims the keyboard while the right pane's text area holds it.");
        Assert.True(
            peer.IsKeyboardFocusable(),
            "The left pane's peer says the pane cannot take the keyboard, and its text area can.");

        peer.SetFocus();
        Assert.True(
            host.Left.TextArea.IsFocused,
            "SetFocus on the pane's peer left the keyboard somewhere other than the pane's text area.");
        Assert.True(
            peer.HasKeyboardFocus(),
            "The pane's text area holds the keyboard and the pane's peer says the pane does not.");

        // And the keyboard is really there: a key moves the pane's own caret.
        int line = host.Left.TextArea.Caret.Line;
        host.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Assert.Equal(line + 1, host.Left.TextArea.Caret.Line);
    }
}
