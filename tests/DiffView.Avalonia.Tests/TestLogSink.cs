using Avalonia.Logging;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Collects Avalonia's internal log events at Warning and above so a test can assert that
/// rendering produced none — a binding that could not resolve is a test failure here, not a
/// line in a console nobody reads.
/// </summary>
internal sealed class TestLogSink : ILogSink
{
    private readonly Lock _gate = new();
    private readonly List<LogEntry> _entries = [];

    public static TestLogSink Instance { get; } = new();

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return level >= LogEventLevel.Warning;
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        Add(level, area, source, messageTemplate, []);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        Add(level, area, source, messageTemplate, propertyValues);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// Fails when any Warning-or-above event was logged in the given areas (every area when none
    /// is given), listing them.
    /// </summary>
    public static void AssertNoWarnings(params string[] areas)
    {
        IReadOnlyList<LogEntry> offending = Instance.Snapshot()
            .Where(e => areas.Length == 0 || areas.Contains(e.Area, StringComparer.Ordinal))
            .ToList();

        if (offending.Count == 0)
        {
            return;
        }

        string listing = string.Join(Environment.NewLine, offending.Select(e => $"  [{e.Level}] {e.Area}: {e.Message} ({e.Source})"));
        Assert.Fail($"Avalonia logged {offending.Count} warning(s) during the test:{Environment.NewLine}{listing}");
    }

    private void Add(LogEventLevel level, string area, object? source, string messageTemplate, object?[] propertyValues)
    {
        string message = propertyValues.Length == 0
            ? messageTemplate
            : $"{messageTemplate} | {string.Join(", ", propertyValues.Select(v => v?.ToString() ?? "(null)"))}";

        lock (_gate)
        {
            _entries.Add(new LogEntry(level, area, source?.GetType().Name ?? "(no source)", message));
        }
    }
}

internal sealed record LogEntry(LogEventLevel Level, string Area, string Source, string Message);
