using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>What unwrapping a document did to it.</summary>
/// <param name="Text">
/// The text afterwards, line endings normalised — and otherwise unchanged when the document was not
/// nested, or was nested and refused.
/// </param>
/// <param name="Levels">
/// How many levels of list nesting were removed: 0 for an ordinary document, 1 for JanusHalo's shape.
/// </param>
/// <param name="Refusal">
/// Why a nested document was left as it was, or <see langword="null"/> when nothing was refused. A
/// refusal is reported rather than worked around, because a guess here corrupts silently.
/// </param>
internal sealed record UnwrapResult(string Text, int Levels, string? Refusal);

/// <summary>
/// Plan 00034 decision 8's unwrap: a document wholly nested in one list item comes out as the plain
/// document it wraps, so the diff compares like with like and a take carries no stray indent.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The one safety property</b>: this removes spaces, and on the item's first line the list
/// marker it has verified there, and <b>nothing else</b>. No other character of the document can be
/// lost, so whatever it gets wrong, it cannot drop a rule's text. What it cannot verify it refuses.
/// </para>
/// <para>
/// <b>The detection is structural, from the parse tree</b> (decision 8), never a heuristic over the
/// lines: setting front matter aside, the document's only block is a list, the list's only item is
/// that item, and the item contains at least one heading. The heading is what separates a wrapped
/// document from a list somebody wrote — <c>- See README.md</c> is wholly nested in a list item too,
/// and unwrapping it would delete its bullet for no gain, since it has no outline either way.
/// </para>
/// <para>
/// Measured on JanusHalo's real file before this was written: one <c>ListBlock</c> holding one item
/// whose <c>ColumnWidth</c> is 2, eighteen headings, seven fenced blocks and nine nested lists inside
/// it; continuation lines at two spaces and a nested list's at four; no tab and no lazy line — and its
/// blank lines are not empty but two spaces, the indent itself, which the strip below turns back into
/// empty lines.
/// </para>
/// </remarks>
internal static class ListUnwrap
{
    /// <summary>
    /// The deepest nesting unwrapped before giving up and saying so. A bound rather than a loop until
    /// done, so a pathological input ends on its own terms with a refusal instead of running on.
    /// </summary>
    internal const int MaxLevels = 4;

    /// <summary>Unwraps <paramref name="text"/> for as many levels as it is wholly nested.</summary>
    internal static UnwrapResult Unwrap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string current = MarkdownOutline.NormaliseLineEndings(text);

        for (int level = 0; level < MaxLevels; level++)
        {
            MarkdownDocument document = Markdig.Markdown.Parse(current, MarkdownOutline.Pipeline);

            if (SoleItem(document) is not (ListBlock list, ListItemBlock item))
            {
                return new UnwrapResult(current, level, null);
            }

            (string? unwrapped, string? refusal) = Once(current.Split('\n'), list, item);

            if (refusal is not null)
            {
                return new UnwrapResult(current, level, refusal);
            }

            current = unwrapped!;
        }

        // The bound was reached. Whether that is a finish or a refusal depends on what is left.
        bool stillNested = SoleItem(Markdig.Markdown.Parse(current, MarkdownOutline.Pipeline)) is not null;

        return new UnwrapResult(
            current,
            MaxLevels,
            stillNested ? $"still wholly nested in a list item after {MaxLevels} levels" : null);
    }

    /// <summary>
    /// The list and its single item when the document is a wrapped one, else <see langword="null"/>.
    /// </summary>
    private static (ListBlock List, ListItemBlock Item)? SoleItem(MarkdownDocument document)
    {
        // Front matter can only open a document, so setting it aside anywhere sets aside only that.
        Block[] blocks = [.. document.Where(b => b is not YamlFrontMatterBlock)];

        if (blocks is not [ListBlock { Count: 1 } list] || list[0] is not ListItemBlock item)
        {
            return null;
        }

        return item.Descendants<HeadingBlock>().Any() ? (list, item) : null;
    }

    /// <summary>Removes one level: the marker from the item's first line, the indent from the rest.</summary>
    private static (string? Text, string? Refusal) Once(string[] lines, ListBlock list, ListItemBlock item)
    {
        int first = item.Line;
        int offset = item.Column + item.ColumnWidth;

        // ⛔ CommonMark measures indentation in COLUMNS, with a tab advancing to the next multiple of
        // four — and Markdig's Column and ColumnWidth are those columns, not character indices
        // (measured: `-\t` reports a width of 4). This strips CHARACTERS, so the two agree only while
        // no tab is involved. And a tab stop is absolute, so the WHOLE indent matters, not just the
        // part being removed: in `  \t# Heading` the tab spans columns 2 to 4, and stripping the two
        // spaces moves it to column 0, where it spans 0 to 4 — the heading becomes an indented code
        // block, and every character survives, so nothing that counts characters would notice. On the
        // first line it is the indent left after the marker that is checked; the marker's own prefix
        // is checked by OpensWithMarker. Refused rather than expanded: expanding would rewrite a code
        // block's tabs as spaces.
        for (int i = first; i < lines.Length; i++)
        {
            string remaining = i != first ? lines[i]
                : lines[i].Length > offset ? lines[i][offset..]
                : string.Empty;

            if (Indent(remaining).Contains('\t'))
            {
                return (null, $"line {i + 1} indents with a tab");
            }
        }

        if (!OpensWithMarker(lines[first], offset, list, item))
        {
            return (null, $"line {first + 1} does not open with the list marker the parse reported");
        }

        string[] result = new string[lines.Length];

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            result[i] =
                i < first ? line
                : i == first ? (line.Length > offset ? line[offset..] : string.Empty)
                : line[Math.Min(LeadingSpaces(line), offset)..];
        }

        return (string.Join('\n', result), null);
    }

    /// <summary>
    /// Whether the first <paramref name="offset"/> characters of the item's first line are exactly
    /// its indent, the marker the parse reported, and the spaces after it — so that removing them
    /// removes no character of content.
    /// </summary>
    private static bool OpensWithMarker(string line, int offset, ListBlock list, ListItemBlock item)
    {
        string prefix = line.Length > offset ? line[..offset] : line;
        int at = 0;

        while (at < item.Column)
        {
            if (at >= prefix.Length || prefix[at] != ' ')
            {
                return false;
            }

            at++;
        }

        if (list.IsOrdered)
        {
            int digits = 0;

            while (at < prefix.Length && char.IsAsciiDigit(prefix[at]))
            {
                at++;
                digits++;
            }

            // CommonMark caps an ordered marker at nine digits.
            if (digits is 0 or > 9 || at >= prefix.Length || prefix[at] != list.OrderedDelimiter)
            {
                return false;
            }
        }
        else if (at >= prefix.Length || prefix[at] != list.BulletType)
        {
            return false;
        }

        for (at++; at < prefix.Length; at++)
        {
            if (prefix[at] != ' ')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The whole run of spaces and tabs a line opens with.</summary>
    private static ReadOnlySpan<char> Indent(string line)
    {
        int end = 0;

        while (end < line.Length && line[end] is ' ' or '\t')
        {
            end++;
        }

        return line.AsSpan(0, end);
    }

    /// <summary>How many spaces a line opens with.</summary>
    private static int LeadingSpaces(string line)
    {
        int count = 0;

        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }
}
