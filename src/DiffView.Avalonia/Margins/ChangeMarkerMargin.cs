using System.Globalization;
using Avalonia;
using Avalonia.Media;
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

    /// <summary>The width of the modified-since-load bar down the margin's inner edge.</summary>
    private const double ModifiedBarWidth = 2;

    private readonly List<(int LineNumber, DiffLineKind Kind)> _lastRendered = [];
    private readonly List<int> _lastModified = [];

    public ChangeMarkerMargin(DiffPanePresenter owner)
        : base(owner, nameof(ChangeMarkerMargin), DiffViewStrings.ChangeMarkersMarginName)
    {
    }

    /// <summary>The lines of the last frame and the kind each was drawn with, in order.</summary>
    public IReadOnlyList<(int LineNumber, DiffLineKind Kind)> LastRendered => _lastRendered;

    /// <summary>The lines that carried a modified-since-load bar in the last frame.</summary>
    public IReadOnlyList<int> LastModified => _lastModified;

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
    /// The tooltip for <paramref name="lineNumber"/>: the change block the line belongs to —
    /// "Change 3 of 5 · +1 −0 ~2" — and, on a modified row whose line exceeds the word-level
    /// limit, why it carries no word highlights; none for an unchanged line.
    /// </summary>
    public override string? TooltipFor(int lineNumber)
    {
        PaneMetadata metadata = Owner.Metadata;
        string? edited = Owner.ModifiedLines.Contains(lineNumber)
            ? DiffViewStrings.Get(DiffViewStrings.MarkerModifiedSinceLoad)
            : null;

        if (metadata.BlockAt(lineNumber) is not { } block || metadata.Document is not { } document)
        {
            // A line the user edited is worth a tooltip even where the diff has nothing to say.
            return edited;
        }

        string summary = DiffViewStrings.Format(
            DiffViewStrings.MarkerTooltip,
            (block.Index + 1).ToString("N0", CultureInfo.CurrentCulture),
            document.Blocks.Count.ToString("N0", CultureInfo.CurrentCulture),
            DiffViewStrings.Format(DiffViewStrings.StatusCounts, block.InsertedCount, block.DeletedCount, block.ModifiedCount));

        if (metadata.KindOf(lineNumber) == DiffLineKind.Modified
            && metadata.RowOf(lineNumber) is { } row
            && Owner.WordDiffLookup is { } lookup
            && lookup.IsLongLine(row))
        {
            summary += Environment.NewLine + DiffViewStrings.Format(DiffViewStrings.WordDiffSkipped, lookup.MaxLineLength.ToString("N0", CultureInfo.CurrentCulture));
        }

        // A line can be both inside a change block and edited this session; it says both.
        if (edited is not null)
        {
            summary += Environment.NewLine + edited;
        }

        return summary;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        FormattedText widest = Format("~", Owner.Palette[DiffBrush.MarkerModified]);
        return new Size(widest.Width + 2 * HorizontalPadding, 0);
    }

    protected override void RenderCore(DrawingContext context, TextView textView)
    {
        _lastRendered.Clear();
        _lastModified.Clear();
        PaneMetadata metadata = Owner.Metadata;
        foreach (VisualLine line in textView.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            DiffLineKind kind = metadata.KindOf(number);
            _lastRendered.Add((number, kind));
            if (Owner.ModifiedLines.Contains(number))
            {
                _lastModified.Add(number);
                RenderModifiedBar(context, textView, line, metadata.PaddingBefore(number));
            }

            if (GlyphFor(kind) is not { } glyph)
            {
                continue;
            }

            FormattedText text = Format(glyph, Owner.Palette.MarkerFor(kind));
            context.DrawText(text, new Point(HorizontalPadding, TextTopOf(line, textView)));
        }
    }

    /// <summary>
    /// The bar drawn down the margin's inner edge for a line the user has edited since the
    /// source was assigned. It sits beside the diff's glyph rather than replacing it: the two
    /// answer different questions — what differs between the sides, and what this session
    /// changed — and a line can well be both.
    /// </summary>
    /// <remarks>
    /// It covers the line's own <em>row</em>, not its whole visual box. A visual box starts above
    /// the padding rows the other side's lines put there, and this session did not edit those —
    /// nobody did, they are not lines. A row rather than the text band, so that consecutive edited
    /// lines make one unbroken bar.
    /// </remarks>
    private void RenderModifiedBar(DrawingContext context, TextView textView, VisualLine line, int paddingAbove)
    {
        double rowHeight = textView.DefaultLineHeight;
        double top = line.VisualTop - textView.VerticalOffset + (paddingAbove * rowHeight);
        Rect bar = new(Bounds.Width - ModifiedBarWidth, top, ModifiedBarWidth, rowHeight);
        context.FillRectangle(Owner.Palette[DiffBrush.ModifiedSinceLoad], bar);
    }
}
