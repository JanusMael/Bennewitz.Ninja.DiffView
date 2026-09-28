using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Demo;

/// <summary>
/// The <see cref="ILoggerFactory"/> the demo hands to the DiffView library, bridged onto the
/// Serilog pipeline the diagnostics package configures. Null until <see cref="Initialize()"/>.
/// </summary>
/// <remarks>
/// Plan 00029: the library writes what its user did under
/// <see cref="DiffViewLogCategories.Interaction"/> at <c>Debug</c>, off unless a host switches the
/// category on. The demo switches it on, so that a by-hand pass reads back from the log. It does so by
/// writing that one category at <c>Information</c>, the level the demo's log keeps, because the
/// diagnostics package owns the Serilog configuration and offers no per-source minimum to lower.
/// </remarks>
internal static class DemoLogging
{
    public static ILoggerFactory Factory { get; private set; } = NullLoggerFactory.Instance;

    public static void Initialize() => Initialize(Serilog.Log.Logger);

    /// <summary>The factory over a given Serilog logger — the process's own, or a test's.</summary>
    internal static void Initialize(Serilog.ILogger logger)
    {
        Factory = new InteractionAtInformation(new SerilogLoggerFactory(logger));
    }

    /// <summary>Back to the factory that logs nothing. Test cleanup hook.</summary>
    internal static void ResetForTesting() => Factory = NullLoggerFactory.Instance;

    /// <summary>Every category as it comes, except the library's interaction lines, raised from Debug to Information.</summary>
    private sealed class InteractionAtInformation(ILoggerFactory inner) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName)
        {
            ILogger logger = inner.CreateLogger(categoryName);
            return categoryName == DiffViewLogCategories.Interaction ? new Raised(logger) : logger;
        }

        public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

        public void Dispose() => inner.Dispose();

        private sealed class Raised(ILogger inner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => inner.BeginScope(state);

            public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(Raise(logLevel));

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                inner.Log(Raise(logLevel), eventId, state, exception, formatter);

            private static LogLevel Raise(LogLevel level) => level == LogLevel.Debug ? LogLevel.Information : level;
        }
    }
}
