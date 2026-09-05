namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>A degraded-but-usable outcome of a build.</summary>
public enum DiffWarningCode
{
    /// <summary>A side uses <c>\r</c>-only or mixed line endings; the diff and the editor tolerate it, the user should know.</summary>
    MixedLineEndings,

    /// <summary>The inputs were too dissimilar to align at their size; they are shown unaligned.</summary>
    TooDifferentToAlign,

    /// <summary>Some modified rows have a line longer than <see cref="DiffOptions.MaxWordDiffLineLength"/> and get no word-level pieces.</summary>
    LongLinesSkipped,

    /// <summary>A side's bytes were decoded as Latin-1 because they were neither BOM-marked nor valid UTF-8.</summary>
    Latin1Fallback,
}

/// <summary>One warning with the message the status strip shows.</summary>
public sealed record DiffWarning(DiffWarningCode Code, string Message);

/// <summary>Why a build failed outright.</summary>
public enum DiffBuildErrorCode
{
    /// <summary>A side is binary; a text diff is not available.</summary>
    BinaryInput,

    /// <summary>The diff engine threw.</summary>
    DiffFailed,
}

/// <summary>A build that could not produce a document. Degraded outcomes are warnings, not exceptions.</summary>
public sealed class DiffBuildException : Exception
{
    /// <param name="code">What failed.</param>
    /// <param name="message">The message the error banner shows.</param>
    /// <param name="inner">The cause, when there is one.</param>
    public DiffBuildException(DiffBuildErrorCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    /// <summary>What failed.</summary>
    public DiffBuildErrorCode Code { get; }
}

/// <summary>What a build measured and counted, shown in the status strip.</summary>
public sealed record DiffDiagnostics
{
    /// <summary>Wall time of the build, all stages.</summary>
    public required TimeSpan BuildTime { get; init; }

    /// <summary>Rows in the alignment table.</summary>
    public required int RowCount { get; init; }

    /// <summary>Change blocks.</summary>
    public required int BlockCount { get; init; }

    /// <summary>Rows of kind <see cref="DiffLineKind.Inserted"/>.</summary>
    public required int Inserted { get; init; }

    /// <summary>Rows of kind <see cref="DiffLineKind.Deleted"/>.</summary>
    public required int Deleted { get; init; }

    /// <summary>Rows of kind <see cref="DiffLineKind.Modified"/>.</summary>
    public required int Modified { get; init; }

    /// <summary>Line-overlap similarity of the two sides, 0..1, as the gate measured it.</summary>
    public required double Similarity { get; init; }

    /// <summary>Whether the sides were aligned by the diff, or concatenated because the gate refused.</summary>
    public required bool Aligned { get; init; }

    /// <summary>The left side's probe.</summary>
    public required TextInfo LeftInfo { get; init; }

    /// <summary>The right side's probe.</summary>
    public required TextInfo RightInfo { get; init; }

    /// <summary>Whether the two sides are equal under the options: no blocks at all.</summary>
    public bool Identical => BlockCount == 0;
}

/// <summary>What a build produced.</summary>
public sealed record DiffBuildResult(SideBySideDocument Document, DiffDiagnostics Diagnostics, IReadOnlyList<DiffWarning> Warnings)
{
    /// <summary>Whether a warning with <paramref name="code"/> is present.</summary>
    public bool Has(DiffWarningCode code) => Warnings.Any(w => w.Code == code);
}
