using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00029's two library seams: what a pane's user did that nothing public reports — where a
/// click landed, the selection a pointer finished, a fold that opened — written under
/// <see cref="DiffViewLogCategories.Interaction"/> at <c>Debug</c>. A host that logs at
/// <c>Information</c> is told none of it; the demo switches the category on.
/// </summary>
public sealed class InteractionLogTests
{
    [AvaloniaFact]
    public async Task A_drag_is_logged_once_at_release_as_the_selection_it_made()
    {
        using CompositeHost host = await Loaded();

        // Five moves with the button held: none of them may write a line.
        Drag(host, TextPoint(host, host.Left, line: 3, column: 1), TextPoint(host, host.Left, line: 5, column: 5));

        Assert.False(host.Left.TextArea.Selection.IsEmpty, "the drag selected nothing, so it proves nothing");
        LogRecord line = Assert.Single(Interactions(host.Logs));
        Assert.Equal(LogLevel.Debug, line.Level);

        // "namespace Sample", "{" and the four spaces before "public": 16 + 1 + 1 + 1 + 4.
        Assert.Equal("Left pane: selected 3:1–5:5 (23 chars)", line.Message);
    }

    [AvaloniaFact]
    public async Task A_click_says_which_surface_of_the_pane_it_landed_on()
    {
        using CompositeHost host = await Loaded();

        Click(host, TextPoint(host, host.Left, line: 3, column: 4));
        Click(host, MarginPoint(host, host.Left.LineNumberMargin, host.Left, line: 4));
        Click(host, MarginPoint(host, host.Left.ChangeMarkerMargin, host.Left, line: 9));

        // The row above left line 2 is padding: the right side's inserted line sits beside it.
        Assert.Equal(1, host.Left.Metadata.PaddingBefore(2));
        Click(host, PaddingPoint(host, host.Left, line: 2));

        Assert.Equal(
            [
                "Left pane: click in the text at 3:4",
                "Left pane: click in the number margin at line 4",
                "Left pane: click in the change markers at line 9",
                "Left pane: click on the padding above line 2",
            ],
            Interactions(host.Logs).Select(r => r.Message));

        // At Debug, every one: a host at Information must be told none of it, which the level decides.
        Assert.All(Interactions(host.Logs), r => Assert.Equal(LogLevel.Debug, r.Level));
    }

