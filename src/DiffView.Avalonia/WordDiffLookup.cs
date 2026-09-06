using System.Diagnostics.CodeAnalysis;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The word-level pieces of a row, read from the two live documents on demand: the model does
/// not hold text, so the lookup finds the row's line on each side, takes its text from that
/// side's <see cref="TextDocument"/>, and asks the <see cref="WordDiffCache"/>, which computes
/// on first request and keeps the answer keyed by the model's version. Bounds-checked: a row
/// that is not modified, or whose line lies beyond its document between a keystroke and the
/// next build, gets no pieces and nothing throws. UI thread only, like the documents it reads.
/// </summary>
public sealed class WordDiffLookup
{
    private readonly TextDocument _left;
    private readonly TextDocument _right;

    /// <param name="cache">The cache computing and keeping the pieces, bound to the build's options.</param>
    /// <param name="left">The left side's live document.</param>
    /// <param name="right">The right side's live document.</param>
    /// <param name="document">The model whose rows are looked up.</param>
    public WordDiffLookup(WordDiffCache cache, TextDocument left, TextDocument right, SideBySideDocument document)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(document);
        Cache = cache;
        _left = left;
        _right = right;
        Document = document;
    }

    /// <summary>The cache behind the lookup.</summary>
    public WordDiffCache Cache { get; }

    /// <summary>The model whose rows are looked up.</summary>
    public SideBySideDocument Document { get; }

    /// <summary>A line longer than this gets no pieces.</summary>
    public int MaxLineLength => Cache.Options.MaxWordDiffLineLength;

    /// <summary>
    /// The pieces of <paramref name="row"/>, or <see cref="WordDiffPieces.None"/> when the row is
    /// not a modified row, word-level is off, a line is too long, or a side's line is not in its
    /// document.
    /// </summary>
    public WordDiffPieces PiecesFor(int row)
    {
        if (!TryGetLines(row, out DocumentLine? leftLine, out DocumentLine? rightLine))
        {
            return WordDiffPieces.None;
        }

        string leftText = _left.GetText(leftLine.Offset, leftLine.Length);
        string rightText = _right.GetText(rightLine.Offset, rightLine.Length);
        return Cache.GetPieces(Document, row, leftText, rightText);
    }

    /// <summary>Whether <paramref name="row"/> is a modified row that gets no pieces because a line exceeds <see cref="MaxLineLength"/>.</summary>
    public bool IsLongLine(int row)
    {
        return TryGetLines(row, out DocumentLine? leftLine, out DocumentLine? rightLine)
               && (leftLine.Length > MaxLineLength || rightLine.Length > MaxLineLength);
    }

    private bool TryGetLines(int row, [NotNullWhen(true)] out DocumentLine? leftLine, [NotNullWhen(true)] out DocumentLine? rightLine)
    {
        leftLine = null;
        rightLine = null;
        if (row < 0 || row >= Document.Rows.Count)
        {
            return false;
        }

        AlignedRow aligned = Document.Rows[row];
        if (aligned.Kind != DiffLineKind.Modified || aligned.LeftLine is not { } left || aligned.RightLine is not { } right)
        {
            return false;
        }

        if (left >= _left.LineCount || right >= _right.LineCount)
        {
            return false;
        }

        leftLine = _left.GetLineByNumber(left + 1);
        rightLine = _right.GetLineByNumber(right + 1);
        return true;
    }
}
