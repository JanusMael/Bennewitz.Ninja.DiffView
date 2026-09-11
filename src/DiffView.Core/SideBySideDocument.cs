namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>What a line, a row or a block is.</summary>
public enum DiffLineKind
{
    /// <summary>Present on both sides, equal under the options.</summary>
    Unchanged,

    /// <summary>Present on the right only.</summary>
    Inserted,

    /// <summary>Present on the left only.</summary>
    Deleted,

    /// <summary>Present on both sides, different.</summary>
    Modified,
}

/// <summary>One line of one side: its kind and the row it sits in.</summary>
public readonly record struct DiffLine(DiffLineKind Kind, int Row);

/// <summary>
/// One aligned row: the line index on each side, or <c>null</c> where that side has a padding
/// row, and the row's kind. Never both <c>null</c>.
/// </summary>
public readonly record struct AlignedRow(int? LeftLine, int? RightLine, DiffLineKind Kind)
{
    /// <summary>The line <paramref name="side"/> has in this row, or <c>null</c> where it pads.</summary>
    public int? LineOf(DiffSide side) => side == DiffSide.Left ? LeftLine : RightLine;
}

/// <summary>A contiguous range of lines on one side; <see cref="Count"/> may be zero.</summary>
public readonly record struct LineRange(int Start, int Count)
{
    /// <summary>An empty range at <paramref name="at"/>.</summary>
    public static LineRange Empty(int at) => new(at, 0);

    /// <summary>Whether the range has no lines.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>One past the last line.</summary>
    public int End => Start + Count;

    /// <summary>Whether <paramref name="line"/> is inside the range.</summary>
    public bool Contains(int line) => line >= Start && line < End;
}

/// <summary>
/// A maximal run of changed rows: where it is in the row table, which lines of each side it
/// covers (either range may be empty), and how many rows of each kind it holds. Copy-to-side
/// is a replace over these ranges.
/// </summary>
public sealed record ChangeBlock(
    int Index,
    DiffLineKind Kind,
    int FirstRow,
    int LastRow,
    LineRange LeftLines,
    LineRange RightLines,
    int InsertedCount,
    int DeletedCount,
    int ModifiedCount)
{
    /// <summary>Rows in the block.</summary>
    public int RowCount => LastRow - FirstRow + 1;

    /// <summary>The lines <paramref name="side"/> has in the block; empty where it has none.</summary>
    public LineRange LinesFor(DiffSide side) => side == DiffSide.Left ? LeftLines : RightLines;
}

/// <summary>One side's per-line metadata, indexed by line. The text itself lives in the editor's document.</summary>
public sealed class DiffPane
{
    internal DiffPane(DiffLine[] lines, TextInfo info)
    {
        Lines = lines;
        Info = info;
    }

    /// <summary>Kind and row per line; index is the line number.</summary>
    public IReadOnlyList<DiffLine> Lines { get; }

    /// <summary>What probing the side found.</summary>
    public TextInfo Info { get; }
}

/// <summary>
/// The source-indexed diff model: per-side line metadata, the alignment table, the change
/// blocks, and a version stamp that changes with every build so caches keyed by row can tell
/// a stale row from a current one. Padding is derived from the rows, never stored as text.
/// </summary>
public sealed class SideBySideDocument
{
    private static int s_nextVersion;

    private readonly Lazy<PaddingTable> _padding;

    internal SideBySideDocument(DiffPane left, DiffPane right, AlignedRow[] rows, IReadOnlyList<ChangeBlock> blocks)
    {
        Left = left;
        Right = right;
        Rows = rows;
        Blocks = blocks;
        Version = Interlocked.Increment(ref s_nextVersion);
        _padding = new Lazy<PaddingTable>(() => PaddingTable.Build(this), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The left side.</summary>
    public DiffPane Left { get; }

    /// <summary>The right side.</summary>
    public DiffPane Right { get; }

    /// <summary>The alignment table, in row order.</summary>
    public IReadOnlyList<AlignedRow> Rows { get; }

    /// <summary>The change blocks, disjoint and ordered, covering every non-unchanged row.</summary>
    public IReadOnlyList<ChangeBlock> Blocks { get; }

    /// <summary>Unique per build within the process.</summary>
    public int Version { get; }

    /// <summary>The side's pane.</summary>
    public DiffPane Pane(DiffSide side) => side == DiffSide.Left ? Left : Right;

    /// <summary>The line index a row holds on <paramref name="side"/>, or <c>null</c> for padding.</summary>
    public static int? LineOf(AlignedRow row, DiffSide side) => side == DiffSide.Left ? row.LeftLine : row.RightLine;

    internal PaddingTable PaddingOf => _padding.Value;

    /// <summary>Padding rows per line, derived once from the rows.</summary>
    internal sealed class PaddingTable
    {
        private PaddingTable(int[] beforeLeft, int[] beforeRight, int trailingLeft, int trailingRight)
        {
            BeforeLeft = beforeLeft;
            BeforeRight = beforeRight;
            TrailingLeft = trailingLeft;
            TrailingRight = trailingRight;
        }

        public int[] BeforeLeft { get; }

        public int[] BeforeRight { get; }

        public int TrailingLeft { get; }

        public int TrailingRight { get; }

        public static PaddingTable Build(SideBySideDocument document)
        {
            int[] beforeLeft = new int[document.Left.Lines.Count];
            int[] beforeRight = new int[document.Right.Lines.Count];
            int pendingLeft = 0;
            int pendingRight = 0;

            foreach (AlignedRow row in document.Rows)
            {
                if (row.LeftLine is { } left)
                {
                    beforeLeft[left] = pendingLeft;
                    pendingLeft = 0;
                }
                else
                {
                    pendingLeft++;
                }

                if (row.RightLine is { } right)
                {
                    beforeRight[right] = pendingRight;
                    pendingRight = 0;
                }
                else
                {
                    pendingRight++;
                }
            }

            return new PaddingTable(beforeLeft, beforeRight, pendingLeft, pendingRight);
        }
    }
}

/// <summary>
/// Padding rows a side needs, read from the alignment table: rows where the other side has a
/// line and this side has none. Computed once per document, on first use.
/// </summary>
public static class Padding
{
    /// <summary>Padding rows immediately above <paramref name="line"/> on <paramref name="side"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> is not a line of that side.</exception>
    public static int Before(SideBySideDocument document, DiffSide side, int line)
    {
        ArgumentNullException.ThrowIfNull(document);
        int[] table = side == DiffSide.Left ? document.PaddingOf.BeforeLeft : document.PaddingOf.BeforeRight;
        if (line < 0 || line >= table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(line), line, $"{side} has {table.Length} lines.");
        }

        return table[line];
    }

    /// <summary>Padding rows after the last line on <paramref name="side"/>.</summary>
    public static int Trailing(SideBySideDocument document, DiffSide side)
    {
        ArgumentNullException.ThrowIfNull(document);
        return side == DiffSide.Left ? document.PaddingOf.TrailingLeft : document.PaddingOf.TrailingRight;
    }
}
