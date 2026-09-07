using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 10, what a keyboard and a screen reader meet: copy works per pane while
/// read-only holds against typing and pasting, the focused pane is visible as such in its
/// header, and every decorator the composite builds announces itself.
/// </summary>
public sealed class InteractionTests
{
    [AvaloniaFact]
    public async Task Each_pane_copies_its_own_selection_and_read_only_holds_against_typing_and_pasting()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        IClipboard clipboard = TopLevel.GetTopLevel(host.Window)?.Clipboard
                               ?? throw new InvalidOperationException("the headless top level has no clipboard");
        string leftText = host.Left.Document.Text;
        string rightText = host.Right.Document.Text;

        host.Left.TextArea.Focus();
        host.Left.Select(0, 5);
        CompositeHost.Layout();
        Assert.Equal("using", host.Left.SelectedText);
        Assert.True(host.Left.CanCopy);
        host.Left.Copy();
        CompositeHost.Layout();
        Assert.Equal("using", await clipboard.TryGetTextAsync());

        // The other pane copies its own selection, not the first pane's.
        host.Right.TextArea.Focus();
        host.Right.Select(6, 6);
        CompositeHost.Layout();
        Assert.Equal("System", host.Right.SelectedText);
        host.Right.Copy();
        CompositeHost.Layout();
        Assert.Equal("System", await clipboard.TryGetTextAsync());

        // Read-only: no paste command, and neither a paste nor a keystroke moves a character.
        await clipboard.SetTextAsync("INJECTED");
        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            Assert.True(pane.IsReadOnly);
            Assert.False(pane.CanPaste);
            pane.TextArea.Focus();
            pane.TextArea.Caret.Offset = 0;
            CompositeHost.Layout();
            pane.Paste();
            CompositeHost.Layout();
            host.Window.KeyTextInput("x");
            CompositeHost.Layout();
        }

        Assert.Equal(leftText, host.Left.Document.Text);
        Assert.Equal(rightText, host.Right.Document.Text);
        Assert.DoesNotContain("INJECTED", host.Left.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_focused_pane_is_accented_in_its_header_and_F6_moves_the_accent()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        Color accent = PresenterHost.Token("DiffView.FocusAccentBrush");

        Assert.False(host.View.LeftHeader!.IsPaneFocused);
        Assert.False(host.View.RightHeader!.IsPaneFocused);
        using (WriteableBitmap frame = host.Capture())
        {
            Assert.Equal(0, AccentPixels(host, host.View.LeftHeader!, frame, accent));
            Assert.Equal(0, AccentPixels(host, host.View.RightHeader!, frame, accent));
        }

        host.Left.TextArea.Focus();
        CompositeHost.Layout();
        Assert.Equal(DiffSide.Left, host.View.FocusedSide);
        Assert.True(host.View.LeftHeader!.IsPaneFocused);
        Assert.False(host.View.RightHeader!.IsPaneFocused);
        using (WriteableBitmap frame = host.Capture())
        {
            Assert.True(AccentPixels(host, host.View.LeftHeader!, frame, accent) > 0, "the left header should carry the accent");
            Assert.Equal(0, AccentPixels(host, host.View.RightHeader!, frame, accent));
        }

        // F6 switches panes, and the accent follows the focus.
        host.Window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, null);
        host.Window.KeyRelease(Key.F6, RawInputModifiers.None, PhysicalKey.F6, null);
        CompositeHost.Layout();
        Assert.Equal(DiffSide.Right, host.View.FocusedSide);
        Assert.False(host.View.LeftHeader!.IsPaneFocused);
        Assert.True(host.View.RightHeader!.IsPaneFocused);
        using (WriteableBitmap frame = host.Capture())
        {
            Assert.Equal(0, AccentPixels(host, host.View.LeftHeader!, frame, accent));
            Assert.True(AccentPixels(host, host.View.RightHeader!, frame, accent) > 0, "the right header should carry the accent");
        }
    }

    [AvaloniaFact]
    public async Task Every_decorator_the_composite_builds_carries_an_automation_name()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        CompositeHost.Layout();

        List<Control> decorators =
        [
            host.Left,
            host.Right,
            host.Left.LineNumberMargin,
            host.Left.ChangeMarkerMargin,
            host.Right.LineNumberMargin,
            host.Right.ChangeMarkerMargin,
            host.View.Gutter ?? throw new InvalidOperationException("no gutter"),
            host.View.Minimap ?? throw new InvalidOperationException("no minimap"),
            host.View.StatusStrip ?? throw new InvalidOperationException("no status strip"),
            host.View.FindBar ?? throw new InvalidOperationException("no find bar"),
        ];

        foreach (Control decorator in decorators)
        {
            string? name = AutomationProperties.GetName(decorator);
            Assert.False(string.IsNullOrWhiteSpace(name), $"{decorator.GetType().Name} carries no automation name");
        }
    }

    /// <summary>Pixels of <paramref name="accent"/> along the bottom edge of <paramref name="header"/>.</summary>
    private static int AccentPixels(CompositeHost host, DiffPaneHeader header, WriteableBitmap frame, Color accent)
    {
        Point origin = header.TranslatePoint(new Point(0, header.Bounds.Height - 2), host.Window)
                       ?? throw new InvalidOperationException("the header is not in the window");
        PixelRect band = PixelProbe.Inside(origin.X, origin.Y, origin.X + header.Bounds.Width, origin.Y + 2, inset: 0);
        return PixelProbe.Count(frame, band, c => PresenterHost.Near(c, accent));
    }
}
