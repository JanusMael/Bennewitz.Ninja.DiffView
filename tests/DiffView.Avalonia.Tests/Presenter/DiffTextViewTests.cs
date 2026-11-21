using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Viewer;

namespace Bennewitz.Ninja.DiffView.Tests.Presenter;

/// <summary>
/// A pane's text view and text area, <see cref="DiffTextView"/> and <see cref="DiffTextArea"/>: what they
/// bring into view, and that in all else they are AvaloniaEdit's.
/// </summary>
/// <remarks>
/// AvaloniaEdit's caret is as tall as the visual line it sits on, padding and all, and
/// <c>TextView.MakeVisible</c> never settles on a rectangle taller than the viewport: shown from its top, its
/// bottom overflows, and shown from its bottom, its top does. While the left button is held, AvaloniaEdit
/// re-extends the selection on every scroll and brings the caret into view again, so the two offsets
/// alternated until the stack overflowed — AgentBaseline died that way on 2026-11-19, under a drag over a
/// line that carried 63 rows of padding. The caret a pane draws is its line's own row, and that row is what
/// is brought into view.
/// </remarks>
public sealed class DiffTextViewTests
{
    /// <summary>The right side's line that carries <see cref="PaddingRows"/> rows of padding above its own.</summary>
    private const int TallLine = 2;

    /// <summary>
    /// Rows of padding on <see cref="TallLine"/>: enough to make it taller than every pane here, and few
    /// enough to stay under twice the pane, where AvaloniaEdit's rule alternates whatever the rounding.
    /// </summary>
    private const int PaddingRows = 25;

    /// <summary>The border AvaloniaEdit's selection handler brings the caret into view with.</summary>
    private const double SelectionBorder = 5;

    private const double Tolerance = PresenterHost.Tolerance;

    private const string RightText = "a\nz\n";

    private static readonly string LeftText =
        "a\n" + string.Concat(Enumerable.Range(1, PaddingRows).Select(n => $"gone {n:D2}\n")) + "z\n";

    [AvaloniaFact]
    public void Bringing_the_caret_into_view_again_leaves_the_scroll_where_the_first_time_put_it()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        DiffPanePresenter pane = host.Right;
        TextView view = pane.TextArea.TextView;
        AssertTallerThanThePane(pane);

        pane.TextArea.Caret.Location = new TextLocation(TallLine, 1);
        PresenterHost.Layout();

        // What the selection handler does after a pointer move, and again after the scroll the move
        // caused: no layout runs between the two.
        pane.TextArea.Caret.BringCaretToView(SelectionBorder);
        double first = view.VerticalOffset;
        pane.TextArea.Caret.BringCaretToView(SelectionBorder);
        double second = view.VerticalOffset;

