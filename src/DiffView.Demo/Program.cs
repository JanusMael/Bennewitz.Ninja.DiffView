using Avalonia;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.LayeredEditors.Avalonia.Diagnostics;
using Serilog;

namespace Bennewitz.Ninja.DiffView.Demo;

// This demo is a HARNESS for driving the controls by hand, not a sample to copy wholesale: its own
// chrome stays English on purpose, and its flags exist for testing. Someone putting the control into
// their own application wants docs/hosting-diffview.md instead.
//
// Bootstrap order follows ClaudeForge's Program.cs (MIT): flags → crash handler → logging →
// deferred flag warnings → boot inside a try that shows both dialogs and flushes the log.
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Flags — parse only. Nothing can be logged yet.
        DebugFlags.Parse(args);

        // 2. AppDomain crash handler — catches fatal exceptions on non-Avalonia threads and during
        //    CLR initialization. The Avalonia runtime is likely dead by then, so the native OS
        //    dialog is the surface. Log.Fatal is a no-op until the pipeline exists.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception? exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "AppDomain.UnhandledException (IsTerminating={IsTerminating})", e.IsTerminating);
            AvaloniaDiagnostics.ShowNativeFatalError(exception?.ToString() ?? "Unknown fatal error.");
        };

        // 3. Logging pipeline: bucketed rolling file (8 h buckets, 3 d retention), Trace, the F12
        //    live-log window, and Avalonia's internal logger bridged with the noisy areas muted.
        string logsDirectory = LogPaths.LogsDirectory;
        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = "DiffView Demo",
            LogsDirectory = logsDirectory,
            MinimumLevel = DebugFlags.MinimumLevel,
            FileNamePrefix = "diffview",
        });
        DemoLogging.Initialize();

        // 4. Deferred flag warnings and the active-flags summary.
        DebugFlags.LogSummary();

        // 4b. --culture drives the library's own text, not the operating system's. It is set here
        //     rather than wired per control because DiffViewStrings resolves at the moment a string
        //     is used, so one assignment before the first window covers everything the library
        //     draws. The demo's own menus stay English on purpose: the surface under judgement in a
        //     by-hand pass is the library's.
        if (DebugFlags.Culture is { } culture)
        {
            DiffViewStrings.Localization = new DiffViewLocalization { Culture = culture };
        }

        // 5. Boot. The Starting/Exiting pair brackets a session so a post-mortem can tell a crash
        //    (Starting present, Exiting absent) from a clean quit.
        try
        {
            Log.Information("Starting DiffView Demo v{Version}", typeof(Program).Assembly.GetName().Version);
            Console.Error.WriteLine($"[DiffView] log directory: {logsDirectory}");
            Log.Information("Log directory: {LogDirectory}", logsDirectory);
            if (OperatingSystem.IsLinux())
            {
                Log.Information("XDG_SESSION_TYPE={SessionType}", Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "(unset)");
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Last resort during bootstrap: both dialogs, because either runtime may be the one
            // that is still alive.
            Log.Fatal(ex, "Fatal error during Avalonia bootstrap");
            AvaloniaDiagnostics.ShowFatalErrorDialog("DiffView Demo encountered a fatal error during startup.", ex);
            AvaloniaDiagnostics.ShowNativeFatalError(ex.ToString());
        }
        finally
        {
            Log.Information("Exiting DiffView Demo");
            Log.CloseAndFlush();
        }
    }

    // LogToTrace() is deliberately absent: Avalonia's events reach Trace through the Serilog
    // bridge, and adding it here would double-emit them.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .WithInterFont();
    }
}
