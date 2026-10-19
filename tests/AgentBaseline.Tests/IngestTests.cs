using System.Text;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 3: ingest normalisation — decode, LF, unwrap a document wholly nested in a list
/// item, and keep the raw bytes.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ The verification the plan names is a <b>round trip over JanusHalo's shape</b>, and it is
/// asserted on <b>content</b>, because a normalisation that corrupts silently is the risk: the
/// unwrapped text must BE the plain document, its headings must be the plain document's, and the raw
/// bytes must be untouched.
/// </para>
/// <para>
/// <see cref="Plain"/> and <see cref="Wrap"/> reproduce the real file's shape, measured on
/// 2026-10-19 rather than recalled: a <c>- </c> marker, continuation at two spaces, a nested list's
/// continuation at four, fenced blocks inside the item — and blank lines that are <b>two spaces</b>,
/// not empty. That last detail is the one plan 00034 step 2's fixture did not have. The same round
/// trip was run against the real file itself, which is not committed: re-wrapping its unwrapped text
/// reproduced all 18,196 bytes.
/// </para>
/// </remarks>
public sealed class IngestTests
{
    /// <summary>A plain document, ending as the real one does, in a line feed.</summary>
    private static readonly string Plain = string.Join("\n",
        "# Working preferences",
        "",
        "Opening paragraph.",
        "",
        "## A rule",
        "",
        "Body of the rule.",
        "",
        "- a bullet",
        "- another bullet",
        "  with a continuation",
        "",
        "## A rule with code",
        "",
        "```",
        "# not a heading",
        "  indented code line",
        "```",
        "",
        "### Deeper",
        "",
        "Text.",
        "");

    private static readonly string[] PlainHeadings =
        ["1 Working preferences", "2 A rule", "2 A rule with code", "3 Deeper"];

    /// <summary>
    /// The plain document wrapped the way JanusHalo's file is: a marker on the first line, two spaces
    /// on every other one — blank lines included — and nothing on the empty string after the last
    /// line feed, which is a terminator rather than a line.
    /// </summary>
    private static string Wrap(string plain)
    {
        string[] lines = plain.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            bool terminatorOnly = i == lines.Length - 1 && lines[i].Length == 0;
            lines[i] = i == 0 ? "- " + lines[i] : terminatorOnly ? lines[i] : "  " + lines[i];
        }

