using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Inline;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

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
