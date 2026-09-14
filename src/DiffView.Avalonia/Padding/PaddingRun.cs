using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using LogicalDirection = AvaloniaEdit.Document.LogicalDirection;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// A zero-width run whose <see cref="Baseline"/> extends the line's ascent by the padding above
/// and whose height below the baseline extends its descent by the padding below. Avalonia's line
/// metrics take the largest ascent and descent of any run, so the line becomes exactly
/// <c>(1 + above + below) · lineHeight</c> tall with the text band where a plain row would put it.
/// The run draws nothing: <see cref="DiffLineBackgroundRenderer"/> paints the padding space.
/// </summary>
internal sealed class PaddingRun : DrawableTextRun
{
    private readonly PaddingMetrics _metrics;
    private readonly PaddingSpec _spec;

    public PaddingRun(TextRunProperties properties, PaddingMetrics metrics, PaddingSpec spec)
    {
        // Avalonia switches on the run properties' BaselineAlignment and throws on null.
        Properties = properties;
        _metrics = metrics;
        _spec = spec;
    }

    public override TextRunProperties Properties { get; }

    public override int Length => 1;

    public override double Baseline => _metrics.Ascent + _metrics.HalfSlack + _spec.Above * _metrics.LineHeight;

    public override Size Size => new(0, Baseline + _metrics.Descent + _metrics.HalfSlack + _spec.Below * _metrics.LineHeight);

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        // Padding is empty space; the background renderer paints over it.
    }
}

/// <summary>
/// The element hosting a <see cref="PaddingRun"/>: one visual column, zero document characters,
/// placed at the start of the line. It owns no caret stop of its own — the text after it supplies
/// the stop for the line's first offset — and it handles the line border itself so the visual
/// line adds no implicit stop at column 0. An empty line has no text to supply a stop, so there
/// the element supplies the single stop at the column after itself.
/// </summary>
internal sealed class PaddingElement : VisualLineElement
{
    private readonly PaddingMetrics _metrics;
    private readonly bool _isAloneOnLine;

    public PaddingElement(PaddingSpec spec, PaddingMetrics metrics, bool isAloneOnLine)
        : base(visualLength: 1, documentLength: 0)
    {
        Spec = spec;
        _metrics = metrics;
        _isAloneOnLine = isAloneOnLine;
    }

    /// <summary>The padding this element renders.</summary>
    public PaddingSpec Spec { get; }

    public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
    {
        return new PaddingRun(TextRunProperties, _metrics, Spec);
    }

    public override bool HandlesLineBorders => true;

    public override int GetNextCaretPosition(int visualColumn, LogicalDirection direction, CaretPositioningMode mode)
    {
        if (!_isAloneOnLine)
        {
            return -1;
        }

        int stop = VisualColumn + VisualLength;
        return direction == LogicalDirection.Forward
            ? (visualColumn < stop ? stop : -1)
            : (visualColumn > stop ? stop : -1);
    }
}
