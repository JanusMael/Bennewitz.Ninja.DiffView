using System.Text;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// A candidate as the right pane shows it — plan 00036 decisions 4, 5, 6 and 8: each paragraph is broken where the
/// accumulator breaks the paragraph holding its words, or else the one it is a version of, so where the words are the
/// accumulator's the lines are too, and the diff shows a change only where the words differ.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Only the right is shown other than as it is held.</b> The left is the baseline, the text a save writes, and is
/// never re-broken. A candidate is read and never written, so showing it re-broken changes nothing that is kept but what
/// a take copies, which is the text the operator read (decision 7).
/// </para>
/// <para>
/// A paragraph is matched by its words, its lines joined as <see cref="ParagraphBreaks.Fold"/> joins them: to the
/// accumulator's paragraph holding the same words in the section at the same heading path, or else to the first holding
/// them anywhere. It is then shown in that paragraph's lines (decision 4).
/// </para>
/// <para>
/// A paragraph holding no accumulator paragraph's words is shown in its counterpart's lines (decision 5). Its
/// counterpart is the accumulator's paragraph that a line diff of the paragraphs at its heading path, one paragraph's
/// words a line, pairs it with. A word diff of the two, one word a line, pairs each of the counterpart's words with one
/// of the paragraph's or with none, and each of the counterpart's lines after its first starts at the word paired with
/// its own first word, or where that word would be had the candidate kept it. So a line starts at the accumulator's word
/// wherever the candidate kept it, a word the candidate added where two lines meet ends the first of them, and a changed
/// word sits in a line pair of its own while every line around it pairs with an equal line. A break never lands in a run
/// of spaces, which a fold reads as one space. Both diffs are the public <see cref="DiffDocumentBuilder.Build"/>, a line
/// ending inside a paragraph's words, a hard break's or one a code span holds, read there as a space so that each
/// paragraph and each word is one line. A paragraph under a heading the accumulator does not have, or one the line diff
/// pairs with none, is shown as the candidate wrote it.
/// </para>
/// <para>
/// Each line after a paragraph's first opens as its own first line does, a list item's marker turned to spaces and a
/// quote's <c>&gt;</c> kept, so the line stays inside the containers the paragraph is in.
/// </para>
/// <para>
/// ⛔ <b>Every shown text is checked before it is shown</b> (decision 6). A line that begins with <c>-</c>,
/// <c>1.</c>, <c>#</c>, <c>&gt;</c>, a fence or an underline opens a block, or makes the paragraph above it a heading,
/// so a break before such a word changes what the text is. Parsed, the shown text must give the candidate's outline,
/// the same heading paths in the same order, and each of its sections, and what stands above the first, must fold to
/// the candidate's own. That makes each section the version the candidate holds, so a take, a leave, a deferral and
/// staleness read it as they read the candidate. A paragraph whose re-break fails the check is shown as the candidate
/// wrote it, and the others keep theirs: each is tried over the ones kept before it.
/// </para>
/// <para>
/// Made when a candidate is shown, against the accumulator as it reads then, and not again while it is shown (decision
/// 8): replacing the right's document under every keystroke in the left would move its caret and its scroll each time.
/// </para>
/// </remarks>
internal sealed class ShownCandidate
{
    private ShownCandidate(string text, MarkdownOutline outline)
    {
        Text = text;
        Outline = outline;
    }

    /// <summary>The text the right pane shows.</summary>
    internal string Text { get; }

    /// <summary>
    /// The outline of <see cref="Text"/>: the sections the right pane's lines belong to, and the ones its verbs take.
    /// </summary>
    internal MarkdownOutline Outline { get; }

    /// <summary><paramref name="candidate"/> as the right pane shows it beside <paramref name="accumulator"/>.</summary>
    internal static ShownCandidate Of(string candidate, string accumulator)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(accumulator);

        string text = MarkdownOutline.NormaliseLineEndings(candidate);
        Expectation own = new(text);
        List<Rebreak> rebreaks = RebreaksOf(text, own.Outline, MarkdownOutline.NormaliseLineEndings(accumulator));

        if (own.Check(Apply(text, rebreaks)) is { } all)
        {
            return all;
        }

        // The candidate as written passes by construction, so there is always something to show.
        ShownCandidate shown = new(text, own.Outline);
        List<Rebreak> kept = [];
        foreach (Rebreak rebreak in rebreaks)
        {
            if (own.Check(Apply(text, [.. kept, rebreak])) is { } more)
            {
                kept.Add(rebreak);
                shown = more;
            }
        }

