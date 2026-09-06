using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>The one state the composite control is always in; the status strip renders it.</summary>
public enum DiffViewState
{
    /// <summary>A side has no source yet.</summary>
    Empty,

    /// <summary>A build is running; the previous result, if any, stays on screen marked stale.</summary>
    Building,

    /// <summary>The last build succeeded with nothing to warn about.</summary>
    Ready,

    /// <summary>The last build succeeded with a warning, or a decorator has faulted; the message says which.</summary>
    Degraded,

    /// <summary>The last build failed; the message and a Retry are shown.</summary>
    Failed,
}

/// <summary>The banner above the panes, when one is shown.</summary>
public enum DiffBannerKind
{
    /// <summary>No banner.</summary>
    None,

    /// <summary>The build failed; the banner carries the message and Retry.</summary>
    Error,

    /// <summary>The sides were too different to align at their size; the banner offers Force.</summary>
    TooDifferentToAlign,

    /// <summary>The sides are identical under the options: a result, not a failure.</summary>
    Identical,
}

/// <summary>A build produced a document.</summary>
public sealed class DiffBuildCompletedEventArgs : EventArgs
{
    /// <param name="result">What the build produced.</param>
    public DiffBuildCompletedEventArgs(DiffBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>What the build produced.</summary>
    public DiffBuildResult Result { get; }
}

/// <summary>A search finished: matches, counts, truncation, or a query that could not run.</summary>
public sealed class DiffFindCompletedEventArgs : EventArgs
{
    /// <param name="result">What the search found.</param>
    public DiffFindCompletedEventArgs(FindResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>What the search found; <see cref="FindResult.Error"/> is set for a query that could not run.</summary>
    public FindResult Result { get; }
}

/// <summary>A build failed outright.</summary>
public sealed class DiffBuildFailedEventArgs : EventArgs
{
    /// <param name="exception">Why.</param>
    public DiffBuildFailedEventArgs(DiffBuildException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    /// <summary>Why.</summary>
    public DiffBuildException Exception { get; }
}
