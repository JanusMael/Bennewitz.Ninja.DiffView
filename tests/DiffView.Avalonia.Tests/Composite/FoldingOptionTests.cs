using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00013 phase 4: <c>UnchangedContextRows</c> on both views, the four commands, and the
/// folding group on the menus plan 00012 built.
/// </summary>
public sealed class FoldingOptionTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaFact]
    public async Task The_option_folds_and_the_default_folds_nothing()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        // The default is null, which is Show All.
        Assert.Null(host.View.UnchangedContextRows);
        double unfolded = host.Left.ExtentHeight;
        Assert.Equal(0, host.Left.CollapsedSectionCount);

        host.View.UnchangedContextRows = 0;
        CompositeHost.Layout();
        double hidingEverything = host.Left.ExtentHeight;
        Assert.True(hidingEverything < unfolded);
        Assert.True(host.Left.CollapsedSectionCount > 0);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);

        // Context keeps rows back, so it hides less than none does.
        host.View.UnchangedContextRows = 3;
        CompositeHost.Layout();
        Assert.True(host.Left.ExtentHeight > hidingEverything);
        Assert.True(host.Left.ExtentHeight < unfolded);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);

        // And null puts every row back.
        host.View.UnchangedContextRows = null;
        CompositeHost.Layout();
        Assert.Equal(unfolded, host.Left.ExtentHeight, Tolerance);
        Assert.Equal(0, host.Left.CollapsedSectionCount);
    }

    [AvaloniaFact]
    public async Task A_negative_context_is_coerced_rather_than_kept()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        host.View.UnchangedContextRows = -5;
        Assert.Equal(0, host.View.UnchangedContextRows);
    }

    [AvaloniaFact]
    public async Task The_three_modes_are_the_one_option_written_three_ways()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        // Each mode is disabled exactly where it is already in force.
        Assert.False(host.View.CommandFor(DiffCommand.ShowAllRows).CanExecute(null));
        Assert.True(host.View.CommandFor(DiffCommand.ShowDifferencesOnly).CanExecute(null));
        Assert.True(host.View.CommandFor(DiffCommand.ShowContext).CanExecute(null));

        host.View.CommandFor(DiffCommand.ShowDifferencesOnly).Execute(null);
        CompositeHost.Layout();
        Assert.Equal(0, host.View.UnchangedContextRows);
        Assert.False(host.View.CommandFor(DiffCommand.ShowDifferencesOnly).CanExecute(null));
        Assert.True(host.View.CommandFor(DiffCommand.ShowAllRows).CanExecute(null));

        host.View.CommandFor(DiffCommand.ShowContext).Execute(null);
        CompositeHost.Layout();
        Assert.Equal(DiffKeyMap.DefaultContextRows, host.View.UnchangedContextRows);

        host.View.CommandFor(DiffCommand.ShowAllRows).Execute(null);
        CompositeHost.Layout();
        Assert.Null(host.View.UnchangedContextRows);
    }

    [AvaloniaFact]
    public async Task All_four_folding_verbs_arrive_in_the_key_map_unbound()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        foreach (DiffCommand verb in (DiffCommand[])[DiffCommand.ShowAllRows, DiffCommand.ShowDifferencesOnly, DiffCommand.ShowContext, DiffCommand.ExpandFold])
        {
            Assert.Contains(verb, host.View.KeyMap.Commands);
            Assert.Null(host.View.KeyMap[verb]);
        }
    }

    [AvaloniaFact]
    public async Task The_unified_view_folds_its_own_runs()
    {
        using InlineHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        DiffPanePresenter pane = host.Pane;
        Assert.Null(host.View.UnchangedContextRows);
        double unfolded = pane.ExtentHeight;

        host.View.UnchangedContextRows = 0;
        InlineHost.Layout();
        Assert.True(pane.CollapsedSectionCount > 0);
        Assert.True(pane.ExtentHeight < unfolded);

        host.View.UnchangedContextRows = null;
        InlineHost.Layout();
        Assert.Equal(0, pane.CollapsedSectionCount);
        Assert.Equal(unfolded, pane.ExtentHeight, Tolerance);
    }

    [AvaloniaFact]
    public async Task A_rebuild_keeps_the_option_and_forgets_the_runs_the_reader_opened()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        host.View.UnchangedContextRows = 0;
        CompositeHost.Layout();
        int folds = host.Left.CollapsedSectionCount;
        Assert.True(folds > 1);

        // Open one, then rebuild over the same text: the option stays, the opened run does not.
        host.View.CommandFor(DiffCommand.ExpandFold);
        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        RowProjection projection = host.View.ApplyFolds(0);
        FoldedRun run = projection.FoldAt(0);
        (int First, int Last) lines = FoldPlan.LinesOf(document, run, DiffSide.Left)
                                      ?? throw new InvalidOperationException("A taken fold hides nothing.");
        host.Left.TextArea.Caret.Line = lines.First - 1;
        Assert.True(host.View.CommandFor(DiffCommand.ExpandFold).CanExecute(null));
        host.View.CommandFor(DiffCommand.ExpandFold).Execute(null);
        CompositeHost.Layout();
        Assert.Equal(folds - 1, host.Left.CollapsedSectionCount);

        // A genuinely different pair: PaneSource is a record, so reloading the same text is not a
        // property change and would rebuild nothing at all.
        await host.LoadAsync(new PaneSource(left + "\nand one more"), new PaneSource(right + "\nand one more"));
        CompositeHost.Layout();

        SideBySideDocument rebuilt = host.View.Document ?? throw new InvalidOperationException("No model.");
        Assert.Equal(0, host.View.UnchangedContextRows);
        Assert.Equal(FoldPlan.For(rebuilt, 0).Count, host.Left.CollapsedSectionCount);
        Assert.True(host.Left.CollapsedSectionCount > folds - 1, "the run the reader opened should not stay open across a rebuild");
    }

    /// <summary>
    /// The unified twin of the test above, over both ways a model is rebuilt: new sources, and an
    /// option that rebuilds over the same two documents. The unified view rewrites its one
    /// document for every model, so folds carried across a rebuild do not merely name the wrong
    /// runs — they describe lines of a text that is gone, and AvaloniaEdit throws from the layout
    /// pass. That throw belongs to a dispatcher job rather than to this method, and the runner
    /// reports it apart from the test, as a cleanup failure; so it is collected here and asserted
    /// on like everything else.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("sources")]
    [InlineData("option")]
    public async Task A_unified_rebuild_keeps_the_option_and_forgets_the_runs_the_reader_opened(string rebuild)
    {
        List<Exception> thrown = [];
        void Collect(object? sender, DispatcherUnhandledExceptionEventArgs e) => thrown.Add(e.Exception);
        Dispatcher.UIThread.UnhandledException += Collect;
        try
        {
            TestLogSink.Instance.Clear();
            using InlineHost host = new();
            host.Show();
            (string left, string right) = FoldingFixture.Pair();
            await host.LoadAsync(new PaneSource(left), new PaneSource(right));

            DiffPanePresenter pane = host.Pane;
            host.View.UnchangedContextRows = 0;
            InlineHost.Layout();
            int folds = pane.CollapsedSectionCount;
            Assert.True(folds > 1);

            // Open the first run from its placeholder's line, which is where the verb looks.
            InlineDocument inline = host.View.Inline ?? throw new InvalidOperationException("No unified table.");
            FoldedRun opened = FoldPlan.For(inline, 0)[0];
            (int First, int Last) lines = FoldPlan.LinesOf(inline, opened)
                                          ?? throw new InvalidOperationException("A taken fold hides nothing.");
            pane.TextArea.Caret.Line = lines.First - 1;
            Assert.True(host.View.CommandFor(DiffCommand.ExpandFold).CanExecute(null));
            host.View.CommandFor(DiffCommand.ExpandFold).Execute(null);
            InlineHost.Layout();
            Assert.Equal(folds - 1, pane.CollapsedSectionCount);

            int blocks = host.View.ChangeCount;
            if (rebuild == "sources")
            {
                // PaneSource is a record, so the same text again would rebuild nothing at all.
                await host.LoadAsync(new PaneSource(left + "\nand one more"), new PaneSource(right + "\nand one more"));
            }
            else
            {
                // The fixture's one modification differs only in case, so ignoring case is a
                // different model over the same two documents: a block fewer, and a line fewer.
                host.View.IgnoreCase = true;
                await host.WaitForBuildAsync();
                Assert.Equal(blocks - 1, host.View.ChangeCount);
            }

            InlineHost.Layout();
            Assert.True(thrown.Count == 0, "the dispatcher threw after the rebuild: " + string.Join("; ", thrown.Select(e => e.Message)));

            // The option stays, and the runs are this model's, the one the reader opened among
            // them: its first row names a run here too, so carrying it over would keep that open.
            InlineDocument rebuilt = host.View.Inline ?? throw new InvalidOperationException("No unified table.");
            IReadOnlyList<FoldedRun> planned = FoldPlan.For(rebuilt, 0);
            Assert.Equal(0, host.View.UnchangedContextRows);
            Assert.Equal(opened.FirstRow, planned[0].FirstRow);
            Assert.Equal(planned.Count, pane.CollapsedSectionCount);

            // What the pane was told is not what it did: the height tree says which lines take no
            // height. Each run's lines are hidden — the line after a run starts where its first
            // hidden line would — and the extent is the lines left standing.
            TextView view = pane.TextArea.TextView;
            int hidden = 0;
            foreach (FoldedRun run in planned)
            {
                (int first, int last) = FoldPlan.LinesOf(rebuilt, run)
                                        ?? throw new InvalidOperationException("A planned fold hides nothing.");
                hidden += last - first + 1;
                Assert.Equal(view.GetVisualTopByDocumentLine(first), view.GetVisualTopByDocumentLine(last + 1), Tolerance);
            }

            Assert.Equal((pane.Document.LineCount - hidden) * view.DefaultLineHeight, pane.ExtentHeight, Tolerance);

            // The border goes through the same folds. The first block sits under the run the
            // reader had opened, where the old folds would still put it a run's height lower.
            host.View.CurrentChangeIndex = 0;
            host.Capture().Dispose();
            double expected = view.GetVisualTopByDocumentLine(rebuilt.LinesOfBlock(0).Start + 1) - view.ScrollOffset.Y;
            Rect border = pane.BackgroundRenderer.LastCurrentBlockBorder
                          ?? throw new InvalidOperationException($"No border drawn, where one belongs at {expected}.");
            Assert.Equal(expected, border.Top, Tolerance);
            Assert.True(thrown.Count == 0, "the dispatcher threw drawing the rebuilt pane: " + string.Join("; ", thrown.Select(e => e.Message)));
            TestLogSink.AssertNoWarnings();
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Collect;
        }
    }

    [AvaloniaFact]
    public async Task Every_surface_that_names_a_run_offers_the_folding_group_and_the_map_does_not()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = FoldingFixture.Pair();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        // The group's exact shape, so the rendered frames are not the only thing that knows it:
        // a separator, then the three modes in order, then the entry that opens one run.
        string?[] group =
        [
            null,
            DiffViewStrings.Get(DiffViewStrings.MenuShowAllRows),
            DiffViewStrings.Get(DiffViewStrings.MenuShowDifferencesOnly),
            DiffViewStrings.Get(DiffViewStrings.MenuShowContext),
            DiffViewStrings.Get(DiffViewStrings.MenuExpandFold),
        ];

        DiffPaneRegion[] carry =
        [
            DiffPaneRegion.Text,
            DiffPaneRegion.LineNumberMargin,
            DiffPaneRegion.ChangeMarkerMargin,
            DiffPaneRegion.ConnectorGutter,
        ];

        foreach (DiffPaneRegion region in carry)
        {
            List<DiffMenuItem> items = ItemsFor(host, region);
            string?[] tail = [.. items.TakeLast(group.Length).Select(item => item.IsSeparator ? null : item.Header)];
            Assert.Equal(group, tail);
        }

        // The map's menu is a navigation surface's, kept short on purpose.
        Assert.DoesNotContain(
            ItemsFor(host, DiffPaneRegion.OverviewMap),
            item => item.Header == DiffViewStrings.Get(DiffViewStrings.MenuShowAllRows));
    }

    /// <summary>
    /// The items that surface's menu carries. Built from a context rather than driven by a
    /// right-click: what is asserted here is the list each surface offers, and five sets of
    /// pointer coordinates would be five more things to get wrong.
    /// </summary>
    private static List<DiffMenuItem> ItemsFor(CompositeHost host, DiffPaneRegion region)
    {
        SideBySideDocument document = host.View.Document ?? throw new InvalidOperationException("No model.");
        ChangeBlock block = document.Blocks[0];
        DiffPaneContext context = new(
            region,
            region == DiffPaneRegion.ConnectorGutter ? null : DiffSide.Left,
            LineNumber: 1,
            SourceSide: DiffSide.Left,
            SourceLine: 1,
            Row: block.FirstRow,
            Block: block,
            Kind: DiffLineKind.Unchanged,
            SelectedLines: null,
            IsUnified: false,
            IsReadOnly: false);

        return host.View.MenuItemsFor(context);
    }
}