        return string.Join('\n', lines);
    }

    private static IngestedDocument Ingest(string text) =>
        IngestedDocument.FromBytes(Encoding.UTF8.GetBytes(text));

    private static string[] HeadingsOf(IngestedDocument document) =>
        [.. document.Outline.Sections.Select(s => $"{s.Level} {s.Title}")];

    /// <summary>Every character that is not whitespace, in order: what a document actually says.</summary>
    private static string Ink(string text) => new([.. text.Where(c => !char.IsWhiteSpace(c))]);

    [Fact]
    public void A_document_wholly_nested_in_a_list_item_unwraps_to_the_plain_document_it_wraps()
    {
        IngestedDocument document = Ingest(Wrap(Plain));

        // ⭐ The round trip, compared as whole text: not "it unwrapped", but "it unwrapped to exactly
        // this". A two-space blank line left behind, or a fenced line stripped one space too far, fails.
        Assert.Equal(Plain, document.Text);
        Assert.Equal(1, document.UnwrappedLevels);
        Assert.Null(document.UnwrapRefusal);
    }

    [Fact]
    public void The_unwrapped_document_has_the_plain_documents_headings()
    {
        IngestedDocument wrapped = Ingest(Wrap(Plain));
        IngestedDocument plain = Ingest(Plain);

        // Equal to the plain document's AND to the set written down, so the comparison cannot pass by
        // both sides finding nothing.
        Assert.Equal(PlainHeadings, HeadingsOf(wrapped));
        Assert.Equal(HeadingsOf(plain), HeadingsOf(wrapped));
    }

    [Fact]
    public void The_raw_bytes_are_retained_unchanged_even_when_the_callers_array_changes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Wrap(Plain));
        byte[] original = [.. bytes];

        IngestedDocument document = IngestedDocument.FromBytes(bytes);
        bytes[0] = (byte)'X';

        // Retained means a copy: the array was the caller's, and what it holds now is not what the
        // source was when it was read.
        Assert.True(document.RawBytes.AsSpan().SequenceEqual(original));
    }

    [Fact]
    public void Unwrapping_removes_only_whitespace_and_the_one_marker()
    {
        string wrapped = Wrap(Plain);
        IngestedDocument document = Ingest(wrapped);

        // ⭐ The unwrap's safety property, asserted rather than claimed: every character that is not
        // whitespace survives, in order, except the list marker it verified — so whatever else it got
        // wrong, it cannot have dropped any of a rule's text.
        Assert.Equal('-', Ink(wrapped)[0]);
        Assert.Equal(Ink(wrapped)[1..], Ink(document.Text));
    }

    [Fact]
    public void An_ordinary_document_is_left_exactly_as_it_was()
    {
        IngestedDocument document = Ingest(Plain);

        Assert.Equal(Plain, document.Text);
        Assert.Equal(0, document.UnwrappedLevels);
        Assert.Null(document.UnwrapRefusal);
    }

    [Fact]
    public void Ingesting_what_ingest_produced_changes_nothing()
    {
        IngestedDocument once = Ingest(Wrap(Plain));
        IngestedDocument twice = Ingest(once.Text);

        // A normaliser is idempotent or it is a transform: run twice, it must agree with itself.
        Assert.Equal(once.Text, twice.Text);
        Assert.Equal(0, twice.UnwrappedLevels);
    }

    [Fact]
    public void A_one_item_list_with_no_heading_is_a_list_not_a_wrapped_document()
    {
        string text = "- See the README for everything else.\n";
        IngestedDocument document = Ingest(text);

        // Structurally this IS wholly nested in a list item. The heading inside is what separates a
        // wrapped document from a list somebody wrote, and unwrapping this would delete its bullet.
        Assert.Equal(text, document.Text);
        Assert.Equal(0, document.UnwrappedLevels);
        Assert.Null(document.UnwrapRefusal);
    }

    [Fact]
    public void A_tab_in_the_indent_is_refused_and_the_document_is_left_nested()
    {
        string[] lines = Wrap(Plain).Split('\n');
        Assert.Equal("  Body of the rule.", lines[6]);
        lines[6] = "\tBody of the rule.";
        string text = string.Join('\n', lines);

        IngestedDocument document = Ingest(text);

        // ⛔ CommonMark measures indentation in columns with tab stops of four, so stripping two
        // characters is not stripping two columns. Refused and reported, never guessed: the text is
        // left as it was, and the outline still finds every heading because the parse walks
        // descendants — the cost of a refusal is a stray indent in a take, not a lost rule.
        Assert.Equal("line 7 indents with a tab", document.UnwrapRefusal);
        Assert.Equal(text, document.Text);
        Assert.Equal(0, document.UnwrappedLevels);
        Assert.Equal(PlainHeadings, HeadingsOf(document));
    }

    [Fact]
    public void A_marker_that_cannot_be_removed_as_characters_is_refused()
    {
        // The continuation is at FOUR spaces because Markdig measures `-\t` as four columns wide, so
        // that is the offset this item's content sits at. At two, the line would close the list and
        // the document would not be wholly nested at all — which is how this fixture's first draft
        // passed its own premise by accident.
        string text = string.Join("\n", "-\t# Title", "", "    body", "");

        IngestedDocument document = Ingest(text);

        // ⛔ What the marker check exists for. The content offset is 4 COLUMNS, and cutting the line
        // at 4 CHARACTERS would cut `-\t# ` and leave `Title` — the heading's own marker gone, a
        // heading turned into a paragraph. The parse still finds the heading in the refused text.
        Assert.Equal("line 1 does not open with the list marker the parse reported", document.UnwrapRefusal);
        Assert.Equal(text, document.Text);
        Assert.Equal(["1 Title"], HeadingsOf(document));
    }

    [Fact]
    public void A_tab_after_the_part_being_removed_is_refused_too_because_tab_stops_are_absolute()
    {
        string[] lines = Wrap(Plain).Split('\n');
        Assert.Equal("  ### Deeper", lines[19]);
        lines[19] = "  \t### Deeper";
        string text = string.Join('\n', lines);

        IngestedDocument document = Ingest(text);

        // ⛔ The tab here is NOT in the two characters being removed, and stripping those two spaces
        // would remove only spaces — yet it would still corrupt. The tab spans columns 2 to 4, a
        // two-column indent the heading allows; with the spaces gone it sits at column 0 and spans 0
        // to 4, which makes the line an indented code block. Every character survives, so the ink
        // test cannot see this one: the heading set is what does.
        Assert.Equal("line 20 indents with a tab", document.UnwrapRefusal);
        Assert.Equal(PlainHeadings, HeadingsOf(document));
    }

    [Fact]
    public void An_ordered_list_marker_is_unwrapped_too()
    {
        string text = string.Join("\n", "1. # Title", "   ", "   body", "");

        IngestedDocument document = Ingest(text);

        // The marker is digits and a delimiter, so the offset is three, not two.
        Assert.Equal(string.Join("\n", "# Title", "", "body", ""), document.Text);
        Assert.Equal(1, document.UnwrappedLevels);
    }

    [Fact]
    public void Front_matter_above_a_nested_document_is_kept_and_the_rest_unwrapped()
    {
        string frontMatter = string.Join("\n", "---", "name: a-note", "---", "");

        IngestedDocument document = Ingest(frontMatter + Wrap(Plain));

        Assert.Equal(frontMatter + Plain, document.Text);
        Assert.True(document.Outline.HasFrontMatter);
        Assert.Equal(PlainHeadings, HeadingsOf(document));
    }

    [Fact]
    public void A_document_nested_twice_unwraps_both_levels()
    {
        IngestedDocument document = Ingest(Wrap(Wrap(Plain)));

        Assert.Equal(Plain, document.Text);
        Assert.Equal(2, document.UnwrappedLevels);
    }

    [Fact]
    public void Nesting_deeper_than_the_bound_is_refused_and_says_so()
    {
        string text = Plain;
        for (int i = 0; i <= ListUnwrap.MaxLevels; i++)
        {
            text = Wrap(text);
        }

        IngestedDocument document = Ingest(text);

        // The bound ends a pathological input on its own terms, and says it did rather than
        // reporting the partial result as finished.
        Assert.Equal(ListUnwrap.MaxLevels, document.UnwrappedLevels);
        Assert.Equal($"still wholly nested in a list item after {ListUnwrap.MaxLevels} levels", document.UnwrapRefusal);
        Assert.Equal(Wrap(Plain), document.Text);
    }

    [Fact]
    public void A_CRLF_source_is_read_as_LF_and_its_bytes_keep_their_CRLF()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Wrap(Plain).Replace("\n", "\r\n", StringComparison.Ordinal));

        IngestedDocument document = IngestedDocument.FromBytes(bytes);

        Assert.Equal(Plain, document.Text);
        Assert.Equal(LineEnding.CrLf, document.SourceLineEnding);
        Assert.Contains((byte)'\r', document.RawBytes);
    }

    [Fact]
    public void An_LF_source_says_it_was_LF()
    {
        // The other half: a SourceLineEnding that always said CrLf would pass the test above.
        Assert.Equal(LineEnding.Lf, Ingest(Wrap(Plain)).SourceLineEnding);
    }

    [Fact]
    public void A_byte_order_mark_is_recorded_and_kept_out_of_the_text()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Plain)];

        IngestedDocument document = IngestedDocument.FromBytes(bytes);

        Assert.True(document.HadByteOrderMark);
        Assert.Equal(Plain, document.Text);
        Assert.True(document.RawBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    [Fact]
    public void No_byte_order_mark_is_claimed_where_there_is_none()
    {
        Assert.False(Ingest(Plain).HadByteOrderMark);
    }

    [Fact]
    public void Bytes_that_are_not_UTF8_fall_back_to_Latin1_and_say_so()
    {
        // 0xE9 alone is not valid UTF-8; in Latin-1 it is é.
        byte[] bytes = [.. Encoding.ASCII.GetBytes("# Caf"), 0xE9, (byte)'\n'];

        IngestedDocument document = IngestedDocument.FromBytes(bytes);

        Assert.True(document.Latin1Fallback);
        Assert.Equal(["1 Café"], HeadingsOf(document));
    }

    [Fact]
    public void A_NUL_marks_the_candidate_binary_and_it_is_still_ingested()
    {
        IngestedDocument document = IngestedDocument.FromBytes([.. Encoding.UTF8.GetBytes("# Title\n"), 0]);

        // Recorded, not refused: whether a binary candidate belongs in a corpus is the corpus's call.
        Assert.True(document.IsBinary);
        Assert.Equal(["1 Title"], HeadingsOf(document));
    }

    [Fact]
    public void A_file_is_ingested_with_its_path()
    {
        string path = Path.Combine(Path.GetTempPath(), $"agent-baseline-ingest-{Guid.NewGuid():N}.md");

        try
        {
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(Wrap(Plain)));

            IngestedDocument document = IngestedDocument.FromFile(path);

            Assert.Equal(path, document.Path);
            Assert.Equal(Plain, document.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\rb")]
    [InlineData("a\r\n\rb\n")]
    [InlineData("\r\n\r\n")]
    [InlineData("x\n")]
    [InlineData("")]
    public void The_line_ending_normaliser_agrees_with_the_cores_line_splitter(string text)
    {
        // AGENTS.md section 1: line splitting agrees three ways, so the diff model, the search and the
        // editor number the same lines. This tool's normaliser is a fourth party to that agreement,
        // and it is held to LineSplitter rather than trusted to match it.
        Assert.Equal(LineSplitter.Split(text), MarkdownOutline.NormaliseLineEndings(text).Split('\n'));
    }
}
