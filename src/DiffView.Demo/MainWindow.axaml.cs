using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Styling;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Avalonia;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.LayeredEditors.Avalonia.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Bennewitz.Ninja.DiffView.Demo;

public sealed partial class MainWindow : Window
{
    private string _diffSummary = "no diff loaded";
    private string? _loadNote;

    public MainWindow()
    {
        InitializeComponent();

        ILogger renderLogger = DemoLogging.Factory.CreateLogger(DiffViewLogCategories.Render);
        LeftPane.Logger = renderLogger;
        RightPane.Logger = renderLogger;
        LeftPane.RenderFault += OnRenderFault;
        RightPane.RenderFault += OnRenderFault;

        LoadPanes();
        UpdateStatus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            AvaloniaDiagnostics.ToggleLiveLogWindow();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Loads the two sides — the files named by <c>--left</c> / <c>--right</c>, or the bundled
    /// small fixture — builds the diff on the UI thread (Phase 5 moves it to a worker) and feeds
    /// both presenters from the one model. A file that cannot be read falls back to the fixture
    /// and says so in the status bar and the log; a build that fails says so the same way.
    /// </summary>
    private void LoadPanes()
    {
        PaneSource left = LoadSource(DebugFlags.LeftPath, "left.txt");
        PaneSource right = LoadSource(DebugFlags.RightPath, "right.txt");
        LeftPane.Document = new TextDocument(left.Text);
        RightPane.Document = new TextDocument(right.Text);

        try
        {
            DiffBuildResult result = DiffDocumentBuilder.Build(left, right);
            LeftPane.DiffDocument = result.Document;
            RightPane.DiffDocument = result.Document;

            DiffDiagnostics diagnostics = result.Diagnostics;
            _diffSummary = $"+{diagnostics.Inserted} −{diagnostics.Deleted} ~{diagnostics.Modified} in {diagnostics.RowCount} rows";
            Log.Information(
                "Diff built: {Rows} rows, {Blocks} blocks (+{Inserted} -{Deleted} ~{Modified}), similarity {Similarity:F2}, {Elapsed:F1} ms, {Warnings} warning(s)",
                diagnostics.RowCount, diagnostics.BlockCount, diagnostics.Inserted, diagnostics.Deleted, diagnostics.Modified,
                diagnostics.Similarity, diagnostics.BuildTime.TotalMilliseconds, result.Warnings.Count);
            foreach (DiffWarning warning in result.Warnings)
            {
                Log.Warning("Diff warning {Code}: {Message}", warning.Code, warning.Message);
            }
        }
        catch (DiffBuildException ex)
        {
            _diffSummary = $"diff failed: {ex.Message}";
            Log.Error(ex, "Diff build failed ({Code})", ex.Code);
        }
    }

    private PaneSource LoadSource(string? path, string fixtureFileName)
    {
        if (path is not null)
        {
            try
            {
                return PaneSource.FromFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _loadNote = $"could not read {path}: {ex.Message}";
                Log.Error(ex, "Could not read {Path}; showing the bundled sample instead", path);
            }
        }

        using Stream stream = AssetLoader.Open(new Uri($"avares://DiffView.Demo/Fixtures/small/{fixtureFileName}"));
        using StreamReader reader = new(stream);
        return new PaneSource(reader.ReadToEnd()) { Title = fixtureFileName };
    }

    private void OnRenderFault(object? sender, RenderFaultEventArgs e)
    {
        // The presenter has already logged it through its Logger; the status bar shows it.
        _loadNote = e.Message;
        UpdateStatus();
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnVariantLight(object? sender, RoutedEventArgs e)
    {
        SetVariant(ThemeVariant.Light);
    }

    private void OnVariantDark(object? sender, RoutedEventArgs e)
    {
        SetVariant(ThemeVariant.Dark);
    }

    private void OnVariantSystem(object? sender, RoutedEventArgs e)
    {
        SetVariant(ThemeVariant.Default);
    }

    private void OnToggleLiveLog(object? sender, RoutedEventArgs e)
    {
        AvaloniaDiagnostics.ToggleLiveLogWindow();
    }

    private async void OnOpenLogsFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LogPaths.LogsDirectory);
            await Launcher.LaunchUriAsync(new Uri(LogPaths.LogsDirectory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Warning(ex, "Could not open the logs folder {LogDirectory}", LogPaths.LogsDirectory);
        }
    }

    private void OnThrowOnUiThread(object? sender, RoutedEventArgs e)
    {
        // Lands in Dispatcher.UIThread.UnhandledException: logged with a stack, fatal-error dialog shown.
        throw new InvalidOperationException("Deliberate test exception from Debug → Throw on the UI thread.");
    }

    private void OnThrowInUnobservedTask(object? sender, RoutedEventArgs e)
    {
        // The faulted task is dropped on purpose; its exception surfaces through
        // TaskScheduler.UnobservedTaskException once the task is finalized, which the forced
        // collection below brings forward from "eventually" to "now".
        _ = Task.Run(() => throw new InvalidOperationException("Deliberate test exception from Debug → Throw in an unobserved task."));
        _ = ForceFinalizationAsync();
    }

    private static async Task ForceFinalizationAsync()
    {
        await Task.Delay(500);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void SetVariant(ThemeVariant variant)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = variant;
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        ThemeVariant requested = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default;
        VariantLight.IsChecked = requested == ThemeVariant.Light;
        VariantDark.IsChecked = requested == ThemeVariant.Dark;
        VariantSystem.IsChecked = requested == ThemeVariant.Default;

        // No machine-specific text in the rendered status: the snapshot tests compare this window
        // across machines. The logs path is one hover away, in the Debug menu, and in the log
        // itself; the build time is in the log. A load note carries a path only when a flag
        // named one, which no snapshot does.
        string note = _loadNote is null ? string.Empty : $"   ·   {_loadNote}";
        StatusText.Text = $"Theme: {DebugFlags.Theme}   ·   Variant: {requested} (actual {ActualThemeVariant})   ·   Diff: {_diffSummary}{note}   ·   F12: live log";
        ToolTip.SetTip(StatusText, $"Logs: {LogPaths.LogsDirectory}");
    }
}