        // From the top, the line's own row comes in at the bottom with the border the handler asked for,
        // where a plain line's row would.
        double expected = OwnRowTop(pane, TallLine) + view.DefaultLineHeight + SelectionBorder - Viewport(view);
        Assert.True(
            Math.Abs(first - expected) < Tolerance,
            $"The first time brought the caret's row in at {first}, where its own row and the border put it at {expected}.");
        Assert.True(
            Math.Abs(second - first) < Tolerance,
            $"Bringing the caret into view again moved the scroll from {first} to {second}.");
        AssertOwnRowInView(pane);
        Assert.Empty(pane.Faults);
    }

    [AvaloniaFact]
    public void Pressing_Up_onto_a_padded_line_taller_than_the_pane_keeps_its_own_row_in_view()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        DiffPanePresenter pane = host.Right;
        AssertTallerThanThePane(pane);

        // The caret on the line below, with the pane scrolled to its end so that line shows.
        pane.TextArea.Focus();
        pane.TextArea.Caret.Location = new TextLocation(TallLine + 1, 1);
        ScrollToEnd(pane);
        Assert.True(pane.TextArea.IsFocused, "the text area should take keyboard focus");

        Press(host.Window, Key.Up, PhysicalKey.ArrowUp);

        Assert.Equal(TallLine, pane.TextArea.Caret.Line);
        AssertOwnRowInView(pane);
        Assert.Empty(pane.Faults);
    }

    [AvaloniaFact]
    public void A_drag_from_the_padding_of_a_line_taller_than_the_pane_settles_with_its_own_row_in_view()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        DiffPanePresenter pane = host.Right;
        AssertTallerThanThePane(pane);

        // In the band of padding above the line's own row, near the top of the pane, with the row itself
        // below the pane's bottom edge.
        Drag(host.Window, pane, new Point(40, 3 * pane.TextArea.TextView.DefaultLineHeight));

        Assert.Equal(TallLine, pane.TextArea.Caret.Line);
        AssertOwnRowInView(pane);
        Assert.Empty(pane.Faults);
    }

    [AvaloniaFact]
    public void A_drag_on_the_text_of_a_line_taller_than_the_pane_settles_with_its_own_row_in_view()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        DiffPanePresenter pane = host.Right;
        AssertTallerThanThePane(pane);

        // Scrolled to the end, where the line's own row shows near the bottom of the pane.
        ScrollToEnd(pane);
        TextView view = pane.TextArea.TextView;
        double rowMiddle = OwnRowTop(pane, TallLine) + view.DefaultLineHeight / 2 - view.VerticalOffset;
        Drag(host.Window, pane, new Point(4, rowMiddle));

        Assert.Equal(TallLine, pane.TextArea.Caret.Line);
        AssertOwnRowInView(pane);
        Assert.Empty(pane.Faults);
    }

    [AvaloniaFact]
    public async Task A_drag_over_a_padded_line_taller_than_the_pane_settles_in_the_editor()
    {
        using CompositeHost host = new(width: 900, height: 380);
        host.Show();
        await host.LoadAsync(new PaneSource(LeftText), new PaneSource(RightText));
        DiffPanePresenter pane = host.Right;
        AssertTallerThanThePane(pane);

        Drag(host.Window, pane, new Point(40, 3 * pane.TextArea.TextView.DefaultLineHeight));

        Assert.Equal(TallLine, pane.TextArea.Caret.Line);
        AssertOwnRowInView(pane);
        Assert.Equal(DiffViewState.Ready, host.View.State);
    }

    [AvaloniaFact]
    public async Task A_drag_over_a_padded_line_taller_than_the_pane_settles_in_the_viewer()
    {
        using ViewerHost host = new(width: 900, height: 380);
        host.Show();
        await host.LoadAsync(new PaneSource(LeftText), new PaneSource(RightText));
        DiffPanePresenter pane = host.Right;
        AssertTallerThanThePane(pane);

        Drag(host.Window, pane, new Point(40, 3 * pane.TextArea.TextView.DefaultLineHeight));

        Assert.Equal(TallLine, pane.TextArea.Caret.Line);
        AssertOwnRowInView(pane);
        Assert.Equal(DiffViewState.Ready, host.View.State);
    }

    [AvaloniaFact]
    public void A_wheel_turned_while_the_button_is_held_over_a_padded_line_taller_than_the_pane_settles()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        DiffPanePresenter pane = host.Right;
        TextView view = pane.TextArea.TextView;
        AssertTallerThanThePane(pane);

        // Pressed in the band of padding above the tall line's own row and held while the wheel turns, as
        // Brian may have done: each notch scrolls the pane, and the selection handler hears of it from the
        // scroll itself, not from a pointer move.
        Point at = view.TranslatePoint(new Point(40, 3 * view.DefaultLineHeight), host.Window)
                   ?? throw new InvalidOperationException("The text view is not in the window's visual tree.");
        using ScrollTripwire tripwire = new(view);

        host.Window.MouseDown(at, MouseButton.Left);
        PresenterHost.Layout();
        tripwire.AssertSettled("The press");

        for (int notch = 1; notch <= 3; notch++)
        {
            host.Window.MouseWheel(at, new Vector(0, -1), RawInputModifiers.LeftMouseButton);
            PresenterHost.Layout();
            tripwire.AssertSettled($"Wheel notch {notch}");
        }

        host.Window.MouseUp(at, MouseButton.Left);
        PresenterHost.Layout();
        tripwire.AssertSettled("The release");

        Assert.Equal(TallLine, pane.TextArea.Caret.Line);
        AssertOwnRowInView(pane);
        Assert.Empty(pane.Faults);
    }

    [AvaloniaFact]
    public void Bringing_the_caret_into_view_in_a_pane_shorter_than_a_row_settles_where_the_first_time_put_it()
    {
        // The left side has no padding at all: here even a plain line's caret, with its border, is taller
        // than the pane.
        using PresenterHost host = new(LeftText, RightText, height: 24);
        host.Show();
        DiffPanePresenter pane = host.Left;
        TextView view = pane.TextArea.TextView;
        Assert.True(
            Viewport(view) > 0 && view.DefaultLineHeight + 2 * SelectionBorder > Viewport(view),
            $"The pane is {Viewport(view)} tall: the premise is a pane shorter than a row and its border.");

        pane.TextArea.Caret.Location = new TextLocation(5, 1);
        PresenterHost.Layout();

        pane.TextArea.Caret.BringCaretToView(SelectionBorder);
        double first = view.VerticalOffset;
        pane.TextArea.Caret.BringCaretToView(SelectionBorder);
        double second = view.VerticalOffset;

        Assert.True(
            Math.Abs(second - first) < Tolerance,
            $"Bringing the caret into view again moved the scroll from {first} to {second}.");
        Assert.Empty(pane.Faults);
    }

    [AvaloniaFact]
    public void A_rectangle_other_than_the_carets_is_brought_into_view_whole()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        DiffPanePresenter pane = host.Right;
        TextView view = pane.TextArea.TextView;

        // The caret on the tall line, and the pane scrolled to its end, away from the first line.
        pane.TextArea.Caret.Location = new TextLocation(TallLine, 1);
        ScrollToEnd(pane);
        Assert.True(view.VerticalOffset > view.DefaultLineHeight, "the pane should be scrolled past the first line");

        // A rectangle over the first line, as a host may ask to see one.
        view.MakeVisible(new Rect(0, 0, 10, view.DefaultLineHeight));

        Assert.True(
            view.VerticalOffset < Tolerance,
            $"A rectangle over the first line left the pane scrolled to {view.VerticalOffset}.");
    }

    [AvaloniaFact]
    public void A_stacked_input_handler_is_offered_each_key_before_the_text_area_handles_it()
    {
        using PresenterHost host = new(LeftText, RightText);
        host.Show();
        TextArea area = host.Right.TextArea;
        area.Focus();
        PresenterHost.Layout();
        KeyRecorder recorder = new(area);
        area.PushStackedInputHandler(recorder);

        Press(host.Window, Key.Down, PhysicalKey.ArrowDown);

        Assert.True(
            recorder.Keys.SequenceEqual(["down Down", "up Down"]),
            $"The stacked input handler was offered [{string.Join(", ", recorder.Keys)}].");
    }

    [AvaloniaFact]
    public void A_style_written_for_AvaloniaEdits_text_area_or_text_view_reaches_a_panes()
    {
        using PresenterHost host = new(LeftText, RightText);
        ImmutablePen ruler = new(Brushes.Red);
        host.Window.Styles.Add(new Style(x => x.OfType<TextArea>())
        {
            Setters = { new Setter(TextArea.SelectionCornerRadiusProperty, 7.0) },
        });
        host.Window.Styles.Add(new Style(x => x.OfType<TextView>())
        {
            Setters = { new Setter(TextView.ColumnRulerPenProperty, ruler) },
        });
        host.Show();

        TextArea area = host.Right.TextArea;
        Assert.True(
            area.SelectionCornerRadius == 7.0,
            $"A style written for TextArea left the pane's text area at a corner radius of {area.SelectionCornerRadius}.");
        Assert.True(
            ReferenceEquals(area.TextView.ColumnRulerPen, ruler),
            "A style written for TextView did not reach the pane's text view.");
    }

    /// <summary>
    /// A press, three small moves with the button held, and a release, at <paramref name="start"/> in the
    /// pane's text-view coordinates — each step asked how often it scrolled the pane.
    /// </summary>
    private static void Drag(Window window, DiffPanePresenter pane, Point start)
    {
        TextView view = pane.TextArea.TextView;
        Point at = view.TranslatePoint(start, window)
                   ?? throw new InvalidOperationException("The text view is not in the window's visual tree.");
        using ScrollTripwire tripwire = new(view);

        window.MouseDown(at, MouseButton.Left);
        PresenterHost.Layout();
        tripwire.AssertSettled("The press");

        for (int step = 1; step <= 3; step++)
        {
            window.MouseMove(at + new Vector(2 * step, 2 * step), RawInputModifiers.LeftMouseButton);
            PresenterHost.Layout();
            tripwire.AssertSettled($"Move {step}");
        }

        window.MouseUp(at + new Vector(6, 6), MouseButton.Left);
        PresenterHost.Layout();
        tripwire.AssertSettled("The release");
    }

    private static void Press(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        PresenterHost.Layout();
    }

    private static void ScrollToEnd(DiffPanePresenter pane)
    {
        PresenterHost.ScrollTo(pane, PresenterHost.ScrollViewerOf(pane).Extent.Height);
    }

    private static double Viewport(TextView view) => ((IScrollable)view).Viewport.Height;

    /// <summary>
    /// Document-relative top of a line's own row, past the padding the model puts above it — read from the
    /// model, not from what the pane drew.
    /// </summary>
    private static double OwnRowTop(DiffPanePresenter pane, int lineNumber)
    {
        TextView view = pane.TextArea.TextView;
        SideBySideDocument document = pane.DiffDocument ?? throw new InvalidOperationException("The pane has no model.");
        return view.GetVisualTopByDocumentLine(lineNumber)
               + Padding.Before(document, pane.Side, lineNumber - 1) * view.DefaultLineHeight;
    }

    /// <summary>
    /// The premise: the tall line, its border included, is taller than the pane and shorter than twice the
    /// pane, so AvaloniaEdit alternates rather than settling on the rectangle's middle.
    /// </summary>
    private static void AssertTallerThanThePane(DiffPanePresenter pane)
    {
        TextView view = pane.TextArea.TextView;
        Assert.Equal(PaddingRows, Padding.Before(pane.DiffDocument!, pane.Side, TallLine - 1));
        double height = view.GetVisualTopByDocumentLine(TallLine + 1) - view.GetVisualTopByDocumentLine(TallLine);
        double brought = height + 2 * SelectionBorder;
        double viewport = Viewport(view);
        Assert.True(
            viewport > 0 && brought > viewport && brought < 2 * viewport,
            $"The padded line is {height} tall in a pane {viewport} tall: the premise is a line taller than the "
            + "pane and shorter than twice it.");
    }

    /// <summary>The caret's line's own row lies inside the viewport.</summary>
    private static void AssertOwnRowInView(DiffPanePresenter pane)
    {
        TextView view = pane.TextArea.TextView;
        double top = OwnRowTop(pane, pane.TextArea.Caret.Line);
        double bottom = top + view.DefaultLineHeight;
        double offset = view.VerticalOffset;
        double viewport = Viewport(view);
        Assert.True(
            top >= offset - Tolerance && bottom <= offset + viewport + Tolerance,
            $"Line {pane.TextArea.Caret.Line}'s own row spans {top} to {bottom}, and the pane shows {offset} to {offset + viewport}.");
    }

    /// <summary>A stacked input handler that writes down every key it is offered.</summary>
    private sealed class KeyRecorder(TextArea area) : TextAreaStackedInputHandler(area)
    {
        public List<string> Keys { get; } = [];

        public override void OnPreviewKeyDown(KeyEventArgs e) => Keys.Add($"down {e.Key}");

        public override void OnPreviewKeyUp(KeyEventArgs e) => Keys.Add($"up {e.Key}");
    }
}
