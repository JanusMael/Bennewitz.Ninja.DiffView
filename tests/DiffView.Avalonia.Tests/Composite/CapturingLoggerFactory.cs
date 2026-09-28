using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>One captured log line: its category, level, rendered message and exception.</summary>
internal sealed record LogRecord(string Category, LogLevel Level, string Message, Exception? Exception)
{
    /// <summary>The message, the exception's own text and its inner texts, for a sentinel search.</summary>
    public string Everything
    {
        get
        {
            List<string> parts = [Message];
            for (Exception? ex = Exception; ex is not null; ex = ex.InnerException)
            {
                parts.Add(ex.Message);
            }

            return string.Join(" | ", parts);
        }
    }
}

/// <summary>
/// A logger factory whose loggers keep every line at or above <c>minimum</c>, so a test can search
/// the log — every line by default, and a host's usual <c>Information</c> when a test needs to see
/// what such a host is told.
/// </summary>
internal sealed class CapturingLoggerFactory(LogLevel minimum = LogLevel.Trace) : ILoggerFactory
{
    private readonly Lock _gate = new();
    private readonly List<LogRecord> _records = [];

    /// <summary>The level below which a line is neither enabled nor kept.</summary>
    public LogLevel Minimum { get; } = minimum;

    public IReadOnlyList<LogRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(this, categoryName);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private void Add(LogRecord record)
    {
        lock (_gate)
        {
            _records.Add(record);
        }
    }

    private sealed class CapturingLogger(CapturingLoggerFactory owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= owner.Minimum;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                owner.Add(new LogRecord(category, logLevel, formatter(state, exception), exception));
            }
        }
    }
}
