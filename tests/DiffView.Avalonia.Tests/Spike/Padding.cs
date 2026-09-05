using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using LogicalDirection = AvaloniaEdit.Document.LogicalDirection;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Spike;

// Phase 1 spike. Everything in this file is throwaway: it proves the mechanism the plan's
// PaddingRun / PaddingElement / PaddingElementGenerator / PaddingHeightPrimer rely on, against
// plain TextEditors. Phase 4 lifts what survives into DiffView.Avalonia.

/// <summary>
/// Rows of padding a line carries: <see cref="Above"/> before its text, <see cref="Below"/>
/// after it. Only the last line of a document carries <see cref="Below"/> (trailing padding).
/// </summary>
internal readonly record struct PaddingSpec(int Above, int Below)
{
    public bool IsEmpty => Above == 0 && Below == 0;
}

/// <summary>
/// The vertical metrics a padded line is computed from. Ascent, descent and line gap are the
/// same numbers Avalonia's line metrics start from (its <c>TextMetrics</c> of the default run
/// properties); <see cref="LineHeight"/> is <c>TextView.DefaultLineHeight</c>, which includes
/// <c>TextEditorOptions.LineHeightFactor</c>.
/// </summary>
internal readonly record struct PaddingMetrics(double Ascent, double Descent, double LineGap, double LineHeight)
{
    /// <summary>Natural height of a text line (before the line-height factor).</summary>
    public double TextHeight => Ascent + Descent + LineGap;

    /// <summary>
    /// Half of what the line-height factor adds. A plain line's text is centred in its row, so
    /// this much sits above and below the text; a padded row keeps the same centring.
    /// </summary>
    public double HalfSlack => (LineHeight - TextHeight) / 2;

    public static PaddingMetrics From(TextView textView, TextRunProperties properties)
    {
        TextMetrics metrics = new(properties.Typeface.GlyphTypeface, properties.FontRenderingEmSize);
        return new PaddingMetrics(-metrics.Ascent, metrics.Descent, metrics.LineGap, textView.DefaultLineHeight);
    }
}

/// <summary>
/// A zero-width run whose <see cref="Baseline"/> extends the line's ascent by the padding above
/// and whose height below the baseline extends its descent by the padding below. Avalonia's line
/// metrics take the largest ascent and descent of any run, so the line becomes exactly
/// <c>(1 + above + below) · lineHeight</c> tall with the text band where a plain row would put it.
/// </summary>
internal sealed class PaddingRun : DrawableTextRun
{
    private readonly PaddingMetrics _metrics;
    private readonly PaddingSpec _spec;

    public PaddingRun(TextRunProperties properties, PaddingMetrics metrics, PaddingSpec spec)
    {
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
        // Padding is empty space; the background renderer paints over it in the real control.
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

/// <summary>
/// Emits a <see cref="PaddingElement"/> at the start of every line whose padding is not empty.
/// </summary>
internal sealed class PaddingGenerator : VisualLineElementGenerator
{
    private readonly Func<int, PaddingSpec> _paddingForLine;
    private PaddingMetrics _metrics;

    public PaddingGenerator(Func<int, PaddingSpec> paddingForLine)
    {
        _paddingForLine = paddingForLine;
    }

    public override void StartGeneration(ITextRunConstructionContext context)
    {
        base.StartGeneration(context);
        _metrics = PaddingMetrics.From(context.TextView, context.GlobalTextRunProperties);
    }

    public override int GetFirstInterestedOffset(int startOffset)
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

        return _paddingForLine(line.LineNumber).IsEmpty ? -1 : startOffset;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        DocumentLine line = CurrentContext.Document.GetLineByOffset(offset);
        return new PaddingElement(_paddingForLine(line.LineNumber), _metrics, isAloneOnLine: line.Length == 0);
    }
}

/// <summary>
/// Builds every padded line once so the height tree learns its height before the line scrolls
/// into view. <c>GetOrConstructVisualLine</c> keeps each built line in the text view's list and
/// re-walks that list per call, so long runs are cut into batches separated by a
/// <c>Redraw</c>, which drops the built lines but keeps their heights.
/// </summary>
internal static class PaddingPrimer
{
    public static int Prime(TextView textView, IEnumerable<int> paddedLineNumbers, int batchSize = int.MaxValue)
    {
        TextDocument document = textView.Document;
        int built = 0;
        int inBatch = 0;
        foreach (int lineNumber in paddedLineNumbers)
        {
            textView.GetOrConstructVisualLine(document.GetLineByNumber(lineNumber));
            built++;
            if (++inBatch >= batchSize)
            {
                textView.Redraw();
                inBatch = 0;
            }
        }

        // Heights are in the tree, but the scroll extent is published only by a measure pass.
        textView.InvalidateMeasure();
        return built;
    }
}
