using System.Text;
using Bennewitz.Ninja.DiffView.Core;
using DiffPlex.Chunkers;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

/// <summary>The builder's outcomes and failure paths, the probe, the loader, and the line splitter.</summary>
public sealed class BuilderTests
{
    [Fact]
    public void Identical_inputs_produce_no_blocks_and_only_unchanged_rows()
    {
        (string left, _) = Fixtures.Small();

        DiffBuildResult result = DiffDocumentBuilder.Build(left, left, CancellationToken.None);

        Assert.Empty(result.Document.Blocks);
        Assert.True(result.Diagnostics.Identical);
        Assert.All(result.Document.Rows, r => Assert.Equal(DiffLineKind.Unchanged, r.Kind));
        Assert.Equal(result.Document.Left.Lines.Count, result.Document.Rows.Count);
        Assert.Empty(result.Warnings);
        Assert.Equal(1.0, result.Diagnostics.Similarity);
    }

    [Fact]
    public void The_small_pair_yields_the_expected_blocks()
    {
        (string left, string right) = Fixtures.Small();

        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, CancellationToken.None);

        // A using added; a field added; the constructor signature changed and a line added; the
        // Greet body changed; Farewell removed. Which lines Myers pairs where a deletion meets an
        // insertion is the engine's choice, so the claims are the ones that hold for any choice.
        DiffDiagnostics diagnostics = result.Diagnostics;
        Assert.Equal(result.Document.Blocks.Count, diagnostics.BlockCount);
        Assert.True(diagnostics.BlockCount >= 3, $"{diagnostics.BlockCount} blocks");
        Assert.True(diagnostics.Inserted >= 1 && diagnostics.Modified >= 1, $"{diagnostics.Inserted} inserted, {diagnostics.Modified} modified");

        // rows = left lines + inserted rows = right lines + deleted rows.
        Assert.Equal(result.Document.Left.Lines.Count + diagnostics.Inserted, diagnostics.RowCount);
        Assert.Equal(result.Document.Right.Lines.Count + diagnostics.Deleted, diagnostics.RowCount);
        Assert.Equal(result.Document.Left.Lines.Count - result.Document.Right.Lines.Count, diagnostics.Deleted - diagnostics.Inserted);
        Assert.True(diagnostics.Deleted > diagnostics.Inserted, "the left side has more lines than the right");

        Assert.True(diagnostics.BuildTime > TimeSpan.Zero);
        Assert.True(diagnostics.Aligned);
        Assert.False(diagnostics.Identical);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void An_empty_left_pairs_its_one_empty_line_with_the_first_right_line_and_inserts_the_rest()
    {
        // An editor's empty document has one line, so the model does too (invariant 1); DiffPlex
        // would call it zero lines. The first right line pairs with it as modified, the rest insert.
        SideBySideDocument document = DiffDocumentBuilder.Build(string.Empty, "a\nb\nc", CancellationToken.None).Document;

        Assert.Single(document.Left.Lines);
        Assert.Equal(
            [new AlignedRow(0, 0, DiffLineKind.Modified), new AlignedRow(null, 1, DiffLineKind.Inserted), new AlignedRow(null, 2, DiffLineKind.Inserted)],
            document.Rows);
        Assert.Equal(DiffLineKind.Modified, Assert.Single(document.Blocks).Kind);
    }

    [Fact]
    public void An_empty_right_pairs_the_first_left_line_and_deletes_the_rest()
    {
        SideBySideDocument document = DiffDocumentBuilder.Build("a\nb\nc", string.Empty, CancellationToken.None).Document;

        Assert.Single(document.Right.Lines);
        Assert.Equal(
            [new AlignedRow(0, 0, DiffLineKind.Modified), new AlignedRow(1, null, DiffLineKind.Deleted), new AlignedRow(2, null, DiffLineKind.Deleted)],
            document.Rows);
    }

    [Fact]
    public void Two_empty_sides_are_identical_with_one_unchanged_row()
    {
        DiffBuildResult result = DiffDocumentBuilder.Build(string.Empty, string.Empty, CancellationToken.None);

        Assert.True(result.Diagnostics.Identical);
        Assert.Equal([new AlignedRow(0, 0, DiffLineKind.Unchanged)], result.Document.Rows);
    }

