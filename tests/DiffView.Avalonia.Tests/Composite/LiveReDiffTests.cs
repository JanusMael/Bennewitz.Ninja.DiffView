using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00003 §Phase 2, live re-diff. An edit rests for <see cref="SideBySideDiffView.ReDiffDelay"/>
/// and then rebuilds through the same latest-wins worker a source assignment uses — but from the
/// pane's live text, and without replacing the document underneath the user.
/// </summary>
public sealed class LiveReDiffTests
{
    [AvaloniaFact]
    public async Task An_edit_rebuilds_the_model_from_the_live_text_after_the_debounce()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);

        int blocksBefore = host.View.Document!.Blocks.Count;
        int versionBefore = host.View.Document.Version;

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("// a line only the left side has\n");
        CompositeHost.Layout();

        // Nothing has rebuilt yet: the edit is resting.
        Assert.Equal(versionBefore, host.View.Document!.Version);
        Assert.True(host.View.IsEdited(DiffSide.Left));
        Assert.False(host.View.IsEdited(DiffSide.Right));

        await host.WaitForReDiffAsync();

        // A new model, built from the edited text rather than from the assigned source.
        Assert.NotEqual(versionBefore, host.View.Document!.Version);
        Assert.NotEqual(blocksBefore, host.View.Document.Blocks.Count);
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.Equal(host.Left.Document.LineCount, host.Left.Metadata.LineCount);
    }

    [AvaloniaFact]
    public async Task The_rebuild_keeps_the_document_the_caret_the_selection_and_the_undo_stack()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 240);
        host.Show();
        await host.LoadAsync(left, right);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("EDIT");
        CompositeHost.Layout();

        TextDocument document = host.Left.Document;
        host.Left.Select(0, 4);
        host.Left.PaneScrollViewer!.Offset = host.Left.PaneScrollViewer.Offset.WithY(24);
        CompositeHost.Layout();
        int caret = host.Left.TextArea.Caret.Offset;
        string selected = host.Left.SelectedText;
        double scroll = host.Left.PaneScrollViewer.Offset.Y;

        await host.WaitForReDiffAsync();

        // The one rule that separates a re-diff from a source assignment.
        Assert.Same(document, host.Left.Document);
        Assert.Equal(caret, host.Left.TextArea.Caret.Offset);
        Assert.Equal(selected, host.Left.SelectedText);
        Assert.Equal(scroll, host.Left.PaneScrollViewer!.Offset.Y, precision: 0);

        // And the undo stack came through, so the edit is still undoable after the rebuild.
        Assert.True(host.Left.CanUndo);
        host.Left.Undo();
        CompositeHost.Layout();
        Assert.Equal(left, host.Left.Document.Text);
    }

    [AvaloniaFact]
    public async Task Keystrokes_inside_the_window_coalesce_into_one_build()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        int versionBefore = host.View.Document!.Version;

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();

        // Four keystrokes, each restarting the wait by advancing less than the delay.
        for (int i = 0; i < 4; i++)
        {
            host.Window.KeyTextInput("x");
            CompositeHost.Layout();
            host.Time.Advance(host.View.ReDiffDelay - TimeSpan.FromMilliseconds(50));
            CompositeHost.Layout();
        }

        Assert.Equal(versionBefore, host.View.Document!.Version);

        await host.WaitForReDiffAsync();

        // One model later, not four: the version moved exactly once past the first.
        Assert.Equal(versionBefore + 1, host.View.Document!.Version);
        Assert.StartsWith("xxxx", host.Left.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_matches_an_edit_invalidated_are_dropped_rather_than_left_pointing_at_moved_text()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);

        host.View.OpenFind();
        await host.FindAsync("Greeter");
        Assert.NotNull(host.View.FindResult);
        Assert.True(host.View.FindResult!.Matches.Count > 0);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("\n\n\n");
        CompositeHost.Layout();

        // Every offset the search returned is now wrong, so the result goes rather than lingering.
        Assert.Null(host.View.FindResult);
    }

    [AvaloniaFact]
    public async Task With_live_re_diff_off_the_model_waits_for_an_explicit_rebuild()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        int versionBefore = host.View.Document!.Version;

        host.View.LiveReDiff = false;
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("// unbuilt\n");
        CompositeHost.Layout();

        // The debounce elapses and nothing happens — the escape hatch for a pair too large to
        // rebuild on every keystroke.
        host.Time.Advance(host.View.ReDiffDelay * 4);
        CompositeHost.Layout();
        Assert.Equal(versionBefore, host.View.Document!.Version);
        Assert.True(host.View.IsEdited(DiffSide.Left));

        host.View.ReDiffNow();
        await host.WaitForBuildAsync();
        Assert.NotEqual(versionBefore, host.View.Document!.Version);
    }

    [AvaloniaFact]
    public async Task Assigning_a_source_again_clears_the_edited_flag_and_stops_the_old_document_talking()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("stale");
        CompositeHost.Layout();
        TextDocument abandoned = host.Left.Document;
        Assert.True(host.View.IsEdited(DiffSide.Left));

        // A fresh source replaces the document, as it always has. It has to differ from the one
        // already assigned: `PaneSource` is a record, so an equal source raises no property
        // change and nothing happens — reverting an edited pane needs its own verb, which is
        // Phase 3's business rather than this one's.
        string reloaded = left + "\n// reloaded from disk\n";
        await host.LoadAsync(reloaded, right);
        Assert.False(host.View.IsEdited(DiffSide.Left));
        Assert.NotSame(abandoned, host.Left.Document);
        Assert.Equal(reloaded, host.Left.Document.Text);

        // The document that was thrown away must not still be arming re-diffs.
        int versionBefore = host.View.Document!.Version;
        abandoned.Insert(0, "ghost");
        CompositeHost.Layout();
        Assert.False(host.View.IsEdited(DiffSide.Left));
        host.Time.Advance(host.View.ReDiffDelay * 4);
        CompositeHost.Layout();
        Assert.Equal(versionBefore, host.View.Document!.Version);
    }
}
