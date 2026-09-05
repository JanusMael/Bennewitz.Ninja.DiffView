using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Demo;

/// <summary>
/// The <see cref="ILoggerFactory"/> the demo hands to the DiffView library, bridged onto the
/// Serilog pipeline the diagnostics package configures. Null until <see cref="Initialize"/>.
/// </summary>
internal static class DemoLogging
{
    public static ILoggerFactory Factory { get; private set; } = NullLoggerFactory.Instance;

    public static void Initialize()
    {
        Factory = new SerilogLoggerFactory(Serilog.Log.Logger);
    }
}
