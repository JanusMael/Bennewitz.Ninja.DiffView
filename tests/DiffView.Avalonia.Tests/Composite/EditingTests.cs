using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00003 §Phase 1, typing. The panes' <c>IsReadOnly</c> is a property every layer honours
/// rather than an assumption any of them relies on, so clearing it is a flip: the pane accepts
/// text, its neighbour is unaffected, and the editor's own undo stack owns the result.
/// </summary>
/// <remarks>
/// No re-diff runs in this phase. That is the point of
/// <see cref="Typing_past_the_model_leaves_every_visible_row_rendered_and_raises_no_fault"/>:
/// between a keystroke and a build that has not been written yet, the metadata describes a
/// document that no longer exists, and the bounds-check in <see cref="PaneMetadata"/> is what
/// keeps the renderers drawing instead of throwing.
/// </remarks>
public sealed class EditingTests
{
    [AvaloniaFact]
    public async Task An_editable_pane_accepts_typing_while_its_neighbour_stays_read_only()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        string rightBefore = host.Right.Document.Text;

        // The default is read-only on both sides; only the left is opened.
        Assert.True(host.View.LeftReadOnly);
        Assert.True(host.View.RightReadOnly);
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        Assert.False(host.Left.IsReadOnly);
        Assert.True(host.Right.IsReadOnly);

        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("X");
        CompositeHost.Layout();

        Assert.StartsWith("X", host.Left.Document.Text, StringComparison.Ordinal);
        Assert.Equal(left.Length + 1, host.Left.Document.TextLength);

        // The neighbour did not move, and still refuses a keystroke of its own.
        host.Right.TextArea.Focus();
        host.Right.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("X");
        CompositeHost.Layout();
        Assert.Equal(rightBefore, host.Right.Document.Text);
    }

    [AvaloniaFact]
    public async Task Typing_past_the_model_leaves_every_visible_row_rendered_and_raises_no_fault()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);

        List<RenderFaultEventArgs> faults = [];
        host.View.RenderFault += (_, e) => faults.Add(e);

        int knownLines = host.Left.Metadata.LineCount;
        Assert.True(knownLines > 0);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.TextLength;
        CompositeHost.Layout();

        // Three lines the model has never heard of. Nothing rebuilds in this phase.
        host.Window.KeyTextInput("\nalpha\nbeta\ngamma");
        CompositeHost.Layout();

        Assert.True(host.Left.Document.LineCount > knownLines);
        Assert.Equal(knownLines, host.Left.Metadata.LineCount);

        // The metadata answers rather than throwing, and answers Unchanged for what it cannot know.
        int beyond = host.Left.Document.LineCount;
        Assert.False(host.Left.Metadata.Knows(beyond));
        Assert.Equal(DiffLineKind.Unchanged, host.Left.Metadata.KindOf(beyond));
        Assert.Null(host.Left.Metadata.BlockAt(beyond));
        Assert.Null(host.Left.Metadata.RowOf(beyond));

        // And the frame still paints, with no renderer or margin faulting on the stale model.
        using (WriteableBitmap frame = host.Capture())
        {
            Assert.NotNull(frame);
        }

        Assert.Empty(faults);
        Assert.NotEqual(DiffViewState.Failed, host.View.State);
    }

    [AvaloniaFact]
    public async Task Undo_restores_the_document_and_the_editor_owns_the_stack()
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
        host.Window.KeyTextInput("Z");
        CompositeHost.Layout();
        Assert.NotEqual(left, host.Left.Document.Text);

        Assert.True(host.Left.CanUndo);
        host.Left.Undo();
        CompositeHost.Layout();
        Assert.Equal(left, host.Left.Document.Text);

        Assert.True(host.Left.CanRedo);
        host.Left.Redo();
        CompositeHost.Layout();
        Assert.StartsWith("Z", host.Left.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Pasting_follows_the_property_in_both_directions()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        IClipboard clipboard = TopLevel.GetTopLevel(host.Window)?.Clipboard
                               ?? throw new InvalidOperationException("the headless top level has no clipboard");
        await clipboard.SetTextAsync("PASTED");

        Assert.False(host.Left.CanPaste);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        Assert.True(host.Left.CanPaste);
        host.Left.Paste();
        CompositeHost.Layout();
        Assert.Contains("PASTED", host.Left.Document.Text, StringComparison.Ordinal);

        // Closing the property again stops the next edit dead.
        string afterPaste = host.Left.Document.Text;
        host.View.LeftReadOnly = true;
        CompositeHost.Layout();
        Assert.True(host.Left.IsReadOnly);
        Assert.False(host.Left.CanPaste);
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("Q");
        host.Left.Paste();
        CompositeHost.Layout();
        Assert.Equal(afterPaste, host.Left.Document.Text);
    }

    [AvaloniaFact]
    public async Task An_edit_does_not_disturb_the_other_pane_scroll_coupling()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 240);
        host.Show();
        await host.LoadAsync(left, right);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.TextLength;
        CompositeHost.Layout();
        int linesBefore = host.Left.Document.LineCount;
        host.Window.KeyTextInput("\nappended");
        CompositeHost.Layout();

        // The edit has to have landed, or the coupling assertion below proves nothing.
        Assert.Equal(linesBefore + 1, host.Left.Document.LineCount);
        Assert.EndsWith("appended", host.Left.Document.Text, StringComparison.Ordinal);

        // The panes still scroll together: the sync is over offsets, not over the model.
        host.Left.PaneScrollViewer!.Offset = host.Left.PaneScrollViewer.Offset.WithY(40);
        CompositeHost.Layout();
        Assert.Equal(40, host.Right.PaneScrollViewer!.Offset.Y, precision: 0);
    }
}
