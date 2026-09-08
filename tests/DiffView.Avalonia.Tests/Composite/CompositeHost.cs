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
/// <remarks>
/// Syntax highlighting is off unless a test asks for it, for the same reason: a grammar's colours
/// arrive from TextMateSharp's own thread, so a frame captured without waiting for them is a
/// coin toss. The tests that want colour pass <c>syntax: true</c> and wait through
/// <see cref="PumpUntilAsync"/>.
/// </remarks>
internal sealed class CompositeHost : IDisposable
{
    public CompositeHost(double width = 900, double height = 600, CapturingLoggerFactory? logs = null, bool syntax = false)
    {
        Time = new FakeTimeProvider();
        Logs = logs ?? new CapturingLoggerFactory();
        View = new SideBySideDiffView
        {
            TimeProvider = Time,
            LoggerFactory = Logs,
            IsCaretBlinkEnabled = false,
            UseSyntaxHighlighting = syntax,
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

    /// <summary>The committed small pair under a C# name, so a grammar is chosen for it.</summary>
    public static (PaneSource Left, PaneSource Right) CsharpFixture()
    {
        (string left, string right) = SmallFixture();
        return (Named(left, "Greeter.cs"), Named(right, "Greeter.cs"));
    }

    /// <summary>The committed JSON pair, named for its grammar.</summary>
    public static (PaneSource Left, PaneSource Right) JsonFixture()
    {
        string directory = RepoPaths.Source(Path.Combine("fixtures", "json"));
        return (
            Named(File.ReadAllText(Path.Combine(directory, "left.json")), "left.json"),
            Named(File.ReadAllText(Path.Combine(directory, "right.json")), "right.json"));
    }

    /// <summary>A source that carries a file name but no path, which is where the grammar comes from.</summary>
    public static PaneSource Named(string text, string fileName)
    {
        return new PaneSource(text) { Title = fileName };
    }

    /// <summary>
    /// Runs the layout until <paramref name="settled"/> holds, or gives up. TextMateSharp
    /// tokenizes on its own thread, so a pane's colours land some frames after its grammar is
    /// installed, and a test that renders has to wait for them.
    /// </summary>
    public static async Task<bool> PumpUntilAsync(Func<bool> settled, TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            Layout();
            if (settled())
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(10);
        }
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

    /// <summary>
    /// Fires the re-diff debounce an edit armed, then waits for the build it posts. The clock is
    /// advanced past <see cref="SideBySideDiffView.ReDiffDelay"/> rather than exactly to it, so a
    /// timer scheduled a tick late still fires.
    /// </summary>
    public async Task WaitForReDiffAsync()
    {
        Time.Advance(View.ReDiffDelay + TimeSpan.FromMilliseconds(1));
        Layout();
        await WaitForBuildAsync();
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
