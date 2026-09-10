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

    public static void FindStarted(ILogger? logger, int generation, int queryLength, FindOptions options)
    {
        // The query is not logged, only its length: Ctrl+F pre-fills it from the pane's selection,
        // so it may be a piece of the document under comparison.
        logger?.LogDebug(
            "Find {Generation} started: {Length} characters, scope={Scope}, matchCase={MatchCase}, wholeWord={WholeWord}, regex={Regex}, changedRowsOnly={ChangedRowsOnly}",
            generation, queryLength, options.Scope, options.MatchCase, options.WholeWord, options.UseRegex, options.ChangedRowsOnly);
    }

    public static void FindCompleted(ILogger? logger, int generation, FindResult result, TimeSpan elapsed)
    {
        logger?.LogInformation(
            "Find {Generation} completed in {Elapsed} ms: {Matches} match(es), {Left} left, {Right} right, truncated={Truncated}",
            generation,
            elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture),
            result.Matches.Count, result.LeftCount, result.RightCount, result.Truncated);
    }

    public static void FindFailed(ILogger? logger, int generation, Exception? exception)
    {
        // The engine's own message quotes the pattern, which is why only the fact is logged.
        if (exception is null)
        {
            logger?.LogWarning("Find {Generation} could not run: the query is not a valid pattern, or matching timed out", generation);
        }
        else
        {
            // The type, not the exception: a message from the matcher can quote the pattern.
            logger?.LogError("Find {Generation} threw {Exception} and was abandoned", generation, exception.GetType().FullName);
        }
    }

    public static void FindCancelled(ILogger? logger, int generation)
    {
        logger?.LogDebug("Find {Generation} cancelled", generation);
    }

    public static void FindSuperseded(ILogger? logger, int generation, int by)
    {
        logger?.LogDebug("Find {Generation} superseded by find {By}; its result is discarded", generation, by);
    }

    public static void SyntaxInstalled(ILogger? logger, DiffSide? side, string languageId)
    {
        // The language, never the file's text; the path is already in the source line above.
        logger?.LogInformation("Syntax highlighting on the {Side} pane: {Language}", Pane(side), languageId);
    }

    public static void SyntaxUnavailable(ILogger? logger, DiffSide? side, string extension)
    {
        logger?.LogDebug("No grammar claims {Extension}; the {Side} pane stays plain text", extension.Length == 0 ? "-" : extension, Pane(side));
    }

    /// <summary>
    /// Two commands share one gesture. Avalonia decides which fires; saying so is better than
    /// refusing a binding the host asked for or dropping one without a word. Command names and a
    /// gesture, never document text.
    /// </summary>
    public static void KeyGestureConflict(ILogger? logger, string gesture, string first, string second)
    {
        logger?.LogWarning("{Gesture} is bound to both {First} and {Second}; which one fires is Avalonia's choice", gesture, first, second);
    }

    public static void RenderFault(ILogger? logger, DiffSide? side, RenderFaultEventArgs fault)
    {
        // The subject is a grammar's language, never document text; a decorator that failed over a
        // line reports the line instead, and one that failed over neither reports "-" for both.
        logger?.LogError(
            fault.Exception,
            "{Decorator} on the {Side} pane failed at line {Line} for {Subject} and was disabled",
            fault.Source, Pane(side), fault.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "-", fault.Subject ?? "-");
    }

    /// <summary>What a pane is called in a log line: its side, or "unified" for the inline view's one pane, which is neither.</summary>
    private static object Pane(DiffSide? side) => side is { } known ? known : "unified";
}
