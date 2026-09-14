using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Tests.Inline;

/// <summary>
/// Plan 00009 phase 2: the unified view holds the same <see cref="DiffKeyMap"/> the side-by-side
/// view does, defaulting to the smaller map — one pane to switch between and one composed
/// document to copy between, so the two-sided verbs have no meaning here and say so.
/// </summary>
public sealed class InlineKeyMapTests
{
    [AvaloniaFact]
    public async Task The_unified_default_is_the_side_by_side_one_less_its_two_sided_verbs()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        // F7, Shift+F7, Ctrl+F, F3, Shift+F3, Escape — the six that were hard-coded, now from
        // the map, and a pin so that changing one is a deliberate act with a failing test.
        Assert.Equal(new KeyGesture(Key.F7), host.View.GestureFor(DiffCommand.NextChange));
        Assert.Equal(new KeyGesture(Key.F7, KeyModifiers.Shift), host.View.GestureFor(DiffCommand.PreviousChange));
        Assert.Equal(new KeyGesture(Key.F, KeyModifiers.Control), host.View.GestureFor(DiffCommand.OpenFind));
        Assert.Equal(new KeyGesture(Key.F3), host.View.GestureFor(DiffCommand.FindNext));
        Assert.Equal(new KeyGesture(Key.F3, KeyModifiers.Shift), host.View.GestureFor(DiffCommand.FindPrevious));
        Assert.Equal(new KeyGesture(Key.Escape), host.View.GestureFor(DiffCommand.CloseFind));
        Assert.Equal(6, host.View.KeyBindings.Count);

        // The read side answers null for unbound, which is what plan 00010's menu will print.
        Assert.Null(host.View.GestureFor(DiffCommand.SwitchPane));
        Assert.Null(host.View.GestureFor(DiffCommand.CopyToLeft));
        Assert.Null(host.View.GestureFor(DiffCommand.CopyToRight));
        Assert.Null(host.View.GestureFor(DiffCommand.CopyBlockToLeft));
        Assert.Null(host.View.GestureFor(DiffCommand.CopyBlockToRight));
    }

    [AvaloniaFact]
    public async Task Rebinding_moves_the_behaviour_off_the_old_key()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.Pane.TextArea.Focus();
        InlineHost.Layout();
        Assert.Equal(-1, host.View.CurrentChangeIndex);

        host.View.KeyMap[DiffCommand.NextChange] = new KeyGesture(Key.F8);
        Press(host, Key.F8, PhysicalKey.F8);
        Assert.Equal(0, host.View.CurrentChangeIndex);

        // The half a rebind test usually forgets: the old key must stop working.
        Press(host, Key.F7, PhysicalKey.F7);
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Assert.Equal(new KeyGesture(Key.F8), host.View.GestureFor(DiffCommand.NextChange));
    }

    [AvaloniaFact]
    public async Task Unbinding_leaves_the_key_doing_nothing_and_the_command_still_callable()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.Pane.TextArea.Focus();
        InlineHost.Layout();

        host.View.KeyMap[DiffCommand.NextChange] = null;
        Press(host, Key.F7, PhysicalKey.F7);
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.Null(host.View.GestureFor(DiffCommand.NextChange));
        Assert.Equal(5, host.View.KeyBindings.Count);

        // Unbound is not removed: the verb is still there for a host to invoke.
        host.View.NextChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task A_binding_the_host_added_survives_a_rebuild()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        // The collection is public, so a host may already have put something in it. Rebuilding it
        // wholesale would delete that silently — the failure a consumer finds, not us.
        KeyBinding mine = new() { Gesture = new KeyGesture(Key.F12), Command = host.View.NextChangeCommand };
        host.View.KeyBindings.Add(mine);

        host.View.KeyMap[DiffCommand.NextChange] = new KeyGesture(Key.F8);
        InlineHost.Layout();

        Assert.Contains(mine, host.View.KeyBindings);
    }

    [AvaloniaFact]
    public async Task A_two_sided_verb_bound_here_is_skipped_and_logged_not_bound_to_nothing()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.Pane.TextArea.Focus();
        InlineHost.Layout();

        // The side-by-side default, assigned whole: F6 and the two copies have no meaning on one
        // pane over one composed document, and a key bound to nothing would just be dead.
        host.View.KeyMap = DiffKeyMap.Default();
        InlineHost.Layout();

        Assert.Equal(6, host.View.KeyBindings.Count);
        Assert.Equal(3, host.Logs.Records.Count(r => r.Message.Contains("has no meaning in the unified view", StringComparison.Ordinal)));
        Assert.All(
            host.Logs.Records.Where(r => r.Message.Contains("has no meaning in the unified view", StringComparison.Ordinal)),
            r => Assert.Equal(LogLevel.Warning, r.Level));

        // The map still says what it was given: the skip is the control's, not the map's.
        Assert.Equal(new KeyGesture(Key.F6), host.View.GestureFor(DiffCommand.SwitchPane));
        Assert.Throws<NotSupportedException>(() => host.View.CommandFor(DiffCommand.SwitchPane));
        Assert.Same(host.View.NextChangeCommand, host.View.CommandFor(DiffCommand.NextChange));

        // F6 does nothing here, which is the state before the map existed.
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Press(host, Key.F6, PhysicalKey.F6);
        Assert.Equal(-1, host.View.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task Two_commands_on_one_gesture_are_both_kept_and_the_clash_is_logged()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        host.View.KeyMap[DiffCommand.PreviousChange] = new KeyGesture(Key.F7);
        InlineHost.Layout();

        Assert.Contains(host.Logs.Records, r => r.Message.Contains("bound to both", StringComparison.Ordinal));
        Assert.Equal(2, host.View.KeyBindings.Count(b => Equals(b.Gesture, new KeyGesture(Key.F7))));
    }

    [AvaloniaFact]
    public async Task A_cleared_collection_stays_cleared()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        host.Pane.TextArea.Focus();
        InlineHost.Layout();

        host.View.KeyBindings.Clear();
        Press(host, Key.F7, PhysicalKey.F7);
        Assert.Equal(-1, host.View.CurrentChangeIndex);
    }

    private static void Press(InlineHost host, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        host.Window.KeyPress(key, modifiers, physical, null);
        host.Window.KeyRelease(key, modifiers, physical, null);
        InlineHost.Layout();
    }
}
