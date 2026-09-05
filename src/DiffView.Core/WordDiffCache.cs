using DiffPlex;
using DiffPlex.Chunkers;
using DiffPlex.Model;

namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>What a piece of a modified line is.</summary>
public enum PieceKind
{
    /// <summary>Equal on both sides under the options.</summary>
    Unchanged,

    /// <summary>Present on the right only.</summary>
    Inserted,

    /// <summary>Present on the left only.</summary>
    Deleted,
}

/// <summary>A character range inside a line. Pieces of a line are contiguous and cover it entirely.</summary>
public readonly record struct PieceRange(int Start, int Length, PieceKind Kind)
{
    /// <summary>One past the last character.</summary>
    public int End => Start + Length;
}

/// <summary>The word-level pieces of one modified row, per side.</summary>
public sealed record WordDiffPieces(IReadOnlyList<PieceRange> Left, IReadOnlyList<PieceRange> Right)
{
    /// <summary>No pieces: word-level is off, or a line is too long.</summary>
    public static WordDiffPieces None { get; } = new([], []);
}

/// <summary>
/// Word-level pieces for modified rows, computed on first request — a DiffPlex run with the
/// word or character chunker over one row's two lines, microseconds each — and kept in an LRU
/// keyed by <c>(document version, row)</c>, so a rebuilt document never serves stale pieces.
/// A line longer than <see cref="DiffOptions.MaxWordDiffLineLength"/> gets no pieces and is
/// counted in <see cref="LongLinesSkipped"/>. Not thread-safe: the renderer calls it on the UI thread.
/// </summary>
public sealed class WordDiffCache
{
    private readonly Dictionary<(int Version, int Row), LinkedListNode<Entry>> _index = [];
    private readonly LinkedList<Entry> _order = [];
    private readonly IChunker? _chunker;

    /// <param name="options">The word-level mode, separators and long-line limit.</param>
    /// <param name="capacity">Rows kept before the least recently used is evicted.</param>
    public WordDiffCache(DiffOptions options, int capacity = 512)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Options = options;
        Capacity = capacity;
        _chunker = options.WordDiff switch
        {
            WordDiffMode.Word => new DelimiterChunker([.. options.WordSeparators]),
            WordDiffMode.Character => CharacterChunker.Instance,
            _ => null,
        };
    }

    /// <summary>The options the pieces are computed under.</summary>
    public DiffOptions Options { get; }

    /// <summary>Rows kept.</summary>
    public int Capacity { get; }

    /// <summary>Rows currently cached.</summary>
    public int Count => _index.Count;

    /// <summary>Rows that got no pieces because a line exceeded the limit, since construction or the last <see cref="Clear"/>.</summary>
    public int LongLinesSkipped { get; private set; }

    /// <summary>
    /// The pieces for <paramref name="row"/> of <paramref name="document"/>, given the two
    /// lines' text (the model does not hold text; the editor's document does). Both piece
    /// lists concatenate back to their line.
    /// </summary>
    public WordDiffPieces GetPieces(SideBySideDocument document, int row, string leftLineText, string rightLineText)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(leftLineText);
        ArgumentNullException.ThrowIfNull(rightLineText);

        if (_chunker is null)
        {
            return WordDiffPieces.None;
        }

        (int Version, int Row) key = (document.Version, row);
        if (_index.TryGetValue(key, out LinkedListNode<Entry>? node))
        {
            _order.Remove(node);
            _order.AddFirst(node);
            return node.Value.Pieces;
        }

        WordDiffPieces pieces;
        if (leftLineText.Length > Options.MaxWordDiffLineLength || rightLineText.Length > Options.MaxWordDiffLineLength)
        {
            LongLinesSkipped++;
            pieces = WordDiffPieces.None;
        }
        else
        {
            pieces = Compute(leftLineText, rightLineText);
        }

        _index[key] = _order.AddFirst(new Entry(key, pieces));
        if (_order.Count > Capacity)
        {
            LinkedListNode<Entry> last = _order.Last!;
            _order.RemoveLast();
            _index.Remove(last.Value.Key);
        }

        return pieces;
    }

    /// <summary>Whether <paramref name="row"/> of <paramref name="document"/> is cached.</summary>
    public bool Contains(SideBySideDocument document, int row)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _index.ContainsKey((document.Version, row));
    }

    /// <summary>Drops every cached row and resets the long-line count.</summary>
    public void Clear()
    {
        _index.Clear();
        _order.Clear();
        LongLinesSkipped = 0;
    }

    private WordDiffPieces Compute(string leftLineText, string rightLineText)
    {
        // DiffPlex returns no pieces for an empty string; the other side is then entirely one kind.
        if (leftLineText.Length == 0 || rightLineText.Length == 0)
        {
            return new WordDiffPieces(
                leftLineText.Length == 0 ? [] : [new PieceRange(0, leftLineText.Length, PieceKind.Deleted)],
                rightLineText.Length == 0 ? [] : [new PieceRange(0, rightLineText.Length, PieceKind.Inserted)]);
        }

        DiffResult result = Differ.Instance.CreateDiffs(leftLineText, rightLineText, Options.IgnoreWhitespace, Options.IgnoreCase, _chunker!);
        return new WordDiffPieces(
            Ranges(result.PiecesOld, result.DiffBlocks, b => (b.DeleteStartA, b.DeleteCountA), PieceKind.Deleted),
            Ranges(result.PiecesNew, result.DiffBlocks, b => (b.InsertStartB, b.InsertCountB), PieceKind.Inserted));
    }

    /// <summary>
    /// Contiguous ranges over one side's pieces: the changed pieces of each block get
    /// <paramref name="changedKind"/>, everything between them is unchanged, adjacent ranges of
    /// the same kind merge, and the ranges cover the line from 0 to its length.
    /// </summary>
    private static IReadOnlyList<PieceRange> Ranges(IReadOnlyList<string> pieces, IList<DiffBlock> blocks, Func<DiffBlock, (int Start, int Count)> select, PieceKind changedKind)
    {
        int[] offsets = new int[pieces.Count + 1];
        for (int i = 0; i < pieces.Count; i++)
        {
            offsets[i + 1] = offsets[i] + pieces[i].Length;
        }

        List<PieceRange> ranges = [];
        int piece = 0;
        foreach (DiffBlock block in blocks)
        {
            (int start, int count) = select(block);
            if (count == 0)
            {
                continue;
            }

            Add(ranges, offsets[piece], offsets[start], PieceKind.Unchanged);
            Add(ranges, offsets[start], offsets[start + count], changedKind);
            piece = start + count;
        }

        Add(ranges, offsets[piece], offsets[pieces.Count], PieceKind.Unchanged);
        return ranges;
    }

    private static void Add(List<PieceRange> ranges, int start, int end, PieceKind kind)
    {
        if (end <= start)
        {
            return;
        }

        if (ranges.Count > 0 && ranges[^1].Kind == kind && ranges[^1].End == start)
        {
            ranges[^1] = ranges[^1] with { Length = end - ranges[^1].Start };
            return;
        }

        ranges.Add(new PieceRange(start, end - start, kind));
    }

    private sealed record Entry((int Version, int Row) Key, WordDiffPieces Pieces);
}
