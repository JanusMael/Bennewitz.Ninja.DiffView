using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A one-character strip beside the line numbers: <c>+</c> for an inserted line, <c>−</c> for a
/// deleted one, <c>≠</c> for a modified one, in the marker brush of the kind, at the line's text
/// band; nothing for an unchanged line and nothing over padding space. One vocabulary of
/// mathematical operators, drawn semibold, so the kind is scannable down the gutter without
/// relying on colour alone — see <see cref="GlyphFor"/> for why these three. The tooltip follows the pointer
/// and, over a modified row whose line was too long for word-level pieces, says so.
/// </summary>
internal sealed class ChangeMarkerMargin : DiffMargin
{
    private const double HorizontalPadding = 4;

    /// <summary>The width of the modified-since-load bar down the margin's inner edge.</summary>
    private const double ModifiedBarWidth = 2;

    /// <summary>
    /// The weight the markers are drawn at. A line number is read one at a time; a marker is
    /// scanned down a column, and at this size the difference matters.
    /// </summary>
    private const FontWeight MarkerWeight = FontWeight.SemiBold;

    /// <summary>The corner radius of a marker chip.</summary>
    private const double ChipRadius = 3;

    /// <summary>What a chip leaves clear at each end of its run, so a badge reads as one.</summary>
    private const double ChipInset = 2;

    private readonly List<(int LineNumber, DiffLineKind Kind)> _lastRendered = [];
    private readonly List<int> _lastModified = [];
    private readonly List<(Rect Bounds, DiffLineKind Kind)> _lastChips = [];

    public ChangeMarkerMargin(DiffPanePresenter owner)
        : base(owner, nameof(ChangeMarkerMargin), DiffViewStrings.ChangeMarkersMarginName)
    {
    }

    /// <summary>The lines of the last frame and the kind each was drawn with, in order.</summary>
    public IReadOnlyList<(int LineNumber, DiffLineKind Kind)> LastRendered => _lastRendered;

    /// <summary>The lines that carried a modified-since-load bar in the last frame.</summary>
    public IReadOnlyList<int> LastModified => _lastModified;

    /// <summary>
    /// The marker chips of the last frame: one per run of same-kind rows, in order. A run that
    /// carries on past the viewport reports a rectangle that extends beyond it.
    /// </summary>
    public IReadOnlyList<(Rect Bounds, DiffLineKind Kind)> LastChips => _lastChips;

    /// <summary>The marker glyph for <paramref name="kind"/>; <c>null</c> for an unchanged line.</summary>
    /// <remarks>
    /// A minus sign rather than a hyphen, and a not-equal rather than a tilde. The glyph is the
    /// channel that carries the kind when colour cannot, and the ASCII pair failed at that: a
    /// hyphen laid down 5 pixels of ink against a digit's 29, so the mark meant to survive a
    /// colour-blind reader was the faintest thing in the gutter. Semibold alone did not fix it —
    /// a hyphen is a short bar at any weight. These three are one vocabulary of mathematical
    /// operators, they weigh 41, 25 and 56 pixels, and <c>≠</c> says what a modified row is
    /// more precisely than <c>~</c> ever did: the two sides are not equal. Chosen over heavier
    /// dingbat and box-drawing candidates because U+2212 and U+2260 are in every monospace font
    /// worth the name, and the pane font is the host's choice, not ours.
    /// </remarks>
    public static string? GlyphFor(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Inserted => "+",
            DiffLineKind.Deleted => "−",
            DiffLineKind.Modified => "≠",
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
        // Every glyph, not an assumed widest: the pane font is the host's, and a fallback for a
        // character it lacks need not share the family's advance width.
        double widest = 0;
        foreach (DiffLineKind kind in (DiffLineKind[])[DiffLineKind.Inserted, DiffLineKind.Deleted, DiffLineKind.Modified])
        {
            widest = Math.Max(widest, Format(GlyphFor(kind)!, Owner.Palette.MarkerFor(kind), MarkerWeight).Width);
        }

        return new Size(widest + (2 * HorizontalPadding), 0);
    }

