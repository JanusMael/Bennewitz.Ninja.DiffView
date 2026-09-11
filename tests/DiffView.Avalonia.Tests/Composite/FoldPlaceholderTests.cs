using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00013 phase 3: the placeholder a folded run leaves behind — what it says, that a click on
/// it asks for the run back, and that it shares its line with the padding generator rather than
/// displacing it.
/// </summary>
public sealed class FoldPlaceholderTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaFact]
    public async Task The_placeholder_spans_its_run_and_says_how_many_rows_it_hides()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();

        FoldedRun run = projection.FoldAt(0);
        (int First, int Last) lines = FoldPlan.LinesOf(document, run, DiffSide.Left)
                                      ?? throw new InvalidOperationException("A taken fold hides nothing.");
        FoldPlaceholderElement element = PlaceholderOn(host.Left, lines.First - 1);

        // One visual line spans the whole run, which is what keeps the measure pass off a
        // collapsed line.
        VisualLine visualLine = host.Left.TextArea.TextView.GetVisualLine(lines.First - 1)
                                ?? throw new InvalidOperationException("The header line has no visual line.");
        Assert.Equal(lines.Last, visualLine.LastDocumentLine.LineNumber);

        int hidden = lines.Last - lines.First + 1;
        Assert.Equal(run.HiddenCount, hidden);
        Assert.Contains(hidden.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), element.PlaceholderText);
        Assert.Equal(DiffViewStrings.Format(DiffViewStrings.FoldPlaceholder, hidden.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), element.PlaceholderText);
    }

    [AvaloniaFact]
    public async Task A_click_on_a_placeholder_gives_that_run_back_and_leaves_the_others_folded()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        RowProjection folded = host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();
        int foldCount = folded.FoldCount;
        Assert.True(foldCount > 1, "the fixture should fold more than one run");

        FoldedRun first = folded.FoldAt(0);
        (int First, int Last) lines = FoldPlan.LinesOf(document, first, DiffSide.Left)
                                      ?? throw new InvalidOperationException("A taken fold hides nothing.");

        // A real press, at the placeholder's own visual column rather than a guessed x.
        TextView textView = host.Left.TextArea.TextView;
        VisualLine visualLine = textView.GetVisualLine(lines.First - 1)
                                ?? throw new InvalidOperationException("The header line has no visual line.");
        FoldPlaceholderElement element = PlaceholderOn(host.Left, lines.First - 1);
        Point inText = visualLine.GetVisualPosition(element.VisualColumn, VisualYPosition.TextMiddle) - textView.ScrollOffset;
        Point inWindow = textView.TranslatePoint(inText + new Vector(4, 0), host.Window)
                         ?? throw new InvalidOperationException("The text view is not in the window.");

        host.Window.MouseDown(inWindow, MouseButton.Left);
        host.Window.MouseUp(inWindow, MouseButton.Left);
        CompositeHost.Layout();

        RowProjection after = host.View.ApplyFolds(contextRows: 0);
        Assert.Equal(foldCount - 1, after.FoldCount);
        Assert.False(after.IsHidden(first.FirstRow + 1));
        for (int fold = 0; fold < after.FoldCount; fold++)
        {
            Assert.NotEqual(first.FirstRow, after.FoldAt(fold).FirstRow);
        }

        // The other runs are untouched, and the panes still agree.
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        Assert.Equal(after.FoldCount, host.Left.CollapsedSectionCount);
    }

    /// <summary>
    /// The two generators are interested in different offsets of the same document — the padding
    /// generator in a line's start, the placeholder in the end of the line before a run — so
    /// neither may take the other's. This fixture has a padded line and a folded run in the same
    /// pane, and asserts both still do their work.
    /// </summary>
    [AvaloniaFact]
    public async Task The_padding_and_the_placeholder_share_a_document_rather_than_displace_each_other()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;

        // A line the left pads: the right has lines there that the left does not.
        int paddedLine = PaddedLine(document, DiffSide.Left);
        int padding = Padding.Before(document, DiffSide.Left, paddedLine - 1);
        Assert.True(padding > 0, "the fixture should pad a left line");

        host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();

        // The padded line is still (1 + padding) rows tall, so the padding element still ran.
        double top = host.Left.TextArea.TextView.GetVisualTopByDocumentLine(paddedLine);
        double next = host.Left.TextArea.TextView.GetVisualTopByDocumentLine(paddedLine + 1);
        Assert.Equal((1 + padding) * lineHeight, next - top, Tolerance);

        // And a run is still spanned, so the placeholder element still ran.
        RowProjection projection = host.View.ApplyFolds(contextRows: 0);
        (int First, int Last) lines = FoldPlan.LinesOf(document, projection.FoldAt(0), DiffSide.Left)
                                      ?? throw new InvalidOperationException("A taken fold hides nothing.");
        Assert.NotNull(PlaceholderOn(host.Left, lines.First - 1));
    }

    /// <summary>
    /// Folding is a reduction in what is drawn, not in what the pane holds: the document keeps
    /// every line, so anything reading the pane's text — a screen reader among them — still has
    /// the rows a fold hides.
    /// </summary>
    [AvaloniaFact]
    public async Task A_folded_run_is_still_in_the_panes_text()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        string before = host.Left.Document.Text;
        host.View.ApplyFolds(contextRows: 0);
        CompositeHost.Layout();

        Assert.Equal(before, host.Left.Document.Text);
        Assert.Equal(left, host.Left.Document.Text);
    }

    private static FoldPlaceholderElement PlaceholderOn(DiffPanePresenter pane, int headerLine)
    {
        VisualLine visualLine = pane.TextArea.TextView.GetOrConstructVisualLine(pane.Document.GetLineByNumber(headerLine));
        return visualLine.Elements.OfType<FoldPlaceholderElement>().Single();
    }

    /// <summary>An AvaloniaEdit line number on <paramref name="side"/> that carries padding above it.</summary>
    private static int PaddedLine(SideBySideDocument document, DiffSide side)
    {
        foreach (AlignedRow row in document.Rows)
        {
            if (row.LineOf(side) is { } line && Padding.Before(document, side, line) > 0)
            {
                return line + 1;
            }
        }

        throw new InvalidOperationException($"No {side} line is padded.");
    }
}
