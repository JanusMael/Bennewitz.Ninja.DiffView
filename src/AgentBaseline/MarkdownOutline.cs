using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// One heading and the lines it governs: the unit a take, a leave, a drop or a defer acts on.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FirstLine"/> and <see cref="LastLine"/> are <b>1-based and inclusive</b>, the
/// convention AvaloniaEdit's <c>TextDocument</c> uses, because these numbers are handed to the diff
/// control. Markdig counts from 0; <see cref="MarkdownOutline"/> is the one place the two meet.
/// </para>
/// <para>
/// <see cref="Text"/> is sliced from the outline's own line array rather than rendered back out of
/// Markdig — plan 00034 decision 3. Markdig keeps a fenced block as slices over the original
/// document rather than as a string, and we already hold the file, so the lines are the truth and
/// the parse is only a locator.
/// </para>
/// <para>
/// ⚠ <see cref="Text"/> keeps whatever indentation the source had, including the indent of a
/// document wholly nested in a list item. Unwrapping that is ingest normalisation's job — plan 00034
/// decision 8, step 3 — and doing it here would make the parse lossy about what the source really
/// was.
/// </para>
/// </remarks>
/// <param name="Level">The heading's level, 1 to 6.</param>
/// <param name="Title">
/// The heading's text with its markers, any closing sequence and any inline markup removed, and its
/// whitespace collapsed. Read from Markdig's inline tree, never from the source line: a setext
/// heading's source line is the underline.
/// </param>
/// <param name="HeadingPath">
/// The titles of this heading and its ancestors, joined with <c>" / "</c>. Plan 00034 decision 6
/// makes this half of a section's identity — the path identifies the rule, a hash of the body
/// identifies its version.
/// </param>
/// <param name="FirstLine">The heading's own first line, 1-based.</param>
/// <param name="LastLine">The last line this section governs, 1-based and inclusive.</param>
/// <param name="Text">The section's lines, heading included, exactly as the source had them.</param>
internal sealed record MarkdownSection(
    int Level,
    string Title,
    string HeadingPath,
    int FirstLine,
    int LastLine,
    string Text)
{
    /// <summary>How many lines this section governs, its heading included.</summary>
    internal int LineCount => LastLine - FirstLine + 1;
}

/// <summary>
/// The heading outline of one markdown document: Markdig as a locator, the text from our own lines.
/// </summary>
/// <remarks>
/// <para>
/// Plan 00034 step 2. Three things were measured against Markdig 1.4.1 before this was written, and
/// each of them would be a silent wrong answer rather than an error:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// ⛔ <c>HeadingBlock.Lines.Count</c> is <b>-1</b>, not 0 — the plan expected empty, and it is worse
/// than empty. The heading's text is in <c>Inline</c>, which <see cref="TitleOf"/> flattens.
/// </description>
/// </item>
/// <item>
/// <description>
/// ⛔ <c>HeadingBlock.Line</c> is the <b>underline</b> row for a setext heading, not the text row,
/// and the text may run several lines above it — so neither <c>Line</c> nor <c>Line - 1</c> finds a
/// setext heading's first line. <c>Span.Start</c> does, in every shape measured, so the span is what
/// this type reads.
/// </description>
/// </item>
/// <item>
/// <description>
/// ⛔ Without <c>UseYamlFrontMatter</c>, a document's front matter parses as a <b>setext heading</b>
/// whose text is the YAML body: the closing <c>---</c> reads as an underline. Every memory-shaped
/// file would gain a heading nobody wrote. The pipeline therefore carries that extension, which is
/// what plan 00034 §4 means by front matter being detected rather than supported.
/// </description>
/// </item>
/// </list>
/// <para>
/// ⚠ The namespace is deliberately <b>not</b> <c>…AgentBaseline.Markdown</c>. C# walks enclosing
/// namespaces before global, so inside such a namespace the name <c>Markdown</c> would resolve to
/// the namespace and <c>Markdown.Parse</c> would not compile — AGENTS.md §1's shadowing rule, one
/// word away from Markdig's own entry point.
/// </para>
/// </remarks>
internal sealed class MarkdownOutline
{
    /// <summary>
    /// Shared because a pipeline is immutable once built and building one per document is pure cost.
    /// </summary>
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();

    private MarkdownOutline(
        IReadOnlyList<string> lines,
        string preamble,
        bool hasFrontMatter,
        IReadOnlyList<MarkdownSection> sections)
    {
        Lines = lines;
        Preamble = preamble;
        HasFrontMatter = hasFrontMatter;
        Sections = sections;
    }

    /// <summary>The document's lines, line endings normalised, without their terminators.</summary>
    internal IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// Everything above the first heading, front matter included, or the whole document when it has
    /// no heading at all. Kept so the parse loses nothing: a document's preamble belongs to no
    /// section and would otherwise vanish between the outline and a saved baseline.
    /// </summary>
    internal string Preamble { get; }

