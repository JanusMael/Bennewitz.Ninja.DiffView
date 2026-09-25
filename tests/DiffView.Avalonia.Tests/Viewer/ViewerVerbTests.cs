using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// Plan 00021: a control you scroll and look at. No surface opens a menu, no key does anything a
/// text pane would not, and the panes still select and copy — which is what they stay focusable for.
/// </summary>
/// <remarks>
/// Every "nothing happens" here is paired with the editor doing something for the very same input,
/// so that an input which never arrives cannot pass for a viewer that ignores it.
/// </remarks>
public sealed class ViewerVerbTests
{
    [AvaloniaFact]
    public async Task No_surface_opens_a_menu_where_the_editor_opens_one()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost editor = new(width: 900, height: 640);
        using ViewerHost viewer = new(width: 900, height: 640);
        editor.Show();
        viewer.Show();
        await editor.LoadAsync(left, right);
        await viewer.LoadAsync(left, right);

        // The connector's polygons are a frame's product, and a right-click is answered from them.
        editor.Capture().Dispose();
        viewer.Capture().Dispose();

        // A plain Avalonia menu on each view itself. A request no surface claims bubbles up to it, so
        // its opening is the positive sign that nothing under the pointer answered.
        ContextMenu editorAncestor = AncestorMenu(editor.View);
        ContextMenu viewerAncestor = AncestorMenu(viewer.View);

        List<(string Surface, Control Target, Point At)> editorSurfaces = Surfaces(
            editor.Left, editor.View.Gutter!, editor.View.Minimap!, editor.View.LeftHeader!, editor.View.RightHeader!, editor.View.Document!);
        List<(string Surface, Control Target, Point At)> viewerSurfaces = Surfaces(
            viewer.Left, viewer.View.Gutter!, viewer.View.Minimap!, viewer.View.LeftHeader!, viewer.View.RightHeader!, viewer.View.Document!);

