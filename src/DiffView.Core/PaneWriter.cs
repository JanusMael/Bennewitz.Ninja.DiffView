using System.Text;

namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>
/// Writes a pane's text back as the bytes it was read from: the encoding the loader detected,
/// the byte-order mark that encoding carries, and the line-terminator convention the text
/// arrived with.
/// </summary>
/// <remarks>
/// The byte-order mark needs no separate flag because the encoding already decides it.
/// <see cref="PaneSource.FromBytes"/> hands back <see cref="Encoding.UTF8"/> for a file whose
/// preamble said so, and a preamble-less <see cref="UTF8Encoding"/> for one that had none;
/// <see cref="Encoding.GetPreamble"/> then returns three bytes or none accordingly. The same
/// holds for the UTF-16 and UTF-32 encodings that a preamble selects.
/// </remarks>
public static class PaneWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    /// <summary>
    /// Rewrites <paramref name="text"/>'s line terminators as <paramref name="lineEnding"/>.
    /// <see cref="LineEnding.None"/> and <see cref="LineEnding.Mixed"/> return the text unchanged:
    /// there is no single convention to impose, and inventing one would rewrite lines the user
    /// never touched.
    /// </summary>
    public static string Normalize(string text, LineEnding lineEnding)
    {
        ArgumentNullException.ThrowIfNull(text);

        string target = lineEnding switch
        {
            LineEnding.Lf => "\n",
            LineEnding.CrLf => "\r\n",
            LineEnding.Cr => "\r",
            _ => string.Empty,
        };

        if (target.Length == 0)
        {
            return text;
        }

        StringBuilder builder = new(text.Length + 16);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                // A CRLF pair is one terminator, not two.
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                builder.Append(target);
            }
            else if (c == '\n')
            {
                builder.Append(target);
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The bytes <paramref name="text"/> becomes under <paramref name="encoding"/> and
    /// <paramref name="lineEnding"/>, preamble included when the encoding carries one. A null
    /// encoding — a side built from a string rather than from bytes — writes UTF-8 without a mark.
    /// </summary>
    public static byte[] ToBytes(string text, Encoding? encoding, LineEnding lineEnding)
    {
        ArgumentNullException.ThrowIfNull(text);

        Encoding target = encoding ?? Utf8NoBom;
        string normalized = Normalize(text, lineEnding);
        byte[] preamble = target.GetPreamble();
        byte[] body = target.GetBytes(normalized);
        if (preamble.Length == 0)
        {
            return body;
        }

        byte[] all = new byte[preamble.Length + body.Length];
        preamble.CopyTo(all, 0);
        body.CopyTo(all, preamble.Length);
        return all;
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="path"/> as <see cref="ToBytes"/> encodes
    /// it. The write replaces the file wholesale; nothing here merges.
    /// </summary>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void Write(string path, string text, Encoding? encoding, LineEnding lineEnding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, ToBytes(text, encoding, lineEnding));
    }
}
