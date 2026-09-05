using System.Text;

namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>The line-terminator convention a text uses.</summary>
public enum LineEnding
{
    /// <summary>No terminator at all: a single line.</summary>
    None,

    /// <summary><c>\n</c> only.</summary>
    Lf,

    /// <summary><c>\r\n</c> only.</summary>
    CrLf,

    /// <summary><c>\r</c> only.</summary>
    Cr,

    /// <summary>More than one kind.</summary>
    Mixed,
}

/// <summary>What probing a side's text established, plus what the loader knew about its bytes.</summary>
public sealed record TextInfo
{
    /// <summary>The terminator convention.</summary>
    public required LineEnding LineEnding { get; init; }

    /// <summary>Lines as an editor counts them: terminators plus one, so the empty text has one line.</summary>
    public required int LineCount { get; init; }

    /// <summary>Length in UTF-16 code units.</summary>
    public required int Length { get; init; }

    /// <summary>NUL characters in the decoded text.</summary>
    public required int NulCount { get; init; }

    /// <summary>The encoding the bytes were decoded with, when known.</summary>
    public Encoding? Encoding { get; init; }

    /// <summary>Whether the loader judged the bytes binary.</summary>
    public bool IsBinary { get; init; }

    /// <summary>Whether the loader fell back to Latin-1.</summary>
    public bool Latin1Fallback { get; init; }

    /// <summary>Whether the text uses the <c>\r</c>-only or mixed convention the diff and the editor both tolerate but a user should see flagged.</summary>
    public bool HasIrregularLineEndings => LineEnding is LineEnding.Cr or LineEnding.Mixed;
}

/// <summary>Counts what the builder and the headers need to know about a text, in one pass.</summary>
public static class TextProbe
{
    /// <summary>Probes <paramref name="text"/>.</summary>
    public static TextInfo Probe(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int lf = 0;
        int crlf = 0;
        int cr = 0;
        int nul = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                lf++;
            }
            else if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (c == '\0')
            {
                nul++;
            }
        }

        int kinds = (lf > 0 ? 1 : 0) + (crlf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
        LineEnding ending = kinds switch
        {
            0 => LineEnding.None,
            1 when lf > 0 => LineEnding.Lf,
            1 when crlf > 0 => LineEnding.CrLf,
            1 => LineEnding.Cr,
            _ => LineEnding.Mixed,
        };

        return new TextInfo
        {
            LineEnding = ending,
            LineCount = lf + crlf + cr + 1,
            Length = text.Length,
            NulCount = nul,
        };
    }

    /// <summary>Probes a source's text and carries the loader's facts across.</summary>
    public static TextInfo Probe(PaneSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Probe(source.Text) with
        {
            Encoding = source.Encoding,
            IsBinary = source.IsBinary,
            Latin1Fallback = source.Latin1Fallback,
        };
    }
}