    /// <summary>
    /// Whether the document opens with YAML front matter. Observable so that "front matter is
    /// detected, never mistaken for a setext heading" is a fact a test can read rather than an
    /// implication of the heading set.
    /// </summary>
    internal bool HasFrontMatter { get; }

    /// <summary>The headings in document order, each with the lines it governs.</summary>
    internal IReadOnlyList<MarkdownSection> Sections { get; }

    /// <summary>
    /// Reads one document's outline.
    /// </summary>
    /// <param name="text">
    /// The document. Line endings are normalised first, so the parse and the line array share one
    /// coordinate system whatever the source used. ⚠ That is idempotent with ingest normalisation
    /// (step 3) and not a substitute for it: this does not unwrap a nested document or retain the
    /// raw bytes.
    /// </param>
    internal static MarkdownOutline Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalised = NormaliseLineEndings(text);
        string[] lines = normalised.Split('\n');
        int[] lineStarts = LineStarts(lines);

        MarkdownDocument document = Markdig.Markdown.Parse(normalised, Pipeline);

        // Descendants, never the top level: a document wholly nested in one list item — JanusHalo's
        // real shape, measured — has every heading inside a ListItemBlock, so a top-level walk finds
        // 0 of 18 and a `^#` scan finds 0 as well. Sorted rather than trusted in walk order, so the
        // ordering this depends on is stated instead of assumed.
        List<HeadingBlock> headings = [.. document.Descendants<HeadingBlock>()];
        List<(int FirstLine, HeadingBlock Heading)> located =
            [.. headings.Select(h => (LineOfOffset(lineStarts, h.Span.Start), h))];
        located.Sort((a, b) => a.FirstLine.CompareTo(b.FirstLine));

        List<MarkdownSection> sections = new(located.Count);
        List<(int Level, string Title)> ancestry = [];

        for (int i = 0; i < located.Count; i++)
        {
            (int firstLine, HeadingBlock heading) = located[i];
            int lastLine = i + 1 < located.Count ? located[i + 1].FirstLine - 1 : lines.Length - 1;

            string title = TitleOf(heading);

            while (ancestry.Count > 0 && ancestry[^1].Level >= heading.Level)
            {
                ancestry.RemoveAt(ancestry.Count - 1);
            }

            ancestry.Add((heading.Level, title));

            sections.Add(new MarkdownSection(
                heading.Level,
                title,
                string.Join(" / ", ancestry.Select(a => a.Title)),
                EditorLine(firstLine),
                EditorLine(lastLine),
                string.Join("\n", lines[firstLine..(lastLine + 1)])));
        }

        int preambleEnd = located.Count > 0 ? located[0].FirstLine : lines.Length;

        return new MarkdownOutline(
            lines,
            string.Join("\n", lines[..preambleEnd]),
            document.FirstOrDefault() is Markdig.Extensions.Yaml.YamlFrontMatterBlock,
            sections);
    }

    /// <summary>
    /// ⭐ The one line-base boundary. Markdig counts lines from 0; AvaloniaEdit's
    /// <c>TextDocument</c>, and therefore every number this tool hands the diff control, counts from
    /// 1. <c>PaneMetadata</c> is the same boundary on the library side, and for the same reason: two
    /// places that each convert is two places that can disagree by one, and an outline off by one
    /// line takes the wrong text with it silently.
    /// </summary>
    private static int EditorLine(int markdigLine) => markdigLine + 1;

    /// <summary>
    /// Collapses every line ending to <c>\n</c>, lone carriage returns included. ⚠ The order
    /// matters: replacing <c>\r</c> first would turn each <c>\r\n</c> into two line breaks.
    /// </summary>
    private static string NormaliseLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>The character offset each line begins at, for mapping a span back to a line.</summary>
    private static int[] LineStarts(string[] lines)
    {
        int[] starts = new int[lines.Length];
        int offset = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            starts[i] = offset;
            offset += lines[i].Length + 1; // the '\n' the split consumed
        }

        return starts;
    }

    /// <summary>The 0-based line a character offset falls on.</summary>
    private static int LineOfOffset(int[] lineStarts, int offset)
    {
        int found = Array.BinarySearch(lineStarts, offset);
        return found >= 0 ? found : Math.Max(0, ~found - 1);
    }

    /// <summary>
    /// The heading's text, from Markdig's inline tree. Inline markup is dropped rather than kept:
    /// this is half of a section's identity (decision 6), and a rule renamed from <c>`code`</c> to
    /// plain text is the same rule.
    /// </summary>
    private static string TitleOf(HeadingBlock heading)
    {
        if (heading.Inline is null)
        {
            return string.Empty;
        }

        StringBuilder builder = new();

        foreach (IMarkdownObject node in heading.Inline.Descendants())
        {
            switch (node)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.AsSpan());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
                case LineBreakInline:
                    // A setext heading's text may run several lines; a title is one line.
                    builder.Append(' ');
                    break;
                default:
                    break;
            }
        }

        return CollapseWhitespace(builder.ToString());
    }

    /// <summary>Trims, and reduces every run of whitespace to one space.</summary>
    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
