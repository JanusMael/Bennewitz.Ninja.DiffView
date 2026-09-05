using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A one-character strip beside the line numbers: <c>+</c> for an inserted line, <c>-</c> for a
/// deleted one, <c>~</c> for a modified one, in the marker brush of the kind, at the line's text
/// band; nothing for an unchanged line and nothing over padding space. ASCII glyphs, so the kind
/// is scannable down the gutter without relying on colour alone.
/// </summary>
internal sealed class ChangeMarkerMargin : DiffMargin
{
    private const double HorizontalPadding = 4;

    private readonly List<(int LineNumber, DiffLineKind Kind)> _lastRendered = [];

    public ChangeMarkerMargin(DiffPanePresenter owner)
        : base(owner, nameof(ChangeMarkerMargin), DiffViewStrings.ChangeMarkersMarginName)
    {
    }

    /// <summary>The lines of the last frame and the kind each was drawn with, in order.</summary>
    public IReadOnlyList<(int LineNumber, DiffLineKind Kind)> LastRendered => _lastRendered;

    /// <summary>The marker glyph for <paramref name="kind"/>; <c>null</c> for an unchanged line.</summary>
    public static string? GlyphFor(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Inserted => "+",
            DiffLineKind.Deleted => "-",
            DiffLineKind.Modified => "~",
            _ => null,
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        FormattedText widest = Format("~", Owner.Palette[DiffBrush.MarkerModified]);
        return new Size(widest.Width + 2 * HorizontalPadding, 0);
    }

    protected override void RenderCore(DrawingContext context, TextView textView)
    {
        _lastRendered.Clear();
        PaneMetadata metadata = Owner.Metadata;
        foreach (VisualLine line in textView.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            DiffLineKind kind = metadata.KindOf(number);
            _lastRendered.Add((number, kind));
            if (GlyphFor(kind) is not { } glyph)
            {
                continue;
            }

            FormattedText text = Format(glyph, Owner.Palette.MarkerFor(kind));
            context.DrawText(text, new Point(HorizontalPadding, TextTopOf(line, textView)));
        }
    }
}