    [Fact]
    public void Binary_bytes_fail_the_build_with_BinaryInput_naming_the_side()
    {
        PaneSource binary = PaneSource.FromBytes(Fixtures.BinaryBytes(), "image.png");
        Assert.True(binary.IsBinary);
        Assert.Equal("image.png", binary.Title);

        DiffBuildException left = Assert.Throws<DiffBuildException>(() => DiffDocumentBuilder.Build(binary, "text", CancellationToken.None));
        Assert.Equal(DiffBuildErrorCode.BinaryInput, left.Code);
        Assert.StartsWith("Left side is binary", left.Message);

        DiffBuildException both = Assert.Throws<DiffBuildException>(() => DiffDocumentBuilder.Build(binary, binary, CancellationToken.None));
        Assert.StartsWith("Both sides are binary", both.Message);
    }

    [Fact]
    public void Mixed_and_CR_only_line_endings_warn_and_still_align()
    {
        DiffBuildResult mixed = DiffDocumentBuilder.Build("a\r\nb\nc\rd", "a\r\nb\nc\rd", CancellationToken.None);
        DiffWarning warning = Assert.Single(mixed.Warnings);
        Assert.Equal(DiffWarningCode.MixedLineEndings, warning.Code);
        Assert.Equal("Both sides use mixed line endings.", warning.Message);
        Assert.Equal(LineEnding.Mixed, mixed.Diagnostics.LeftInfo.LineEnding);
        Assert.Equal(4, mixed.Document.Left.Lines.Count);
        Assert.True(mixed.Diagnostics.Identical);

        DiffBuildResult cr = DiffDocumentBuilder.Build("a\rb", "a\nb", CancellationToken.None);
        Assert.Equal("Left side uses CR-only line endings.", Assert.Single(cr.Warnings).Message);
        Assert.True(cr.Diagnostics.Identical);

        Assert.Empty(DiffDocumentBuilder.Build("a\r\nb", "a\nb", CancellationToken.None).Warnings);
    }

    [Fact]
    public void Latin1_fallback_is_recorded_and_warned()
    {
        byte[] bytes = [.. Encoding.ASCII.GetBytes("caf"), 0xE9, (byte)'\n']; // "café" in Latin-1: invalid UTF-8
        PaneSource source = PaneSource.FromBytes(bytes);

        Assert.True(source.Latin1Fallback);
        Assert.Equal("café\n", source.Text);
        Assert.Same(Encoding.Latin1, source.Encoding);
        Assert.False(source.IsBinary);

        DiffBuildResult result = DiffDocumentBuilder.Build(source, "café\n", CancellationToken.None);
        Assert.Equal(DiffWarningCode.Latin1Fallback, Assert.Single(result.Warnings).Code);
        Assert.True(result.Diagnostics.LeftInfo.Latin1Fallback);
    }

    [Fact]
    public void A_line_over_the_word_diff_limit_warns_at_build_and_gets_no_pieces()
    {
        DiffOptions options = new() { MaxWordDiffLineLength = 100 };
        string longLine = Fixtures.SingleLongLine(200);
        string left = "a\n" + longLine + "\nc";
        string right = "a\n" + longLine.Replace("alpha", "ALPHA", StringComparison.Ordinal) + "x\nc";

        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, CancellationToken.None, options);
        DiffWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(DiffWarningCode.LongLinesSkipped, warning.Code);
        Assert.Equal("Word-level highlighting skipped on 1 line longer than 100 characters.", warning.Message);

        WordDiffCache cache = new(options);
        AlignedRow modified = Assert.Single(result.Document.Rows, r => r.Kind == DiffLineKind.Modified);
        WordDiffPieces pieces = cache.GetPieces(result.Document, result.Document.Left.Lines[modified.LeftLine!.Value].Row, LineSplitter.Split(left)[1], LineSplitter.Split(right)[1]);
        Assert.Same(WordDiffPieces.None, pieces);
        Assert.Equal(1, cache.LongLinesSkipped);

