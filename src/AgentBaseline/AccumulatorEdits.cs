namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>What a take did to the accumulator.</summary>
internal enum TakeResult
{
    /// <summary>The accumulator held a section at that path, and now holds the candidate's version of it.</summary>
    Replaced,

    /// <summary>The accumulator held no section at that path, and now does.</summary>
    Inserted,

    /// <summary>The accumulator already held exactly the candidate's version, so nothing was written.</summary>
    Unchanged,
}

/// <summary>A span of the accumulator's text and what replaces it: one edit, so one step of undo.</summary>
/// <param name="Offset">Where the span begins.</param>
/// <param name="Length">How long it is; 0 for an insertion.</param>
/// <param name="Text">What replaces it; empty for a removal.</param>
internal readonly record struct TextEdit(int Offset, int Length, string Text)
{
    /// <summary>The text <paramref name="text"/> becomes once this edit is made to it.</summary>
    internal string ApplyTo(string text) =>
        string.Concat(text.AsSpan(0, Offset), Text, text.AsSpan(Offset + Length));
}

/// <summary>
/// A decision the accumulator would not carry out, and why: it names nothing the accumulator holds,
/// or names something twice, or carrying it out would change what it is not about. Nothing has been
/// written when this is thrown.
/// </summary>
internal sealed class DecisionRefusedException(string message) : Exception(message);

/// <summary>
/// Where a take or a drop lands in the accumulator, read from the accumulator's own parse — plan
/// 00034 decision 5 — and the check that it changes nothing else.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Never the diff's alignment.</b> Draft 1 made a take <c>CopyToLeft</c>, and a copy is a
/// replace over the <i>aligned</i> range: over a candidate whose sections are in another order, the
/// diff sets a section beside whatever left lines the line diff matched it with, and the copy writes
/// over those — another rule, deleted. So the section is found in the accumulator by its heading
/// path, in a parse of the accumulator's own live text, and how the right is laid out cannot move
/// anything on the left.
/// </para>
/// <para>
/// A take of a path the accumulator holds <b>replaces</b> that section's content. A take of one it
/// does not hold is <b>inserted</b> where the candidate has it: after the nearest preceding sibling
/// the accumulator holds — its whole subtree — or, with none, first under the parent, which the
/// accumulator must hold, because a section put anywhere else would sit under some other heading and
/// change its own path. A <b>drop</b> removes the section's lines, the blank lines after it included.
/// Every edit carries a section's <see cref="MarkdownSection.Content"/> and keeps the accumulator's
/// own blank lines where they were, so the text between two sections is the accumulator's, not the
/// candidate's.
/// </para>
/// <para>
/// ⭐ <b>Every edit is checked before it is written.</b> The result is parsed, and every section the
/// decision is not about must come out byte-identical, at the same heading path, in the same order,
/// with the text above the first heading unchanged. That is plan 00034 step 6's assertion made a
/// precondition of the edit rather than a property a test hopes for, and it refuses the edits no
/// positioning rule can make safe: a section whose fence never closes, which would turn every heading
/// after it into code; a heading of another level, which would move the sections under it; a setext
/// heading set against a paragraph, which would take the paragraph into its title.
/// </para>
/// </remarks>
internal static class AccumulatorEdits
{
    /// <summary>How many of the sections a refused edit would change are named in the refusal.</summary>
    private const int Named = 5;

    /// <summary>
    /// The edit that takes the candidate's section <paramref name="index"/> into
    /// <paramref name="accumulator"/>, and what it does.
    /// </summary>
    /// <exception cref="DecisionRefusedException">
    /// The accumulator holds that path twice, or lacks the section it would go under, or the edit
    /// would change another section.
    /// </exception>
    internal static (TextEdit Edit, TakeResult Result) Take(string accumulator, MarkdownOutline candidate, int index)
    {
        ArgumentNullException.ThrowIfNull(accumulator);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, candidate.Sections.Count);

        MarkdownSection taken = candidate.Sections[index];
        MarkdownOutline before = MarkdownOutline.Read(accumulator);
        LineTable lines = new(accumulator);
        IReadOnlyList<int> held = before.IndicesOf(taken.HeadingPath);

        if (held.Count > 1)
        {
            throw new DecisionRefusedException(
                $"the accumulator holds {held.Count} sections at \"{taken.HeadingPath}\", so a take cannot tell which "
                + "of them to replace — make their headings differ by hand first");
        }

        if (held.Count == 1)
        {
            MarkdownSection replaced = before.Sections[held[0]];
            if (string.Equals(replaced.Content, taken.Content, StringComparison.Ordinal))
            {
                return (new TextEdit(0, 0, string.Empty), TakeResult.Unchanged);
            }

            int start = lines.Offset(replaced.FirstLine);
            TextEdit replace = new(start, lines.EndOffset(replaced.ContentLastLine) - start, taken.Content);
            Verify(before, accumulator, replace, Expected.Replace(held[0], taken), $"taking \"{taken.HeadingPath}\"");
            return (replace, TakeResult.Replaced);
        }

