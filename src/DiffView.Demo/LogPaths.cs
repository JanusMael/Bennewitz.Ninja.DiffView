namespace Bennewitz.Ninja.DiffView.Demo;

/// <summary>
/// Where the demo writes its logs: the per-user, OS-conventional location. Properties, not
/// <c>static readonly</c> fields, so nothing about the host is captured at type initialization.
/// </summary>
internal static class LogPaths
{
    public static string LogsDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiffView", "logs");
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(home, "Library", "Logs", "DiffView");
            }

            string? xdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            string state = string.IsNullOrEmpty(xdgState) ? Path.Combine(home, ".local", "state") : xdgState;
            return Path.Combine(state, "DiffView", "logs");
        }
    }
}