        // With word-level off there is nothing to skip.
        Assert.Empty(DiffDocumentBuilder.Build(left, right, CancellationToken.None, options with { WordDiff = WordDiffMode.Off }).Warnings);
    }

    [Fact]
    public void Cancellation_between_stages_is_honoured()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        (string left, string right) = Fixtures.Small();

        Assert.Throws<OperationCanceledException>(() => DiffDocumentBuilder.Build(left, right, cts.Token, null));
    }

    [Fact]
    public void The_options_change_what_counts_as_a_difference()
    {
        Assert.False(DiffDocumentBuilder.Build("  a\nb", "a  \nB", CancellationToken.None).Diagnostics.Identical);
        Assert.True(DiffDocumentBuilder.Build("  a\nb", "a  \nb", CancellationToken.None, new DiffOptions { IgnoreWhitespace = true }).Diagnostics.Identical);
        Assert.True(DiffDocumentBuilder.Build("a\nb", "A\nB", CancellationToken.None, new DiffOptions { IgnoreCase = true }).Diagnostics.Identical);
        Assert.True(DiffDocumentBuilder.Build("  a\nb", "A  \nB", CancellationToken.None, new DiffOptions { IgnoreWhitespace = true, IgnoreCase = true }).Diagnostics.Identical);
    }

    [Theory]
    [InlineData("", 1, LineEnding.None)]
    [InlineData("one", 1, LineEnding.None)]
    [InlineData("a\n", 2, LineEnding.Lf)]
    [InlineData("a\r\nb\r\n", 3, LineEnding.CrLf)]
    [InlineData("a\rb", 2, LineEnding.Cr)]
    [InlineData("a\r\nb\nc", 3, LineEnding.Mixed)]
    [InlineData("a\r\r\n", 3, LineEnding.Mixed)]
    public void The_probe_counts_lines_as_an_editor_does_and_the_splitter_agrees_with_DiffPlex(string text, int lines, LineEnding ending)
    {
        TextInfo info = TextProbe.Probe(text);
        Assert.Equal(lines, info.LineCount);
        Assert.Equal(ending, info.LineEnding);
        Assert.Equal(text.Length, info.Length);

        string[] split = LineSplitter.Split(text);
        Assert.Equal(lines, split.Length);
        Assert.Equal(text, string.Concat(Rejoin(text, split)));

        // DiffPlex's LineChunker splits on the same three terminators (it drops the empty text to nothing, which the builder handles).
        if (text.Length > 0)
        {
            Assert.Equal(split, LineChunker.Instance.Chunk(text));
        }
    }

    [Fact]
    public void The_probe_counts_NULs_and_carries_the_loaders_facts()
    {
        Assert.Equal(2, TextProbe.Probe("a\0b\0").NulCount);

        PaneSource source = PaneSource.FromBytes([0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i'], "/tmp/x/hi.txt");
        TextInfo info = TextProbe.Probe(source);
        Assert.Equal("hi", source.Text);
        Assert.Same(Encoding.UTF8, source.Encoding);
        Assert.Equal("hi.txt", source.Title);
        Assert.Same(source.Encoding, info.Encoding);
        Assert.False(info.IsBinary);
        Assert.False(info.Latin1Fallback);
    }

    [Fact]
    public void The_loader_honours_byte_order_marks()
    {
        Assert.Equal("hi", PaneSource.FromBytes([0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0]).Text);
        Assert.Equal("hi", PaneSource.FromBytes([0xFE, 0xFF, 0, (byte)'h', 0, (byte)'i']).Text);
        Assert.Equal("hi", PaneSource.FromBytes([0xFF, 0xFE, 0, 0, (byte)'h', 0, 0, 0, (byte)'i', 0, 0, 0]).Text);
        Assert.Equal("hé", PaneSource.FromBytes(Encoding.UTF8.GetBytes("hé")).Text); // plain UTF-8, no BOM

        // A UTF-16 file has NULs among its bytes; the BOM is not consulted for the binary check,
        // so it reads as binary — the same call a caller makes for any file, and a known limit.
        Assert.True(PaneSource.FromBytes([0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0]).IsBinary);
    }

    [Fact]
    public void A_source_from_a_file_carries_its_path_and_title()
    {
        string path = Path.Combine(Fixtures.RepoRoot, "fixtures", "small", "left.txt");
        PaneSource source = PaneSource.FromFile(path);

        Assert.Equal(path, source.Path);
        Assert.Equal("left.txt", source.Title);
        Assert.Equal(File.ReadAllText(path), source.Text);
        Assert.Throws<FileNotFoundException>(() => PaneSource.FromFile(Path.Combine(Fixtures.RepoRoot, "fixtures", "nope.txt")));
    }

    /// <summary>Puts the terminators back so the split can be checked against the original text.</summary>
    private static IEnumerable<string> Rejoin(string text, string[] split)
    {
        int at = 0;
        for (int i = 0; i < split.Length; i++)
        {
            yield return split[i];
            at += split[i].Length;
            if (i < split.Length - 1)
            {
                string terminator = text[at] == '\r' && at + 1 < text.Length && text[at + 1] == '\n' ? "\r\n" : text[at].ToString();
                yield return terminator;
                at += terminator.Length;
            }
        }
    }
}
