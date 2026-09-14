using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// One presenter's view of a <see cref="SideBySideDocument"/> — a side of it, or the unified
/// line table over both sides — addressed by the editor's 1-based line numbers and
/// bounds-checked: a line the model does not know renders as <see cref="DiffLineKind.Unchanged"/>
/// with no padding, and nothing throws. Between a keystroke and the next build the document and
/// the model disagree; this is the only place that disagreement is absorbed.
/// <see cref="Version"/> is the model's build stamp, so a cache keyed by row can tell a stale
/// row from a current one. The Core model is 0-based; the conversion happens here and nowhere
/// else in the presenter.
/// </summary>
/// <remarks>
/// Unified metadata answers the same questions from the <see cref="InlineDocument"/>'s line
/// table: a line's kind and row come from the table, its side varies line by line, and there is
/// no padding at all, because a unified document holds every displayed line itself.
/// </remarks>
internal sealed class PaneMetadata
{
    private readonly SideBySideDocument? _document;
    private readonly InlineDocument? _inline;
    private readonly IReadOnlyList<DiffLine> _lines;
    private readonly bool _unified;

    private PaneMetadata(SideBySideDocument? document, DiffSide side)
    {
        _document = document;
        Side = side;
        _lines = document?.Pane(side).Lines ?? [];
        Version = document?.Version ?? 0;
    }

    private PaneMetadata(InlineDocument? inline)
    {
        _inline = inline;
        _unified = true;
        _document = inline?.Model;
        Side = DiffSide.Left;
        _lines = [];
        Version = inline?.Version ?? 0;
    }

    /// <summary>No model: every line is unchanged and unpadded.</summary>
    public static PaneMetadata Empty { get; } = new(null, DiffSide.Left);

    /// <summary>The unified reading with no model: every line is unchanged and unpadded.</summary>
    public static PaneMetadata EmptyUnified { get; } = new((InlineDocument?)null);

    /// <summary>The metadata of <paramref name="side"/> of <paramref name="document"/>, or <see cref="Empty"/>.</summary>
    public static PaneMetadata For(SideBySideDocument? document, DiffSide side)
    {
        return document is null ? Empty : new PaneMetadata(document, side);
    }

    /// <summary>The metadata of <paramref name="inline"/>'s unified line table, or <see cref="EmptyUnified"/>.</summary>
    public static PaneMetadata Unified(InlineDocument? inline)
    {
        return inline is null ? EmptyUnified : new PaneMetadata(inline);
    }

    /// <summary>Which side this is; <see cref="DiffSide.Left"/> and meaningless while <see cref="IsUnified"/>.</summary>
    public DiffSide Side { get; }

    /// <summary>Whether this is the unified line table rather than one side of the model.</summary>
    public bool IsUnified => _unified;

    /// <summary>The model's build stamp; 0 without one.</summary>
    public int Version { get; }

    /// <summary>Lines the model knows on this side, or unified lines.</summary>
    public int LineCount => _inline?.Lines.Count ?? _lines.Count;

    /// <summary>Whether there is no model behind this metadata.</summary>
    public bool IsEmpty => _document is null;

    /// <summary>The model, when there is one.</summary>
    public SideBySideDocument? Document => _document;

    /// <summary>The unified table, when this is the unified reading.</summary>
    public InlineDocument? Inline => _inline;

    /// <summary>The side <paramref name="lineNumber"/> was taken from; <see cref="Side"/> unless unified.</summary>
    public DiffSide SideOf(int lineNumber)
    {
        return _inline is { } inline && Knows(lineNumber) ? inline.Lines[lineNumber - 1].Side : Side;
    }

    /// <summary>The unified line at <paramref name="lineNumber"/>, or <c>null</c> when this is a side's metadata or the line is unknown.</summary>
    public InlineLine? UnifiedLineAt(int lineNumber)
    {
        return _inline is { } inline && Knows(lineNumber) ? inline.Lines[lineNumber - 1] : null;
    }

    /// <summary>The 1-based line on the other side that shares <paramref name="lineNumber"/>'s row, or <c>null</c> when that side has padding there or the line is unknown.</summary>
    public int? OtherLine(int lineNumber)
    {
        if (RowOf(lineNumber) is not { } row)
        {
            return null;
        }

        DiffSide other = SideOf(lineNumber) == DiffSide.Left ? DiffSide.Right : DiffSide.Left;
        return SideBySideDocument.LineOf(_document!.Rows[row], other) + 1;
    }

