using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Inline;

/// <summary>
/// A window holding one <see cref="InlineDiffView"/> on a hand-advanced clock, with the log
/// captured and the caret kept from blinking so frames are stable — the unified twin of
/// <see cref="CompositeHost"/>, down to the zero-time builder and the syntax rule.
/// </summary>
internal sealed class InlineHost : IDisposable
{
    public InlineHost(double width = 900, double height = 600, CapturingLoggerFactory? logs = null, bool syntax = false)
    {
        Time = new FakeTimeProvider();
        Logs = logs ?? new CapturingLoggerFactory();
        View = new InlineDiffView
        {
            TimeProvider = Time,
            LoggerFactory = Logs,
            IsCaretBlinkEnabled = false,
            UseSyntaxHighlighting = syntax,
            Builder = CompositeHost.ZeroTimeBuilder,
        };
        Window = new Window { Width = width, Height = height, Content = View };
    }

    public InlineDiffView View { get; }

    public Window Window { get; }

    public FakeTimeProvider Time { get; }

    public CapturingLoggerFactory Logs { get; }

    public DiffPanePresenter Pane => View.Pane ?? throw new InvalidOperationException("The template has not applied.");

    public static (string Left, string Right) SmallFixture()
    {
        return CompositeHost.SmallFixture();
    }

    public static void Layout()
    {
        Dispatcher.UIThread.RunJobs();
    }

    public void Show()
    {
        Window.Show();
        Layout();
    }

    /// <summary>Assigns both sources and waits for the build they trigger to land.</summary>
    public async Task LoadAsync(PaneSource left, PaneSource right)
    {
        View.LeftSource = left;
        View.RightSource = right;
        await WaitForBuildAsync();
    }

    /// <summary>Waits for the in-flight build, if any, then runs the layout it invalidated.</summary>
    public async Task WaitForBuildAsync()
    {
        Task? build = View.CurrentBuild;
        if (build is not null)
        {
            await build;
        }

        Layout();
    }

    /// <summary>Sets the query, then lets the debounce fire and the search it starts land.</summary>
    public async Task FindAsync(string query)
    {
        View.FindQuery = query;
        await WaitForFindAsync();
    }

    /// <summary>Fires the find debounce, runs the search it posts, and waits for its outcome.</summary>
    public async Task WaitForFindAsync()
    {
        Time.Advance(SideBySideDiffView.FindDebounce);
        Layout();
        Task? find = View.CurrentFind;
        if (find is not null)
        {
            await find;
        }

        Layout();
    }

    public WriteableBitmap Capture()
    {
        Layout();
        return Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
    }

    public void Dispose()
    {
        Window.Close();
    }
}
