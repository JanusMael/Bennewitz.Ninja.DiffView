using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Reads pixels out of a captured frame, for assertions of the form "this band carries the
/// selection brush and that band does not". Coordinates are window pixels; the headless host
/// renders at scale 1, so they equal layout units.
/// </summary>
internal static class PixelProbe
{
    public static Color At(WriteableBitmap frame, int x, int y)
    {
        using ILockedFramebuffer buffer = frame.Lock();
        return Read(buffer, x, y);
    }

    /// <summary>Number of pixels inside <paramref name="area"/> (clipped to the frame) that satisfy <paramref name="predicate"/>.</summary>
    public static int Count(WriteableBitmap frame, PixelRect area, Func<Color, bool> predicate)
    {
        using ILockedFramebuffer buffer = frame.Lock();
        PixelRect clipped = area.Intersect(new PixelRect(buffer.Size));
        int count = 0;
        for (int y = clipped.Y; y < clipped.Bottom; y++)
        {
            for (int x = clipped.X; x < clipped.Right; x++)
            {
                if (predicate(Read(buffer, x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// The ink laid over <paramref name="ground"/> in <paramref name="ink"/> inside <paramref name="area"/>,
    /// as coverage rather than a count: each pixel contributes how far it sits along the colour axis
    /// from the ground to the ink, clamped to [0, 1], so an edge a rasterizer antialiased counts for the
    /// share of it the glyph covered. Also the ink's horizontal centroid in window pixels, NaN when
    /// there is none.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A pixel count past a colour threshold measures one rasterizer's rounding, not the ink.</b>
    /// The same '−' at the same size, in the same bundled font, counted 15 exact-colour pixels either
    /// side of its centre on Linux and 0 on Windows and macOS, which antialias it so that no pixel
    /// reaches the marker's colour; its coverage read 50, 54 and 37.
    /// </remarks>
    public static (double Mass, double CentroidX) Coverage(WriteableBitmap frame, PixelRect area, Color ground, Color ink)
    {
        double dr = ink.R - ground.R;
        double dg = ink.G - ground.G;
        double db = ink.B - ground.B;
        double axis = (dr * dr) + (dg * dg) + (db * db);
        if (axis == 0)
        {
            throw new ArgumentException("The ink and the ground are one colour, so no coverage can be read.", nameof(ink));
        }

        using ILockedFramebuffer buffer = frame.Lock();
        PixelRect clipped = area.Intersect(new PixelRect(buffer.Size));
        double mass = 0;
        double weighted = 0;
        for (int y = clipped.Y; y < clipped.Bottom; y++)
        {
            for (int x = clipped.X; x < clipped.Right; x++)
            {
                Color pixel = Read(buffer, x, y);
                double along = (((pixel.R - ground.R) * dr) + ((pixel.G - ground.G) * dg) + ((pixel.B - ground.B) * db)) / axis;
                double coverage = Math.Clamp(along, 0, 1);
                mass += coverage;
                weighted += coverage * (x + 0.5);
            }
        }

        return (mass, mass > 0 ? weighted / mass : double.NaN);
    }

    /// <summary>A rectangle from doubles, rounded inward so anti-aliased edges are excluded.</summary>
    public static PixelRect Inside(double left, double top, double right, double bottom, int inset = 1)
    {
        int x = (int)Math.Ceiling(left) + inset;
        int y = (int)Math.Ceiling(top) + inset;
        int r = (int)Math.Floor(right) - inset;
        int b = (int)Math.Floor(bottom) - inset;
        return new PixelRect(x, y, Math.Max(0, r - x), Math.Max(0, b - y));
    }

    public static bool IsDark(Color c) => c.R < 100 && c.G < 100 && c.B < 100;

    public static bool IsRed(Color c) => c.R > 180 && c.G < 90 && c.B < 90;

    public static bool IsBlue(Color c) => c.B > 180 && c.R < 90 && c.G < 90;

    private static Color Read(ILockedFramebuffer buffer, int x, int y)
    {
        if (x < 0 || y < 0 || x >= buffer.Size.Width || y >= buffer.Size.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the {buffer.Size.Width}×{buffer.Size.Height} frame.");
        }

        int value = Marshal.ReadInt32(buffer.Address, y * buffer.RowBytes + x * 4);
        byte b0 = (byte)value;
        byte b1 = (byte)(value >> 8);
        byte b2 = (byte)(value >> 16);
        byte b3 = (byte)(value >> 24);

        if (buffer.Format == PixelFormat.Bgra8888)
        {
            return Color.FromArgb(b3, b2, b1, b0);
        }

        if (buffer.Format == PixelFormat.Rgba8888)
        {
            return Color.FromArgb(b3, b0, b1, b2);
        }

        throw new NotSupportedException($"Frame pixel format {buffer.Format} is not handled.");
    }
}
