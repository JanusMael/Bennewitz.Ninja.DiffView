using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Avalonia.Threading;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.LayeredEditors.Avalonia.Diagnostics;
using Semi.Avalonia;
using Serilog;

namespace Bennewitz.Ninja.DiffView.Demo;

public sealed class App : Application
{
    private static DiffViewColorBlindPalette? s_colourBlindPalette;

    /// <summary>
    /// Merges the colour-blind-safe palette over the default tokens, or removes it. The compiled
    /// dictionary class keeps the switch trim-safe.
    /// </summary>
    public static void UseColourBlindPalette(bool enabled)
    {
        if (Current is not { } application)
        {
            return;
        }

        if (enabled)
        {
            s_colourBlindPalette ??= new DiffViewColorBlindPalette();
            if (!application.Resources.MergedDictionaries.Contains(s_colourBlindPalette))
            {
                application.Resources.MergedDictionaries.Add(s_colourBlindPalette);
            }
        }
        else if (s_colourBlindPalette is not null)
        {
            application.Resources.MergedDictionaries.Remove(s_colourBlindPalette);
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // The base theme goes first so DiffView's styles (loaded from XAML) layer on top of it.
        Styles styles = DebugFlags.Theme switch
        {
            DemoTheme.Fluent => new FluentTheme(),
            DemoTheme.Simple => new SimpleTheme(),
            _ => new SemiTheme { Locale = CultureInfo.GetCultureInfo("en-US") },
        };
        Styles.Insert(0, styles);

        RequestedThemeVariant = DebugFlags.Variant switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Post-framework-init half of the diagnostics bootstrap: the F12 live-log window and the
        // binding-validation error logger (coercion errors that bypass Avalonia's own logger).
        AvaloniaDiagnostics.InstallAvaloniaHooks();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Global handlers before any window exists, so startup crashes are captured too.
            Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            desktop.MainWindow = new MainWindow();
            // The F12 log window has no owner; closing the main window must end the process.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── Global exception handlers — adapted from ClaudeForge's App.axaml.cs (MIT) ──────────

    private static void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Handled, so Avalonia does not terminate the process before the dialog can show.
        e.Handled = true;

        AggregateException? aggregate = e.Exception as AggregateException;
        if (IsBenignLinuxInfrastructureException(e.Exception)
            || (aggregate is not null && aggregate.Flatten().InnerExceptions.All(IsBenignLinuxInfrastructureException)))
        {
            Log.Debug(e.Exception, "DBus/portal unavailable ({Source}) — ignored", "UIThread");
            return;
        }

        Log.Error(e.Exception, "Unhandled exception on {Source}", "UIThread");
        ShowCrashDialog(e.Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Observed, so the runtime does not re-throw on the finalizer thread.
        e.SetObserved();
        AggregateException exception = e.Exception.Flatten();

        // Cancellation is control flow, not a crash.
        if (exception.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            Log.Verbose(exception, "Task cancelled silently ({Source})", "TaskScheduler");
            return;
        }

        if (exception.InnerExceptions.All(IsBenignLinuxInfrastructureException))
        {
            Log.Debug(exception, "DBus/portal unavailable ({Source}) — ignored", "TaskScheduler");
            return;
        }

        Log.Error(exception, "Unhandled exception on {Source}", "TaskScheduler");
        Dispatcher.UIThread.Post(() => ShowCrashDialog(exception));
    }

    /// <summary>
    /// True for a known benign Linux infrastructure miss — the XDG portal or DBus session service
    /// is absent on this desktop. A capability-detection failure, not a bug. The type-name check
    /// avoids a hard reference to Tmds.DBus; the message-prefix checks are guarded to Linux so a
    /// coincidentally named exception elsewhere is never downgraded.
    /// </summary>
    private static bool IsBenignLinuxInfrastructureException(Exception exception)
    {
        if (exception.GetType().FullName == "Tmds.DBus.Protocol.DBusException")
        {
            return true;
        }

        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        return exception.Message.StartsWith("org.freedesktop.DBus.Error.", StringComparison.Ordinal)
               || exception.Message.StartsWith("org.freedesktop.portal.", StringComparison.Ordinal);
    }

    private static void ShowCrashDialog(Exception exception)
    {
        AvaloniaDiagnostics.ShowFatalErrorDialog(
            "DiffView Demo encountered an unexpected error.\nYou can copy the details below for bug reporting.",
            exception);
    }
}
