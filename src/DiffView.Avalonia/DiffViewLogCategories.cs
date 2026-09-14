namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The logger categories the library logs under. A host creates one <c>ILogger</c> per category
/// it wants and hands it to the control; the library never writes a file, shows a dialog or
/// touches the console, and it never logs document text — counts, line numbers, lengths, paths
/// and timings only.
/// </summary>
public static class DiffViewLogCategories
{
    /// <summary>Builds: state transitions, warnings, timings.</summary>
    public const string Build = "DiffView.Build";

    /// <summary>Rendering and layout: decorator faults, priming.</summary>
    public const string Render = "DiffView.Render";

    /// <summary>Find: counts, truncation, pattern errors.</summary>
    public const string Find = "DiffView.Find";

    /// <summary>Theming: unresolved tokens, grammar installs.</summary>
    public const string Theme = "DiffView.Theme";
}
