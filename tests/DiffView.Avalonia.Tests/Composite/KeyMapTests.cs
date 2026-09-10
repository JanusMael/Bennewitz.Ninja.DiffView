using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00009, key bindings a host can change: the defaults pinned, rebinding and unbinding, the
/// host's own bindings surviving a rebuild, and the copy chords now following the selection the
/// way the gutter always has.
/// </summary>
public sealed class KeyMapTests
{
    [AvaloniaFact]
    public void The_default_map_is_the_bindings_that_were_hard_coded()
    {
        DiffKeyMap map = DiffKeyMap.Default();

        // A pin: changing a default should be a deliberate act with a failing test attached.
        Assert.Equal(new KeyGesture(Key.F7), map[DiffCommand.NextChange]);
        Assert.Equal(new KeyGesture(Key.F7, KeyModifiers.Shift), map[DiffCommand.PreviousChange]);
        Assert.Equal(new KeyGesture(Key.F6), map[DiffCommand.SwitchPane]);
        Assert.Equal(new KeyGesture(Key.F, KeyModifiers.Control), map[DiffCommand.OpenFind]);
        Assert.Equal(new KeyGesture(Key.F3), map[DiffCommand.FindNext]);
        Assert.Equal(new KeyGesture(Key.F3, KeyModifiers.Shift), map[DiffCommand.FindPrevious]);
        Assert.Equal(new KeyGesture(Key.Escape), map[DiffCommand.CloseFind]);
        Assert.Equal(new KeyGesture(Key.Left, KeyModifiers.Alt), map[DiffCommand.CopyToLeft]);
        Assert.Equal(new KeyGesture(Key.Right, KeyModifiers.Alt), map[DiffCommand.CopyToRight]);

        // The block-always pair exists so the old behaviour keeps a name, and is unbound.
        Assert.Null(map[DiffCommand.CopyBlockToLeft]);
        Assert.Null(map[DiffCommand.CopyBlockToRight]);

        // The unified view has one pane and no other side.
        DiffKeyMap unified = DiffKeyMap.UnifiedDefault();
        Assert.Null(unified[DiffCommand.SwitchPane]);
        Assert.Null(unified[DiffCommand.CopyToLeft]);
        Assert.Null(unified[DiffCommand.CopyToRight]);
        Assert.Equal(new KeyGesture(Key.F7), unified[DiffCommand.NextChange]);
    }

    [AvaloniaFact]
    public async Task Rebinding_moves_the_behaviour_off_the_old_key()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\nFOUR\nfive\n", "one\ntwo\nthree\nfour\nfive\n");
        host.Left.TextArea.Focus();
        CompositeHost.Layout();
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
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\nFOUR\nfive\n", "one\ntwo\nthree\nfour\nfive\n");
        host.Left.TextArea.Focus();
        CompositeHost.Layout();

        host.View.KeyMap[DiffCommand.NextChange] = null;
        Press(host, Key.F7, PhysicalKey.F7);
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.Null(host.View.GestureFor(DiffCommand.NextChange));

        // Unbound is not removed: the verb is still there for a host to invoke.
        host.View.NextChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task A_command_with_no_default_can_be_bound()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.View.RightReadOnly = false;
        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        host.View.NextChange();

        // Selected lines, and the block-always command bound to a key of its own: it copies the
        // block, which is exactly what the default chord no longer does.
        host.Left.Select(4, 3);
        host.View.KeyMap[DiffCommand.CopyBlockToRight] = new KeyGesture(Key.F9);
        Press(host, Key.F9, PhysicalKey.F9);
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nthree\n", host.Right.Document.Text);
    }

    [AvaloniaFact]
    public async Task A_binding_the_host_added_survives_a_rebuild()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");

        // The collection is public, so a host may already have put something in it. Rebuilding it
        // wholesale would delete that silently — the failure a consumer finds, not us.
        KeyBinding mine = new() { Gesture = new KeyGesture(Key.F12), Command = host.View.NextChangeCommand };
        host.View.KeyBindings.Add(mine);

        host.View.KeyMap[DiffCommand.NextChange] = new KeyGesture(Key.F8);
        CompositeHost.Layout();

        Assert.Contains(mine, host.View.KeyBindings);
    }

    [AvaloniaFact]
    public async Task A_cleared_collection_stays_cleared()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.Left.TextArea.Focus();
        CompositeHost.Layout();

        host.View.KeyBindings.Clear();
        Press(host, Key.F7, PhysicalKey.F7);
        Assert.Equal(-1, host.View.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task Two_commands_on_one_gesture_are_both_kept_and_the_clash_is_logged()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");

        host.View.KeyMap[DiffCommand.PreviousChange] = new KeyGesture(Key.F7);
        CompositeHost.Layout();

        Assert.Contains(host.Logs.Records, r => r.Message.Contains("bound to both", StringComparison.Ordinal));
        Assert.Equal(2, host.View.KeyBindings.Count(b => Equals(b.Gesture, new KeyGesture(Key.F7))));
    }

    [AvaloniaFact]
    public async Task The_copy_chord_takes_the_selection_when_there_is_one()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        host.View.NextChange();

        // Line 2 only, inside a block that covers lines 2 and 3: if the chord took the block it
        // would write both, and the difference is what this test is for.
        host.Left.Select(4, 3);
        Assert.True(host.View.CanCopyToward(DiffSide.Right));
        Press(host, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nthree\nfour\n", host.Right.Document.Text);
        Assert.Equal(1, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task With_no_selection_the_copy_chord_is_the_block_as_before()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        host.View.NextChange();

        Press(host, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nTHREE\nfour\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task The_gutter_and_the_chord_agree_on_the_contested_cell()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nTHREE\nfour\n", "one\ntwo\nthree\nfour\n");
        host.View.RightReadOnly = false;
        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        host.View.NextChange();

        // A selection beginning on the block's anchor row: plan 00006 gives that cell to the
        // selection's arrow. This is the assertion the change exists for — what the gutter draws
        // and what the chord fires are now the same operation.
        host.Left.Select(4, 3);
        host.Capture().Dispose();
        Assert.NotNull(host.Left.LineNumberMargin.LastSelectionArrow);
        Assert.Empty(host.Left.LineNumberMargin.LastCopyArrows);

        Press(host, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        await host.WaitForReDiffAsync();

        // The selection's lines, not the block's.
        Assert.Equal("one\nTWO\nthree\nfour\n", host.Right.Document.Text);
    }

    private static void Press(CompositeHost host, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        host.Window.KeyPress(key, modifiers, physical, null);
        host.Window.KeyRelease(key, modifiers, physical, null);
        CompositeHost.Layout();
    }
}
