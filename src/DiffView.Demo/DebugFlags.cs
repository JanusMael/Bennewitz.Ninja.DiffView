using Serilog;
using Serilog.Events;

namespace Bennewitz.Ninja.DiffView.Demo;

internal enum DemoTheme
{
    Semi,
    Fluent,
    Simple,
}

/// <summary>
/// Command-line flags for the demo. Two-phase, in ClaudeForge's order: <see cref="Parse"/> runs
/// before the logging pipeline exists and emits nothing; <see cref="LogSummary"/> runs after it
/// and flushes the deferred warnings plus one summary line.
/// </summary>
internal static class DebugFlags
{
    private static readonly List<string> Deferred = [];

    /// <summary>Base theme: Semi (default), Fluent or Simple. <c>--theme &lt;name&gt;</c>.</summary>
    public static DemoTheme Theme { get; private set; } = DemoTheme.Semi;

    /// <summary>Requested variant: <c>light</c>, <c>dark</c>, or <c>default</c> (follow the OS). <c>--variant &lt;name&gt;</c>.</summary>
    public static string Variant { get; private set; } = "default";

    /// <summary>File to open in the left pane. <c>--left &lt;path&gt;</c>.</summary>
    public static string? LeftPath { get; private set; }

    /// <summary>File to open in the right pane. <c>--right &lt;path&gt;</c>.</summary>
    public static string? RightPath { get; private set; }

    /// <summary>Start in the unified (inline) view rather than side by side. <c>--unified</c>.</summary>
    public static bool Unified { get; private set; }

    /// <summary>Minimum Serilog level. <c>--log-level &lt;verbose|debug|information|warning|error|fatal&gt;</c>.</summary>
    public static LogEventLevel MinimumLevel { get; private set; } = LogEventLevel.Information;

    /// <summary>Parses <paramref name="args"/>. Emits no log lines; problems are deferred to <see cref="LogSummary"/>.</summary>
    public static void Parse(string[] args)
    {
        // Index-based loop: two-token flags consume their value by advancing i.
        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i].ToLowerInvariant();
            switch (flag)
            {
                case "--theme":
                    if (TryTakeValue(args, ref i, flag, out string? theme))
                    {
                        if (Enum.TryParse(theme, ignoreCase: true, out DemoTheme parsed))
                        {
                            Theme = parsed;
                        }
                        else
                        {
                            Deferred.Add($"Unknown theme '{theme}'; expected semi, fluent or simple. Using {Theme}.");
                        }
                    }

                    break;
                case "--variant":
                    if (TryTakeValue(args, ref i, flag, out string? variant))
                    {
                        string normalized = variant.ToLowerInvariant();
                        if (normalized is "light" or "dark" or "default")
                        {
                            Variant = normalized;
                        }
                        else
                        {
                            Deferred.Add($"Unknown variant '{variant}'; expected light, dark or default. Using {Variant}.");
                        }
                    }

                    break;
                case "--left":
                    if (TryTakeValue(args, ref i, flag, out string? left))
                    {
                        LeftPath = left;
                    }

                    break;
                case "--right":
                    if (TryTakeValue(args, ref i, flag, out string? right))
                    {
                        RightPath = right;
                    }

                    break;
                case "--unified":
                    Unified = true;
                    break;
                case "--log-level":
                    if (TryTakeValue(args, ref i, flag, out string? level))
                    {
                        if (Enum.TryParse(level, ignoreCase: true, out LogEventLevel parsedLevel))
                        {
                            MinimumLevel = parsedLevel;
                        }
                        else
                        {
                            Deferred.Add($"Unknown log level '{level}'. Using {MinimumLevel}.");
                        }
                    }

                    break;
                default:
                    Deferred.Add($"Unknown flag '{args[i]}' ignored.");
                    break;
            }
        }
    }

    /// <summary>Logs the deferred warnings and the active-flags summary. Call once the pipeline is configured.</summary>
    public static void LogSummary()
    {
        foreach (string warning in Deferred)
        {
            Log.Warning("[DebugFlags] {Warning}", warning);
        }

        Deferred.Clear();
        Log.Information(
            "[DebugFlags] active: theme={Theme} variant={Variant} left={Left} right={Right} unified={Unified} level={Level}",
            Theme, Variant, LeftPath ?? "(none)", RightPath ?? "(none)", Unified, MinimumLevel);
    }

    /// <summary>Restores every flag to its default. Test cleanup hook.</summary>
    internal static void ResetForTesting()
    {
        Theme = DemoTheme.Semi;
        Variant = "default";
        LeftPath = null;
        RightPath = null;
        Unified = false;
        MinimumLevel = LogEventLevel.Information;
        Deferred.Clear();
    }

    private static bool TryTakeValue(string[] args, ref int index, string flag, out string value)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            Deferred.Add($"Flag '{flag}' needs a value; ignored.");
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }
}
