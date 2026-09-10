using Avalonia;
using Avalonia.Media;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The copy-to-side arrow: a head at the leading edge of its square zone and a shaft running back
/// from it. A bare triangle reads as a wedge, and two of them facing away from each other read as
/// one glyph rather than as two things to click, which is why the shaft exists and why
/// <see cref="InnerGap"/> is left unpainted at the trailing edge.
/// </summary>
/// <remarks>
/// Laid out from the tip inwards, so the head sits on the zone's leading edge whatever the rest is
/// set to. The zone is square and <see cref="Size"/> a side; a caller that has less room than that
/// draws nothing rather than a clipped arrow.
/// </remarks>
internal static class CopyArrowGlyph
{
    /// <summary>The side of the arrow's square zone, in pixels.</summary>
    public const double Size = 12;

    /// <summary>How far the tip sits inside the zone's leading edge.</summary>
    private const double TipInset = 1;

    /// <summary>The head, along the shaft and across it.</summary>
    private const double HeadLength = 5;
    private const double HeadHalfHeight = 4.5;

    /// <summary>The shaft behind the head, along it and across it.</summary>
    private const double ShaftLength = 4;
    private const double ShaftHalfHeight = 1.5;

    /// <summary>What the tip inset, the head and the shaft leave unpainted at the trailing edge.</summary>
    public const double InnerGap = Size - TipInset - HeadLength - ShaftLength;

    /// <summary>The tail bar's half-height: the head's, so the bar caps a shaft three times narrower.</summary>
    private const double BarHalfHeight = HeadHalfHeight;

    /// <summary>
    /// Draws one arrow filling <paramref name="zone"/>, pointing left or right: the silhouette in
    /// <paramref name="outline"/> and the interior in <paramref name="fill"/>. Two colours rather
    /// than one because a flat silhouette reads as a mark and an outlined shape reads as a control.
    /// </summary>
    /// <remarks>
    /// <paramref name="tailBar"/> draws a stroke across the shaft's trailing end, which reads as
    /// "this bounded thing goes that way" against a plain arrow's "that way" and survives a
    /// reading with no colour at all. Which arrows carry one is the palette's decision and not
    /// this method's: the caller always lays the bar out and paints it in a token brush, so a
    /// transparent one simply is not seen.
    /// </remarks>
    public static void Draw(DrawingContext context, IBrush fill, IPen outline, Rect zone, bool pointsLeft, IPen? tailBar = null)
    {
        double tipX = pointsLeft ? zone.Left + TipInset : zone.Right - TipInset;
        double inwards = pointsLeft ? 1 : -1;
        double headBackX = tipX + (inwards * HeadLength);
        double shaftEndX = pointsLeft ? zone.Right - InnerGap : zone.Left + InnerGap;
        double centreY = zone.Center.Y;

        StreamGeometry arrow = new();
        using (StreamGeometryContext path = arrow.Open())
        {
            path.BeginFigure(new Point(tipX, centreY), isFilled: true);
            path.LineTo(new Point(headBackX, centreY - HeadHalfHeight));
            path.LineTo(new Point(headBackX, centreY - ShaftHalfHeight));
            path.LineTo(new Point(shaftEndX, centreY - ShaftHalfHeight));
            path.LineTo(new Point(shaftEndX, centreY + ShaftHalfHeight));
            path.LineTo(new Point(headBackX, centreY + ShaftHalfHeight));
            path.LineTo(new Point(headBackX, centreY + HeadHalfHeight));
            path.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fill, outline, arrow);

        if (tailBar is not null)
        {
            context.DrawLine(tailBar, new Point(shaftEndX, centreY - BarHalfHeight), new Point(shaftEndX, centreY + BarHalfHeight));
        }
    }
}