    [AvaloniaFact]
    public async Task The_unified_pane_names_the_file_each_end_of_a_selection_belongs_to()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 900);
        host.Show();
        await host.LoadAsync(left, right);
        InlineHost.Layout();
        DiffPanePresenter pane = host.Pane;

        // The constructor's modified pair: its removal from the left file, then its addition from the right.
        int removed = UnifiedLineOf(pane, DiffSide.Left, 9);
        int added = UnifiedLineOf(pane, DiffSide.Right, 11);
        Assert.Equal(removed + 1, added);

        Point from = TextPoint(host.Window, pane, removed, column: 1);
        Point to = TextPoint(host.Window, pane, added, column: 5);
        Drag(host.Window, from, to);

        int characters = pane.Document.GetOffset(added, 5) - pane.Document.GetOffset(removed, 1);
        LogRecord line = Assert.Single(host.Logs.Records, r => r.Category == DiffViewLogCategories.Interaction);
        Assert.Equal($"unified pane: selected left 9:1–right 11:5 ({characters} chars)", line.Message);
        Assert.Equal(LogLevel.Debug, line.Level);
    }

    [AvaloniaFact]
    public async Task Opening_a_fold_is_logged_with_the_rows_it_gives_back()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));
        host.View.UnchangedContextRows = 0;
        CompositeHost.Layout();

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        FoldedRun run = host.View.ApplyFolds(0).FoldAt(0);
        (int First, int Last) lines = FoldPlan.LinesOf(document, run, DiffSide.Left)
                                      ?? throw new InvalidOperationException("A taken fold hides nothing.");
        host.Left.TextArea.Caret.Line = lines.First - 1;
        host.View.CommandFor(DiffCommand.ExpandFold).Execute(null);
        CompositeHost.Layout();

        LogRecord line = Assert.Single(Interactions(host.Logs), r => r.Message.StartsWith("Fold opened", StringComparison.Ordinal));
        Assert.Equal($"Fold opened: rows {run.FirstRow + 1}–{run.FirstRow + run.RowCount}", line.Message);
        Assert.Equal(LogLevel.Debug, line.Level);
    }

    [AvaloniaFact]
    public async Task A_host_logging_at_Information_is_told_nothing()
    {
        CapturingLoggerFactory quiet = new(LogLevel.Information);
        using CompositeHost host = await Loaded(quiet);

        Drag(host, TextPoint(host, host.Left, line: 3, column: 1), TextPoint(host, host.Left, line: 5, column: 5));
        Assert.False(host.Left.TextArea.Selection.IsEmpty, "the drag selected nothing, so it proves nothing");
        Click(host, TextPoint(host, host.Left, line: 3, column: 4));
        Click(host, PaddingPoint(host, host.Left, line: 2));

        Assert.Empty(Interactions(quiet));

        // Not a deaf host: the same session's builds reached it.
        Assert.Contains(quiet.Records, r => r.Category == DiffViewLogCategories.Build);
    }

    [AvaloniaFact]
    public async Task No_interaction_line_carries_document_text()
    {
        const string sentinel = "SENTINEL_7f3a9c";
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 700);
        host.Show();
        await host.LoadAsync(
            left.Replace("Greeter", sentinel, StringComparison.Ordinal),
            right.Replace("Greeter", sentinel + "2", StringComparison.Ordinal));
        CompositeHost.Layout();

        // Line 5 is "    public sealed class " and the sentinel: select all of it, then click inside it.
        Drag(host, TextPoint(host, host.Left, line: 5, column: 1), TextPoint(host, host.Left, line: 5, column: 40));
        Click(host, TextPoint(host, host.Left, line: 5, column: 30));

        Assert.NotEmpty(Interactions(host.Logs));
        Assert.DoesNotContain(host.Logs.Records, r => r.Everything.Contains(sentinel, StringComparison.Ordinal));
    }

    private static async Task<CompositeHost> Loaded(CapturingLoggerFactory? logs = null)
    {
        (string left, string right) = CompositeHost.SmallFixture();
        CompositeHost host = new(width: 900, height: 700, logs: logs);
        host.Show();
        await host.LoadAsync(left, right);
        CompositeHost.Layout();
        return host;
    }

    private static IReadOnlyList<LogRecord> Interactions(CapturingLoggerFactory logs) =>
        [.. logs.Records.Where(r => r.Category == DiffViewLogCategories.Interaction)];

    private static void Click(CompositeHost host, Point at)
    {
        host.Window.MouseDown(at, MouseButton.Left);
        host.Window.MouseUp(at, MouseButton.Left);
        CompositeHost.Layout();
    }

    private static void Drag(CompositeHost host, Point from, Point to) => Drag(host.Window, from, to);

    private static void Drag(Avalonia.Controls.Window window, Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left);
        foreach (double share in (double[])[0.2, 0.4, 0.6, 0.8, 1.0])
        {
            window.MouseMove(from + ((to - from) * share), RawInputModifiers.LeftMouseButton);
        }

        window.MouseUp(to, MouseButton.Left);
        CompositeHost.Layout();
    }

    private static Point TextPoint(CompositeHost host, DiffPanePresenter pane, int line, int column) =>
        TextPoint(host.Window, pane, line, column);

    /// <summary>Just inside the left edge of a character, on its line's text band, in window coordinates.</summary>
    private static Point TextPoint(Avalonia.Controls.Window window, DiffPanePresenter pane, int line, int column)
    {
        TextView view = pane.TextArea.TextView;
        VisualLine visual = view.GetOrConstructVisualLine(pane.Document.GetLineByNumber(line));
        TextLine text = visual.TextLines[0];
        double y = visual.GetTextLineVisualYPosition(text, VisualYPosition.TextMiddle) - view.VerticalOffset;
        double x = visual.GetTextLineVisualXPosition(text, visual.GetVisualColumn(column - 1)) - view.HorizontalOffset;
        return view.TranslatePoint(new Point(x + 1, y), window) ?? throw new InvalidOperationException("The text view is not in the window.");
    }

    /// <summary>The middle of a margin, beside a line's text.</summary>
    private static Point MarginPoint(CompositeHost host, Avalonia.Controls.Control margin, DiffPanePresenter pane, int line)
    {
        Point beside = TextPoint(host, pane, line, column: 1);
        Point inMargin = host.Window.TranslatePoint(beside, margin) ?? throw new InvalidOperationException("The margin is not in the window.");
        return margin.TranslatePoint(new Point(margin.Bounds.Width / 2, inMargin.Y), host.Window)
               ?? throw new InvalidOperationException("The margin is not in the window.");
    }

    /// <summary>Halfway down the padding above a line's text, a little way into the text area.</summary>
    private static Point PaddingPoint(CompositeHost host, DiffPanePresenter pane, int line)
    {
        TextView view = pane.TextArea.TextView;
        VisualLine visual = view.GetOrConstructVisualLine(pane.Document.GetLineByNumber(line));
        double textTop = visual.GetTextLineVisualYPosition(visual.TextLines[0], VisualYPosition.TextTop);
        double y = ((visual.VisualTop + textTop) / 2) - view.VerticalOffset;
        return view.TranslatePoint(new Point(20, y), host.Window) ?? throw new InvalidOperationException("The text view is not in the window.");
    }

    /// <summary>The unified document's line that shows <paramref name="sourceLine"/> of one side's file.</summary>
    private static int UnifiedLineOf(DiffPanePresenter pane, DiffSide side, int sourceLine)
    {
        for (int line = 1; line <= pane.Document.LineCount; line++)
        {
            DiffPaneContext context = pane.ContextAt(line);
            if (context.SourceSide == side && context.SourceLine == sourceLine)
            {
                return line;
            }
        }

        throw new InvalidOperationException($"No unified line shows {side} line {sourceLine}.");
    }
}
