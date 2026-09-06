using System.Runtime.CompilerServices;
using SkiaSharp;
using VerifyTests;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// Verify configuration: snapshots live under <c>Snapshots/</c>, and PNGs are compared by a
/// SkiaSharp tolerance comparer (Skia is already loaded for rendering) instead of byte equality,
/// so anti-aliasing differences between platforms do not fail a snapshot while a moved
/// highlight still does. On a mismatch Verify writes the <c>.received.png</c> beside the
/// <c>.verified.png</c> and DiffEngine opens the pair in Beyond Compare when it is installed.
/// </summary>
internal static class VerifySetup
{
    /// <summary>Per-channel difference below which two pixels count as the same.</summary>
    private const int ChannelTolerance = 8;

    /// <summary>Fraction of differing pixels above which the images are not equal.</summary>
    private const double MaxDifferingFraction = 0.005;

    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.RegisterStreamComparer("png", ComparePng);
        Verifier.DerivePathInfo((_, projectDirectory, type, method) =>
            new PathInfo(Path.Combine(projectDirectory, "Snapshots"), type.Name, method.Name));
    }

    private static Task<CompareResult> ComparePng(Stream received, Stream verified, IReadOnlyDictionary<string, object> context)
    {
        // Decoded from copies: SkiaSharp closes a stream it decodes, and on a mismatch Verify
        // reads the received stream again to write the .received file.
        using SKBitmap? receivedBitmap = SKBitmap.Decode(ReadAll(received));
        using SKBitmap? verifiedBitmap = SKBitmap.Decode(ReadAll(verified));
        if (receivedBitmap is null || verifiedBitmap is null)
        {
            return Task.FromResult(CompareResult.NotEqual("A PNG could not be decoded."));
        }

        if (receivedBitmap.Width != verifiedBitmap.Width || receivedBitmap.Height != verifiedBitmap.Height)
        {
            return Task.FromResult(CompareResult.NotEqual(
                $"Size differs: received {receivedBitmap.Width}×{receivedBitmap.Height}, verified {verifiedBitmap.Width}×{verifiedBitmap.Height}."));
        }

        SKColor[] receivedPixels = receivedBitmap.Pixels;
        SKColor[] verifiedPixels = verifiedBitmap.Pixels;
        long differing = 0;
        for (int i = 0; i < receivedPixels.Length; i++)
        {
            if (!SamePixel(receivedPixels[i], verifiedPixels[i]))
            {
                differing++;
            }
        }

        double fraction = receivedPixels.Length == 0 ? 0 : differing / (double)receivedPixels.Length;
        return Task.FromResult(fraction <= MaxDifferingFraction
            ? CompareResult.Equal
            : CompareResult.NotEqual($"{fraction:P2} of pixels differ ({differing} of {receivedPixels.Length})."));
    }

    private static byte[] ReadAll(Stream stream)
    {
        long position = stream.CanSeek ? stream.Position : 0;
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        return buffer.ToArray();
    }

    private static bool SamePixel(SKColor a, SKColor b)
    {
        return Math.Abs(a.Red - b.Red) <= ChannelTolerance
               && Math.Abs(a.Green - b.Green) <= ChannelTolerance
               && Math.Abs(a.Blue - b.Blue) <= ChannelTolerance
               && Math.Abs(a.Alpha - b.Alpha) <= ChannelTolerance;
    }
}