        int at = InsertionIndex(before, candidate, index);
        TextEdit insert = at < before.Sections.Count
            ? new TextEdit(lines.Offset(before.Sections[at].FirstLine), 0, taken.Content + "\n\n")
            : AtEnd(accumulator, taken.Content);
        Verify(before, accumulator, insert, Expected.Insert(at, taken), $"taking \"{taken.HeadingPath}\"");
        return (insert, TakeResult.Inserted);
    }

    /// <summary>
    /// The edit that drops the accumulator's own section at <paramref name="headingPath"/>, and that
    /// section as it stood.
    /// </summary>
    /// <exception cref="DecisionRefusedException">
    /// The accumulator holds no section at that path, or holds two, or removing it would change
    /// another section — one with sections under it, which would move to the heading above.
    /// </exception>
    internal static (TextEdit Edit, MarkdownSection Dropped) Drop(string accumulator, string headingPath)
    {
        ArgumentNullException.ThrowIfNull(accumulator);
        ArgumentNullException.ThrowIfNull(headingPath);

        MarkdownOutline before = MarkdownOutline.Read(accumulator);
        IReadOnlyList<int> holding = before.IndicesOf(headingPath);

        if (holding.Count == 0)
        {
            throw new DecisionRefusedException(
                $"the accumulator holds no section at \"{headingPath}\" to drop — a section only a candidate carries "
                + "is left, not dropped");
        }

        if (holding.Count > 1)
        {
            throw new DecisionRefusedException(
                $"the accumulator holds {holding.Count} sections at \"{headingPath}\", so a drop cannot tell which of "
                + "them to remove — make their headings differ by hand first");
        }

        int index = holding[0];
        MarkdownSection dropped = before.Sections[index];
        LineTable lines = new(accumulator);

        TextEdit remove;
        if (index + 1 < before.Sections.Count)
        {
            int start = lines.Offset(dropped.FirstLine);
            remove = new TextEdit(start, lines.Offset(before.Sections[index + 1].FirstLine) - start, string.Empty);
        }
        else if (dropped.FirstLine > 1)
        {
            // The last section: the line ending in front of it goes too, so the line above becomes the
            // last one and keeps exactly the text it had. Left behind, that ending would be a new empty
            // line on the end of the section above.
            int start = lines.EndOffset(dropped.FirstLine - 1);
            remove = new TextEdit(start, accumulator.Length - start, string.Empty);
        }
        else
        {
            remove = new TextEdit(0, accumulator.Length, string.Empty);
        }

        Verify(before, accumulator, remove, Expected.Drop(index), $"dropping \"{headingPath}\"");
        return (remove, dropped);
    }

    /// <summary>
    /// Where the candidate's section <paramref name="index"/> goes in an accumulator that does not hold
    /// it: the index it will have among the accumulator's sections.
    /// </summary>
    private static int InsertionIndex(MarkdownOutline accumulator, MarkdownOutline candidate, int index)
    {
        MarkdownSection taken = candidate.Sections[index];
        int parent = candidate.ParentOf(index);
        int heldParent = -1;

        if (parent >= 0)
        {
            string parentPath = candidate.Sections[parent].HeadingPath;
            IReadOnlyList<int> parents = accumulator.IndicesOf(parentPath);
            if (parents.Count == 0)
            {
                throw new DecisionRefusedException(
                    $"the accumulator has no \"{parentPath}\" to put \"{taken.Title}\" under — take that section first, "
                    + "or the new one would sit under another heading and lose its own path");
            }

            if (parents.Count > 1)
            {
                throw new DecisionRefusedException(
                    $"the accumulator holds {parents.Count} sections at \"{parentPath}\", so a take cannot tell which "
                    + $"of them \"{taken.Title}\" goes under — make their headings differ by hand first");
            }

            heldParent = parents[0];
        }

        // After the nearest preceding sibling the accumulator holds once, and after everything under it.
        for (int sibling = index - 1; sibling > parent; sibling--)
        {
            if (candidate.ParentOf(sibling) != parent)
            {
                continue;
            }

            IReadOnlyList<int> found = accumulator.IndicesOf(candidate.Sections[sibling].HeadingPath);
            if (found.Count == 1)
            {
                return accumulator.SubtreeEnd(found[0]) + 1;
            }
        }

        // No sibling before it is held, so it comes first under its parent — or first of all.
        return heldParent + 1;
    }

    /// <summary>
    /// An insertion after everything the accumulator holds. Each shape keeps the last line where it
    /// was: a document ending in a line ending gains a blank line before the new section and keeps its
    /// final line ending, and one ending without gains neither.
    /// </summary>
    private static TextEdit AtEnd(string accumulator, string content) =>
        accumulator.Length == 0
            ? new TextEdit(0, 0, content)
            : accumulator[^1] is '\n' or '\r'
                ? new TextEdit(accumulator.Length, 0, "\n" + content + "\n")
                : new TextEdit(accumulator.Length, 0, "\n" + content);

    /// <summary>
    /// Parses what <paramref name="edit"/> makes of <paramref name="accumulator"/> and refuses it when
    /// anything the decision is not about would come out different.
    /// </summary>
    private static void Verify(MarkdownOutline before, string accumulator, TextEdit edit, Expected expected, string what)
    {
        MarkdownOutline after = MarkdownOutline.Read(edit.ApplyTo(accumulator));
        List<string> changed = expected.Changes(before, after);
        if (changed.Count == 0)
        {
            return;
        }

        string named = string.Join("; ", changed.Take(Named));
        string more = changed.Count > Named ? $"; and {changed.Count - Named} more" : string.Empty;
        throw new DecisionRefusedException($"{what} would change what it is not about: {named}{more}. Nothing was written.");
    }

    /// <summary>What a parse of the result must show: every section but the decision's own, unchanged.</summary>
    /// <param name="Index">The decision's own section in the accumulator: the one replaced, inserted or dropped.</param>
    /// <param name="Taken">The candidate's section a take lands, or null for a drop.</param>
    /// <param name="Shift">How the count of sections moves: 0 for a replacement, 1 for an insertion, -1 for a drop.</param>
    private sealed record Expected(int Index, MarkdownSection? Taken, int Shift)
    {
        internal static Expected Replace(int index, MarkdownSection taken) => new(index, taken, 0);

        internal static Expected Insert(int index, MarkdownSection taken) => new(index, taken, 1);

        internal static Expected Drop(int index) => new(index, null, -1);

        internal List<string> Changes(MarkdownOutline before, MarkdownOutline after)
        {
            List<string> changed = [];
            if (!string.Equals(before.Preamble, after.Preamble, StringComparison.Ordinal))
            {
                changed.Add("the text above the first heading");
            }

            int count = before.Sections.Count + Shift;
            if (after.Sections.Count != count)
            {
                // The positions no longer correspond, so say which headings came and went instead.
                List<string> wanted = [.. before.Sections.Select(s => s.HeadingPath)];
                if (Shift < 0)
                {
                    wanted.RemoveAt(Index);
                }
                else if (Shift > 0)
                {
                    wanted.Insert(Index, Taken!.HeadingPath);
                }

                List<string> found = [.. after.Sections.Select(s => s.HeadingPath)];
                changed.Add($"the result would hold {after.Sections.Count} sections where {count} are expected");
                changed.AddRange(Without(wanted, found).Select(p => $"\"{p}\" would no longer be a heading"));
                changed.AddRange(Without(found, wanted).Select(p => $"\"{p}\" would become a heading"));
                return changed;
            }

            for (int i = 0; i < before.Sections.Count; i++)
            {
                if (i == Index && Shift <= 0)
                {
                    continue;
                }

                int j = i >= Index && Shift != 0 ? i + Shift : i;
                MarkdownSection was = before.Sections[i];
                MarkdownSection now = after.Sections[j];
                if (was.Level == now.Level
                    && string.Equals(was.HeadingPath, now.HeadingPath, StringComparison.Ordinal)
                    && string.Equals(was.Text, now.Text, StringComparison.Ordinal))
                {
                    continue;
                }

                changed.Add(string.Equals(was.HeadingPath, now.HeadingPath, StringComparison.Ordinal)
                    ? $"the text of \"{was.HeadingPath}\""
                    : $"\"{was.HeadingPath}\", which would read as \"{now.HeadingPath}\"");
            }

            if (Taken is { } taken)
            {
                MarkdownSection landed = after.Sections[Index];
                if (!string.Equals(landed.HeadingPath, taken.HeadingPath, StringComparison.Ordinal)
                    || !string.Equals(landed.Content, taken.Content, StringComparison.Ordinal))
                {
                    changed.Add($"the section taken, which would read as \"{landed.HeadingPath}\"");
                }
            }

            return changed;
        }

        /// <summary>The items of <paramref name="items"/> left once each of <paramref name="removed"/> has taken one away.</summary>
        private static List<string> Without(List<string> items, List<string> removed)
        {
            List<string> left = [.. items];
            foreach (string item in removed)
            {
                left.Remove(item);
            }

            return left;
        }
    }

    /// <summary>
    /// Where each line of a text begins, and where its own text ends, counting lines exactly as
    /// <see cref="MarkdownOutline"/> does — <c>\r\n</c>, <c>\r</c> and <c>\n</c> each ending one — so
    /// an outline's line numbers name the same lines here.
    /// </summary>
    private sealed class LineTable
    {
        private readonly List<int> starts = [0];
        private readonly List<int> ends = [];

        internal LineTable(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c is not ('\n' or '\r'))
                {
                    continue;
                }

                ends.Add(i);
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }

            ends.Add(text.Length);
        }

        /// <summary>Where 1-based line <paramref name="line"/> begins.</summary>
        internal int Offset(int line) => starts[line - 1];

        /// <summary>Where 1-based line <paramref name="line"/>'s text ends: where its line ending begins.</summary>
        internal int EndOffset(int line) => ends[line - 1];
    }
}
