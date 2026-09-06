using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 7, navigation: next, previous, first and last change with the current-block
/// border, "change i of n" in the strip, the stop at either end, and the F7 / Shift+F7 / F6 keys.
/// </summary>
public sealed class NavigationTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaFact]
    public async Task NextChange_from_the_top_lands_on_the_first_block_and_stops_at_the_last_with_the_strip_saying_so()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        SideBySideDocument document = host.View.Document!;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.Equal(5, host.View.ChangeCount);
        Assert.Equal("5 changes", host.View.StatusStrip!.ChangesText);
        Assert.True(host.View.NextChangeCommand.CanExecute(null));

        host.View.NextChange();
        CompositeHost.Layout();
        ChangeBlock first = document.Blocks[0];
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Assert.Same(first, host.Left.CurrentBlock);
        Assert.Same(first, host.Right.CurrentBlock);
        Assert.Equal("change 1 of 5", host.View.StatusStrip.ChangesText);
        AssertBlockInView(host, first, lineHeight);

        // The border is drawn around the block's rows in both panes, in the border token's colour.
        using WriteableBitmap frame = host.Capture();
        Color border = PresenterHost.Token("DiffView.CurrentBlockBorderBrush");
        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            Rect drawn = pane.BackgroundRenderer.LastCurrentBlockBorder ?? throw new InvalidOperationException($"{pane.Side}: no border drawn");
            Assert.Equal(first.RowCount * lineHeight, drawn.Height, Tolerance);
            Assert.Equal(first.FirstRow * lineHeight - pane.VerticalOffset, drawn.Top, Tolerance);
            Point origin = pane.TextArea.TextView.TranslatePoint(new Point(0, 0), host.Window) ?? throw new InvalidOperationException("text view not in tree");
            PixelRect edge = PixelProbe.Inside(origin.X + drawn.Width - 30, origin.Y + drawn.Top, origin.X + drawn.Width - 6, origin.Y + drawn.Top + 2, inset: 0);
            Assert.True(PixelProbe.Count(frame, edge, c => PresenterHost.Near(c, border)) > 0, $"{pane.Side}: the border's top edge should carry {border}");
        }

        for (int index = 1; index < 5; index++)
        {
            host.View.NextChange();
            CompositeHost.Layout();
            Assert.Equal(index, host.View.CurrentChangeIndex);
            AssertBlockInView(host, document.Blocks[index], lineHeight);
            Assert.Equal(host.Left.VerticalOffset, host.Right.VerticalOffset, Tolerance);
        }

        // At the last block it stays, and the strip's transient lane says so.
        host.View.NextChange();
        CompositeHost.Layout();
        Assert.Equal(4, host.View.CurrentChangeIndex);
        Assert.Equal(StatusKind.Warning, host.View.Status.Kind);
        Assert.Equal("No next change", host.View.Status.Text);
        Assert.Equal("change 5 of 5", host.View.StatusStrip.ChangesText);

        host.View.FirstChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);
        host.View.PreviousChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Assert.Equal("No previous change", host.View.Status.Text);
        host.View.LastChange();
        Assert.Equal(4, host.View.CurrentChangeIndex);

        // The property setter clamps and centres.
        host.View.CurrentChangeIndex = 2;
        CompositeHost.Layout();
        Assert.Equal(2, host.View.CurrentChangeIndex);
        AssertBlockInView(host, document.Blocks[2], lineHeight);
        host.View.CurrentChangeIndex = 99;
        Assert.Equal(4, host.View.CurrentChangeIndex);
        host.View.CurrentChangeIndex = -5;
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.Null(host.Left.CurrentBlock);
        Assert.Equal("5 changes", host.View.StatusStrip.ChangesText);
    }

    [AvaloniaFact]
    public async Task F7_and_Shift_F7_navigate_and_F6_switches_panes()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        // F7, Shift+F7, F6, Ctrl+F, F3, Shift+F3, Escape.
        Assert.Equal(7, host.View.KeyBindings.Count);

        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        Assert.Equal(DiffSide.Left, host.View.FocusedSide);

        Press(host, Key.F7, RawInputModifiers.None);
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Press(host, Key.F7, RawInputModifiers.None);
        Assert.Equal(1, host.View.CurrentChangeIndex);
        Press(host, Key.F7, RawInputModifiers.Shift);
        Assert.Equal(0, host.View.CurrentChangeIndex);

        Press(host, Key.F6, RawInputModifiers.None);
        Assert.True(host.Right.TextArea.IsFocused);
        Assert.Equal(DiffSide.Right, host.View.FocusedSide);
        Press(host, Key.F6, RawInputModifiers.None);
        Assert.True(host.Left.TextArea.IsFocused);
        Assert.Equal(DiffSide.Left, host.View.FocusedSide);

        // A host can take the bindings away.
        host.View.KeyBindings.Clear();
        Press(host, Key.F7, RawInputModifiers.None);
        Assert.Equal(0, host.View.CurrentChangeIndex);
    }

    [AvaloniaFact]
    public async Task Navigation_without_changes_says_so_and_a_new_model_clears_the_current_change()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, left);
        Assert.Equal(0, host.View.ChangeCount);
        Assert.False(host.View.NextChangeCommand.CanExecute(null));
        host.View.NextChange();
        Assert.Equal("No changes to navigate", host.View.Status.Text);
        Assert.Equal(-1, host.View.CurrentChangeIndex);

        host.View.RightSource = right;
        await host.WaitForBuildAsync();
        host.View.NextChange();
        Assert.Equal(0, host.View.CurrentChangeIndex);

        host.View.IgnoreCase = true;
        await host.WaitForBuildAsync();
        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.Null(host.Left.CurrentBlock);
        Assert.Equal(-1, host.View.Gutter!.CurrentChangeIndex);
    }

    private static void AssertBlockInView(CompositeHost host, ChangeBlock block, double lineHeight)
    {
        double top = block.FirstRow * lineHeight;
        double bottom = (block.LastRow + 1) * lineHeight;
        double offset = host.Left.VerticalOffset;
        double viewport = host.Left.ViewportHeight;
        Assert.True(top >= offset - Tolerance && bottom <= offset + viewport + Tolerance, $"block {block.Index} rows {block.FirstRow}-{block.LastRow} should be within the viewport at offset {offset}");
        Assert.Equal(host.Left.VerticalOffset, host.Right.VerticalOffset, Tolerance);
    }

    private static void Press(CompositeHost host, Key key, RawInputModifiers modifiers)
    {
        PhysicalKey physical = key switch
        {
            Key.F6 => PhysicalKey.F6,
            Key.F7 => PhysicalKey.F7,
            _ => PhysicalKey.None,
        };
        host.Window.KeyPress(key, modifiers, physical, null);
        host.Window.KeyRelease(key, modifiers, physical, null);
        CompositeHost.Layout();
    }
}
