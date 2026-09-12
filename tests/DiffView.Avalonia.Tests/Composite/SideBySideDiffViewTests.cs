using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 5: the composite control's state machine, sources and options, the
/// latest-wins worker, the banners, the logging rule, the string resolver, and scroll sync.
/// </summary>
public sealed class SideBySideDiffViewTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaTheory]
    [MemberData(nameof(ThemeTargets.All), MemberType = typeof(ThemeTargets))]
    public async Task Renders_under_every_theme_target_with_no_binding_or_resource_warnings(string theme, string variant)
    {
        using ThemeSwap swap = ThemeSwap.To(theme, variant);
        TestLogSink.Instance.Clear();
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        using WriteableBitmap frame = host.Capture();

        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.NotNull(host.View.LeftHeader);
        Assert.NotNull(host.View.StatusStrip);

        // The header painted its own token, so the template applied and the token resolved.
        Color headerBackground = PresenterHost.Token("DiffView.HeaderBackgroundBrush");
        Point origin = host.View.LeftHeader!.TranslatePoint(new Point(0, 0), host.Window) ?? throw new InvalidOperationException("header not in tree");
        Color sampled = PixelProbe.At(frame, (int)(origin.X + host.View.LeftHeader.Bounds.Width - 3), (int)origin.Y + 2);
        Assert.True(PresenterHost.Near(sampled, headerBackground), $"{theme} {variant}: expected header {headerBackground}, got {sampled}");

        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    [AvaloniaFact]
    public async Task Sources_build_the_document_and_the_state_moves_from_Empty_through_Building_to_Ready()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        List<DiffViewState> states = [];
        host.View.PropertyChanged += (_, e) =>
        {
            if (e.Property == SideBySideDiffView.StateProperty)
            {
                states.Add(host.View.State);
            }
        };
        int completed = 0;
        host.View.BuildCompleted += (_, _) => completed++;

        Assert.Equal(DiffViewState.Empty, host.View.State);
        await host.LoadAsync(left, right);

        Assert.Equal([DiffViewState.Building, DiffViewState.Ready], states.Distinct());
        Assert.Equal(DiffViewState.Ready, states[^1]);
        Assert.Equal(1, completed);
        Assert.NotNull(host.View.Document);
        Assert.Same(host.View.Document, host.Left.DiffDocument);
        Assert.Same(host.View.Document, host.Right.DiffDocument);
        Assert.Same(host.View.LeftDocument, host.Left.Document);
        Assert.Equal(left, host.View.LeftDocument.Text);
        Assert.Equal(host.View.Document!.Blocks.Count, host.View.ChangeCount);
        Assert.Equal(DiffBannerKind.None, host.View.BannerKind);
        Assert.False(host.View.IsStale);

        DiffStatusStrip strip = host.View.StatusStrip!;
        Assert.Equal("Ready", strip.StateText);
        Assert.Equal("+3 −5 ~2", strip.CountsText);
        Assert.Equal("5 changes", strip.ChangesText);
        Assert.Equal(StatusKind.Success, strip.TransientKind);
        Assert.StartsWith("Compared ", strip.TransientText, StringComparison.Ordinal);

        Assert.Equal("Left", host.View.LeftHeader!.Title);
        Assert.Contains("30 lines", host.View.LeftHeader.Detail, StringComparison.Ordinal);
        Assert.Contains("LF", host.View.LeftHeader.Detail, StringComparison.Ordinal);
        Assert.Null(host.View.LeftHeader.Badge);
    }

    [AvaloniaFact]
    public async Task A_missing_source_leaves_the_control_Empty_with_the_header_saying_so()
    {
        (string left, _) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        host.View.LeftSource = new PaneSource(left) { Title = "before.cs" };
        await host.WaitForBuildAsync();

        Assert.Equal(DiffViewState.Empty, host.View.State);
        Assert.Equal("Load both sides to compare", host.View.StateMessage);
        Assert.Null(host.View.Document);
        Assert.Equal("before.cs", host.View.LeftHeader!.Title);
        Assert.Equal("Right", host.View.RightHeader!.Title);
        Assert.Equal("No content", host.View.RightHeader.Detail);
        Assert.Equal("Empty", host.View.StatusStrip!.StateText);
        Assert.Null(host.View.StatusStrip.CountsText);
    }

    [AvaloniaFact]
    public async Task A_change_during_a_build_supersedes_it_and_the_final_state_reflects_the_last_input()
    {
        using CompositeHost host = new();
        host.Show();
        using ManualResetEventSlim gate = new(false);
        host.View.Builder = (left, right, options, token) =>
        {
            if (right.Text.StartsWith("slow", StringComparison.Ordinal))
            {
                gate.Wait(TimeSpan.FromSeconds(10));
            }

            return DiffDocumentBuilder.Build(left, right, options, token);
        };
        int completed = 0;
        host.View.BuildCompleted += (_, _) => completed++;

        host.View.LeftSource = "alpha\nbeta\ngamma";
        host.View.RightSource = "slow\nbeta\ngamma";
        Task first = host.View.CurrentBuild ?? throw new InvalidOperationException("no build started");
        Assert.Equal(DiffViewState.Building, host.View.State);

        // The second input supersedes the first while it is still running.
        host.View.RightSource = "alpha\nbeta\ngamma\ndelta";
        await host.WaitForBuildAsync();
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.Equal(4, host.View.Document!.Right.Lines.Count);
        Assert.Equal(1, completed);

        // The first build lands late and is discarded: the document is still the second's.
        SideBySideDocument second = host.View.Document;
        gate.Set();
        await first;
        CompositeHost.Layout();
        Assert.Same(second, host.View.Document);
        Assert.Equal(1, completed);
        Assert.Contains(host.Logs.Records, r => r.Level == LogLevel.Debug && r.Message.Contains("superseded", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Changing_an_option_rebuilds_and_preserves_caret_selection_scroll_and_undo_in_both_panes()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(height: 320);
        host.Show();
        await host.LoadAsync(left, right);
        SideBySideDocument before = host.View.Document!;
        TextDocument leftDocument = host.View.LeftDocument;
        TextDocument rightDocument = host.View.RightDocument;

        // An edit on the (writable) left side gives the undo stack something to keep.
        host.View.LeftReadOnly = false;
        Assert.False(host.Left.IsReadOnly);
        leftDocument.Insert(0, "x");
        Assert.True(leftDocument.UndoStack.CanUndo);

        TextArea leftArea = host.Left.TextArea;
        leftArea.Focus();
        leftArea.Caret.Offset = 40;
        leftArea.Selection = Selection.Create(leftArea, 20, 40);
        host.Right.TextArea.Caret.Offset = 12;
        double maxOffset = host.Left.ExtentHeight - host.Left.ViewportHeight;
        Assert.True(maxOffset > 0, "the fixture should be taller than the viewport");
        host.Left.PaneScrollViewer!.Offset = new Vector(0, maxOffset / 2);
        CompositeHost.Layout();
        double scroll = host.Left.VerticalOffset;
        Assert.True(scroll > 0);

        host.View.IgnoreWhitespace = true;
        Assert.Equal(DiffViewState.Building, host.View.State);
        Assert.True(host.View.IsStale);
        await host.WaitForBuildAsync();

        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.False(host.View.IsStale);
        Assert.NotSame(before, host.View.Document);
        Assert.Same(leftDocument, host.View.LeftDocument);
        Assert.Same(rightDocument, host.View.RightDocument);
        Assert.Same(leftDocument, host.Left.Document);
        Assert.Same(rightDocument, host.Right.Document);
        Assert.Equal(40, leftArea.Caret.Offset);
        Assert.Equal(20, leftArea.Selection.SurroundingSegment!.Offset);
        Assert.Equal(20, leftArea.Selection.Length);
        Assert.Equal(12, host.Right.TextArea.Caret.Offset);
        Assert.Equal(scroll, host.Left.VerticalOffset, Tolerance);
        Assert.True(leftDocument.UndoStack.CanUndo);
        Assert.Contains("ignore whitespace", host.View.StatusStrip!.OptionsText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_throwing_builder_puts_the_control_in_Failed_with_the_message_and_Retry_rebuilds()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        bool fail = true;
        host.View.Builder = (l, r, o, token) => fail ? throw new InvalidOperationException("kaboom") : DiffDocumentBuilder.Build(l, r, o, token);
        List<DiffBuildFailedEventArgs> failures = [];
        host.View.BuildFailed += (_, e) => failures.Add(e);

        await host.LoadAsync(left, right);
        Assert.Equal(DiffViewState.Failed, host.View.State);
        Assert.Equal("kaboom", host.View.StateMessage);
        Assert.Equal(DiffBannerKind.Error, host.View.BannerKind);
        Assert.Equal("kaboom", host.View.BannerMessage);
        Assert.Equal("Retry", host.View.BannerActionText);
        Assert.Equal("Retry", host.View.BannerAction!.Content);
        Assert.True(host.View.BannerAction.IsVisible);
        Assert.Equal(StatusKind.Failure, host.View.Status.Kind);
        Assert.Equal("Failed", host.View.StatusStrip!.StateText);
        DiffBuildFailedEventArgs failure = Assert.Single(failures);
        Assert.Equal(DiffBuildErrorCode.DiffFailed, failure.Exception.Code);
        Assert.IsType<InvalidOperationException>(failure.Exception.InnerException);
        Assert.True(host.View.RetryCommand.CanExecute(null));

        fail = false;
        host.View.RetryCommand.Execute(null);
        await host.WaitForBuildAsync();
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.Equal(DiffBannerKind.None, host.View.BannerKind);
        Assert.NotNull(host.View.Document);
        Assert.False(host.View.RetryCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Binary_input_fails_the_unrelated_pair_degrades_until_forced_and_identical_input_is_Ready_with_the_banner()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();

        // Binary: Failed with BinaryInput, and the header says so.
        host.View.LeftSource = new PaneSource("\0\0junk") { IsBinary = true, Title = "blob.bin" };
        host.View.RightSource = right;
        await host.WaitForBuildAsync();
        Assert.Equal(DiffViewState.Failed, host.View.State);
        Assert.Contains("binary", host.View.StateMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("binary", host.View.LeftHeader!.Badge);
        Assert.Equal(StatusKind.Failure, host.View.LeftHeader.BadgeKind);
        Assert.Equal(DiffBannerKind.Error, host.View.BannerKind);

        // Unrelated and large: Degraded with the too-different banner; Force aligns it.
        string unrelatedLeft = string.Join("\n", Enumerable.Range(0, 6000).Select(i => $"L{i} {(i * 7919) % 1000}"));
        string unrelatedRight = string.Join("\n", Enumerable.Range(0, 6000).Select(i => $"R{i} {(i * 104729) % 1000}"));
        host.View.LeftSource = unrelatedLeft;
        host.View.RightSource = unrelatedRight;
        await host.WaitForBuildAsync();
        Assert.Equal(DiffViewState.Degraded, host.View.State);
        Assert.False(host.View.Diagnostics!.Aligned);
        Assert.Contains(host.View.Warnings, w => w.Code == DiffWarningCode.TooDifferentToAlign);
        Assert.Equal(DiffBannerKind.TooDifferentToAlign, host.View.BannerKind);
        Assert.Equal("Force alignment", host.View.BannerActionText);
        Assert.Equal(StatusKind.Warning, host.View.Status.Kind);
        Assert.True(host.View.ForceAlignmentCommand.CanExecute(null));

        host.View.ForceAlignmentCommand.Execute(null);
        await host.WaitForBuildAsync();
        Assert.True(host.View.ForceAlignment);
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.True(host.View.Diagnostics!.Aligned);
        Assert.Equal(DiffBannerKind.None, host.View.BannerKind);
        Assert.Contains("forced alignment", host.View.StatusStrip!.OptionsText, StringComparison.Ordinal);

        // Identical: Ready, the banner says so, both headers carry the badge.
        host.View.ForceAlignment = false;
        host.View.LeftSource = left;
        host.View.RightSource = left;
        await host.WaitForBuildAsync();
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.True(host.View.Diagnostics!.Identical);
        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.Equal("Files are identical", host.View.BannerMessage);
        Assert.Null(host.View.BannerActionText);
        Assert.Equal("identical", host.View.LeftHeader.Badge);
        Assert.Equal("identical", host.View.RightHeader!.Badge);
        Assert.Equal(StatusKind.Success, host.View.LeftHeader.BadgeKind);
        Assert.Equal("Files are identical", host.View.Status.Text);
        Assert.Equal("no changes", host.View.StatusStrip.ChangesText);
    }

    [AvaloniaFact]
    public async Task The_log_never_carries_document_text_and_each_state_transition_appears_exactly_once()
    {
        const string sentinel = "SENTINEL_7f3a9c";
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left.Replace("Greeter", sentinel, StringComparison.Ordinal), right.Replace("Greeter", sentinel + "2", StringComparison.Ordinal));
        using (WriteableBitmap _ = host.Capture())
        {
        }

        // A find over the sentinel: the query is a piece of the document — Ctrl+F pre-fills it
        // from the selection — so nothing of it but its length may reach the log.
        host.View.OpenFind();
        await host.FindAsync(sentinel);
        Assert.Equal(4, host.View.FindResult!.Matches.Count);
        host.View.FindNext();
        CompositeHost.Layout();
        host.View.CloseFind();

        // A forced render fault, logged under the Render category with its exception.
        List<RenderFaultEventArgs> faults = [];
        host.View.RenderFault += (_, e) => faults.Add(e);
        host.Left.TextArea.TextView.BackgroundRenderers.Add(new ThrowingRenderer(host.Left));
        host.Left.TextArea.TextView.Redraw();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        // The fault is raised after the render pass that caught it.
        CompositeHost.Layout();
        Assert.Single(faults);
        Assert.Equal(DiffViewState.Degraded, host.View.State);
        Assert.Equal(StatusKind.Failure, host.View.Status.Kind);

        IReadOnlyList<LogRecord> records = host.Logs.Records;
        Assert.NotEmpty(records);
        Assert.DoesNotContain(records, r => r.Everything.Contains(sentinel, StringComparison.Ordinal));
        Assert.DoesNotContain(records, r => r.Everything.Contains("Sample", StringComparison.Ordinal));

        IReadOnlyList<LogRecord> transitions = records.Where(r => r.Level == LogLevel.Information && r.Message.StartsWith("State ", StringComparison.Ordinal)).ToList();
        Assert.Equal(["State Empty → Building", "State Building → Ready", "State Ready → Degraded"], transitions.Select(r => r.Message.Split(':')[0]));
        Assert.All(transitions, r => Assert.Equal(DiffViewLogCategories.Build, r.Category));
        LogRecord fault = Assert.Single(records, r => r.Level == LogLevel.Error && r.Category == DiffViewLogCategories.Render);
        Assert.NotNull(fault.Exception);
        Assert.Contains("ThrowingRenderer", fault.Message, StringComparison.Ordinal);
        LogRecord find = Assert.Single(records, r => r.Category == DiffViewLogCategories.Find && r.Level == LogLevel.Information);
        Assert.Contains("4 match(es)", find.Message, StringComparison.Ordinal);
        Assert.Contains(records, r => r.Level == LogLevel.Information && r.Message.Contains("completed in", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Swapping_the_string_resolver_before_load_changes_the_rendered_strings()
    {
        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Resolver = (key, _) => key switch
            {
                DiffViewStrings.StateReady => "Bereit",
                DiffViewStrings.LeftTitle => "Links",
                DiffViewStrings.StatusChanges => "{0} Änderungen",
                _ => null,
            },
        }))
        {
            (string left, string right) = CompositeHost.SmallFixture();
            using CompositeHost host = new();
            host.Show();
            await host.LoadAsync(left, right);
            Assert.Equal("Bereit", host.View.StatusStrip!.StateText);
            Assert.Equal("Links", host.View.LeftHeader!.Title);
            Assert.Equal("5 Änderungen", host.View.StatusStrip.ChangesText);
        }
    }

    [AvaloniaFact]
    public async Task Scroll_sync_is_one_to_one_at_top_middle_and_bottom_with_no_feedback_loop()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.NotNull(host.View.Sync);
        ScrollViewer leftViewer = host.Left.PaneScrollViewer!;
        ScrollViewer rightViewer = host.Right.PaneScrollViewer!;
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        double maxOffset = host.Left.ExtentHeight - host.Left.ViewportHeight;
        Assert.True(maxOffset > 0, "the fixture should be taller than the viewport");

        int events = 0;
        leftViewer.ScrollChanged += (_, _) => events++;
        rightViewer.ScrollChanged += (_, _) => events++;

        foreach (double offset in new[] { maxOffset / 2, maxOffset, 0 })
        {
            events = 0;
            leftViewer.Offset = new Vector(0, offset);
            CompositeHost.Layout();
            Assert.Equal(offset, host.Left.VerticalOffset, Tolerance);
            Assert.Equal(offset, host.Right.VerticalOffset, Tolerance);
            AssertQuiescent(ref events);
            AssertSameFirstRow(host);

            events = 0;
            rightViewer.Offset = new Vector(0, maxOffset - offset);
            CompositeHost.Layout();
            Assert.Equal(maxOffset - offset, host.Left.VerticalOffset, Tolerance);
            Assert.Equal(maxOffset - offset, host.Right.VerticalOffset, Tolerance);
            AssertQuiescent(ref events);
            AssertSameFirstRow(host);
        }

        // A bounded number of events per change, and none once the panes agree: no feedback loop.
        static void AssertQuiescent(ref int events)
        {
            Assert.True(events <= 4, $"{events} scroll events for one offset change: a feedback loop");
            int settled = events;
            CompositeHost.Layout();
            CompositeHost.Layout();
            Assert.Equal(settled, events);
        }
    }

    [AvaloniaFact]
    public async Task Scrolling_the_right_pane_with_headless_pointer_input_keeps_both_panes_on_the_same_first_row()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.Equal(0, host.Right.VerticalOffset, Tolerance);

        // Drag the right pane's scrollbar thumb when the theme exposes one; otherwise the wheel.
        ScrollBar? bar = host.Right.PaneScrollViewer!.GetVisualDescendants().OfType<ScrollBar>().FirstOrDefault(b => b.Orientation == Orientation.Vertical);
        Thumb? thumb = bar?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault(t => t.Bounds.Height > 0);
        if (thumb is not null)
        {
            Point start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), host.Window)!.Value;
            host.Window.MouseDown(start, MouseButton.Left);
            host.Window.MouseMove(start + new Point(0, 40));
            host.Window.MouseMove(start + new Point(0, 80));
            host.Window.MouseUp(start + new Point(0, 80), MouseButton.Left);
            CompositeHost.Layout();
        }

        if (host.Right.VerticalOffset <= 0)
        {
            Point inside = host.Right.TextArea.TextView.TranslatePoint(new Point(40, 40), host.Window)!.Value;
            host.Window.MouseWheel(inside, new Vector(0, -3));
            CompositeHost.Layout();
        }

        Assert.True(host.Right.VerticalOffset > 0, "pointer input should have scrolled the right pane");
        Assert.Equal(host.Right.VerticalOffset, host.Left.VerticalOffset, Tolerance);
        AssertSameFirstRow(host);
    }

    [AvaloniaFact]
    public async Task Horizontal_sync_is_optional_and_both_panes_show_the_same_horizontal_bar()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 600, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.True(host.Left.ExtentWidth > host.Left.ViewportWidth, "the fixture's longest line should overflow a 300 px pane");
        Assert.Equal(ScrollBarVisibility.Visible, host.Left.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Visible, host.Right.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Hidden, host.Left.VerticalScrollBarVisibility);

        host.Left.PaneScrollViewer!.Offset = new Vector(30, 0);
        CompositeHost.Layout();
        Assert.Equal(30, host.Left.HorizontalOffset, Tolerance);
        Assert.Equal(0, host.Right.HorizontalOffset, Tolerance);

        host.View.SyncHorizontalScroll = true;
        CompositeHost.Layout();
        Assert.Equal(30, host.Right.HorizontalOffset, Tolerance);
        host.Right.PaneScrollViewer!.Offset = new Vector(50, 0);
        CompositeHost.Layout();
        Assert.Equal(50, host.Left.HorizontalOffset, Tolerance);
    }

    [AvaloniaFact]
    public async Task A_slow_build_shows_progress_after_the_threshold_and_the_previous_result_is_marked_stale()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        SideBySideDocument previous = host.View.Document!;

        using ManualResetEventSlim gate = new(false);
        host.View.Builder = (l, r, o, token) =>
        {
            gate.Wait(TimeSpan.FromSeconds(10));
            return DiffDocumentBuilder.Build(l, r, o, token);
        };
        host.View.IgnoreCase = true;
        Assert.Equal(DiffViewState.Building, host.View.State);
        Assert.True(host.View.IsStale);
        Assert.Same(previous, host.View.Document);
        Assert.Same(previous, host.Left.DiffDocument);
        Assert.False(host.View.IsBuildingSlowly);
        Assert.Equal("Building…", host.View.StatusStrip!.StateText);
        Assert.True(host.View.StatusStrip.IsStale);

        host.Time.Advance(SideBySideDiffView.SlowBuildThreshold + TimeSpan.FromMilliseconds(10));
        CompositeHost.Layout();
        Assert.True(host.View.IsBuildingSlowly);
        Assert.True(host.View.StatusStrip.IsBuildingSlowly);

        gate.Set();
        await host.WaitForBuildAsync();
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.False(host.View.IsBuildingSlowly);
        Assert.False(host.View.IsStale);
        Assert.NotSame(previous, host.View.Document);
    }

    [AvaloniaFact]
    public async Task The_status_strip_shows_the_focused_panes_caret_and_a_failure_can_be_dismissed()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        Assert.Null(host.View.StatusStrip!.CaretText);

        // Line 4 of the right fixture is "namespace Sample"; line 3 is blank.
        host.Right.TextArea.Focus();
        host.Right.TextArea.Caret.Offset = host.Right.Document.GetLineByNumber(4).Offset + 4;
        CompositeHost.Layout();
        Assert.Equal(DiffSide.Right, host.View.FocusedSide);
        Assert.Equal(4, host.View.CaretLine);
        Assert.Equal(5, host.View.CaretColumn);
        Assert.Equal("Ln 4, Col 5", host.View.StatusStrip.CaretText);

        host.View.Status.SetFailure("something failed");
        Assert.True(host.View.StatusStrip.IsTransientDismissible);
        Assert.Equal("something failed", host.View.StatusStrip.TransientText);
        host.View.StatusStrip.DismissCommand.Execute(null);
        Assert.Null(host.View.Status.Text);
        Assert.Null(host.View.StatusStrip.TransientText);

        // A success clears on the clock.
        host.View.Status.SetSuccess("ok");
        host.Time.Advance(StatusController.DefaultSuccessAutoClearDelay + TimeSpan.FromMilliseconds(1));
        CompositeHost.Layout();
        Assert.Null(host.View.StatusStrip.TransientText);
    }

    /// <summary>
    /// Both panes show the same row at the top: the offsets are equal, and every pair of lines
    /// that share a row sits at the same document-relative top on both sides, so the row under
    /// the viewport's top edge is the same one. (The first visual line's top is not comparable:
    /// a padding block straddling the edge starts above it on one side only.)
    /// </summary>
    private static void AssertSameFirstRow(CompositeHost host)
    {
        Assert.Equal(host.Left.VerticalOffset, host.Right.VerticalOffset, Tolerance);
        SideBySideDocument document = host.View.Document!;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        foreach (AlignedRow row in document.Rows)
        {
            if (row.LeftLine is { } left && row.RightLine is { } right)
            {
                double leftTop = host.Left.TextArea.TextView.GetVisualTopByDocumentLine(left + 1) + Padding.Before(document, DiffSide.Left, left) * lineHeight;
                double rightTop = host.Right.TextArea.TextView.GetVisualTopByDocumentLine(right + 1) + Padding.Before(document, DiffSide.Right, right) * lineHeight;
                Assert.Equal(leftTop, rightTop, Tolerance);
            }
        }
    }

    internal sealed class ThrowingRenderer(DiffPanePresenter owner) : GuardedBackgroundRenderer(owner, KnownLayer.Background, "ThrowingRenderer")
    {
        protected override void DrawCore(TextView textView, DrawingContext drawingContext)
        {
            throw new InvalidOperationException("deliberate renderer fault");
        }
    }
}
