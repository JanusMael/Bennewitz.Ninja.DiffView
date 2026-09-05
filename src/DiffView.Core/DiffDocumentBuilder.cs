using System.Diagnostics;
using DiffPlex;
using DiffPlex.Chunkers;
using DiffPlex.Model;

namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>
/// Builds a <see cref="SideBySideDocument"/> from two sources in stages — probe, similarity
/// gate, line diff, rows, blocks — honouring cancellation between stages (the Myers run itself
/// cannot be interrupted, which is why the gate runs first). Genuine failures throw
/// <see cref="DiffBuildException"/>; degraded outcomes come back as warnings.
/// </summary>
public static class DiffDocumentBuilder
{
    /// <summary>Builds the document.</summary>
    /// <exception cref="DiffBuildException">A side is binary, or the diff engine failed.</exception>
    /// <exception cref="OperationCanceledException">Cancelled between stages.</exception>
    public static DiffBuildResult Build(PaneSource left, PaneSource right, DiffOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        options ??= DiffOptions.Default;

        Stopwatch stopwatch = Stopwatch.StartNew();
        List<DiffWarning> warnings = [];

        // Stage 1: probe. Binary is a failure, not a warning.
        TextInfo leftInfo = TextProbe.Probe(left);
        TextInfo rightInfo = TextProbe.Probe(right);
        if (leftInfo.IsBinary || rightInfo.IsBinary)
        {
            string which = leftInfo.IsBinary && rightInfo.IsBinary ? "Both sides are" : leftInfo.IsBinary ? "Left side is" : "Right side is";
            throw new DiffBuildException(DiffBuildErrorCode.BinaryInput, $"{which} binary — text diff is not available.");
        }

        AddLineEndingWarnings(warnings, leftInfo, rightInfo);
        if (leftInfo.Latin1Fallback || rightInfo.Latin1Fallback)
        {
            string which = leftInfo.Latin1Fallback && rightInfo.Latin1Fallback ? "Both sides were" : leftInfo.Latin1Fallback ? "Left side was" : "Right side was";
            warnings.Add(new DiffWarning(DiffWarningCode.Latin1Fallback, $"{which} not valid UTF-8 and decoded as Latin-1."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Stage 2: the similarity gate, O(N) over line hashes.
        string[] leftLines = LineSplitter.Split(left.Text);
        string[] rightLines = LineSplitter.Split(right.Text);
        double similarity = SimilarityGate.Measure(leftLines, rightLines, options);
        bool gated = leftLines.Length + rightLines.Length >= options.AlignmentSizeThreshold
                     && similarity < options.AlignmentSimilarityFloor
                     && !options.ForceAlignment;

        cancellationToken.ThrowIfCancellationRequested();

        // Stage 3: the line diff (skipped when the gate refuses), then rows and blocks.
        AlignedRow[] rows;
        if (gated)
        {
            warnings.Add(new DiffWarning(
                DiffWarningCode.TooDifferentToAlign,
                $"Files are too different to align ({similarity:P0} of lines in common) — shown unaligned."));
            rows = RowBuilder.Unaligned(leftLines.Length, rightLines.Length);
        }
        else
        {
            IList<DiffBlock> blocks = Diff(left.Text, right.Text, leftLines.Length, rightLines.Length, options);
            cancellationToken.ThrowIfCancellationRequested();
            rows = RowBuilder.FromBlocks(blocks, leftLines.Length, rightLines.Length);
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ChangeBlock> changeBlocks = RowBuilder.Blocks(rows);
        DiffPane leftPane = new(RowBuilder.Lines(rows, DiffSide.Left, leftLines.Length), leftInfo);
        DiffPane rightPane = new(RowBuilder.Lines(rows, DiffSide.Right, rightLines.Length), rightInfo);
        SideBySideDocument document = new(leftPane, rightPane, rows, changeBlocks);

        // Long lines on modified rows get no word-level pieces; say so once, with the count.
        if (options.WordDiff != WordDiffMode.Off)
        {
            int longLines = 0;
            foreach (AlignedRow row in rows)
            {
                if (row.Kind == DiffLineKind.Modified
                    && (leftLines[row.LeftLine!.Value].Length > options.MaxWordDiffLineLength
                        || rightLines[row.RightLine!.Value].Length > options.MaxWordDiffLineLength))
                {
                    longLines++;
                }
            }

            if (longLines > 0)
            {
                warnings.Add(new DiffWarning(
                    DiffWarningCode.LongLinesSkipped,
                    $"Word-level highlighting skipped on {longLines} line{(longLines == 1 ? string.Empty : "s")} longer than {options.MaxWordDiffLineLength:N0} characters."));
            }
        }

        stopwatch.Stop();
        DiffDiagnostics diagnostics = new()
        {
            BuildTime = stopwatch.Elapsed,
            RowCount = rows.Length,
            BlockCount = changeBlocks.Count,
            Inserted = rows.Count(r => r.Kind == DiffLineKind.Inserted),
            Deleted = rows.Count(r => r.Kind == DiffLineKind.Deleted),
            Modified = rows.Count(r => r.Kind == DiffLineKind.Modified),
            Similarity = similarity,
            Aligned = !gated,
            LeftInfo = leftInfo,
            RightInfo = rightInfo,
        };

        return new DiffBuildResult(document, diagnostics, warnings);
    }

    private static void AddLineEndingWarnings(List<DiffWarning> warnings, TextInfo leftInfo, TextInfo rightInfo)
    {
        bool leftIrregular = leftInfo.HasIrregularLineEndings;
        bool rightIrregular = rightInfo.HasIrregularLineEndings;
        if (!leftIrregular && !rightIrregular)
        {
            return;
        }

        string which = leftIrregular && rightIrregular ? "Both sides use" : leftIrregular ? "Left side uses" : "Right side uses";
        LineEnding ending = leftIrregular ? leftInfo.LineEnding : rightInfo.LineEnding;
        string what = ending == LineEnding.Cr ? "CR-only line endings" : "mixed line endings";
        warnings.Add(new DiffWarning(DiffWarningCode.MixedLineEndings, $"{which} {what}."));
    }

    /// <summary>
    /// DiffPlex on the two texts, except that DiffPlex sees the empty string as zero lines
    /// where an editor (and <see cref="LineSplitter"/>) sees one empty line; an empty side is
    /// therefore diffed here as its one line against the other side's lines.
    /// </summary>
    private static IList<DiffBlock> Diff(string leftText, string rightText, int leftCount, int rightCount, DiffOptions options)
    {
        if (leftText.Length == 0 || rightText.Length == 0)
        {
            if (leftText.Length == 0 && rightText.Length == 0)
            {
                return [];
            }

            return [new DiffBlock(0, leftCount, 0, rightCount)];
        }

        try
        {
            DiffResult result = Differ.Instance.CreateDiffs(leftText, rightText, options.IgnoreWhitespace, options.IgnoreCase, LineChunker.Instance);
            if (result.PiecesOld.Count != leftCount || result.PiecesNew.Count != rightCount)
            {
                throw new DiffBuildException(
                    DiffBuildErrorCode.DiffFailed,
                    $"The diff engine split the text into {result.PiecesOld.Count}/{result.PiecesNew.Count} lines; the model expected {leftCount}/{rightCount}.");
            }

            return result.DiffBlocks;
        }
        catch (Exception ex) when (ex is not DiffBuildException)
        {
            throw new DiffBuildException(DiffBuildErrorCode.DiffFailed, "The diff engine failed: " + ex.Message, ex);
        }
    }
}

/// <summary>
/// The similarity gate: the Dice coefficient of the two sides' line multisets under the
/// options' normalisation, in one linear pass over each side.
/// </summary>
public static class SimilarityGate
{
    /// <summary>The similarity of two line lists, 0 (nothing in common) to 1 (the same multiset).</summary>
    public static double Measure(IReadOnlyList<string> leftLines, IReadOnlyList<string> rightLines, DiffOptions options)
    {
        ArgumentNullException.ThrowIfNull(leftLines);
        ArgumentNullException.ThrowIfNull(rightLines);
        ArgumentNullException.ThrowIfNull(options);

        int total = leftLines.Count + rightLines.Count;
        if (total == 0)
        {
            return 1.0;
        }

        StringComparer comparer = options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, int> counts = new(comparer);
        foreach (string line in leftLines)
        {
            string key = options.IgnoreWhitespace ? line.Trim() : line;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        int overlap = 0;
        foreach (string line in rightLines)
        {
            string key = options.IgnoreWhitespace ? line.Trim() : line;
            if (counts.TryGetValue(key, out int remaining) && remaining > 0)
            {
                counts[key] = remaining - 1;
                overlap++;
            }
        }

        return 2.0 * overlap / total;
    }
}

/// <summary>Turns DiffPlex blocks into rows, and rows into lines and blocks.</summary>
internal static class RowBuilder
{
    /// <summary>
    /// Rows from the diff blocks: unchanged runs between blocks pair line for line; inside a
    /// block the first <c>min(deleted, inserted)</c> lines pair as modified, and the remainder
    /// are deleted or inserted rows with padding on the other side.
    /// </summary>
    public static AlignedRow[] FromBlocks(IList<DiffBlock> blocks, int leftCount, int rightCount)
    {
        List<AlignedRow> rows = new(Math.Max(leftCount, rightCount));
        int left = 0;
        int right = 0;

        foreach (DiffBlock block in blocks)
        {
            while (left < block.DeleteStartA)
            {
                rows.Add(new AlignedRow(left++, right++, DiffLineKind.Unchanged));
            }

            int paired = Math.Min(block.DeleteCountA, block.InsertCountB);
            for (int i = 0; i < paired; i++)
            {
                rows.Add(new AlignedRow(left++, right++, DiffLineKind.Modified));
            }

            for (int i = paired; i < block.DeleteCountA; i++)
            {
                rows.Add(new AlignedRow(left++, null, DiffLineKind.Deleted));
            }

            for (int i = paired; i < block.InsertCountB; i++)
            {
                rows.Add(new AlignedRow(null, right++, DiffLineKind.Inserted));
            }
        }

        while (left < leftCount && right < rightCount)
        {
            rows.Add(new AlignedRow(left++, right++, DiffLineKind.Unchanged));
        }

        // DiffPlex reports every difference, so both sides are exhausted together here; the
        // guards keep the invariant "every line in exactly one row" even if they were not.
        while (left < leftCount)
        {
            rows.Add(new AlignedRow(left++, null, DiffLineKind.Deleted));
        }

        while (right < rightCount)
        {
            rows.Add(new AlignedRow(null, right++, DiffLineKind.Inserted));
        }

        return [.. rows];
    }

    /// <summary>The unaligned table: every left line deleted, then every right line inserted.</summary>
    public static AlignedRow[] Unaligned(int leftCount, int rightCount)
    {
        AlignedRow[] rows = new AlignedRow[leftCount + rightCount];
        for (int i = 0; i < leftCount; i++)
        {
            rows[i] = new AlignedRow(i, null, DiffLineKind.Deleted);
        }

        for (int i = 0; i < rightCount; i++)
        {
            rows[leftCount + i] = new AlignedRow(null, i, DiffLineKind.Inserted);
        }

        return rows;
    }

    /// <summary>Per-line metadata for one side, from the rows.</summary>
    public static DiffLine[] Lines(AlignedRow[] rows, DiffSide side, int count)
    {
        DiffLine[] lines = new DiffLine[count];
        for (int row = 0; row < rows.Length; row++)
        {
            if (SideBySideDocument.LineOf(rows[row], side) is { } line)
            {
                lines[line] = new DiffLine(rows[row].Kind, row);
            }
        }

        return lines;
    }

    /// <summary>Maximal runs of changed rows, each with its per-side line ranges and counts.</summary>
    public static IReadOnlyList<ChangeBlock> Blocks(AlignedRow[] rows)
    {
        List<ChangeBlock> blocks = [];
        int row = 0;
        while (row < rows.Length)
        {
            if (rows[row].Kind == DiffLineKind.Unchanged)
            {
                row++;
                continue;
            }

            int first = row;
            int inserted = 0;
            int deleted = 0;
            int modified = 0;
            int? leftStart = null;
            int? rightStart = null;
            int leftCount = 0;
            int rightCount = 0;

            while (row < rows.Length && rows[row].Kind != DiffLineKind.Unchanged)
            {
                AlignedRow current = rows[row];
                switch (current.Kind)
                {
                    case DiffLineKind.Inserted:
                        inserted++;
                        break;
                    case DiffLineKind.Deleted:
                        deleted++;
                        break;
                    default:
                        modified++;
                        break;
                }

                if (current.LeftLine is { } l)
                {
                    leftStart ??= l;
                    leftCount++;
                }

                if (current.RightLine is { } r)
                {
                    rightStart ??= r;
                    rightCount++;
                }

                row++;
            }

            // An empty range sits where the side's next line would be — after the last line any
            // earlier row placed on that side — so a copy-to-side inserts at the right spot.
            LineRange leftRange = leftStart is { } ls ? new LineRange(ls, leftCount) : LineRange.Empty(NextLine(rows, first, DiffSide.Left));
            LineRange rightRange = rightStart is { } rs ? new LineRange(rs, rightCount) : LineRange.Empty(NextLine(rows, first, DiffSide.Right));

            DiffLineKind kind = deleted == 0 && modified == 0 ? DiffLineKind.Inserted
                : inserted == 0 && modified == 0 ? DiffLineKind.Deleted
                : DiffLineKind.Modified;

            blocks.Add(new ChangeBlock(blocks.Count, kind, first, row - 1, leftRange, rightRange, inserted, deleted, modified));
        }

        return blocks;
    }

    /// <summary>The line index on <paramref name="side"/> that follows the rows before <paramref name="row"/>.</summary>
    private static int NextLine(AlignedRow[] rows, int row, DiffSide side)
    {
        for (int i = row - 1; i >= 0; i--)
        {
            if (SideBySideDocument.LineOf(rows[i], side) is { } line)
            {
                return line + 1;
            }
        }

        return 0;
    }
}
