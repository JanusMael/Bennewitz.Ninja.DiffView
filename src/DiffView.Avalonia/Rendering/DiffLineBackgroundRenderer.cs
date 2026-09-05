using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>What the background renderer drew for one visual line; read by tests.</summary>
internal readonly record struct DrawnLine(int LineNumber, DiffLineKind Kind, PaddingSpec Padding);

/// <summary>
/// Fills every visible row by kind — the full row, <c>lineHeight</c> tall, so consecutive rows
/// touch — and hatches the padding space above or below a padded line. The row geometry comes
/// from the <see cref="PaddingElement"/> the visual line actually carries, not from the
/// metadata, so a disabled generator leaves no phantom padding fills. Draws on
/// <see cref="KnownLayer.Background"/>, under the selection and the text.
/// </summary>
internal sealed class DiffLineBackgroundRenderer : GuardedBackgroundRenderer
{
    private const double HatchSpacing = 6;

    private readonly List<DrawnLine> _lastDrawn = [];

    public DiffLineBackgroundRenderer(DiffPanePresenter owner)
        : base(owner, KnownLayer.Background, nameof(DiffLineBackgroundRenderer))
    {
    }

    /// <summary>The lines of the last frame, in order, with the kind and padding drawn for each.</summary>
    public IReadOnlyList<DrawnLine> LastDrawn => _lastDrawn;

    /// <summary>The padding the visual line carries, from its element; none when the generator emitted nothing.</summary>
    public static PaddingSpec PaddingOf(VisualLine line)
    {
        foreach (VisualLineElement element in line.Elements)
        {
            if (element is PaddingElement padding)
            {
                return padding.Spec;
            }
        }

        return PaddingSpec.None;
    }

    protected override void DrawCore(TextView textView, DrawingContext drawingContext)
    {
        _lastDrawn.Clear();
        double width = textView.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        double lineHeight = textView.DefaultLineHeight;
        Vector scroll = textView.ScrollOffset;
        PaneMetadata metadata = Owner.Metadata;
        DiffBrushes palette = Owner.Palette;
        IBrush paddingBrush = palette[DiffBrush.Padding];
        Pen hatchPen = new(paddingBrush, 1);

        foreach (VisualLine line in textView.VisualLines)
        {
            int lineNumber = line.FirstDocumentLine.LineNumber;
            DiffLineKind kind = metadata.KindOf(lineNumber);
            PaddingSpec padding = PaddingOf(line);
            double top = line.VisualTop - scroll.Y;
            double rowTop = top + padding.Above * lineHeight;

            if (padding.Above > 0)
            {
                DrawPadding(drawingContext, new Rect(0, top, width, padding.Above * lineHeight), paddingBrush, hatchPen);
            }

            if (kind != DiffLineKind.Unchanged)
            {
                drawingContext.FillRectangle(palette.ForKind(kind), new Rect(0, rowTop, width, lineHeight));
            }

            if (padding.Below > 0)
            {
                DrawPadding(drawingContext, new Rect(0, rowTop + lineHeight, width, padding.Below * lineHeight), paddingBrush, hatchPen);
            }

            _lastDrawn.Add(new DrawnLine(lineNumber, kind, padding));
        }
    }

    private static void DrawPadding(DrawingContext drawingContext, Rect area, IBrush brush, IPen pen)
    {
        drawingContext.FillRectangle(brush, area);
        using DrawingContext.PushedState clip = drawingContext.PushClip(area);
        for (double x = area.Left - area.Height; x < area.Right; x += HatchSpacing)
        {
            drawingContext.DrawLine(pen, new Point(x, area.Bottom), new Point(x + area.Height, area.Top));
        }
    }
}
