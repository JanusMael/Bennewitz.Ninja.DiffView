using System.Globalization;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;
using TextInfo = Bennewitz.Ninja.DiffView.Core.TextInfo;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The one place the library formats log lines, so the rule is enforced in one place: a line
/// carries counts, line numbers, lengths, paths, codes and timings — never document text, not a
/// line, not a match, not a piece, because the content under comparison may be a secret. Every
/// state transition is <c>Information</c>, every warning <c>Warning</c>, every fault
/// <c>Error</c> with the exception attached, cancellation and supersession <c>Debug</c>.
/// </summary>
internal static class DiffViewLog
{
    public static void StateChanged(ILogger? logger, DiffViewState from, DiffViewState to, string? reason)
    {
        logger?.LogInformation("State {From} → {To}{Reason}", from, to, reason is null ? string.Empty : ": " + reason);
    }

    public static void SourceAssigned(ILogger? logger, DiffSide side, PaneSource? source)
    {
        if (source is null)
        {
            logger?.LogInformation("{Side} source cleared", side);
            return;
        }

        TextInfo info = TextProbe.Probe(source);
        logger?.LogInformation(
            "{Side} source: {Lines} lines, {Length} chars, {Encoding}, {LineEnding}, binary={Binary}, path={Path}",
            side, info.LineCount, info.Length, info.Encoding?.WebName ?? "text", info.LineEnding, info.IsBinary, source.Path ?? "-");
    }

    public static void BuildStarted(ILogger? logger, int generation, DiffOptions options)
    {
        logger?.LogDebug(
            "Build {Generation} started: ignoreWhitespace={IgnoreWhitespace} ignoreCase={IgnoreCase} wordDiff={WordDiff} force={Force}",
            generation, options.IgnoreWhitespace, options.IgnoreCase, options.WordDiff, options.ForceAlignment);
    }

    public static void BuildCompleted(ILogger? logger, int generation, DiffDiagnostics diagnostics, int warningCount)
    {
        logger?.LogInformation(
            "Build {Generation} completed in {Elapsed} ms: {Rows} rows, {Blocks} blocks (+{Inserted} -{Deleted} ~{Modified}), similarity {Similarity}, aligned={Aligned}, {Warnings} warning(s)",
            generation,
            diagnostics.BuildTime.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture),
            diagnostics.RowCount, diagnostics.BlockCount, diagnostics.Inserted, diagnostics.Deleted, diagnostics.Modified,
            diagnostics.Similarity.ToString("F3", CultureInfo.InvariantCulture), diagnostics.Aligned, warningCount);
    }

    public static void BuildWarning(ILogger? logger, DiffWarning warning)
    {
        // The warning messages are the builder's own sentences over counts and codes.
        logger?.LogWarning("Build warning {Code}: {Message}", warning.Code, warning.Message);
    }

    public static void BuildFailed(ILogger? logger, int generation, DiffBuildException exception)
    {
        logger?.LogError(exception, "Build {Generation} failed with {Code}", generation, exception.Code);
    }

    public static void BuildCancelled(ILogger? logger, int generation)
    {
        logger?.LogDebug("Build {Generation} cancelled", generation);
    }

    public static void BuildSuperseded(ILogger? logger, int generation, int by)
    {
        logger?.LogDebug("Build {Generation} superseded by build {By}; its result is discarded", generation, by);
    }

    public static void RenderFault(ILogger? logger, DiffSide side, RenderFaultEventArgs fault)
    {
        logger?.LogError(
            fault.Exception,
            "{Decorator} on the {Side} pane failed at line {Line} and is disabled until the next model",
            fault.Source, side, fault.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "-");
    }
}
