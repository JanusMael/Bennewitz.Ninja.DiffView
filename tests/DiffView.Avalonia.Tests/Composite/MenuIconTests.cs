using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Core;

// The assembly has an implicit `using System.IO`, whose Path is not this one.
using Path = Avalonia.Controls.Shapes.Path;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00012 phase 4: the column plan 00010 reserved, filled. No new visual language — the copy
/// entries carry the gutter's own arrow and the entries about a change carry the marker margin's
/// own operator — so most of what is worth asserting is that these really are those, and that the
/// entries with nothing to show still carry nothing.
/// </summary>
public sealed class MenuIconTests
{
    [AvaloniaFact]
    public async Task The_copy_entries_carry_the_gutters_own_arrow_pointing_the_way_the_text_travels()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        // The left pane copies rightwards, so both its copy entries point right; the right pane's
        // point left. A single arrow could be the wrong one and still be an arrow.
        AssertArrow(IconOf(ItemsAt(host, 2, host.Left), DiffViewStrings.MenuCopySelection(DiffSide.Right)), DiffSide.Right);
        AssertArrow(IconOf(ItemsAt(host, 2, host.Left), DiffViewStrings.MenuCopyChange(DiffSide.Right)), DiffSide.Right);
        AssertArrow(IconOf(ItemsAt(host, 2, host.Right), DiffViewStrings.MenuCopySelection(DiffSide.Left)), DiffSide.Left);
        AssertArrow(IconOf(ItemsAt(host, 2, host.Right), DiffViewStrings.MenuCopyChange(DiffSide.Left)), DiffSide.Left);
    }

    [AvaloniaFact]
    public async Task The_change_entries_carry_that_changes_own_kind()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();

        // One block of each kind, in order: a modification, then an insertion, then a deletion.
        await host.LoadAsync("a\nMOD\nb\nc\ngone\nd\n", "a\nmod\nb\nadded\nc\nd\n");
        CompositeHost.Layout();

        SideBySideDocument document = host.View.Document!;
        List<DiffLineKind> kinds = [.. document.Blocks.Select(b => b.Kind)];
        Assert.Contains(DiffLineKind.Modified, kinds);
        Assert.Contains(DiffLineKind.Inserted, kinds);
        Assert.Contains(DiffLineKind.Deleted, kinds);

        foreach (ChangeBlock block in document.Blocks)
        {
            // The marker margin already answers "what kind is this" with one of three operators;
            // the menu row about the same block answers with the same one, as strokes.
            List<DiffMenuItem> items = MarginItemsFor(host, block);
            Control? icon = IconOf(items, DiffViewStrings.Get(DiffViewStrings.MenuGoToChange));
            Assert.NotNull(icon);
            Assert.Equal(StrokeCountFor(block.Kind), Figures(icon));

            // Both entries about the change carry it, and they are two controls rather than one:
            // a visual has a single parent, so a shared instance would vanish from the first row.
            Control? second = IconOf(items, DiffViewStrings.Get(DiffViewStrings.MenuSelectChange));
            Assert.NotNull(second);
            Assert.NotSame(icon, second);
        }
    }

    [AvaloniaFact]
    public async Task Off_a_change_the_entries_are_there_and_their_column_is_empty()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\nfour\n", "one\ntwo\nthree\nfour\n");
        CompositeHost.Layout();

        // Line 1 is unchanged, so the change entries are present and disabled — and an unchanged
        // line has no kind, so there is nothing for the column to show. That the row still lines
        // up with the ones that do is the assertion the reserved column exists for.
        List<DiffMenuItem> items = MarginItems(host, host.Left, line: 1);
        DiffMenuItem goTo = items.Single(i => i.Header == DiffViewStrings.Get(DiffViewStrings.MenuGoToChange));
        Assert.False(goTo.IsEnabled);
        Assert.Null(goTo.Icon);
    }

    [AvaloniaFact]
    public async Task The_entries_with_nothing_to_show_carry_nothing()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        List<DiffMenuItem> items = ItemsAt(host, 2, host.Left);

        // Scoped to the vocabulary already on screen: navigate, find, save and revert have no
        // glyph in the gutter, so inventing one for them would be starting an icon set rather
        // than finishing this one.
        foreach (string key in (string[])
                 [
                     DiffViewStrings.MenuNextChange,
                     DiffViewStrings.MenuPreviousChange,
                     DiffViewStrings.MenuFind,
                     DiffViewStrings.MenuSaveLeft,
                     DiffViewStrings.MenuRevertLeft,
                 ])
        {
            string label = DiffViewStrings.Get(key);
            Assert.Null(items.Single(i => i.Header == label).Icon);
        }
    }

    [AvaloniaFact]
    public async Task An_icon_follows_the_foreground_rather_than_the_gutters_palette()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        CompositeHost.Layout();

        Path arrow = Assert.IsType<Path>(DiffMenuIcons.CopyArrow(DiffSide.Right));

        // A palette swap is a theme's business and this follows the theme, not DiffBrushes: the
        // gutter's yellows carry meaning against the gutter's background and would be a second,
        // unexplained palette in a host's menu.
        Border host2 = new() { Child = arrow };
        TextElement.SetForeground(host2, Brushes.Magenta);
        host.Window.Content = host2;
        CompositeHost.Layout();

        Assert.Equal(Brushes.Magenta, arrow.Fill);

        TextElement.SetForeground(host2, Brushes.SeaGreen);
        CompositeHost.Layout();
        Assert.Equal(Brushes.SeaGreen, arrow.Fill);
    }

    /// <summary>The icon of the entry headed <paramref name="label"/>, which must exist.</summary>
    private static Control? IconOf(List<DiffMenuItem> items, string label)
    {
        return items.Single(i => i.Header == label).Icon as Control;
    }

    /// <summary>How many separate strokes an operator is drawn from: − one, + two, ≠ three.</summary>
    private static int StrokeCountFor(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Deleted => 1,
            DiffLineKind.Inserted => 2,
            DiffLineKind.Modified => 3,
            _ => 0,
        };
    }

    private static int Figures(Control icon)
    {
        return Assert.IsType<PathGeometry>(Assert.IsType<Path>(icon).Data).Figures?.Count ?? 0;
    }

    /// <summary>
    /// Asserts <paramref name="icon"/> is the gutter's arrow pointing at <paramref name="toSide"/>.
    /// </summary>
    /// <remarks>
    /// By probing the shape rather than by comparing it: a <c>StreamGeometry</c> is opaque once
    /// closed — it enumerates nothing and its <c>ToString</c> is the type name, so two geometries
    /// of different shapes compare equal as strings, which is how the first draft of this
    /// assertion passed for an arrow pointing the wrong way. The probe is the pixel just behind a
    /// left-pointing arrow's tip, which is in front of a right-pointing one's shaft and so lies
    /// outside it: one point that only one of the two shapes contains.
    /// </remarks>
    private static void AssertArrow(Control? icon, DiffSide toSide)
    {
        Assert.NotNull(icon);
        Geometry data = Assert.IsType<Path>(icon).Data!;
        Point behindLeftTip = new(1.5, DiffMenuIcons.Size / 2);

        Assert.Equal(toSide == DiffSide.Left, data.FillContains(behindLeftTip));

        // The probe is only meaningful if the two directions really do disagree about it, which
        // is a fact about CopyArrowGlyph rather than about this icon.
        Rect zone = new(0, 0, DiffMenuIcons.Size, DiffMenuIcons.Size);
        Assert.True(CopyArrowGlyph.Geometry(zone, pointsLeft: true).FillContains(behindLeftTip));
        Assert.False(CopyArrowGlyph.Geometry(zone, pointsLeft: false).FillContains(behindLeftTip));
    }

    /// <summary>
    /// The change-marker margin's items for <paramref name="block"/>, asked on whichever side has
    /// lines in it — an insertion has none on the left, and a margin can only be clicked beside a
    /// line its own pane has.
    /// </summary>
    /// <remarks>
    /// The marker margin rather than the line-number one because its chip names a run, so it is
    /// the gutter carrying both change verbs; the line-number margin's glyph is a copy arrow and
    /// it carries only the go-to.
    /// </remarks>
    private static List<DiffMenuItem> MarginItemsFor(CompositeHost host, ChangeBlock block)
    {
        DiffSide side = block.LeftLines.IsEmpty ? DiffSide.Right : DiffSide.Left;
        DiffPanePresenter pane = side == DiffSide.Left ? host.Left : host.Right;
        return MarginItems(host, pane, block.LinesFor(side).Start + 1);
    }

    /// <summary>The change-marker margin's items for <paramref name="line"/> of <paramref name="pane"/>.</summary>
    private static List<DiffMenuItem> MarginItems(CompositeHost host, DiffPanePresenter pane, int line)
    {
        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffPaneContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        pane.TextArea.Caret.Line = line;
        CompositeHost.Layout();

        host.View.PaneContextMenuOpening += Capture;
        try
        {
            pane.ChangeMarkerMargin.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        }
        finally
        {
            host.View.PaneContextMenuOpening -= Capture;
        }

        Assert.NotEmpty(captured);
        return captured;
    }

    /// <summary>The text menu's items for <paramref name="line"/> of <paramref name="pane"/>.</summary>
    private static List<DiffMenuItem> ItemsAt(CompositeHost host, int line, DiffPanePresenter pane)
    {
        pane.TextArea.Caret.Line = line;
        CompositeHost.Layout();

        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffPaneContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        host.View.PaneContextMenuOpening += Capture;
        try
        {
            pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        }
        finally
        {
            host.View.PaneContextMenuOpening -= Capture;
        }

        Assert.NotEmpty(captured);
        return captured;
    }
}
