namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>
/// One line of the unified view: which side it was taken from, that side's 0-based line, the
/// model row it belongs to, and how it reads. A context line is the left side's; a changed row
/// contributes its left line and its right line as two lines, both carrying the row's kind, so
/// a modified pair keeps its word-level pieces.
/// </summary>
public readonly record struct InlineLine(DiffSide Side, int SourceLine, int Row, DiffLineKind Kind);

/// <summary>
/// The unified — inline — reading of a <see cref="SideBySideDocument"/>: one line table over
/// both sides, in the order a unified diff shows them. Context rows contribute one line; a
/// maximal run of changed rows contributes every left line of the run, then every right line,
/// which is the order <c>diff -u</c> prints and the order that keeps a block's removals and
/// additions together.
/// </summary>
/// <remarks>
/// The table holds no text: a line names its side and that side's line, and the text stays in
/// that side's document. <see cref="LineOf"/> maps back — a right line of a context row is not
/// displayed and has no unified line, which is what a search over both sides is filtered
/// through.
/// </remarks>
public sealed class InlineDocument
{
    private readonly InlineLine[] _lines;
    private readonly int[] _leftToLine;
    private readonly int[] _rightToLine;
    private readonly LineRange[] _blocks;

    private InlineDocument(SideBySideDocument model, InlineLine[] lines, int[] leftToLine, int[] rightToLine, LineRange[] blocks)
    {
        Model = model;
        _lines = lines;
        _leftToLine = leftToLine;
        _rightToLine = rightToLine;
        _blocks = blocks;
    }

    /// <summary>The model this is a reading of.</summary>
    public SideBySideDocument Model { get; }

    /// <summary>The unified lines, in display order.</summary>
    public IReadOnlyList<InlineLine> Lines => _lines;

    /// <summary>The model's build stamp, so a cache keyed by line can tell a stale line from a current one.</summary>
    public int Version => Model.Version;

    /// <summary>Builds the unified table of <paramref name="model"/>.</summary>
    /// <param name="model">The model to read unified.</param>
    public static InlineDocument Build(SideBySideDocument model)
    {
        ArgumentNullException.ThrowIfNull(model);

        IReadOnlyList<AlignedRow> rows = model.Rows;
        int[] leftToLine = Filled(model.Left.Lines.Count);
        int[] rightToLine = Filled(model.Right.Lines.Count);
        List<InlineLine> lines = new(rows.Count);

        for (int row = 0; row < rows.Count;)
        {
            AlignedRow aligned = rows[row];
            if (aligned.Kind == DiffLineKind.Unchanged)
            {
                // Both sides hold the row's text; the left is the one shown, so a search hit on
                // the right line of a context row has nowhere to land.
                if (aligned.LeftLine is { } context)
                {
                    Add(lines, leftToLine, rightToLine, new InlineLine(DiffSide.Left, context, row, DiffLineKind.Unchanged));
                }
                else if (aligned.RightLine is { } only)
                {
                    Add(lines, leftToLine, rightToLine, new InlineLine(DiffSide.Right, only, row, DiffLineKind.Unchanged));
                }

                row++;
                continue;
            }

            // A maximal run of changed rows — a change block — prints as its removals then its
            // additions, so the two halves of a modified pair sit in their own groups.
            int start = row;
            while (row < rows.Count && rows[row].Kind != DiffLineKind.Unchanged)
            {
                row++;
            }

            for (int i = start; i < row; i++)
            {
                if (rows[i].LeftLine is { } left)
                {
                    DiffLineKind kind = rows[i].Kind == DiffLineKind.Modified ? DiffLineKind.Modified : DiffLineKind.Deleted;
                    Add(lines, leftToLine, rightToLine, new InlineLine(DiffSide.Left, left, i, kind));
                }
            }

            for (int i = start; i < row; i++)
            {
                if (rows[i].RightLine is { } right)
                {
                    DiffLineKind kind = rows[i].Kind == DiffLineKind.Modified ? DiffLineKind.Modified : DiffLineKind.Inserted;
                    Add(lines, leftToLine, rightToLine, new InlineLine(DiffSide.Right, right, i, kind));
                }
            }
        }

        return new InlineDocument(model, [.. lines], leftToLine, rightToLine, BlockRanges(model, lines));
    }

    /// <summary>
    /// The 0-based unified line showing <paramref name="sourceLine"/> of <paramref name="side"/>,
    /// or <c>null</c> when that line is not displayed — the right line of a context row — or is
    /// not a line of that side.
    /// </summary>
    /// <param name="side">The side the line belongs to.</param>
    /// <param name="sourceLine">The 0-based line on that side.</param>
    public int? LineOf(DiffSide side, int sourceLine)
    {
        int[] table = side == DiffSide.Left ? _leftToLine : _rightToLine;
        if (sourceLine < 0 || sourceLine >= table.Length)
        {
            return null;
        }

        int line = table[sourceLine];
        return line < 0 ? null : line;
    }

    /// <summary>
    /// The unified lines of the block at <paramref name="blockIndex"/>, or an empty range when
    /// there is no such block. The lines of a block are contiguous: a block is a run of rows and
    /// a run prints as one group.
    /// </summary>
    /// <param name="blockIndex">The block's index in <see cref="SideBySideDocument.Blocks"/>.</param>
    public LineRange LinesOfBlock(int blockIndex)
    {
        return blockIndex < 0 || blockIndex >= _blocks.Length ? LineRange.Empty(0) : _blocks[blockIndex];
    }

    private static int[] Filled(int length)
    {
        int[] table = new int[length];
        Array.Fill(table, -1);
        return table;
    }

    private static void Add(List<InlineLine> lines, int[] leftToLine, int[] rightToLine, InlineLine line)
    {
        int[] table = line.Side == DiffSide.Left ? leftToLine : rightToLine;
        if (line.SourceLine >= 0 && line.SourceLine < table.Length)
        {
            table[line.SourceLine] = lines.Count;
        }

        lines.Add(line);
    }

    /// <summary>
    /// The unified range of every block, found by walking the lines beside the blocks. The rows
    /// of a run repeat — its left lines, then its right lines — but they never leave the run's
    /// own block, so one forward pass over the blocks is enough.
    /// </summary>
    private static LineRange[] BlockRanges(SideBySideDocument model, List<InlineLine> lines)
    {
        IReadOnlyList<ChangeBlock> blocks = model.Blocks;
        int[] first = Filled(blocks.Count);
        int[] last = Filled(blocks.Count);
        int block = 0;
        for (int line = 0; line < lines.Count; line++)
        {
            int row = lines[line].Row;
            while (block < blocks.Count && blocks[block].LastRow < row)
            {
                block++;
            }

            if (block >= blocks.Count || row < blocks[block].FirstRow)
            {
                continue;
            }

            if (first[block] < 0)
            {
                first[block] = line;
            }

            last[block] = line;
        }

        LineRange[] ranges = new LineRange[blocks.Count];
        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i] = first[i] < 0 ? LineRange.Empty(0) : new LineRange(first[i], last[i] - first[i] + 1);
        }

        return ranges;
    }
}
