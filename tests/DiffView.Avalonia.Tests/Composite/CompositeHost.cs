using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// A window holding one <see cref="SideBySideDiffView"/> on a hand-advanced clock, with the
/// log captured and the caret kept from blinking so frames are stable.
/// </summary>
internal sealed class CompositeHost : IDisposable
{
    public CompositeHost(double width = 900, double height = 600, CapturingLoggerFactory? logs = null)
    {
        Time = new FakeTimeProvider();
        Logs = logs ?? new CapturingLoggerFactory();
        View = new SideBySideDiffView
        {
            TimeProvider = Time,
            LoggerFactory = Logs,
            IsCaretBlinkEnabled = false,
            Builder = ZeroTimeBuilder,
        };
        Window = new Window { Width = width, Height = height, Content = View };
    }

    /// <summary>
    /// The real builder with the measured build time zeroed, so the strip's timings read the
    /// same on every machine and a rendered frame carries no machine-specific text.
    /// </summary>
    public static DiffBuildResult ZeroTimeBuilder(PaneSource left, PaneSource right, DiffOptions options, CancellationToken token)
    {
        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, options, token);
        return result with { Diagnostics = result.Diagnostics with { BuildTime = TimeSpan.Zero } };
    }

    public SideBySideDiffView View { get; }

    public Window Window { get; }

    public FakeTimeProvider Time { get; }

    public CapturingLoggerFactory Logs { get; }

    public DiffPanePresenter Left => View.LeftPane ?? throw new InvalidOperationException("The template has not applied.");

    public DiffPanePresenter Right => View.RightPane ?? throw new InvalidOperationException("The template has not applied.");

    public static (string Left, string Right) SmallFixture()
    {
        return PresenterHost.SmallFixture();
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
