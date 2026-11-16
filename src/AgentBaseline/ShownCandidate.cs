using System.Text;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// A candidate as the right pane shows it — plan 00036 decisions 4, 6 and 8: each paragraph holding the words of one of
/// the accumulator's is broken where the accumulator breaks that one, so where the words are the accumulator's the
/// lines are too, and the diff shows no change there.
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
/// them anywhere. It is then shown in that paragraph's lines, each line after its first opening as the paragraph's own
/// first line does, a list item's marker turned to spaces and a quote's <c>&gt;</c> kept, so the line stays inside the
/// containers the paragraph is in.
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
    /// The re-break of each paragraph of <paramref name="text"/> that holds the words of one of
    /// <paramref name="accumulator"/>'s paragraphs and breaks its lines otherwise, in document order.
    /// </summary>
    private static List<Rebreak> RebreaksOf(string text, MarkdownOutline own, string accumulator)
    {
        MarkdownOutline theirs = MarkdownOutline.Read(accumulator);

        // The accumulator's paragraphs by their words, in document order.
        Dictionary<string, List<Holder>> holders = new(StringComparer.Ordinal);
        foreach (ParagraphText paragraph in ParagraphBreaks.Paragraphs(accumulator))
        {
            IReadOnlyList<string> lines = paragraph.LinesIn(accumulator);
            string words = string.Join(' ', lines);
            if (!holders.TryGetValue(words, out List<Holder>? holding))
            {
                holders[words] = holding = [];
            }

            holding.Add(new Holder(PathAt(theirs, accumulator, paragraph.Start), lines));
        }

        List<Rebreak> rebreaks = [];
        foreach (ParagraphText paragraph in ParagraphBreaks.Paragraphs(text))
        {
            if (!holders.TryGetValue(string.Join(' ', paragraph.LinesIn(text)), out List<Holder>? holding))
            {
                continue;
            }

            string? path = PathAt(own, text, paragraph.Start);
            Holder holder = holding.FirstOrDefault(h => string.Equals(h.Path, path, StringComparison.Ordinal), holding[0]);
            string shown = string.Join("\n" + OpeningOf(text, paragraph.Start), holder.Lines);
            if (!string.Equals(shown, text[paragraph.Start..paragraph.End], StringComparison.Ordinal))
            {
                rebreaks.Add(new Rebreak(paragraph.Start, paragraph.End, shown));
            }
        }

        return rebreaks;
    }

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

    /// <summary>One of the accumulator's paragraphs: the heading path of the section it sits in, and its lines.</summary>
    private readonly record struct Holder(string? Path, IReadOnlyList<string> Lines);

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
