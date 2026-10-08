using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00031: an edit under the editor's folds, before its re-diff lands. The folds follow the lines
/// AvaloniaEdit moves, shrinks and uncollapses as the text changes, so the layout stays whole; and each
/// fold keeps the identity it was collapsed under, so a click or the expand command still opens a fold
/// that moved.
/// </summary>
/// <remarks>
/// A layout failure belongs to a dispatcher job, which the runner reports apart from the test, so each
/// test collects the dispatcher's unhandled exceptions and asserts on them before anything else. A fold
/// is followed by its text rather than its number: every line of the folding pair is unique, so the line
/// a fold started on can be found again wherever an edit moved it.
/// </remarks>
public sealed class EditUnderFoldTests
{
    private const double Tolerance = 0.5;

    /// <summary>A right-side fold before the edit: its lines, and the text of each end.</summary>
    private sealed record Fold(int First, int Last, string FirstText, string LastText);

    [AvaloniaFact]
    public async Task A_line_typed_above_the_folds_leaves_each_fold_over_the_lines_it_covered()
    {
        await EditAsync(host => host.View.RightDocument.Insert(0, "typed above every fold\n"));
    }

    [AvaloniaFact]
    public async Task A_block_copied_above_the_folds_leaves_each_fold_over_the_lines_it_covered()
    {
        // Block 0 is the left's three-line deletion after the first run: copying it right inserts three
        // lines on the right above every fold but the first.
        await EditAsync(host => Assert.True(host.View.CopyBlock(0, DiffSide.Right)));
    }

    /// <summary>
    /// A revert is the one edit here that does not wait for the debounce: it replaces the whole text
    /// and re-diffs at once. AvaloniaEdit keeps the sections across that replacement, but every line
    /// they covered has gone, so nothing is collapsed until the build lands — the folds come back from
    /// the re-diff rather than by following their lines, which is what the other two edits prove. So
    /// the three stages are asserted apart, and the last wait is on the build the revert started.
    /// </summary>
    /// <remarks>
    /// Waiting on a pump instead is what made this test fail on macOS alone and intermittently: the
    /// worker runs the diff on the thread pool and posts its outcome to the dispatcher, so a single
    /// <see cref="CompositeHost.Layout"/> lands the re-diff only when the pool happened to finish
    /// first. The run that caught it read the first fold's nineteen lines at their full height.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_revert_after_a_same_line_edit_leaves_each_fold_over_the_lines_it_covered()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            List<Fold> before = RightFolds(host);
            Assert.True(before.Count > 1, "the pair should fold more than one run on the right");

            // The same-line edit moves no line, and it re-diffs on the debounce that the
            // hand-advanced clock never fires, so every fold stands through it on the sections alone.
            host.View.RightDocument.Insert(0, "x");
            Assert.Null(host.View.CurrentBuild);
            CompositeHost.Layout();
            host.Capture().Dispose();
            AssertWhole(host, thrown, "after the edit");
            AssertEachFolded(host, before);

            // The revert's own build is held on a gate, which is the only way to be sure of reading
            // what the replacement left behind: the dispatcher cannot run between two statements
            // here, but this thread can be descheduled while the worker finishes, and that is the
            // race the test used to depend on.
            using ManualResetEventSlim gate = new(initialState: false);
            host.View.Builder = (left, right, options, token) =>
            {
                gate.Wait(token);
                return CompositeHost.ZeroTimeBuilder(left, right, options, token);
            };

            try
            {
                host.View.Revert(DiffSide.Right);
                Assert.NotNull(host.View.CurrentBuild);
                AssertNoneFolded(host, before);
            }
            finally
            {
                gate.Set();
            }

