using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A one-character strip beside the line numbers: <c>+</c> for an inserted line, <c>-</c> for a
/// deleted one, <c>~</c> for a modified one, in the marker brush of the kind, at the line's text
/// band; nothing for an unchanged line and nothing over padding space. ASCII glyphs, so the kind
/// is scannable down the gutter without relying on colour alone. The tooltip follows the pointer
/// and, over a modified row whose line was too long for word-level pieces, says so.
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

    /// <summary>
    /// The tooltip for <paramref name="lineNumber"/>: on a modified row whose line exceeds the
    /// word-level limit, why it carries no word highlights; otherwise none.
    /// </summary>
    public string? TooltipFor(int lineNumber)
    {
        if (Owner.Metadata.KindOf(lineNumber) != DiffLineKind.Modified
            || Owner.Metadata.RowOf(lineNumber) is not { } row
            || Owner.WordDiffLookup is not { } lookup
            || !lookup.IsLongLine(row))
        {
            return null;
        }

        return DiffViewStrings.Format(DiffViewStrings.WordDiffSkipped, lookup.MaxLineLength.ToString("N0", CultureInfo.CurrentCulture));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        FormattedText widest = Format("~", Owner.Palette[DiffBrush.MarkerModified]);
        return new Size(widest.Width + 2 * HorizontalPadding, 0);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (TextView is not { } view || Document is null)
        {
            return;
        }

        DocumentLine? line = view.GetDocumentLineByVisualTop(e.GetPosition(this).Y + view.VerticalOffset);
        ToolTip.SetTip(this, line is null ? null : TooltipFor(line.LineNumber));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetTip(this, null);
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
