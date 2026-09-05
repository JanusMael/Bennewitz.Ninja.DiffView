using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Bennewitz.Ninja.LayeredEditors.Avalonia.Diagnostics;
using Serilog;

namespace Bennewitz.Ninja.DiffView.Demo;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
        // across machines. The logs path is one hover away, in the Debug menu, and in the log itself.
        StatusText.Text = $"Theme: {DebugFlags.Theme}   ·   Variant: {requested} (actual {ActualThemeVariant})   ·   F12: live log";
        ToolTip.SetTip(StatusText, $"Logs: {LogPaths.LogsDirectory}");
    }
}
