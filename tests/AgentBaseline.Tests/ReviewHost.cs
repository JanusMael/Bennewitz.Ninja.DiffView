using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// A shown window holding one <see cref="SideBySideDiffView"/> on a hand-advanced clock — this
/// project's own <c>CompositeHost</c>, which plan 00034 step 5 says it must write, that one being
/// internal to the library's test assembly. The window is a bare one, or since step 7 the tool's own
/// <see cref="ReviewWindow"/>, its view on the same clock.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A build is awaited, never pumped for</b> — AGENTS.md §5. The view runs a diff on the thread
/// pool and posts its outcome to the dispatcher, so a layout pass lands a build only when the pool
/// happened to finish first. <c>CompositeHost</c> awaits the view's internal <c>CurrentBuild</c>; this
/// host reaches only the public surface, so it waits for <see cref="SideBySideDiffView.BuildCompleted"/>
/// instead, subscribed before the change that starts the build so the build cannot land unseen.
/// </para>
/// <para>
/// The clock is never advanced here, so the re-diff an edit arms stays armed and no debounce fires
/// behind a test's back: every build in these tests is one an assignment started. Syntax highlighting
/// stays off for the reason <c>CompositeHost</c> keeps it off — TextMateSharp works on its own thread.
/// </para>
/// </remarks>
internal sealed class ReviewHost : IDisposable
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A bare window holding a view, for a session driven from code.</summary>
    internal ReviewHost()
        : this(new SideBySideDiffView(), view => new Window { Width = 900, Height = 600, Content = view })
    {
    }

    /// <summary>The tool's own window, for a review driven through its commands — plan 00034 step 7.</summary>
    internal ReviewHost(ReviewWindow window)
        : this(window.View, _ => window)
    {
    }

    private ReviewHost(SideBySideDiffView view, Func<SideBySideDiffView, Window> windowFor)
    {
        Time = new FakeTimeProvider();
        View = view;
        View.TimeProvider = Time;
        View.IsCaretBlinkEnabled = false;
        View.UseSyntaxHighlighting = false;
        Window = windowFor(view);
        Window.Show();
        Layout();
    }

    internal SideBySideDiffView View { get; }

    internal Window Window { get; }

    internal FakeTimeProvider Time { get; }

    /// <summary>The left pane, found by the part name the view publishes.</summary>
    internal DiffPanePresenter Left => Pane(SideBySideDiffView.LeftPanePart);

    /// <summary>The right pane, found by the part name the view publishes.</summary>
    internal DiffPanePresenter Right => Pane(SideBySideDiffView.RightPanePart);

    internal static void Layout() => Dispatcher.UIThread.RunJobs();

    /// <summary>Runs <paramref name="change"/>, which must start a build, and waits for that build to land.</summary>
    internal async Task<T> AwaitBuildAsync<T>(Func<T> change)
    {
        TaskCompletionSource landed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, DiffBuildCompletedEventArgs e) => landed.TrySetResult();
        void Failed(object? sender, DiffBuildFailedEventArgs e) =>
            landed.TrySetException(new InvalidOperationException($"The build failed: {e.Exception.Message}", e.Exception));

        View.BuildCompleted += Completed;
        View.BuildFailed += Failed;
        try
        {
            T result = change();
            try
            {
                await landed.Task.WaitAsync(BuildTimeout);
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"No build landed within {BuildTimeout.TotalSeconds} s of the change: it started none, or one never finished.",
                    ex);
            }

            Layout();
            return result;
        }
        finally
        {
            View.BuildCompleted -= Completed;
            View.BuildFailed -= Failed;
        }
    }

    /// <inheritdoc cref="AwaitBuildAsync{T}(Func{T})"/>
    internal Task AwaitBuildAsync(Action change) =>
        AwaitBuildAsync(() =>
        {
            change();
            return true;
        });

    /// <summary>
    /// Types <paramref name="text"/> into <paramref name="pane"/> at <paramref name="offset"/>, through
    /// the keyboard — so a pane that is read-only refuses it, where an edit made straight on the
    /// document would not be refused at all.
    /// </summary>
    internal void Type(DiffPanePresenter pane, int offset, string text)
    {
        pane.TextArea.Focus();
        pane.TextArea.Caret.Offset = offset;
        Layout();
        Window.KeyTextInput(text);
        Layout();
    }

    /// <summary>
    /// Presses <paramref name="key"/> with <paramref name="modifiers"/> in <paramref name="pane"/>, through
    /// the keyboard, so whatever the window and the view bind is asked in the order it would be.
    /// </summary>
    internal void Press(DiffPanePresenter pane, Key key, RawInputModifiers modifiers, PhysicalKey physical)
    {
        pane.TextArea.Focus();
        Layout();
        Window.KeyPress(key, modifiers, physical, null);
        Window.KeyRelease(key, modifiers, physical, null);
        Layout();
    }

    /// <summary>
    /// Puts <paramref name="pane"/>'s caret at the start of 1-based <paramref name="line"/>, where a click
    /// in the text would put it.
    /// </summary>
    internal static void PutCaret(DiffPanePresenter pane, int line)
    {
        pane.TextArea.Caret.Position = new TextViewPosition(line, 1);
        Layout();
    }

    public void Dispose() => Window.Close();

    private DiffPanePresenter Pane(string part) =>
        View.GetVisualDescendants().OfType<DiffPanePresenter>().Single(p => p.Name == part);
}
