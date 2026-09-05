using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// One pane's view of a <see cref="SideBySideDocument"/>, addressed by the editor's 1-based
/// line numbers and bounds-checked: a line the model does not know renders as
/// <see cref="DiffLineKind.Unchanged"/> with no padding, and nothing throws. Between a keystroke
/// and the next build the document and the model disagree; this is the only place that
/// disagreement is absorbed. <see cref="Version"/> is the model's build stamp, so a cache keyed
/// by row can tell a stale row from a current one. The Core model is 0-based; the conversion
/// happens here and nowhere else in the presenter.
/// </summary>
internal sealed class PaneMetadata
{
    private readonly SideBySideDocument? _document;
    private readonly IReadOnlyList<DiffLine> _lines;

    private PaneMetadata(SideBySideDocument? document, DiffSide side)
    {
        _document = document;
        Side = side;
        _lines = document?.Pane(side).Lines ?? [];
        Version = document?.Version ?? 0;
    }

    /// <summary>No model: every line is unchanged and unpadded.</summary>
    public static PaneMetadata Empty { get; } = new(null, DiffSide.Left);

    /// <summary>The metadata of <paramref name="side"/> of <paramref name="document"/>, or <see cref="Empty"/>.</summary>
    public static PaneMetadata For(SideBySideDocument? document, DiffSide side)
    {
        return document is null ? Empty : new PaneMetadata(document, side);
    }

    /// <summary>Which side this is.</summary>
    public DiffSide Side { get; }

    /// <summary>The model's build stamp; 0 for <see cref="Empty"/>.</summary>
    public int Version { get; }

    /// <summary>Lines the model knows on this side.</summary>
    public int LineCount => _lines.Count;

    /// <summary>Whether there is no model behind this metadata.</summary>
    public bool IsEmpty => _document is null;

    /// <summary>Padding rows after the last line, when the model knows the side.</summary>
    public int TrailingPadding => _document is null ? 0 : Padding.Trailing(_document, Side);

    /// <summary>Whether <paramref name="lineNumber"/> (1-based) is a line the model knows.</summary>
    public bool Knows(int lineNumber) => lineNumber >= 1 && lineNumber <= _lines.Count;

    /// <summary>The kind of <paramref name="lineNumber"/>, or <see cref="DiffLineKind.Unchanged"/> when unknown.</summary>
    public DiffLineKind KindOf(int lineNumber)
    {
        return Knows(lineNumber) ? _lines[lineNumber - 1].Kind : DiffLineKind.Unchanged;
    }

    /// <summary>The row of <paramref name="lineNumber"/>, or <c>null</c> when unknown.</summary>
    public int? RowOf(int lineNumber)
    {
        return Knows(lineNumber) ? _lines[lineNumber - 1].Row : null;
    }

    /// <summary>Padding rows above <paramref name="lineNumber"/>, or 0 when unknown.</summary>
    public int PaddingBefore(int lineNumber)
    {
        return Knows(lineNumber) ? Padding.Before(_document!, Side, lineNumber - 1) : 0;
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
