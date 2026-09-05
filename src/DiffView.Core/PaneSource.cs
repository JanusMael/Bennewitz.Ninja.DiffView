using System.Text;

namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>
/// One side's input: the text to compare and what is known about where it came from. Built
/// from a string directly (implicit conversion) or from bytes, which decodes by byte-order
/// mark, then strict UTF-8, then Latin-1 as the last resort, and detects binary content on
/// the bytes before any decoding.
/// </summary>
public sealed record PaneSource
{
    /// <summary>How many leading bytes the binary check inspects; a NUL among them marks the input binary.</summary>
    public const int BinaryProbeLength = 8192;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <param name="text">The text to compare. Never null; an empty side is the empty string.</param>
    public PaneSource(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>The text to compare.</summary>
    public string Text { get; }

    /// <summary>The encoding the bytes were decoded with, when the source came from bytes.</summary>
    public Encoding? Encoding { get; init; }

    /// <summary>The file the text came from, when it came from one.</summary>
    public string? Path { get; init; }

    /// <summary>What to call this side in a header; the file name when a path is known.</summary>
    public string? Title { get; init; }

    /// <summary>Whether the bytes looked binary (a NUL within the first <see cref="BinaryProbeLength"/> bytes).</summary>
    public bool IsBinary { get; init; }

    /// <summary>Whether the bytes were neither BOM-marked nor valid UTF-8, so Latin-1 was used.</summary>
    public bool Latin1Fallback { get; init; }

    /// <summary>A source from plain text.</summary>
    public static implicit operator PaneSource(string text) => new(text);

    /// <summary>
    /// Decodes <paramref name="bytes"/>: a UTF-8, UTF-16 or UTF-32 byte-order mark decides;
    /// otherwise strict UTF-8; otherwise Latin-1 with <see cref="Latin1Fallback"/> set. A NUL
    /// among the first <see cref="BinaryProbeLength"/> bytes marks the source <see cref="IsBinary"/>
    /// (the text is still decoded, so a caller can show something).
    /// </summary>
    public static PaneSource FromBytes(byte[] bytes, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        bool binary = bytes.AsSpan(0, Math.Min(bytes.Length, BinaryProbeLength)).IndexOf((byte)0) >= 0;
        (Encoding encoding, int preamble) = DetectByPreamble(bytes);

        string text;
        bool fallback = false;
        if (preamble > 0)
        {
            text = encoding.GetString(bytes, preamble, bytes.Length - preamble);
        }
        else
        {
            try
            {
                text = StrictUtf8.GetString(bytes);
                encoding = StrictUtf8;
            }
            catch (DecoderFallbackException)
            {
                encoding = Encoding.Latin1;
                text = encoding.GetString(bytes);
                fallback = true;
            }
        }

        return new PaneSource(text)
        {
            Encoding = encoding,
            Path = path,
            Title = path is null ? null : System.IO.Path.GetFileName(path),
            IsBinary = binary,
            Latin1Fallback = fallback,
        };
    }

    /// <summary>Reads <paramref name="path"/> and decodes it as <see cref="FromBytes"/> does.</summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static PaneSource FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromBytes(File.ReadAllBytes(path), path);
    }

    private static (Encoding Encoding, int PreambleLength) DetectByPreamble(byte[] bytes)
    {
        // UTF-32 before UTF-16: FF FE 00 00 would otherwise read as UTF-16 LE.
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return (Encoding.UTF32, 4);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return (new UTF32Encoding(bigEndian: true, byteOrderMark: true), 4);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Encoding.UTF8, 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode, 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode, 2);
        }

        return (StrictUtf8, 0);
    }
}
