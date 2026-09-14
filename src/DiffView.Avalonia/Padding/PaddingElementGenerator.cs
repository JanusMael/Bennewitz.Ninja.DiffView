using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Emits a <see cref="PaddingElement"/> at the start of every line whose padding is not empty.
/// A layout boundary: any exception is reported once through the fault callback, the generator
/// disables itself, and from then on it emits nothing — the pane is misaligned but alive.
/// <see cref="Reset"/> re-enables it when a new document arrives.
/// </summary>
internal sealed class PaddingElementGenerator : VisualLineElementGenerator
{
    private readonly Func<int, PaddingSpec> _paddingForLine;
    private readonly Action<int?, Exception> _onFault;
    private PaddingMetrics _metrics;

    /// <param name="paddingForLine">The padding of a 1-based document line number.</param>
    /// <param name="onFault">Receives the faulting line, when known, and the exception.</param>
    public PaddingElementGenerator(Func<int, PaddingSpec> paddingForLine, Action<int?, Exception> onFault)
    {
        _paddingForLine = paddingForLine;
        _onFault = onFault;
    }

    /// <summary>Whether a fault has disabled the generator.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Re-enables the generator after a fault.</summary>
    public void Reset()
    {
        IsDisabled = false;
    }

    public override void StartGeneration(ITextRunConstructionContext context)
    {
        base.StartGeneration(context);
        if (IsDisabled)
        {
            return;
        }

        try
        {
            _metrics = PaddingMetrics.From(context.TextView, context.GlobalTextRunProperties);
        }
        catch (Exception ex)
        {
            Fault(null, ex);
        }
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (IsDisabled)
        {
            return -1;
        }

        int? lineNumber = null;
        try
        {
            TextDocument document = CurrentContext.Document;
            if (startOffset > document.TextLength)
            {
                return -1;
            }

            DocumentLine line = document.GetLineByOffset(startOffset);
            if (line.Offset != startOffset)
            {
                return -1;
            }

            lineNumber = line.LineNumber;
            return _paddingForLine(line.LineNumber).IsEmpty ? -1 : startOffset;
        }
        catch (Exception ex)
        {
            Fault(lineNumber, ex);
            return -1;
        }
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        if (IsDisabled)
        {
            return null;
        }

        int? lineNumber = null;
        try
        {
            DocumentLine line = CurrentContext.Document.GetLineByOffset(offset);
            lineNumber = line.LineNumber;
            PaddingSpec spec = _paddingForLine(line.LineNumber);
            return spec.IsEmpty ? null : new PaddingElement(spec, _metrics, isAloneOnLine: line.Length == 0);
        }
        catch (Exception ex)
        {
            Fault(lineNumber, ex);
            return null;
        }
    }

    private void Fault(int? lineNumber, Exception exception)
    {
        IsDisabled = true;
        _onFault(lineNumber, exception);
    }
}
