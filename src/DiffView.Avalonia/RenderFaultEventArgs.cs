using System.Globalization;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A decorator — a background renderer, a margin, the padding generator or the height primer —
/// threw. It has disabled itself so the frame still renders; the text is always visible. Raised
/// once per fault by <see cref="DiffPanePresenter.RenderFault"/>.
/// </summary>
public sealed class RenderFaultEventArgs : EventArgs
{
    /// <param name="source">The decorator that failed, by type name.</param>
    /// <param name="lineNumber">The 1-based line being processed, when known.</param>
    /// <param name="exception">What was thrown.</param>
    public RenderFaultEventArgs(string source, int? lineNumber, Exception exception)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentNullException.ThrowIfNull(exception);
        Source = source;
        LineNumber = lineNumber;
        Exception = exception;
    }

    /// <summary>The decorator that failed, by type name.</summary>
    public string Source { get; }

    /// <summary>The 1-based line being processed, when known.</summary>
    public int? LineNumber { get; }

    /// <summary>What was thrown.</summary>
    public Exception Exception { get; }

    /// <summary>The message the status strip shows. Carries no document text.</summary>
    public string Message => LineNumber is { } line
        ? DiffViewStrings.Format(DiffViewStrings.RenderFaultOnLine, Source, line.ToString(CultureInfo.CurrentCulture), Exception.Message)
        : DiffViewStrings.Format(DiffViewStrings.RenderFault, Source, Exception.Message);
}