    /// <summary>The change block containing <paramref name="lineNumber"/>'s row, or <c>null</c> for an unchanged or unknown line.</summary>
    public ChangeBlock? BlockAt(int lineNumber)
    {
        return RowOf(lineNumber) is { } row ? BlockAtRow(row) : null;
    }

    /// <summary>
    /// The change block covering <paramref name="row"/>, or <c>null</c> for an unchanged row.
    /// A row rather than a line, because a block can occupy rows a side has no lines for — its
    /// padding — and those rows still belong to it.
    /// </summary>
    public ChangeBlock? BlockAtRow(int row)
    {
        if (_document is null)
        {
            return null;
        }

        IReadOnlyList<ChangeBlock> blocks = _document.Blocks;
        int low = 0;
        int high = blocks.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            ChangeBlock block = blocks[middle];
            if (row < block.FirstRow)
            {
                high = middle - 1;
            }
            else if (row > block.LastRow)
            {
                low = middle + 1;
            }
            else
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>
    /// The rows <paramref name="block"/> occupies on screen: the model's rows for a side's
    /// metadata, the block's own unified lines when unified, where a modified pair takes two.
    /// </summary>
    public LineRange DisplayRowsOf(ChangeBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return _inline is { } inline ? inline.LinesOfBlock(block.Index) : new LineRange(block.FirstRow, block.RowCount);
    }

    /// <summary>Padding rows after the last line, when the model knows the side; never any when unified.</summary>
    public int TrailingPadding => _document is null || _inline is not null ? 0 : Padding.Trailing(_document, Side);

    /// <summary>Whether <paramref name="lineNumber"/> (1-based) is a line the model knows.</summary>
    public bool Knows(int lineNumber) => lineNumber >= 1 && lineNumber <= LineCount;

    /// <summary>The kind of <paramref name="lineNumber"/>, or <see cref="DiffLineKind.Unchanged"/> when unknown.</summary>
    public DiffLineKind KindOf(int lineNumber)
    {
        if (!Knows(lineNumber))
        {
            return DiffLineKind.Unchanged;
        }

        return _inline is { } inline ? inline.Lines[lineNumber - 1].Kind : _lines[lineNumber - 1].Kind;
    }

    /// <summary>The row of <paramref name="lineNumber"/>, or <c>null</c> when unknown.</summary>
    public int? RowOf(int lineNumber)
    {
        if (!Knows(lineNumber))
        {
            return null;
        }

        return _inline is { } inline ? inline.Lines[lineNumber - 1].Row : _lines[lineNumber - 1].Row;
    }

    /// <summary>Padding rows above <paramref name="lineNumber"/>, or 0 when unknown or unified.</summary>
    public int PaddingBefore(int lineNumber)
    {
        return _inline is null && Knows(lineNumber) ? Padding.Before(_document!, Side, lineNumber - 1) : 0;
    }

    /// <summary>
    /// The padding of <paramref name="lineNumber"/> in a document of <paramref name="documentLineCount"/>
    /// lines. Trailing padding belongs to the document's last line only while the document and
    /// the model agree on the line count; otherwise it would sit on the wrong line.
    /// </summary>
    public PaddingSpec PaddingFor(int lineNumber, int documentLineCount)
    {
        int above = PaddingBefore(lineNumber);
        int below = documentLineCount == LineCount && lineNumber == documentLineCount ? TrailingPadding : 0;
        return new PaddingSpec(above, below);
    }

    /// <summary>Every line (1-based) that carries padding in a document of <paramref name="documentLineCount"/> lines.</summary>
    public IEnumerable<int> PaddedLineNumbers(int documentLineCount)
    {
        if (_inline is not null)
        {
            // A unified document holds every line it shows: there is nothing to pad and nothing
            // to prime, so its rows are the editor's own uniform ones.
            yield break;
        }

        int count = Math.Min(LineCount, documentLineCount);
        for (int line = 1; line <= count; line++)
        {
            if (!PaddingFor(line, documentLineCount).IsEmpty)
            {
                yield return line;
            }
        }
    }
}
