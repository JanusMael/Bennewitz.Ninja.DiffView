using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// One soft break inside a paragraph, as offsets into the text it was read from: the characters a fold turns into one
/// space, the spaces and tabs ending the line through the indent and any quote markers beginning the next.
/// </summary>
/// <param name="Start">The first character the break takes: the first of the spaces ending its line, or the line ending.</param>
/// <param name="End">The first character after it, where the next line's text begins.</param>
internal readonly record struct SoftBreak(int Start, int End);

/// <summary>One paragraph's text and its soft breaks, as offsets into the text it was read from.</summary>
/// <param name="Start">Where the paragraph's text begins, past whatever its containers put before it on its first line.</param>
/// <param name="End">Where its text ends, after its last inline and before the spaces CommonMark strips from its last line.</param>
/// <param name="Breaks">Its soft breaks, in the order of their offsets.</param>
internal sealed record ParagraphText(int Start, int End, IReadOnlyList<SoftBreak> Breaks)
{
    /// <summary>
    /// The paragraph's lines as a fold reads them from <paramref name="text"/>, the text it was read from: the runs
    /// between its soft breaks, without what a break takes. Joined with one space they are its words, the paragraph as
    /// <see cref="ParagraphBreaks.Fold"/> writes it.
    /// </summary>
    internal IReadOnlyList<string> LinesIn(string text)
    {
        List<string> lines = new(Breaks.Count + 1);
        int from = Start;
        foreach (SoftBreak join in Breaks)
        {
            lines.Add(text[from..join.Start]);
            from = join.End;
        }

        lines.Add(text[from..End]);
        return lines;
    }
}

/// <summary>
/// Plan 00036 decision 1: a soft line break inside a paragraph is one space. Where a paragraph breaks its lines is
/// layout, not content, so two texts that differ only there fold to one text.
/// </summary>
/// <remarks>
/// <para>
/// CommonMark renders a soft break as whitespace, and removes the spaces that end its line and those that begin the
/// next. <see cref="Fold"/> writes one space in their place, taking the tabs there too, and the markers a quote puts at
/// the start of the next line, the folded line being inside the same containers. Every other line break it leaves as
/// it is: a hard break, two trailing spaces or a backslash; a heading; a fenced or an indented code block; an HTML
/// block; a table; a blank line; and the structure of a list or a quote.
/// </para>
/// <para>
/// ⛔ <b>The breaks are read from Markdig's inline tree, never from a paragraph's lines.</b> Measured against Markdig
/// 1.4.1 before this was written: a <c>ParagraphBlock</c>'s <c>Lines</c> are empty once its inlines are parsed, and a
/// soft break is a <c>LineBreakInline</c> whose <c>IsHard</c> is false, its span on the line ending, the inline after
/// it starting where the next line's text does, past the indent and any <c>&gt;</c> a quote puts there. A
/// <c>&gt;</c> that is text, as on a continuation line indented four spaces, is where that inline starts, so it stays;
/// an escape's inline starts at its backslash. A setext heading's lines are one literal, with no break in it at all.
/// </para>
/// <para>
/// ⭐ <b>One reading of where a paragraph's lines break</b>, <see cref="Paragraphs"/>, which the fold and the shown
/// candidate share (plan 00036 decision 4), so the two never disagree about what a paragraph's words are. A paragraph
/// is where its inlines are, measured: a <c>ParagraphBlock</c>'s own span takes in the spaces ending its last line,
/// and above a table that pipe tables carved out of it, the whole table.
/// </para>
/// <para>
/// ⚠ <b>A soft break with nothing after it in its paragraph is kept.</b> Two shapes leave one, both measured: pipe
/// tables take a table out of the paragraph above it and leave that paragraph its break before the table's first row,
/// and a link whose text ends at a line ending keeps the break as its last child, the next line opening with the
/// link's <c>](…)</c>. Joining either would put a table's row or a link's syntax in the middle of a line. Neither
/// occurs in the 2026-11-10 corpus.
/// </para>
/// <para>
/// ⚠ A line ending inside a code span or an HTML tag is not a soft break as CommonMark defines one, and is kept,
/// though a code span renders it as a space. None crosses a line ending in the 2026-11-10 corpus, whose seed holds 138
/// soft breaks and no hard one.
/// </para>
/// <para>
/// The parse has pipe tables, which <see cref="MarkdownOutline.Pipeline"/> does not: under the outline's pipeline a
/// table is a paragraph whose rows a fold would join, and the seed of the 2026-11-10 corpus holds one. It keeps the
/// outline's front matter, so a document's YAML is never read as paragraphs.
/// </para>
/// </remarks>
internal static class ParagraphBreaks
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseYamlFrontMatter().UsePipeTables().Build();

    /// <summary>
    /// <paramref name="text"/> with each soft break inside a paragraph turned into one space. Text with no soft break
    /// comes out as it went in, byte for byte, once its line endings are LF, which every text this tool holds after
    /// ingest has.
    /// </summary>
    internal static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalised = MarkdownOutline.NormaliseLineEndings(text);
        StringBuilder folded = new(normalised.Length);
        int copied = 0;

        foreach (ParagraphText paragraph in Paragraphs(normalised))
        {
            foreach (SoftBreak join in paragraph.Breaks)
            {
                folded.Append(normalised, copied, join.Start - copied).Append(' ');
                copied = join.End;
            }
        }

        return folded.Append(normalised, copied, normalised.Length - copied).ToString();
    }

    /// <summary>Every paragraph of <paramref name="normalised"/>, in document order, with its soft breaks.</summary>
    /// <param name="normalised">A text whose line endings are LF, as every text this tool holds after ingest is.</param>
    internal static IReadOnlyList<ParagraphText> Paragraphs(string normalised)
    {
        ArgumentNullException.ThrowIfNull(normalised);

        MarkdownDocument document = Markdig.Markdown.Parse(normalised, Pipeline);
        List<ParagraphText> paragraphs = [];

        // Descendants walks the blocks, and each paragraph's inlines, in document order, so the breaks come in the
        // order of their offsets: one out of that order would hand a fold a negative length and throw, never fold the
        // wrong text.
        foreach (ParagraphBlock paragraph in document.Descendants<ParagraphBlock>())
        {
            if (paragraph.Inline is not { } inlines)
            {
                continue;
            }

            List<SoftBreak> breaks = [];
            foreach (LineBreakInline lineBreak in inlines.Descendants<LineBreakInline>())
            {
                Inline? next = lineBreak.NextSibling;

                if (lineBreak.IsHard || next is null)
                {
                    continue;
                }

                // The break's span is its line ending; the spaces and tabs before it go with it.
                int end = lineBreak.Span.Start;
                while (end > 0 && normalised[end - 1] is ' ' or '\t')
                {
                    end--;
                }

                breaks.Add(new SoftBreak(end, next.Span.Start));
            }

            paragraphs.Add(new ParagraphText(inlines.Span.Start, inlines.Span.End + 1, breaks));
        }

        return paragraphs;
    }
}