            await host.WaitForBuildAsync();
            host.Capture().Dispose();
            AssertWhole(host, thrown, "across the revert's re-diff");
            AssertEachFolded(host, before);
            TestLogSink.AssertNoWarnings();
        });
    }

    [AvaloniaFact]
    public async Task Deleting_a_folds_first_line_shrinks_that_fold_by_one()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            List<Fold> before = RightFolds(host);
            Fold target = before[1];
            TextDocument document = host.View.RightDocument;
            string nextText = document.GetText(document.GetLineByNumber(target.First + 1));

            DocumentLine first = document.GetLineByNumber(target.First);
            document.Remove(first.Offset, first.TotalLength);
            CompositeHost.Layout();
            host.Capture().Dispose();

            AssertWhole(host, thrown, "after the deletion");
            // The fold now starts on the line after the one deleted and ends where it did: one line shorter.
            AssertFolded(host.Right, LineOf(document, nextText), LineOf(document, target.LastText));
            TestLogSink.AssertNoWarnings();
        });
    }

    [AvaloniaFact]
    public async Task Deleting_every_line_of_a_fold_leaves_no_placeholder_and_the_other_folds_stand()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            List<Fold> before = RightFolds(host);
            Fold target = before[1];
            TextDocument document = host.View.RightDocument;
            string headerText = document.GetText(document.GetLineByNumber(target.First - 1));

            // From the end of the header's text to the end of the fold's last line: the header keeps
            // its line and every line of the fold goes, so the height tree uncollapses the section. A
            // removal that starts on the fold's first line is a different edit — see the next test.
            DocumentLine header = document.GetLineByNumber(target.First - 1);
            DocumentLine last = document.GetLineByNumber(target.Last);
            document.Remove(header.EndOffset, last.EndOffset - header.EndOffset);
            CompositeHost.Layout();
            host.Capture().Dispose();

            AssertWhole(host, thrown, "after the deletion");
            Assert.Null(PlaceholderOn(host.Right, LineOf(document, headerText)));
            foreach (Fold other in before.Where(fold => fold != target))
            {
                AssertFolded(host.Right, LineOf(document, other.FirstText), LineOf(document, other.LastText));
            }

            TestLogSink.AssertNoWarnings();
        });
    }

    /// <summary>
    /// A removal that starts at the beginning of a fold's first line and runs to the beginning of the
    /// line after the fold is the same text gone, but not the same lines: AvaloniaEdit keeps the first
    /// line and gives it what followed the removal, so the section keeps that one line and the fold
    /// hides the line after it until the re-diff folds again. The layout stays whole either way; this
    /// pins which way AvaloniaEdit 12.0.0 goes, so a version that changes it says so here.
    /// </summary>
    [AvaloniaFact]
    public async Task A_removal_from_a_folds_first_line_keeps_that_line_so_the_fold_shrinks_onto_the_next()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            List<Fold> before = RightFolds(host);
            Fold target = before[1];
            TextDocument document = host.View.RightDocument;
            DocumentLine first = document.GetLineByNumber(target.First);
            DocumentLine after = document.GetLineByNumber(target.Last + 1);
            string afterText = document.GetText(after);

            document.Remove(first.Offset, after.Offset - first.Offset);
            CompositeHost.Layout();
            host.Capture().Dispose();

            AssertWhole(host, thrown, "after the removal");
            int kept = LineOf(document, afterText);
            AssertFolded(host.Right, kept, kept);
            foreach (Fold other in before.Where(fold => fold != target))
            {
                AssertFolded(host.Right, LineOf(document, other.FirstText), LineOf(document, other.LastText));
            }

            TestLogSink.AssertNoWarnings();
        });
    }

    [AvaloniaFact]
    public async Task A_moved_placeholder_clicked_before_the_rediff_opens_its_fold()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            Fold target = RightFolds(host)[0];
            int folds = host.Right.CollapsedSectionCount;

            host.View.RightDocument.Insert(0, "typed above every fold\n");
            CompositeHost.Layout();
            AssertWhole(host, thrown, "after the edit");

            int header = LineOf(host.View.RightDocument, target.FirstText) - 1;
            FoldPlaceholderElement placeholder = Assert.IsType<FoldPlaceholderElement>(PlaceholderOn(host.Right, header));
            placeholder.Expand();
            CompositeHost.Layout();

            AssertWhole(host, thrown, "after the click");
            Assert.Equal(folds - 1, host.Right.CollapsedSectionCount);
            TestLogSink.AssertNoWarnings();
        });
    }

    [AvaloniaFact]
    public async Task The_expand_command_on_a_moved_placeholders_line_opens_its_fold()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            Fold target = RightFolds(host)[0];
            int folds = host.Right.CollapsedSectionCount;

            host.View.RightDocument.Insert(0, "typed above every fold\n");
            CompositeHost.Layout();
            AssertWhole(host, thrown, "after the edit");

            // The command reads the focused pane's caret, and the caret can only be on a placeholder's
            // own line: the line before its fold.
            host.Right.TextArea.Focus();
            host.Right.TextArea.Caret.Line = LineOf(host.View.RightDocument, target.FirstText) - 1;
            CompositeHost.Layout();
            Assert.Equal(DiffSide.Right, host.View.FocusedSide);
            Assert.True(host.View.CommandFor(DiffCommand.ExpandFold).CanExecute(null));
            host.View.CommandFor(DiffCommand.ExpandFold).Execute(null);
            CompositeHost.Layout();

            AssertWhole(host, thrown, "after the command");
            Assert.Equal(folds - 1, host.Right.CollapsedSectionCount);
            TestLogSink.AssertNoWarnings();
        });
    }

    [AvaloniaFact]
    public async Task Once_the_rediff_lands_the_folds_are_the_new_models()
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();

            host.View.RightDocument.Insert(0, "typed above every fold\n");
            await host.WaitForReDiffAsync();
            host.Capture().Dispose();

            AssertWhole(host, thrown, "across the re-diff");
            SideBySideDocument rebuilt = host.View.Document ?? throw new InvalidOperationException("No model.");
            IReadOnlyList<FoldedRun> planned = FoldPlan.For(rebuilt, 0);
            Assert.Equal(planned.Count, host.Right.CollapsedSectionCount);
            foreach (FoldedRun run in planned)
            {
                (int first, int last) = FoldPlan.LinesOf(rebuilt, run, DiffSide.Right)
                                        ?? throw new InvalidOperationException("A planned fold hides nothing on the right.");
                AssertFolded(host.Right, first, last);
            }

            TestLogSink.AssertNoWarnings();
        });
    }

    /// <summary>
    /// Folds the pair with its right side editable, applies <paramref name="change"/>, and requires the
    /// layout whole and every fold collapsed over the text it covered before the change. For a change
    /// that leaves the re-diff to the debounce; one that re-diffs at once is written out in full.
    /// </summary>
    private static async Task EditAsync(Action<CompositeHost> change)
    {
        await WithCollectorAsync(async thrown =>
        {
            using CompositeHost host = await FoldedAsync();
            List<Fold> before = RightFolds(host);
            Assert.True(before.Count > 1, "the pair should fold more than one run on the right");

            change(host);

            // Neither change that reaches here re-diffs: both go through the debounce, which the
            // hand-advanced clock never fires. So the folds below stand on the sections following
            // their lines, and not on a build having landed and refolded them. A change that
            // re-diffs at once has to wait for that build — the revert's own test does, and says why.
            Assert.Null(host.View.CurrentBuild);
            CompositeHost.Layout();
            host.Capture().Dispose();

            AssertWhole(host, thrown, "after the edit");
            AssertEachFolded(host, before);

            TestLogSink.AssertNoWarnings();
        });
    }

    /// <summary>The folding pair loaded, the right side editable, and every run folded with no context.</summary>
    private static async Task<CompositeHost> FoldedAsync()
    {
        TestLogSink.Instance.Clear();
        CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));
        host.View.RightReadOnly = false;
        host.View.ApplyFolds(0);
        CompositeHost.Layout();
        return host;
    }

    /// <summary>The right pane's folds as the model has them before any edit, with the text of each end.</summary>
    private static List<Fold> RightFolds(CompositeHost host)
    {
        SideBySideDocument model = host.View.Document ?? throw new InvalidOperationException("No model.");
        RowProjection projection = host.View.ApplyFolds(0);
        CompositeHost.Layout();
        TextDocument document = host.View.RightDocument;
        List<Fold> folds = [];
        for (int fold = 0; fold < projection.FoldCount; fold++)
        {
            if (FoldPlan.LinesOf(model, projection.FoldAt(fold), DiffSide.Right) is { } lines)
            {
                folds.Add(new Fold(
                    lines.First,
                    lines.Last,
                    document.GetText(document.GetLineByNumber(lines.First)),
                    document.GetText(document.GetLineByNumber(lines.Last))));
            }
        }

        return folds;
    }

    /// <summary>
    /// What the height tree says rather than what the pane was told: the fold's lines take no height —
    /// the line after it starts where its first line would — and its header carries the placeholder.
    /// </summary>
    private static void AssertFolded(DiffPanePresenter pane, int first, int last)
    {
        TextView view = pane.TextArea.TextView;
        Assert.Equal(view.GetVisualTopByDocumentLine(first), view.GetVisualTopByDocumentLine(last + 1), Tolerance);
        Assert.NotNull(PlaceholderOn(pane, first - 1));
    }

    /// <summary>
    /// The counterpart of <see cref="AssertFolded"/>: the run takes at least a line's height per line
    /// it holds, so nothing at all is collapsed over it. A floor rather than an equality because
    /// padding only adds — a line at a run's edge can carry rows for the other side.
    /// </summary>
    private static void AssertNotFolded(DiffPanePresenter pane, int first, int last)
    {
        TextView view = pane.TextArea.TextView;
        double height = view.GetVisualTopByDocumentLine(last + 1) - view.GetVisualTopByDocumentLine(first);
        double owed = (last - first + 1) * view.DefaultLineHeight;
        Assert.True(
            height >= owed - Tolerance,
            $"lines {first}-{last} take {height:0.##} of the {owed:0.##} they owe, so something is collapsed over them");
    }

    /// <summary>Every fold of <paramref name="before"/> collapsed over the text it covered.</summary>
    private static void AssertEachFolded(CompositeHost host, List<Fold> before)
    {
        TextDocument document = host.View.RightDocument;
        foreach (Fold fold in before)
        {
            AssertFolded(host.Right, LineOf(document, fold.FirstText), LineOf(document, fold.LastText));
        }
    }

    /// <summary>Not one of them collapsed: the lines each covered take their own height back.</summary>
    private static void AssertNoneFolded(CompositeHost host, List<Fold> before)
    {
        TextDocument document = host.View.RightDocument;
        foreach (Fold fold in before)
        {
            AssertNotFolded(host.Right, LineOf(document, fold.FirstText), LineOf(document, fold.LastText));
        }
    }

    private static FoldPlaceholderElement? PlaceholderOn(DiffPanePresenter pane, int headerLine)
    {
        VisualLine line = pane.TextArea.TextView.GetOrConstructVisualLine(pane.Document.GetLineByNumber(headerLine));
        return line.Elements.OfType<FoldPlaceholderElement>().SingleOrDefault();
    }

    /// <summary>The line holding exactly <paramref name="text"/>, which the fixture makes unique.</summary>
    private static int LineOf(TextDocument document, string text)
    {
        DocumentLine? line = document.Lines.SingleOrDefault(candidate => document.GetText(candidate) == text);
        Assert.NotNull(line);
        return line.LineNumber;
    }

    private static async Task WithCollectorAsync(Func<List<Exception>, Task> test)
    {
        List<Exception> thrown = [];
        void Collect(object? sender, DispatcherUnhandledExceptionEventArgs e) => thrown.Add(e.Exception);
        Dispatcher.UIThread.UnhandledException += Collect;
        try
        {
            await test(thrown);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Collect;
        }
    }

    /// <summary>
    /// The layout whole: nothing thrown from the dispatcher, and no decorator degraded. A decorator is a
    /// fault boundary that catches, reports and disables itself, so a generator that broke shows only in
    /// the panes' faults — the frame still renders without it.
    /// </summary>
    private static void AssertWhole(CompositeHost host, List<Exception> thrown, string when)
    {
        Assert.True(thrown.Count == 0, $"the layout threw {when}: " + Messages(thrown));
        Assert.True(
            host.Left.Faults.Count == 0 && host.Right.Faults.Count == 0,
            $"a decorator faulted {when}: "
            + string.Join("; ", host.Left.Faults.Concat(host.Right.Faults).Select(fault => $"{fault.Source}: {fault.Exception.Message}")));
    }

    private static string Messages(List<Exception> thrown) => string.Join("; ", thrown.Select(e => e.Message));
}
