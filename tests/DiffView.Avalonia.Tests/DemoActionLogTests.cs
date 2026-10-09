using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Demo;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using LogArea = Avalonia.Logging.LogArea;
using MelLogger = Microsoft.Extensions.Logging.ILogger;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00029 phase 2: the demo writes what its user did to its own log, at the <c>Information</c> it
/// keeps — the machine, the window, the menu, the commands, the copies and the edits — and raises the
/// library's interaction lines to that level, so that a bug found by hand reads back from the log.
/// </summary>
/// <remarks>
/// The real demo window, driven headlessly, writing to the demo's real log: <see cref="Log.Logger"/>,
/// which <see cref="ActionLog"/> writes to, is swapped for a logger of the test's own at the demo's
/// level, and <see cref="DemoLogging.Factory"/> is built over it. The rests the log waits for run on
/// the side-by-side view's clock, which is advanced by hand, never waited out.
/// </remarks>
public sealed class DemoActionLogTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    [Fact]
    public void The_machine_is_named_by_its_host_name_beside_its_system_and_runtime()
    {
        using DemoLog log = new();

        ActionLog.LogMachine();

        Assert.Equal(
            [$"Host: {Environment.MachineName} · {RuntimeInformation.OSDescription} · {RuntimeInformation.FrameworkDescription}"],
            log.Lines());
    }

    [Fact]
    public void Only_the_interaction_category_is_raised_to_the_level_the_demo_keeps()
    {
        using DemoLog log = new();
        string[] categories =
        [
            .. typeof(DiffViewLogCategories).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (string)f.GetRawConstantValue()!),
        ];
        Assert.Contains(DiffViewLogCategories.Interaction, categories);
        Assert.True(categories.Length > 1, "there is no other category to hold at the level it had");

        foreach (string category in categories)
        {
            MelLogger logger = DemoLogging.Factory.CreateLogger(category);
            Assert.True(logger.IsEnabled(MelLogLevel.Information), $"{category} is not written at the demo's level");
            Assert.Equal(category == DiffViewLogCategories.Interaction, logger.IsEnabled(MelLogLevel.Debug));
        }
    }

    [AvaloniaFact]
    public async Task The_window_is_written_when_it_opens_and_again_once_a_resize_has_rested()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        FakeTimeProvider time = new();
        MainWindow window = await OpenAsync(time);
        try
        {
            // Written as it opens, before any rest; and opening lays the window out, which moves its
            // size without being a resize, so the rest after it writes nothing more.
            Assert.Equal(["Window: 800×500 at scaling 1"], log.Lines("Window:"));
            await RestAsync(window, time, ActionLog.Quiet);
            Assert.Equal(["Window: 800×500 at scaling 1"], log.Lines("Window:"));

            // Two steps of one drag, and nothing until the size has been still for the whole rest.
            window.Width = 900;
            Layout();
            window.Width = 1000;
            Layout();
            await RestAsync(window, time, ActionLog.Quiet - Tick);
            Assert.Single(log.Lines("Window:"));

            await RestAsync(window, time, Tick);
            Assert.Equal(["Window: 800×500 at scaling 1", "Window: 1000×500 at scaling 1"], log.Lines("Window:"));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_menu_choice_is_written_as_its_path_and_the_state_it_left()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        MainWindow window = await OpenAsync(new FakeTimeProvider());
        try
        {
            Choose(window, window.ShowWhitespace);
            Assert.True(window.Diff.ShowWhitespace, "the toggle did not reach the view, so its state proves nothing");
            Choose(window, window.NextChange);
            Choose(window, window.ShowWhitespace);

            Assert.Equal(
                [
                    "Menu: View ▸ Show whitespace → on",
                    "Menu: View ▸ Key bindings ▸ Next change",
                    "Menu: View ▸ Show whitespace → off",
                ],
                log.Lines("Menu:"));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_chord_is_written_as_the_command_it_ran_and_typing_is_never_written()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        MainWindow window = await OpenAsync(new FakeTimeProvider());
        try
        {
            window.Diff.LeftReadOnly = false;
            window.Diff.RightReadOnly = false;
            DiffPanePresenter left = Left(window);

            // Pressed where no view holds the keyboard, a chord runs nothing, so nothing is written.
            ((MenuItem)window.ShowWhitespace.Parent!).IsSubMenuOpen = true;
            Layout();
            Assert.True(window.ShowWhitespace.Focus(), "the menu item took no focus, so the chord would reach a view");
            Assert.Equal(-1, window.Diff.CurrentChangeIndex);
            Press(window, Key.F7, PhysicalKey.F7);
            Assert.Equal(-1, window.Diff.CurrentChangeIndex);
            window.MainMenu.Close();
            Layout();

            left.TextArea.Focus();
            Layout();
            Press(window, Key.F7, PhysicalKey.F7);
            Assert.Equal(0, window.Diff.CurrentChangeIndex);

            // Found by the pass by hand: Escape with no find bar open closes nothing, so it is not
            // written — it was, as CloseFind, while it dismissed a context menu. With the bar open it runs.
            Assert.False(window.Diff.IsFindBarOpen);
            Press(window, Key.Escape, PhysicalKey.Escape);
            Press(window, Key.F, PhysicalKey.F, RawInputModifiers.Control);
            Assert.True(window.Diff.IsFindBarOpen, "Ctrl+F opened no find bar, so the Escape after it proves nothing");
            Press(window, Key.Escape, PhysicalKey.Escape);
            Assert.False(window.Diff.IsFindBarOpen);
            left.TextArea.Focus();
            Layout();

            // Typing reaches the document and nothing at all reaches the log.
            int before = log.Lines().Count;
            left.TextArea.Caret.Offset = 0;
            Press(window, Key.Z, PhysicalKey.Z);
            window.KeyTextInput("z");
            Layout();
            Assert.StartsWith("z", left.Document.Text, StringComparison.Ordinal);
            Assert.Equal(before, log.Lines().Count);

            // A copy that acts on the selection says which lines it acted on.
            SelectLines(left, 9, 11);
            Press(window, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Alt);

            // The unified view's keys are its own map's.
            Choose(window, window.UnifiedView);
            DiffPanePresenter unified = window.Unified.Pane ?? throw new InvalidOperationException("The template has not applied.");
            unified.TextArea.Focus();
            Layout();
            Press(window, Key.F7, PhysicalKey.F7);
            Assert.Equal(0, window.Unified.CurrentChangeIndex);

            Assert.Equal(
                [
                    "Command NextChange from the keyboard",
                    "Command OpenFind from the keyboard",
                    "Command CloseFind from the keyboard",
                    "Command CopyToRight from the keyboard on left lines 9–11",
                    "Command NextChange from the keyboard",
                ],
                log.Lines("Command"));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_navigation_says_where_it_landed_even_when_it_moved_nothing()
    {
        // Plan 00033 phase 3. The pass can prove a navigation command FIRED from the log; what it
        // could not prove is that the view went anywhere, and nothing exposes the current change —
        // no Toggle pattern, no Value pattern, and the status strip's text is unreachable. So the
        // demo says where it landed, which is the host reading its own public API rather than a peer
        // advertising anything.
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        MainWindow window = await OpenAsync(new FakeTimeProvider());
        try
        {
            DiffPanePresenter left = Left(window);
            left.TextArea.Focus();
            Layout();

            int count = window.Diff.ChangeCount;
            Assert.True(count >= 2, $"this pair has {count} changes, so walking two of them proves nothing");
            Assert.Equal(-1, window.Diff.CurrentChangeIndex);

            Press(window, Key.F7, PhysicalKey.F7);
            Press(window, Key.F7, PhysicalKey.F7);

            // ⛔ The number is the one the status strip shows. CurrentChangeIndex is 0-based and the
            // strip counts from one, so a reader comparing the log with the window would otherwise
            // find them one apart — which is the kind of discrepancy that gets read as a bug.
            Assert.Equal(1, window.Diff.CurrentChangeIndex);
            Assert.Equal(
                [$"Now at change 1 of {count}", $"Now at change 2 of {count}"],
                log.Lines("Now at"));

            // And the case the whole line exists for. Next at the last change moves nothing, and a
            // reading that only spoke when the index CHANGED would say nothing here — indistinguishable
            // from a command that never ran, which is the confusion plan 00033 is about.
            window.Diff.CurrentChangeIndex = count - 1;
            Layout();
            int before = log.Lines("Now at").Count;

            Press(window, Key.F7, PhysicalKey.F7);

            Assert.Equal(count - 1, window.Diff.CurrentChangeIndex);
            Assert.Equal(
                [$"Now at change {count} of {count}"],
                log.Lines("Now at").Skip(before).ToArray());
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task A_context_menu_choice_is_written_as_its_verb_and_the_demo_s_own_entry_by_its_header()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        MainWindow window = await OpenAsync(new FakeTimeProvider());
        try
        {
            window.Diff.RightReadOnly = false;
            DiffPanePresenter left = Left(window);
            left.TextArea.Focus();
            SelectLines(left, 9, 11);

            // Each choice from a menu of its own, as a pointer would make it.
            Run(PaneMenu(window, left), DiffCommand.CopyToRight);
            Run(PaneMenu(window, left), DiffCommand.NextChange);
            Run(PaneMenu(window, left), "What did I click?");
            Run(HeaderMenu(window), "What did I click?");

            // The unified view's pane menu is watched too.
            Choose(window, window.UnifiedView);
            DiffPanePresenter unified = window.Unified.Pane ?? throw new InvalidOperationException("The template has not applied.");
            Run(PaneMenu(window, unified), DiffCommand.NextChange);

            Assert.Equal(
                [
                    "Command CopyToRight from the left pane menu on left lines 9–11",
                    "Command NextChange from the left pane menu",
                    "Command \"What did I click?\" from the left pane menu",
                    "Command \"What did I click?\" from the left header menu",
                    "Command NextChange from the unified pane menu",
                ],
                log.Lines("Command"));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [Fact]
    public void A_menu_is_named_by_the_surface_it_opened_on_and_the_side_it_belongs_to()
    {
        static DiffPaneContext On(DiffPaneRegion region, DiffSide? side, bool unified = false) =>
            new(region, side, LineNumber: 1, SourceSide: side, SourceLine: 1, Row: 0, Block: null, DiffLineKind.Unchanged,
                SelectedLines: null, IsUnified: unified, IsReadOnly: true);

        Assert.Equal(
            [
                "left pane", "right number margin", "left change markers", "connector", "right overview map",
                "overview map", "unified pane", "unified number margin",
            ],
            [
                ActionLog.MenuName(On(DiffPaneRegion.Text, DiffSide.Left)),
                ActionLog.MenuName(On(DiffPaneRegion.LineNumberMargin, DiffSide.Right)),
                ActionLog.MenuName(On(DiffPaneRegion.ChangeMarkerMargin, DiffSide.Left)),
                ActionLog.MenuName(On(DiffPaneRegion.ConnectorGutter, side: null)),
                ActionLog.MenuName(On(DiffPaneRegion.OverviewMap, DiffSide.Right)),

                // The map's marker column, between its two lanes, belongs to neither side.
                ActionLog.MenuName(On(DiffPaneRegion.OverviewMap, side: null)),
                ActionLog.MenuName(On(DiffPaneRegion.Text, side: null, unified: true)),
                ActionLog.MenuName(On(DiffPaneRegion.LineNumberMargin, side: null, unified: true)),
            ]);
    }

    [AvaloniaFact]
    public async Task A_copy_a_pane_asks_for_is_written_as_what_it_asked_to_send()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        FakeTimeProvider time = new();
        MainWindow window = await OpenAsync(time);
        try
        {
            window.Diff.RightReadOnly = false;
            DiffPanePresenter left = Left(window);
            Frame(window);

            // The first block is the right side's inserted line, so the left pane's arrow for it is drawn in its padding.
            (Rect zone, _, _) = Assert.Single(left.LineNumberMargin.LastCopyArrows, a => a.BlockIndex == 0);
            Click(window, left.LineNumberMargin.TranslatePoint(zone.Center, window));
            await RestAsync(window, time, window.Diff.ReDiffDelay + Tick);

            // A selection's arrow takes the number cell of its first line.
            left.TextArea.Focus();
            SelectLines(left, 9, 11);
            Frame(window);
            (Rect bounds, _) = left.LineNumberMargin.LastSelectionArrow ?? throw new InvalidOperationException("No selection arrow was drawn.");
            Click(window, left.LineNumberMargin.TranslatePoint(bounds.Center, window));
            await RestAsync(window, time, window.Diff.ReDiffDelay + Tick);

            Assert.Equal(
                ["Copy requested: left block 1 → right", "Copy requested: left lines 9–11 → right"],
                log.Lines("Copy requested"));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Edits_are_written_once_they_rest_as_the_lines_they_touched_and_the_lines_they_moved()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        FakeTimeProvider time = new();
        MainWindow window = await OpenAsync(time);
        try
        {
            window.Diff.LeftReadOnly = false;
            DiffPanePresenter left = Left(window);
            left.TextArea.Focus();
            left.TextArea.Caret.Offset = left.Document.GetLineByNumber(3).Offset;
            Layout();

            // Three keystrokes are one edit, and it waits for the whole rest.
            foreach (string key in (string[])["a", "b", "c"])
            {
                window.KeyTextInput(key);
            }

            Layout();
            await RestAsync(window, time, ActionLog.Quiet - Tick);
            Assert.Empty(log.Lines("Left pane: edit"));

            await RestAsync(window, time, Tick);
            Assert.Equal(["Left pane: edit at line 3, +0 −0 lines"], log.Lines("Left pane: edit"));

            // After a rest, the next edit is a line of its own: lines 5 and 6 deleted whole.
            left.Select(left.Document.GetLineByNumber(5).Offset, left.Document.GetLineByNumber(7).Offset - left.Document.GetLineByNumber(5).Offset);
            Layout();
            Press(window, Key.Delete, PhysicalKey.Delete);
            await RestAsync(window, time, ActionLog.Quiet);

            Assert.Equal(
                ["Left pane: edit at line 3, +0 −0 lines", "Left pane: edit at line 5, +0 −2 lines"],
                log.Lines("Left pane: edit"));

            // The unified view composes its document from both sides: the control writing, not the reader.
            Choose(window, window.UnifiedView);
            await RestAsync(window, time, ActionLog.Quiet);
            DiffPanePresenter unified = window.Unified.Pane ?? throw new InvalidOperationException("The template has not applied.");
            Assert.True(unified.Document.LineCount > 1, "the unified pane composed nothing, so it proves nothing");
            Assert.Equal(2, log.Lines().Count(l => l.Contains(" pane: edit", StringComparison.Ordinal)));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_library_s_interaction_lines_reach_the_demo_s_log_at_the_level_it_keeps()
    {
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        MainWindow window = await OpenAsync(new FakeTimeProvider());
        try
        {
            DiffPanePresenter left = Left(window);
            Click(window, TextPoint(window, left, line: 3, column: 4));
            Drag(window, TextPoint(window, left, line: 3, column: 1), TextPoint(window, left, line: 5, column: 5));

            // Written as the library words them: Serilog quotes a string value its template does not mark literal.
            IReadOnlyList<LogEvent> lines = [.. log.Events().Where(e => CategoryOf(e) == DiffViewLogCategories.Interaction)];
            Assert.Equal(
                ["Left pane: click in the text at 3:4", "Left pane: selected 3:1–5:5 (23 chars)"],
                lines.Select(e => e.RenderMessage(CultureInfo.InvariantCulture)));
            Assert.All(lines, e => Assert.Equal(LogEventLevel.Information, e.Level));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task No_line_the_demo_writes_carries_the_text_under_comparison()
    {
        const string sentinel = "SENTINEL_d41c8e";
        TestLogSink.Instance.Clear();
        using DemoLog log = new();
        FakeTimeProvider time = new();
        MainWindow window = await OpenAsync(time);
        try
        {
            (string left, string right) = CompositeHost.SmallFixture();
            window.Diff.LeftSource = new PaneSource(left.Replace("Greeter", sentinel, StringComparison.Ordinal));
            window.Diff.RightSource = new PaneSource(right.Replace("Greeter", sentinel + "2", StringComparison.Ordinal));
            await SettleAsync(window);
            window.Diff.LeftReadOnly = false;
            window.Diff.RightReadOnly = false;
            DiffPanePresenter pane = Left(window);
            Layout();
            IClipboard clipboard = TopLevel.GetTopLevel(window)?.Clipboard
                                   ?? throw new InvalidOperationException("the headless top level has no clipboard");

            // Selected with the pointer: line 5 is "    public sealed class " and the sentinel.
            Drag(window, TextPoint(window, pane, line: 5, column: 1), TextPoint(window, pane, line: 5, column: 40));
            Assert.Contains(sentinel, pane.SelectedText, StringComparison.Ordinal);

            // Copied across to the other side by the selection's arrow, to the clipboard, and across again by the chord.
            Frame(window);
            (Rect arrow, _) = pane.LineNumberMargin.LastSelectionArrow ?? throw new InvalidOperationException("No selection arrow was drawn.");
            Click(window, pane.LineNumberMargin.TranslatePoint(arrow.Center, window));
            pane.Copy();
            Press(window, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Alt);

            // Typed, and pasted.
            pane.TextArea.Caret.Offset = 0;
            window.KeyTextInput(sentinel);
            await clipboard.SetTextAsync(sentinel + "\n");
            pane.Paste();
            Layout();

            // Searched for.
            window.Diff.FindQuery = sentinel;
            await RestAsync(window, time, SideBySideDiffView.FindDebounce);
            if (window.Diff.CurrentFind is { } find)
            {
                await find;
            }

            // And every edit written.
            await RestAsync(window, time, ActionLog.Quiet);

            Assert.Contains(log.Lines(), l => l.StartsWith("Left pane: selected", StringComparison.Ordinal));
            Assert.Contains("Copy requested: left line 5 → right", log.Lines());
            Assert.Contains(log.Lines(), l => l.StartsWith("Command CopyToRight", StringComparison.Ordinal));
            Assert.Contains(log.Lines(), l => l.StartsWith("Left pane: edit", StringComparison.Ordinal));
            Assert.Contains(log.Lines(), l => l.StartsWith("Right pane: edit", StringComparison.Ordinal));
            Assert.DoesNotContain(log.Events(), e => Everything(e).Contains(sentinel, StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    /// <summary>The demo window, shown and built, its three views building with no machine's timings and resting on <paramref name="time"/>.</summary>
    private static async Task<MainWindow> OpenAsync(FakeTimeProvider time)
    {
        MainWindow window = new() { Width = 800, Height = 500 };
        window.Diff.Builder = CompositeHost.ZeroTimeBuilder;
        window.Unified.Builder = CompositeHost.ZeroTimeBuilder;
        window.Viewer.Builder = CompositeHost.ZeroTimeBuilder;
        window.Diff.TimeProvider = time;
        window.Unified.TimeProvider = time;
        window.Show();
        await SettleAsync(window);
        return window;
    }

    /// <summary>Runs the queued work, waits for whichever builds are in flight, and runs what they queued.</summary>
    private static async Task SettleAsync(MainWindow window)
    {
        Layout();
        foreach (Task? build in new[] { window.Diff.CurrentBuild, window.Unified.CurrentBuild, window.Viewer.CurrentBuild })
        {
            if (build is not null)
            {
                await build;
            }
        }

        Layout();
    }

    /// <summary>Lets <paramref name="span"/> pass on the view's clock, then settles whatever that fired.</summary>
    private static async Task RestAsync(MainWindow window, FakeTimeProvider time, TimeSpan span)
    {
        time.Advance(span);
        await SettleAsync(window);
    }

    private static void Layout() => Dispatcher.UIThread.RunJobs();

    private static DiffPanePresenter Left(MainWindow window) =>
        window.Diff.LeftPane ?? throw new InvalidOperationException("The template has not applied.");

    /// <summary>A frame, which is when a margin records the arrows it drew.</summary>
    private static void Frame(MainWindow window)
    {
        Layout();
        window.CaptureRenderedFrame()?.Dispose();
    }

    /// <summary>
    /// Opens the menus down to <paramref name="item"/> and presses Enter on it. That is Avalonia's own
    /// click — its menu interaction handler toggles a check or radio item and then raises
    /// <see cref="MenuItem.ClickEvent"/>, the order a pointer gets too — where raising the event here
    /// would skip the toggle, and put an order of the test's own in its place.
    /// </summary>
    private static void Choose(MainWindow window, MenuItem item)
    {
        Stack<MenuItem> path = new();
        for (StyledElement? at = item.Parent; at is MenuItem parent; at = parent.Parent)
        {
            path.Push(parent);
        }

        foreach (MenuItem parent in path)
        {
            parent.IsSubMenuOpen = true;
            Layout();
        }

        Assert.True(item.Focus(), $"{item.Header} took no focus, so Enter would reach something else");
        Layout();
        Press(window, Key.Enter, PhysicalKey.Enter);
        Assert.False(window.MainMenu.IsOpen, "the menu is still open, so Enter did not click the item");
    }

    /// <summary>
    /// The items of a pane's menu, in whichever view holds the pane, asked for with the keyboard so the
    /// selection stays. The menu itself is not opened; its items are what it would have shown.
    /// </summary>
    private static List<DiffMenuItem> PaneMenu(MainWindow window, DiffPanePresenter pane)
    {
        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffPaneContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        window.Diff.PaneContextMenuOpening += Capture;
        window.Unified.PaneContextMenuOpening += Capture;
        try
        {
            pane.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        }
        finally
        {
            window.Diff.PaneContextMenuOpening -= Capture;
            window.Unified.PaneContextMenuOpening -= Capture;
        }

        Assert.NotEmpty(captured);
        return captured;
    }

    /// <summary>The items of the left header's menu.</summary>
    private static List<DiffMenuItem> HeaderMenu(MainWindow window)
    {
        List<DiffMenuItem> captured = [];
        void Capture(object? sender, DiffHeaderContextMenuEventArgs e)
        {
            captured.AddRange(e.Items);
            e.Cancel = true;
        }

        window.Diff.HeaderContextMenuOpening += Capture;
        try
        {
            DiffPaneHeader header = window.Diff.LeftHeader ?? throw new InvalidOperationException("The template has not applied.");
            header.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
        }
        finally
        {
            window.Diff.HeaderContextMenuOpening -= Capture;
        }

        return captured;
    }

    private static void Run(List<DiffMenuItem> items, DiffCommand verb)
    {
        DiffMenuItem item = Assert.Single(items, i => i.Verb == verb);
        (item.Command ?? throw new InvalidOperationException($"{verb} has no command.")).Execute(null);
        Layout();
    }

    private static void Run(List<DiffMenuItem> items, string header)
    {
        DiffMenuItem item = Assert.Single(items, i => i.Header == header);
        (item.Command ?? throw new InvalidOperationException($"\"{header}\" has no command.")).Execute(null);
        Layout();
    }

    /// <summary>Whole lines <paramref name="first"/> to <paramref name="last"/>, through the end of the last one's text.</summary>
    private static void SelectLines(DiffPanePresenter pane, int first, int last)
    {
        int start = pane.Document.GetLineByNumber(first).Offset;
        pane.Select(start, pane.Document.GetLineByNumber(last).EndOffset - start);
        Layout();
    }

    private static void Press(MainWindow window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        Layout();
    }

    private static void Click(MainWindow window, Point? at)
    {
        Point point = at ?? throw new InvalidOperationException("The point is not in the window.");
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Layout();
    }

    private static void Drag(MainWindow window, Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left);
        foreach (double share in (double[])[0.25, 0.5, 0.75, 1.0])
        {
            window.MouseMove(from + ((to - from) * share), RawInputModifiers.LeftMouseButton);
        }

        window.MouseUp(to, MouseButton.Left);
        Layout();
    }

    /// <summary>Just inside the left edge of a character, on its line's text band, in window coordinates.</summary>
    private static Point TextPoint(MainWindow window, DiffPanePresenter pane, int line, int column)
    {
        TextView view = pane.TextArea.TextView;
        VisualLine visual = view.GetOrConstructVisualLine(pane.Document.GetLineByNumber(line));
        TextLine text = visual.TextLines[0];
        double y = visual.GetTextLineVisualYPosition(text, VisualYPosition.TextMiddle) - view.VerticalOffset;
        double x = visual.GetTextLineVisualXPosition(text, visual.GetVisualColumn(column - 1)) - view.HorizontalOffset;
        return view.TranslatePoint(new Point(x + 1, y), window) ?? throw new InvalidOperationException("The text view is not in the window.");
    }

    private static string? CategoryOf(LogEvent e) =>
        e.Properties.TryGetValue(Constants.SourceContextPropertyName, out LogEventPropertyValue? value) && value is ScalarValue { Value: string category }
            ? category
            : null;

    /// <summary>Everything an event carries that a log file could show: its template, its rendering, each property and any exception.</summary>
    private static string Everything(LogEvent e) =>
        string.Join(
            "\n",
            [e.MessageTemplate.Text, e.RenderMessage(CultureInfo.InvariantCulture), .. e.Properties.Values.Select(v => v.ToString()), e.Exception?.ToString() ?? string.Empty]);

    /// <summary>
    /// The demo's log, captured: <see cref="Log.Logger"/> swapped for a logger into this list at the
    /// demo's own level, and the library's factory built over it; both are put back on dispose.
    /// </summary>
    private sealed class DemoLog : IDisposable
    {
        private readonly Serilog.ILogger _previous = Log.Logger;
        private readonly Logger _logger;
        private readonly Sink _sink = new();

        public DemoLog()
        {
            // Information, as the demo keeps: a Debug line reaches this list only if the demo raised it.
            _logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(_sink).CreateLogger();
            Log.Logger = _logger;
            DemoLogging.Initialize(_logger);
        }

        public IReadOnlyList<LogEvent> Events() => _sink.Events();

        public IReadOnlyList<string> Lines(string prefix = "") =>
            [.. Events().Select(e => e.RenderMessage(CultureInfo.InvariantCulture)).Where(l => l.StartsWith(prefix, StringComparison.Ordinal))];

        public void Dispose()
        {
            Log.Logger = _previous;
            DemoLogging.ResetForTesting();
            _logger.Dispose();
        }

        /// <summary>The list the logger writes into. Not disposable: the logger disposes its sinks.</summary>
        private sealed class Sink : ILogEventSink
        {
            private readonly List<LogEvent> _events = [];

            public void Emit(LogEvent logEvent)
            {
                lock (_events)
                {
                    _events.Add(logEvent);
                }
            }

            public IReadOnlyList<LogEvent> Events()
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }
    }
}