    protected override void RenderCore(DrawingContext context, TextView textView)
    {
        _lastRendered.Clear();
        _lastModified.Clear();
        _lastChips.Clear();
        PaneMetadata metadata = Owner.Metadata;
        double rowHeight = textView.DefaultLineHeight;

        List<Row> rows = [];
        foreach (VisualLine line in textView.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            DiffLineKind kind = metadata.KindOf(number);
            _lastRendered.Add((number, kind));
            rows.Add(new Row(number, kind, RowTopOf(line, textView, metadata.PaddingBefore(number), rowHeight), line));
        }

        RenderChips(context, metadata, rows, rowHeight);

        foreach (Row row in rows)
        {
            if (Owner.ModifiedLines.Contains(row.Number))
            {
                _lastModified.Add(row.Number);
                RenderModifiedBar(context, textView, row.Line, metadata.PaddingBefore(row.Number));
            }

            if (GlyphFor(row.Kind) is not { } glyph)
            {
                continue;
            }

            // Centred in the chip rather than placed at the padding: the chip is symmetric about
            // the margin's centre, and a host font's fallback for one of the three characters need
            // not share the family's advance width.
            FormattedText text = Format(glyph, Owner.Palette.MarkerFor(row.Kind), MarkerWeight);
            context.DrawText(text, new Point((Bounds.Width - text.Width) / 2, TextTopOf(row.Line, textView)));
        }
    }

    /// <summary>One visible line, with the top of the row it owns.</summary>
    private readonly record struct Row(int Number, DiffLineKind Kind, double RowTop, VisualLine Line);

    /// <summary>The top of a line's own row: its visual top, past any padding above it.</summary>
    private static double RowTopOf(VisualLine line, TextView textView, int paddingAbove, double rowHeight)
    {
        return line.VisualTop - textView.VerticalOffset + (paddingAbove * rowHeight);
    }

    /// <summary>
    /// One rounded chip per run of consecutive same-kind rows, so a lone changed line reads as a
    /// badge and a block reads as one band — the block's extent, which neither the glyph nor the
    /// row tint shows in the gutter. There is no second code path for the two cases: a one-row run
    /// is simply a short rectangle with the same corner radius.
    /// </summary>
    private void RenderChips(DrawingContext context, PaneMetadata metadata, List<Row> rows, double rowHeight)
    {
        for (int i = 0; i < rows.Count;)
        {
            DiffLineKind kind = rows[i].Kind;
            if (GlyphFor(kind) is null)
            {
                i++;
                continue;
            }

            // Adjacent lines of one changed kind are always adjacent *rows*, so a run needs no
            // padding test. A side's lines inside a block are contiguous — the layout gives this
            // side its rows first and the other side's padding after — and blocks are separated by
            // at least one unchanged row, which ends the run by kind. A mutation that ran the loop
            // through padding changed no frame, which is how the guard was found to be unreachable.
            int last = i;
            while (last + 1 < rows.Count && rows[last + 1].Kind == kind)
            {
                last++;
            }

            // A run that carries on past the viewport gets no rounded end there: the rectangle
            // runs a row beyond the edge so the rounding falls outside the visible area, rather
            // than making a scrolled block look as though it ends where the window does.
            bool continuesAbove = metadata.KindOf(rows[i].Number - 1) == kind;
            bool continuesBelow = metadata.KindOf(rows[last].Number + 1) == kind;
            double top = rows[i].RowTop + (continuesAbove ? -rowHeight : ChipInset);
            double bottom = rows[last].RowTop + rowHeight + (continuesBelow ? rowHeight : -ChipInset);

            // Symmetric about the margin's centre, stopping exactly where the modified-since-load
            // bar begins, so an edited line inside a changed block paints both without overlap.
            Rect chip = new(ModifiedBarWidth, top, Bounds.Width - (2 * ModifiedBarWidth), bottom - top);
            double radius = Math.Min(ChipRadius, chip.Height / 2);
            context.DrawRectangle(Owner.Palette.MarkerChipFor(kind), null, chip, radius, radius);
            _lastChips.Add((chip, kind));
            i = last + 1;
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
