using Avalonia;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00003 §Phase 4, copy to side. A block's lines on one side replace the other side's lines
/// of the same block, over the ranges <see cref="ChangeBlock"/> already carries, so the re-diff
/// that follows collapses the block. Undo is the editor's own.
/// </summary>
public sealed class CopyToSideTests
{
    [AvaloniaFact]
    public async Task Copying_a_modified_block_collapses_it_and_leaves_the_sides_equal_there()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");

        Assert.Equal(1, host.View.ChangeCount);
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        Assert.True(host.View.CopyBlock(0, DiffSide.Right));
        await host.WaitForReDiffAsync();

        Assert.Equal("one\nTWO\nthree\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task Copying_an_insertion_puts_the_missing_lines_in_and_copying_back_takes_them_out()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("a\nb\nc\n", "a\nc\n");
        Assert.Equal(1, host.View.ChangeCount);

        // Right is missing "b": copying the block rightwards inserts it.
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        Assert.True(host.View.CopyBlock(0, DiffSide.Right));
        await host.WaitForReDiffAsync();
        Assert.Equal("a\nb\nc\n", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);

        // And the other direction removes it again — in a fresh host, because re-assigning an
        // equal `PaneSource` to this one would raise no property change and do nothing.
        using CompositeHost other = new();
        other.Show();
        await other.LoadAsync("a\nb\nc\n", "a\nc\n");
        other.View.LeftReadOnly = false;
        CompositeHost.Layout();
        Assert.True(other.View.CopyBlock(0, DiffSide.Left));
        await other.WaitForReDiffAsync();
        Assert.Equal("a\nc\n", other.Left.Document.Text);
        Assert.Equal(0, other.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task Copying_a_block_at_the_very_end_does_not_strand_a_terminator()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("head\ntail", "head");

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        Assert.True(host.View.CopyBlock(0, DiffSide.Right));
        await host.WaitForReDiffAsync();

        // The left side's last line carries no terminator, and neither should the copy.
        Assert.Equal("head\ntail", host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task Copying_onto_an_unterminated_last_line_gives_it_the_source_terminator()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("a\nb\n", "a\nX");

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        Assert.True(host.View.CopyBlock(0, DiffSide.Right));
        await host.WaitForReDiffAsync();

        // The target's last line had no terminator and the source's does; keeping the source's
        // is what makes the two sides identical, which is the whole point of the copy.
        Assert.Equal("a\nb\n", host.Right.Document.Text);
        Assert.Equal(host.Left.Document.Text, host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task Copying_every_block_makes_the_sides_identical()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        Assert.True(host.View.ChangeCount > 1);

        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        // Back to front, so an earlier copy cannot move a later block's lines out from under it.
        for (int i = host.View.Document!.Blocks.Count - 1; i >= 0; i--)
        {
            host.View.CopyBlock(i, DiffSide.Right);
        }

        await host.WaitForReDiffAsync();
        Assert.Equal(host.Left.Document.Text, host.Right.Document.Text);
        Assert.Equal(0, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task A_copy_is_one_undo_away_from_never_having_happened()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        Assert.True(host.View.CopyBlock(0, DiffSide.Right));
        await host.WaitForReDiffAsync();
        Assert.Equal(0, host.View.ChangeCount);

        host.Right.Undo();
        await host.WaitForReDiffAsync();

        Assert.Equal("one\ntwo\nthree\n", host.Right.Document.Text);
        Assert.Equal(1, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task A_read_only_target_refuses_the_copy()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        string before = host.Right.Document.Text;

        // Both sides start read-only.
        Assert.False(host.View.CanCopyBlock(0, DiffSide.Right));
        Assert.False(host.View.CopyBlock(0, DiffSide.Right));
        Assert.Equal(before, host.Right.Document.Text);

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        Assert.True(host.View.CanCopyBlock(0, DiffSide.Right));

        // An index the model does not have is refused whatever the flags say.
        Assert.False(host.View.CanCopyBlock(99, DiffSide.Right));
        Assert.False(host.View.CopyBlock(-1, DiffSide.Right));
    }

    [AvaloniaFact]
    public async Task The_commands_follow_the_current_block_and_the_read_only_flags()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);

        // The commands target the *current* block, and there is not one until navigation picks
        // one — so they are unavailable even on an editable side.
        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.False(host.View.CopyToRightCommand.CanExecute(null));

        host.View.FirstChange();
        CompositeHost.Layout();
        Assert.True(host.View.CopyToRightCommand.CanExecute(null));
        Assert.False(host.View.CopyToLeftCommand.CanExecute(null));

        int before = host.View.ChangeCount;
        host.View.CopyToRightCommand.Execute(null);
        await host.WaitForReDiffAsync();
        Assert.Equal(before - 1, host.View.ChangeCount);
    }

    [AvaloniaFact]
    public async Task The_gutter_draws_an_arrow_only_towards_an_editable_side()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        ChangeConnectorGutter gutter = host.View.Gutter!;

        // Both sides read-only: the column is drawn, but it offers nothing.
        host.Capture().Dispose();
        Assert.Empty(gutter.LastArrows);

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();
        Assert.NotEmpty(gutter.LastArrows);
        Assert.All(gutter.LastArrows, a => Assert.Equal(DiffSide.Right, a.ToSide));

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();
        Assert.Contains(gutter.LastArrows, a => a.ToSide == DiffSide.Left);
        Assert.Contains(gutter.LastArrows, a => a.ToSide == DiffSide.Right);
    }

    [AvaloniaFact]
    public async Task An_arrow_is_hit_before_the_polygon_it_sits_inside()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");

        host.View.RightReadOnly = false;
        CompositeHost.Layout();
        host.Capture().Dispose();

        (Rect bounds, int index, DiffSide side) = Assert.Single(host.View.Gutter!.LastArrows);
        Assert.Equal(0, index);
        Assert.Equal(DiffSide.Right, side);

        // The arrow's centre is inside its own block's polygon, so the order of the two
        // hit-tests is what decides whether a click copies or merely selects.
        Assert.NotNull(host.View.Gutter.PolygonAt(bounds.Center));
        Assert.Equal((0, DiffSide.Right), host.View.Gutter.ArrowAt(bounds.Center));
    }
}