        for (int i = 0; i < editorSurfaces.Count; i++)
        {
            (string surface, Control target, Point at) = editorSurfaces[i];
            RightClick(editor.Window, target, at);
            Assert.True(editor.View.LastMenu is { IsOpen: true }, $"the editor should answer a right-click on its {surface}; without that, the viewer's silence below proves nothing");
            Assert.False(editorAncestor.IsOpen);
            editor.View.LastMenu!.Close();
            CompositeHost.Layout();

            (_, target, at) = viewerSurfaces[i];
            RightClick(viewer.Window, target, at);
            Assert.True(viewerAncestor.IsOpen, $"something on the viewer's {surface} claimed the right-click instead of letting it reach the view's own menu");
            viewerAncestor.Close();
            ViewerHost.Layout();
        }
    }

    [AvaloniaFact]
    public async Task No_key_the_editor_binds_does_anything_and_the_panes_keys_still_scroll()
    {
        string left = Lines("same", 60) + "changed\n" + Lines("more", 60) + "gone\n" + Lines("tail", 20);
        string right = Lines("same", 60) + "CHANGED\n" + Lines("more", 60) + Lines("tail", 20);
        using CompositeHost editor = new(width: 900, height: 400);
        using ViewerHost viewer = new(width: 900, height: 400);
        editor.Show();
        viewer.Show();
        await editor.LoadAsync(left, right);
        await viewer.LoadAsync(left, right);

        // The presses arrive: on the editor, the same keys walk the changes and switch panes. Each
        // pane is focused just before its window is pressed, because focus is one device's and
        // focusing the second window's pane takes it from the first.
        editor.Left.TextArea.Focus();
        CompositeHost.Layout();
        Press(editor.Window, new KeyGesture(Key.F7));
        Assert.Equal(0, editor.View.CurrentChangeIndex);
        Press(editor.Window, new KeyGesture(Key.F6));
        Assert.Equal(DiffSide.Right, editor.View.FocusedSide);

        // Every gesture the editor binds by default, derived from its map rather than listed here.
        KeyGesture[] chords = [.. DiffKeyMap.Default().Select(binding => binding.Value).OfType<KeyGesture>()];
        Assert.Contains(new KeyGesture(Key.F, KeyModifiers.Control), chords);
        Assert.Empty(viewer.View.KeyBindings);

        viewer.Left.TextArea.Focus();
        ViewerHost.Layout();
        Assert.Equal(DiffSide.Left, viewer.View.FocusedSide);
        string leftText = viewer.Left.Document.Text;
        string rightText = viewer.Right.Document.Text;
        foreach (KeyGesture chord in chords)
        {
            Press(viewer.Window, chord);
            Assert.True(viewer.View.CurrentChangeIndex == -1, $"{chord} moved the current change");
            Assert.True(viewer.View.FocusedSide == DiffSide.Left, $"{chord} moved the focus to {viewer.View.FocusedSide}");
        }

        Assert.Equal(leftText, viewer.Left.Document.Text);
        Assert.Equal(rightText, viewer.Right.Document.Text);
        Assert.Empty(viewer.View.GetVisualDescendants().OfType<DiffFindBar>());
        Assert.DoesNotContain(viewer.Left.TextArea.DefaultInputHandler.NestedInputHandlers, h => h.GetType().Name == "SearchInputHandler");

        // What a text pane does with a key is still the pane's: an arrow moves the caret, and a page
        // scrolls — the second pane with it, since the vertical offsets are always coupled.
        int line = viewer.View.CaretLine;
        Press(viewer.Window, new KeyGesture(Key.Down));
        Assert.Equal(line + 1, viewer.View.CaretLine);

        double before = viewer.Left.VerticalOffset;
        Press(viewer.Window, new KeyGesture(Key.PageDown));
        Assert.True(viewer.Left.VerticalOffset > before, $"Page Down should scroll the pane; {before} → {viewer.Left.VerticalOffset}");
        Assert.Equal(viewer.Left.VerticalOffset, viewer.Right.VerticalOffset, 1e-6);
    }

    [AvaloniaFact]
    public async Task Each_pane_selects_and_copies_and_neither_takes_a_keystroke_or_a_paste()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using ViewerHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        IClipboard clipboard = TopLevel.GetTopLevel(host.Window)?.Clipboard
                               ?? throw new InvalidOperationException("the headless top level has no clipboard");
        string leftText = host.Left.Document.Text;
        string rightText = host.Right.Document.Text;

        host.Left.TextArea.Focus();
        host.Left.Select(0, 5);
        ViewerHost.Layout();
        Assert.Equal("using", host.Left.SelectedText);
        host.Left.Copy();
        ViewerHost.Layout();
        Assert.Equal("using", await clipboard.TryGetTextAsync());

        await clipboard.SetTextAsync("INJECTED");
        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            Assert.True(pane.IsReadOnly);
            Assert.False(pane.CanPaste);
            pane.TextArea.Focus();
            pane.TextArea.Caret.Offset = 0;
            ViewerHost.Layout();
            pane.Paste();
            ViewerHost.Layout();
            host.Window.KeyTextInput("x");
            ViewerHost.Layout();
        }

        Assert.Equal(leftText, host.Left.Document.Text);
        Assert.Equal(rightText, host.Right.Document.Text);
    }

    /// <summary>
    /// A point on each of the six surfaces the editor answers — the text, its two margins, the
    /// connector, the map and the headers — with the connector's inside its tallest polygon, since
    /// off every polygon the editor opens nothing either.
    /// </summary>
    private static List<(string Surface, Control Target, Point At)> Surfaces(
        DiffPanePresenter pane, ChangeConnectorGutter gutter, DiffMinimap map, DiffPaneHeader leftHeader, DiffPaneHeader rightHeader, SideBySideDocument document)
    {
        double y = pane.TextArea.TextView.DefaultLineHeight * 2.5;
        ConnectorPolygon polygon = gutter.LastPolygons[document.Blocks.MaxBy(b => b.RowCount)!.Index];
        double polygonMiddle = (polygon.LeftTop.Y + Math.Max(polygon.LeftBottom.Y, polygon.RightBottom.Y)) / 2;
        double laneX = Enumerable.Range(0, (int)map.Bounds.Width).Select(i => i + 0.5).First(x => map.LaneAt(x) == DiffSide.Left);
        return
        [
            ("text", pane.TextArea.TextView, new Point(40, y)),
            ("line-number margin", pane.LineNumberMargin, new Point(pane.LineNumberMargin.Bounds.Width / 2, y)),
            ("change-marker margin", pane.ChangeMarkerMargin, new Point(pane.ChangeMarkerMargin.Bounds.Width / 2, y)),
            ("connector", gutter, new Point(gutter.Bounds.Width / 2, polygonMiddle)),
            ("map", map, new Point(laneX, map.Bounds.Height / 2)),
            ("left header", leftHeader, new Point(leftHeader.Bounds.Width / 2, leftHeader.Bounds.Height / 2)),
            ("right header", rightHeader, new Point(rightHeader.Bounds.Width / 2, rightHeader.Bounds.Height / 2)),
        ];
    }

    private static ContextMenu AncestorMenu(Control view)
    {
        ContextMenu menu = new();
        menu.Items.Add(new MenuItem { Header = "Host" });
        view.ContextMenu = menu;
        return menu;
    }

    private static void RightClick(Window window, Control control, Point inControl)
    {
        Point inWindow = control.TranslatePoint(inControl, window)
                         ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
        window.MouseDown(inWindow, MouseButton.Right);
        window.MouseUp(inWindow, MouseButton.Right);
        CompositeHost.Layout();
    }

    private static void Press(Window window, KeyGesture chord)
    {
        RawInputModifiers modifiers = RawInputModifiers.None;
        if (chord.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= RawInputModifiers.Control;
        }

        if (chord.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= RawInputModifiers.Shift;
        }

        if (chord.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= RawInputModifiers.Alt;
        }

        if (chord.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= RawInputModifiers.Meta;
        }

        PhysicalKey physical = chord.Key switch
        {
            Key.F => PhysicalKey.F,
            Key.F3 => PhysicalKey.F3,
            Key.F6 => PhysicalKey.F6,
            Key.F7 => PhysicalKey.F7,
            Key.Escape => PhysicalKey.Escape,
            Key.Left => PhysicalKey.ArrowLeft,
            Key.Right => PhysicalKey.ArrowRight,
            Key.Down => PhysicalKey.ArrowDown,
            Key.PageDown => PhysicalKey.PageDown,
            _ => PhysicalKey.None,
        };
        window.KeyPress(chord.Key, modifiers, physical, null);
        window.KeyRelease(chord.Key, modifiers, physical, null);
        CompositeHost.Layout();
    }

    private static string Lines(string text, int count)
    {
        return string.Concat(Enumerable.Range(0, count).Select(i => $"{text}{i}\n"));
    }
}
