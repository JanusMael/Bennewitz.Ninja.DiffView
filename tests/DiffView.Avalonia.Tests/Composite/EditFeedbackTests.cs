using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00003 §Phase 5, the feedback an edit leaves behind: which lines this session touched,
/// which sides hold unsaved changes, and what the margin says about both.
/// </summary>
public sealed class EditFeedbackTests
{
    [AvaloniaFact]
    public async Task An_edited_line_is_marked_and_the_mark_moves_with_the_line()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\ntwo\nthree\nfour\n", "one\ntwo\nthree\nfour\n");

        Assert.Empty(host.View.ModifiedLines(DiffSide.Left));

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        // Edit line 3.
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.GetLineByNumber(3).Offset;
        CompositeHost.Layout();
        host.Window.KeyTextInput("X");
        CompositeHost.Layout();
        Assert.Equal([3], host.View.ModifiedLines(DiffSide.Left).OrderBy(n => n));

        // Insert a whole line above it: the mark has to follow the text, not the number.
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("zero\n");
        CompositeHost.Layout();

        Assert.Contains(4, host.View.ModifiedLines(DiffSide.Left));
        Assert.DoesNotContain(3, host.View.ModifiedLines(DiffSide.Left).Where(n => n != 1));
        Assert.Equal("Xthree", host.Left.Document.GetText(host.Left.Document.GetLineByNumber(4)));
    }

    [AvaloniaFact]
    public async Task A_multi_line_insert_marks_every_line_it_added()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("head\ntail\n", "head\ntail\n");

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.GetLineByNumber(2).Offset;
        CompositeHost.Layout();

        // Three new lines pushed in above "tail": every one of them is this session's work,
        // not just the line the caret happened to be on.
        host.Window.KeyTextInput("a\nb\nc\n");
        CompositeHost.Layout();

        Assert.Equal("head\na\nb\nc\ntail\n", host.Left.Document.Text);
        Assert.Equal([2, 3, 4, 5], host.View.ModifiedLines(DiffSide.Left).OrderBy(n => n));
    }

    [AvaloniaFact]
    public async Task The_margin_draws_a_bar_on_the_lines_this_session_changed()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\ntwo\nthree\n", "one\ntwo\nthree\n");

        host.Capture().Dispose();
        Assert.Empty(host.Left.ChangeMarkerMargin.LastModified);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.GetLineByNumber(2).Offset;
        CompositeHost.Layout();
        host.Window.KeyTextInput("Y");
        CompositeHost.Layout();
        host.Capture().Dispose();

        Assert.Equal([2], host.Left.ChangeMarkerMargin.LastModified);

        // The other pane was not touched, and says so.
        Assert.Empty(host.Right.ChangeMarkerMargin.LastModified);
    }

    [AvaloniaFact]
    public async Task The_bar_covers_the_line_s_own_row_and_not_the_padding_above_it()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();

        // Two padding rows above the left's line 2, for the right's inserted X and Y.
        await host.LoadAsync("a\nb\n", "a\nX\nY\nb\n");
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.GetLineByNumber(2).Offset;
        CompositeHost.Layout();
        host.Window.KeyTextInput("Z");
        CompositeHost.Layout();
        Assert.Equal("a\nZb\n", host.Left.Document.Text);

        using WriteableBitmap frame = host.Capture();
        ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
        Assert.Equal([2], margin.LastModified);

        TextView view = host.Left.TextArea.TextView;
        VisualLine line = view.GetVisualLine(2)!;
        double rowHeight = view.DefaultLineHeight;
        double paddingTop = line.VisualTop - view.VerticalOffset;
        double rowTop = paddingTop + (host.Left.Metadata.PaddingBefore(2) * rowHeight);
        Assert.True(rowTop > paddingTop, "line 2 should carry padding above it, or this proves nothing");

        Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;
        Color bar = PresenterHost.Token("DiffView.ModifiedSinceLoadBrush");
        double x = margin.Bounds.Width - 3;

        // The bar marks the row holding the edited line. The padding above it belongs to lines of
        // the *other* side, which this session did not touch.
        Assert.Equal(0, Painted(frame, origin, new Rect(x, paddingTop, 3, rowTop - paddingTop), bar));
        Assert.True(Painted(frame, origin, new Rect(x, rowTop, 3, rowHeight), bar) > 0, "the edited line's own row should carry the bar");
    }

    private static int Painted(WriteableBitmap frame, Point origin, Rect area, Color colour)
    {
        PixelRect probe = PixelProbe.Inside(origin.X + area.Left, origin.Y + area.Top, origin.X + area.Right, origin.Y + area.Bottom, inset: 0);
        return PixelProbe.Count(frame, probe, c => PresenterHost.Near(c, colour));
    }

    [AvaloniaFact]
    public async Task The_margin_tooltip_says_a_line_was_edited_even_where_the_diff_is_silent()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\ntwo\nthree\n", "one\ntwo\nthree\n");

        // Identical sides: no block anywhere, so the tooltip is normally null.
        Assert.Null(host.Left.ChangeMarkerMargin.TooltipFor(2));

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.GetLineByNumber(2).Offset;
        CompositeHost.Layout();
        host.Window.KeyTextInput("Y");
        CompositeHost.Layout();

        string? tooltip = host.Left.ChangeMarkerMargin.TooltipFor(2);
        Assert.NotNull(tooltip);
        Assert.Contains("Edited", tooltip, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_strip_names_the_sides_holding_unsaved_edits()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        Assert.Null(host.View.StatusStrip!.DirtyText);

        host.View.LeftReadOnly = false;
        host.View.RightReadOnly = false;
        CompositeHost.Layout();

        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("L");
        CompositeHost.Layout();
        Assert.Contains("Left", host.View.StatusStrip!.DirtyText!, StringComparison.Ordinal);
        Assert.DoesNotContain("Right", host.View.StatusStrip!.DirtyText!, StringComparison.Ordinal);

        host.Right.TextArea.Focus();
        host.Right.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("R");
        CompositeHost.Layout();
        Assert.Contains("Left", host.View.StatusStrip!.DirtyText!, StringComparison.Ordinal);
        Assert.Contains("Right", host.View.StatusStrip!.DirtyText!, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Reverting_clears_the_marks_and_the_lane()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput("gone\n");
        CompositeHost.Layout();
        Assert.NotEmpty(host.View.ModifiedLines(DiffSide.Left));
        Assert.NotNull(host.View.StatusStrip!.DirtyText);

        host.View.Revert(DiffSide.Left);
        await host.WaitForBuildAsync();
        host.Capture().Dispose();

        Assert.Empty(host.View.ModifiedLines(DiffSide.Left));
        Assert.Empty(host.Left.ChangeMarkerMargin.LastModified);
        Assert.Null(host.View.StatusStrip!.DirtyText);
    }
}
