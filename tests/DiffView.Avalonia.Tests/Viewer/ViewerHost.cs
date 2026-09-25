using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// A window holding one <see cref="DiffViewer"/> on a hand-advanced clock, with the log captured
/// and the caret kept from blinking so frames are stable — the viewer's twin of
/// <see cref="CompositeHost"/>, down to the zero-time builder and the syntax rule.
/// </summary>
/// <remarks>
/// A fixture of its own because <see cref="CompositeHost.View"/> is typed as the editor: none of the
/// coverage written against it reaches the viewer, which is the point this class exists to make.
/// </remarks>
internal sealed class ViewerHost : IDisposable
{
    public ViewerHost(double width = 900, double height = 600, CapturingLoggerFactory? logs = null, bool syntax = false)
    {
        Time = new FakeTimeProvider();
        Logs = logs ?? new CapturingLoggerFactory();
        View = new DiffViewer
        {
            TimeProvider = Time,
            LoggerFactory = Logs,
            IsCaretBlinkEnabled = false,
            UseSyntaxHighlighting = syntax,
            Builder = CompositeHost.ZeroTimeBuilder,
        };
        Window = new Window { Width = width, Height = height, Content = View };
    }

    public DiffViewer View { get; }

    public Window Window { get; }

    public FakeTimeProvider Time { get; }

    public CapturingLoggerFactory Logs { get; }

    public DiffPanePresenter Left => View.LeftPane ?? throw new InvalidOperationException("The template has not applied.");

    public DiffPanePresenter Right => View.RightPane ?? throw new InvalidOperationException("The template has not applied.");

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
