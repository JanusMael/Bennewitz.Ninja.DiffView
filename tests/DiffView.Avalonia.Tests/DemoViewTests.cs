using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Demo;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00021 phase 3: the demo hosts the viewer beside the editor and the unified view — one choice,
/// made by View ▸ Control or by <c>--unified</c> / <c>--viewer</c> — and only the view on screen
/// holds the sources, so the other two neither build nor keep a model.
/// </summary>
/// <remarks>
/// The accessibility gate reads the demo's markup and so sees the viewer declared there; what it
/// cannot see is whether the window ever puts that viewer on screen with something to show, which is
/// the reason the demo hosts it at all — so that a person looking at the demo sees the control.
/// </remarks>
public sealed class DemoViewTests
{
    [AvaloniaFact]
    public async Task Each_entry_of_the_control_submenu_puts_its_view_alone_on_screen_with_the_sources()
    {
        TestLogSink.Instance.Clear();
        MainWindow window = Open();
        try
        {
            await SettleAsync(window);
            AssertOnScreen(window, DemoView.SideBySide);

            foreach (DemoView view in new[] { DemoView.Viewer, DemoView.Unified, DemoView.SideBySide })
            {
                EntryFor(window, view).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                await SettleAsync(window);
                AssertOnScreen(window, view);
            }
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_viewer_flag_starts_the_demo_in_the_viewer()
    {
        TestLogSink.Instance.Clear();
        DebugFlags.Parse(["--viewer"]);
        try
        {
            MainWindow window = Open();
            try
            {
                await SettleAsync(window);
                AssertOnScreen(window, DemoView.Viewer);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            DebugFlags.ResetForTesting();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [Fact]
    public void The_two_view_flags_are_one_choice_and_the_last_one_wins()
    {
        try
        {
            DebugFlags.Parse(["--unified", "--viewer"]);
            Assert.Equal(DemoView.Viewer, DebugFlags.View);

            DebugFlags.ResetForTesting();
            DebugFlags.Parse(["--viewer", "--unified"]);
            Assert.Equal(DemoView.Unified, DebugFlags.View);

            DebugFlags.ResetForTesting();
            DebugFlags.Parse([]);
            Assert.Equal(DemoView.SideBySide, DebugFlags.View);
        }
        finally
        {
            DebugFlags.ResetForTesting();
        }
    }

    /// <summary>The demo window, shown, its three views building with no machine's timings.</summary>
    private static MainWindow Open()
    {
        MainWindow window = new() { Width = 800, Height = 500 };
        window.Diff.Builder = CompositeHost.ZeroTimeBuilder;
        window.Unified.Builder = CompositeHost.ZeroTimeBuilder;
        window.Viewer.Builder = CompositeHost.ZeroTimeBuilder;
        window.Show();
        return window;
    }

    /// <summary>Runs the queued work, waits for whichever builds are in flight, and runs what they queued.</summary>
    private static async Task SettleAsync(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        foreach (Task? build in new[] { window.Diff.CurrentBuild, window.Unified.CurrentBuild, window.Viewer.CurrentBuild })
        {
            if (build is not null)
            {
                await build;
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static MenuItem EntryFor(MainWindow window, DemoView view) => view switch
    {
        DemoView.Unified => window.UnifiedView,
        DemoView.Viewer => window.ViewerView,
        _ => window.EditorView,
    };

    /// <summary>
    /// <paramref name="view"/> is on screen with its entry ticked, holds both sources and has built
    /// them; the other two are hidden, unticked and hold nothing.
    /// </summary>
    private static void AssertOnScreen(MainWindow window, DemoView view)
    {
        (DemoView View, Control Control, PaneSource? Left, PaneSource? Right, DiffViewState State)[] views =
        [
            (DemoView.SideBySide, window.Diff, window.Diff.LeftSource, window.Diff.RightSource, window.Diff.State),
            (DemoView.Unified, window.Unified, window.Unified.LeftSource, window.Unified.RightSource, window.Unified.State),
            (DemoView.Viewer, window.Viewer, window.Viewer.LeftSource, window.Viewer.RightSource, window.Viewer.State),
        ];

        foreach ((DemoView each, Control control, PaneSource? left, PaneSource? right, DiffViewState state) in views)
        {
            bool chosen = each == view;
            Assert.True(control.IsVisible == chosen, $"With {view} chosen, {each} is {(control.IsVisible ? "on screen" : "hidden")}.");
            Assert.True(
                EntryFor(window, each).IsChecked == chosen,
                $"With {view} chosen, the Control submenu's {each} entry is {(EntryFor(window, each).IsChecked ? "ticked" : "unticked")}.");
            Assert.True(
                (left is not null && right is not null) == chosen && (left is null && right is null) == !chosen,
                $"With {view} chosen, {each} holds {(left is null ? "no" : "a")} left source and {(right is null ? "no" : "a")} right source.");
            if (chosen)
            {
                Assert.True(state == DiffViewState.Ready, $"{each} is on screen with the sources and is {state}, not Ready.");
            }
        }
    }
}
