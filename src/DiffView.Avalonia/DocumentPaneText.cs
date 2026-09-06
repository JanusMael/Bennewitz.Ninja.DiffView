using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// An <see cref="IPaneText"/> over a captured editor document: an immutable
/// <see cref="ITextSource"/> snapshot plus the line table copied on the document's own thread.
/// The search worker reads neither the live <see cref="TextDocument"/> nor its line tree — both
/// enforce owner-thread access — so a search may run while the UI thread edits.
/// </summary>
internal sealed class DocumentPaneText : IPaneText
{
    private readonly ITextSource _snapshot;
    private readonly int[] _offsets;
    private readonly int[] _lengths;

    private DocumentPaneText(ITextSource snapshot, int[] offsets, int[] lengths)
    {
        _snapshot = snapshot;
        _offsets = offsets;
        _lengths = lengths;
    }

    /// <inheritdoc/>
    public int LineCount => _offsets.Length;

    /// <summary>Captures <paramref name="document"/>; call it on the document's own thread.</summary>
    public static DocumentPaneText Capture(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        int[] offsets = new int[document.LineCount];
        int[] lengths = new int[offsets.Length];
        int index = 0;
        foreach (DocumentLine line in document.Lines)
        {
            offsets[index] = line.Offset;
            // Length excludes the terminator, which is what IPaneText asks for.
            lengths[index] = line.Length;
            index++;
        }

        return new DocumentPaneText(document.CreateSnapshot(), offsets, lengths);
    }

    /// <inheritdoc/>
    public string GetLine(int line) => _snapshot.GetText(_offsets[line], _lengths[line]);
}
