using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Bennewitz.Ninja.DiffView.Core;

// The assembly has an implicit `using System.IO`, whose Path is not this one.
using Path = Avalonia.Controls.Shapes.Path;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The icons for the menu column plan 00010 reserved and left empty. No new visual language: the
/// copy entries carry the gutter's own arrow, and the entries about a change carry the marker
/// margin's own operator for that change's kind.
/// </summary>
/// <remarks>
/// <para>
/// Geometry rather than text, and painted from the inherited <see cref="TextElement.Foreground"/>
/// rather than from <c>DiffBrushes</c>. Two reasons. A menu is drawn in the host theme's font,
/// which is not the pane font the marker margin measured its three operators against — <c>+</c>,
/// <c>−</c> and <c>≠</c> were chosen for weighing 41, 25 and 56 pixels *there* — so a glyph's
/// ink is no longer something this library can predict. And an icon beside a label should read as
/// part of the label: the gutter's yellows and blues carry meaning against the gutter's own
/// background and would be an unexplained second palette in a menu. Following the foreground
/// means a theme or variant swap carries the icons with it and contrast is the host's, already
/// solved for its own menu text.
/// </para>
/// <para>
/// Only the entries that have something to show carry one. Next, previous, find, save, revert,
/// go-to-row and hide-the-map are left null, which is what the reserved column was reserved for:
/// a menu where some rows have icons and some do not is the arrangement that would misalign if
/// the column were not laid out whether or not anything filled it.
/// </para>
/// </remarks>
internal static class DiffMenuIcons
{
    /// <summary>The icon's square side, matching the gutter arrow's own zone.</summary>
    public const double Size = CopyArrowGlyph.Size;

    /// <summary>How thick the operator strokes are drawn.</summary>
    private const double StrokeWidth = 1.5;

    /// <summary>How far the operators inset from the zone, so they optically match the arrow.</summary>
    private const double Inset = 1.5;

    /// <summary>
    /// The copy arrow, pointing the way the text would travel — the same silhouette the gutter
    /// draws over a line number, from the same geometry.
    /// </summary>
    public static Control CopyArrow(DiffSide toSide)
    {
        Path arrow = new()
        {
            Data = CopyArrowGlyph.Geometry(new Rect(0, 0, Size, Size), pointsLeft: toSide == DiffSide.Left),
        };

        arrow[!Shape.FillProperty] = arrow[!TextElement.ForegroundProperty];
        return Sized(arrow);
    }

    /// <summary>
    /// The marker margin's operator for <paramref name="kind"/> — <c>+</c>, <c>−</c>, <c>≠</c> —
    /// as strokes, or <c>null</c> for an unchanged line, which has no kind to show.
    /// </summary>
    public static Control? Operator(DiffLineKind kind)
    {
        if (Strokes(kind) is not { } geometry)
        {
            return null;
        }

        Path glyph = new()
        {
            Data = geometry,
            StrokeThickness = StrokeWidth,
            StrokeLineCap = PenLineCap.Round,
        };

        glyph[!Shape.StrokeProperty] = glyph[!TextElement.ForegroundProperty];
        return Sized(glyph);
    }

    /// <summary>The zone the shape is laid out in, so a row's icon never resizes its column.</summary>
    private static Control Sized(Path shape)
    {
        shape.Width = Size;
        shape.Height = Size;
        shape.Stretch = Stretch.None;
        return shape;
    }

    /// <summary>
    /// The operator's strokes: a bar for a deletion, a bar and an upright for an insertion, and
    /// two bars under a slash for a modification — <c>≠</c> saying "these two are not equal",
    /// which is the reading the margin chose it for.
    /// </summary>
    /// <remarks>
    /// A <see cref="PathGeometry"/> rather than a <see cref="StreamGeometry"/>, which the arrow
    /// uses: a stream geometry is opaque once closed — it does not enumerate its figures and its
    /// <c>ToString</c> is the type name — so a test asserting an operator is drawn from the right
    /// number of strokes has nothing to read. This one carries its figures.
    /// </remarks>
    private static PathGeometry? Strokes(DiffLineKind kind)
    {
        double low = Inset;
        double high = Size - Inset;
        double middle = Size / 2;
        const double Gap = 2;

        PathGeometry geometry = new();
        switch (kind)
        {
            case DiffLineKind.Inserted:
                Line(geometry, new Point(low, middle), new Point(high, middle));
                Line(geometry, new Point(middle, low), new Point(middle, high));
                break;

            case DiffLineKind.Deleted:
                Line(geometry, new Point(low, middle), new Point(high, middle));
                break;

            case DiffLineKind.Modified:
                Line(geometry, new Point(low, middle - Gap), new Point(high, middle - Gap));
                Line(geometry, new Point(low, middle + Gap), new Point(high, middle + Gap));
                Line(geometry, new Point(high - 1, low), new Point(low + 1, high));
                break;

            default:
                return null;
        }

        return geometry;
    }

    private static void Line(PathGeometry geometry, Point from, Point to)
    {
        geometry.Figures ??= [];
        geometry.Figures.Add(new PathFigure
        {
            StartPoint = from,
            IsClosed = false,
            IsFilled = false,
            Segments = [new LineSegment { Point = to }],
        });
    }
}