        return shown;
    }

    /// <summary>
    /// The re-break of each paragraph of <paramref name="text"/> that <paramref name="accumulator"/> holds a version of
    /// and that breaks its lines otherwise, in document order.
    /// </summary>
    private static List<Rebreak> RebreaksOf(string text, MarkdownOutline own, string accumulator)
    {
        List<Paragraph> mine = ParagraphsOf(text, own);
        List<Paragraph> theirs = ParagraphsOf(accumulator, MarkdownOutline.Read(accumulator));

        // The accumulator's paragraphs by their words, in document order.
        Dictionary<string, List<Paragraph>> holders = new(StringComparer.Ordinal);
        foreach (Paragraph paragraph in theirs)
        {
            if (!holders.TryGetValue(paragraph.Words, out List<Paragraph>? holding))
            {
                holders[paragraph.Words] = holding = [];
            }

            holding.Add(paragraph);
        }

        // Found once, when the first paragraph holding no accumulator paragraph's words asks for its counterpart.
        Paragraph?[]? counterparts = null;
        List<Rebreak> rebreaks = [];
        for (int i = 0; i < mine.Count; i++)
        {
            Paragraph paragraph = mine[i];
            IReadOnlyList<string> lines;
            if (holders.TryGetValue(paragraph.Words, out List<Paragraph>? holding))
            {
                lines = holding
                    .FirstOrDefault(h => string.Equals(h.Path, paragraph.Path, StringComparison.Ordinal), holding[0])
                    .Lines;
            }
            else if ((counterparts ??= CounterpartsOf(mine, theirs))[i] is { } counterpart)
            {
                lines = LinesAs(paragraph, counterpart);
            }
            else
            {
                continue;
            }

            string shown = string.Join("\n" + OpeningOf(text, paragraph.Start), lines);
            if (!string.Equals(shown, text[paragraph.Start..paragraph.End], StringComparison.Ordinal))
            {
                rebreaks.Add(new Rebreak(paragraph.Start, paragraph.End, shown));
            }
        }

        return rebreaks;
    }

    /// <summary>The paragraphs of <paramref name="text"/>, whose outline is <paramref name="outline"/>, in document order.</summary>
    private static List<Paragraph> ParagraphsOf(string text, MarkdownOutline outline) =>
        [.. ParagraphBreaks.Paragraphs(text)
            .Select(p => new Paragraph(PathAt(outline, text, p.Start), p.LinesIn(text), p.Start, p.End))];

    /// <summary>
    /// The counterpart of each of <paramref name="mine"/>, the candidate's paragraphs, among <paramref name="theirs"/>,
    /// the accumulator's: the one a line diff of the two sides' paragraphs at its heading path, one paragraph's words a
    /// line, pairs it with, or <see langword="null"/> where the diff pairs it with none or the accumulator has no
    /// paragraph there.
    /// </summary>
    private static Paragraph?[] CounterpartsOf(List<Paragraph> mine, List<Paragraph> theirs)
    {
        Paragraph?[] counterparts = new Paragraph?[mine.Count];
        foreach (IGrouping<string?, int> path in Enumerable.Range(0, mine.Count).GroupBy(i => mine[i].Path))
        {
            List<Paragraph> there = [.. theirs.Where(p => string.Equals(p.Path, path.Key, StringComparison.Ordinal))];

            // Nothing to pair with, and the diff would read no lines as one empty line.
            if (there.Count == 0)
            {
                continue;
            }

            List<int> here = [.. path];
            foreach (AlignedRow row in RowsOf(there.Select(p => p.Words), here.Select(i => mine[i].Words)))
            {
                if (row is { LeftLine: { } left, RightLine: { } right })
                {
                    counterparts[here[right]] = there[left];
                }
            }
        }

        return counterparts;
    }

    /// <summary>
    /// The words of <paramref name="paragraph"/> in lines broken where <paramref name="counterpart"/>'s are: each of the
    /// counterpart's lines after its first starts at the paragraph's word that a word diff pairs with that line's first
    /// word, or where that word would be had the paragraph kept it, unless that is in a run of spaces.
    /// </summary>
    private static List<string> LinesAs(Paragraph paragraph, Paragraph counterpart)
    {
        string[] words = paragraph.Words.Split(' ');

        // The counterpart's words, and the index among them of the word each of its lines starts with.
        List<string> theirs = [];
        HashSet<int> starts = [];
        foreach (string line in counterpart.Lines)
        {
            starts.Add(theirs.Count);
            theirs.AddRange(line.Split(' '));
        }

        // The first line starts where the paragraph does, which no break makes.
        starts.Remove(0);

        // The rows hold the two sides' words in order, so the paragraph's words before a row are counted as they go.
        HashSet<int> breaks = [];
        int before = 0;
        foreach (AlignedRow row in RowsOf(theirs, words))
        {
            if (row.LeftLine is { } word && starts.Contains(word))
            {
                breaks.Add(before);
            }

            if (row.RightLine is not null)
            {
                before++;
            }
        }

        // An empty word is one of two spaces side by side, and a break beside one would leave a space at the end of a
        // line or the start of the next, which a fold takes with the break.
        List<string> lines = [];
        int from = 0;
        for (int i = 1; i < words.Length; i++)
        {
            if (breaks.Contains(i) && words[i - 1].Length > 0 && words[i].Length > 0)
            {
                lines.Add(string.Join(' ', words[from..i]));
                from = i;
            }
        }

        lines.Add(string.Join(' ', words[from..]));
        return lines;
    }

    /// <summary>
    /// The rows of the public line diff of <paramref name="left"/> against <paramref name="right"/>, set one item a line,
    /// so a row's line on either side is the index of an item there.
    /// </summary>
    private static IReadOnlyList<AlignedRow> RowsOf(IEnumerable<string> left, IEnumerable<string> right) =>
        DiffDocumentBuilder.Build(OneALine(left), OneALine(right), CancellationToken.None).Document.Rows;

    /// <summary>
    /// <paramref name="items"/> one a line, a line ending inside an item, a hard break's or one a code span holds, read as
    /// a space so the item stays one line.
    /// </summary>
    private static PaneSource OneALine(IEnumerable<string> items) =>
        new(string.Join('\n', items.Select(item => item.Replace('\n', ' '))));

    /// <summary>
    /// The heading path of <paramref name="outline"/>'s section holding <paramref name="offset"/> of
    /// <paramref name="text"/>, or <see langword="null"/> above its first heading.
    /// </summary>
    private static string? PathAt(MarkdownOutline outline, string text, int offset)
    {
        int line = text.AsSpan(0, offset).Count('\n') + 1;
        return outline.Sections.FirstOrDefault(s => s.FirstLine <= line && line <= s.LastLine)?.HeadingPath;
    }

    /// <summary>
    /// What opens each line after the first of the paragraph whose text starts at <paramref name="start"/>: what its
    /// first line holds before it, with a list item's marker turned to spaces and a quote's <c>&gt;</c> kept.
    /// </summary>
    private static string OpeningOf(string text, int start)
    {
        int lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        StringBuilder opening = new(start - lineStart);
        foreach (char c in text.AsSpan(lineStart, start - lineStart))
        {
            opening.Append(c is ' ' or '\t' or '>' ? c : ' ');
        }

        return opening.ToString();
    }

    /// <summary><paramref name="text"/> with each of <paramref name="rebreaks"/>, which come in document order, made.</summary>
    private static string Apply(string text, IEnumerable<Rebreak> rebreaks)
    {
        StringBuilder shown = new(text.Length);
        int copied = 0;
        foreach (Rebreak made in rebreaks)
        {
            shown.Append(text, copied, made.Start - copied).Append(made.Text);
            copied = made.End;
        }

        return shown.Append(text, copied, text.Length - copied).ToString();
    }

    /// <summary>
    /// One paragraph of a text: the heading path of the section it sits in, its lines as a fold reads them, and where
    /// its text starts and ends.
    /// </summary>
    private sealed record Paragraph(string? Path, IReadOnlyList<string> Lines, int Start, int End)
    {
        /// <summary>Its words: its lines joined with one space, the paragraph as a fold writes it.</summary>
        internal string Words { get; } = string.Join(' ', Lines);
    }

    /// <summary>One paragraph shown in other lines: its text, from <see cref="Start"/> to <see cref="End"/>, as <see cref="Text"/>.</summary>
    private readonly record struct Rebreak(int Start, int End, string Text);

    /// <summary>
    /// What decision 6 holds a shown text to: the candidate's own outline, with what stands above its first heading and
    /// each of its sections folded once, rather than once for every text checked.
    /// </summary>
    private sealed class Expectation
    {
        private readonly string preamble;
        private readonly string[] sections;

        internal Expectation(string text)
        {
            Outline = MarkdownOutline.Read(text);
            preamble = ParagraphBreaks.Fold(Outline.Preamble);
            sections = [.. Outline.Sections.Select(s => ParagraphBreaks.Fold(s.Content))];
        }

        /// <summary>The candidate's own outline.</summary>
        internal MarkdownOutline Outline { get; }

        /// <summary>
        /// <paramref name="text"/> as a shown candidate when it gives the candidate's outline and folds to it section
        /// by section, or <see langword="null"/> when it does not.
        /// </summary>
        internal ShownCandidate? Check(string text)
        {
            MarkdownOutline outline = MarkdownOutline.Read(text);
            if (outline.Sections.Count != sections.Length
                || !string.Equals(ParagraphBreaks.Fold(outline.Preamble), preamble, StringComparison.Ordinal))
            {
                return null;
            }

            for (int i = 0; i < sections.Length; i++)
            {
                MarkdownSection section = outline.Sections[i];
                if (!string.Equals(section.HeadingPath, Outline.Sections[i].HeadingPath, StringComparison.Ordinal)
                    || !string.Equals(ParagraphBreaks.Fold(section.Content), sections[i], StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return new ShownCandidate(text, outline);
        }
    }
}
