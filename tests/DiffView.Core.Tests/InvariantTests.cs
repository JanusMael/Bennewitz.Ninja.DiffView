using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

/// <summary>
/// The seven invariants of plan 00001 §DiffView.Core, each on the committed small pair and on
/// a generated pair with every block kind, plus the degenerate inputs.
/// </summary>
public sealed class InvariantTests
{
    public static TheoryData<string, string, string> Pairs()
    {
        (string smallLeft, string smallRight) = Fixtures.Small();
        (string similarLeft, string similarRight) = Fixtures.SimilarPair(1000, seed: 1);
        return new TheoryData<string, string, string>
        {
            { "small", smallLeft, smallRight },
            { "similar-1000", similarLeft, similarRight },
            { "identical", smallLeft, smallLeft },
            { "empty-left", string.Empty, smallRight },
            { "empty-right", smallLeft, string.Empty },
            { "both-empty", string.Empty, string.Empty },
            { "no-terminator", "one\ntwo\nthree", "one\nthree" },
        };
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Invariant_1_every_line_of_each_side_appears_exactly_once_in_rows_in_order(string name, string left, string right)
    {
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;

        foreach (DiffSide side in new[] { DiffSide.Left, DiffSide.Right })
        {
            int[] lines = document.Rows.Select(r => SideBySideDocument.LineOf(r, side)).Where(l => l is not null).Select(l => l!.Value).ToArray();
            Assert.Equal(Enumerable.Range(0, document.Pane(side).Lines.Count), lines);

            // The pane's own row pointers agree with the table.
            for (int line = 0; line < lines.Length; line++)
            {
                DiffLine metadata = document.Pane(side).Lines[line];
                Assert.Equal(line, SideBySideDocument.LineOf(document.Rows[metadata.Row], side));
                Assert.Equal(document.Rows[metadata.Row].Kind, metadata.Kind);
            }
        }

        // And the line count is the editor's: terminators plus one.
        Assert.Equal(LineSplitter.Split(left).Length, document.Left.Lines.Count);
        Assert.Equal(LineSplitter.Split(right).Length, document.Right.Lines.Count);
        _ = name;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Invariant_2_no_row_has_both_sides_null_and_kinds_match_the_sides_present(string name, string left, string right)
    {
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;

        Assert.All(document.Rows, row =>
        {
            Assert.True(row.LeftLine is not null || row.RightLine is not null, $"{name}: a row with no side");
            switch (row.Kind)
            {
                case DiffLineKind.Inserted:
                    Assert.Null(row.LeftLine);
                    Assert.NotNull(row.RightLine);
                    break;
                case DiffLineKind.Deleted:
                    Assert.NotNull(row.LeftLine);
                    Assert.Null(row.RightLine);
                    break;
                default:
                    Assert.NotNull(row.LeftLine);
                    Assert.NotNull(row.RightLine);
                    break;
            }
        });
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Invariant_3_pieces_of_a_modified_row_concatenate_to_their_lines(string name, string left, string right)
    {
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;
        string[] leftLines = LineSplitter.Split(left);
        string[] rightLines = LineSplitter.Split(right);
        WordDiffCache cache = new(DiffOptions.Default);

        int modified = 0;
        foreach (AlignedRow row in document.Rows.Where(r => r.Kind == DiffLineKind.Modified))
        {
            string l = leftLines[row.LeftLine!.Value];
            string r = rightLines[row.RightLine!.Value];
            WordDiffPieces pieces = cache.GetPieces(document, document.Left.Lines[row.LeftLine.Value].Row, l, r);
            AssertCovers(pieces.Left, l, PieceKind.Deleted);
            AssertCovers(pieces.Right, r, PieceKind.Inserted);
            modified++;
        }

        if (name is "small" or "similar-1000")
        {
            Assert.True(modified > 0, "the pair should have modified rows");
        }
    }

    private static void AssertCovers(IReadOnlyList<PieceRange> pieces, string line, PieceKind changedKind)
    {
        int expected = 0;
        foreach (PieceRange piece in pieces)
        {
            Assert.Equal(expected, piece.Start);
            Assert.True(piece.Length > 0, "empty piece");
            Assert.True(piece.Kind == PieceKind.Unchanged || piece.Kind == changedKind, $"a {piece.Kind} piece on the {changedKind} side");
            expected = piece.End;
        }

        Assert.Equal(line.Length, expected);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Invariant_4_blocks_are_disjoint_ordered_cover_every_changed_row_and_carry_exact_line_ranges(string name, string left, string right)
    {
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;

        int previousLast = -1;
        HashSet<int> covered = [];
        foreach (ChangeBlock block in document.Blocks)
        {
            Assert.Equal(covered.Count == 0 ? 0 : document.Blocks[block.Index - 1].Index + 1, block.Index);
            Assert.True(block.FirstRow > previousLast, $"{name}: block {block.Index} overlaps or is out of order");
            Assert.True(block.LastRow >= block.FirstRow);
            previousLast = block.LastRow;

            // The rows just outside the block are unchanged (maximal runs).
            Assert.True(block.FirstRow == 0 || document.Rows[block.FirstRow - 1].Kind == DiffLineKind.Unchanged);
            Assert.True(block.LastRow == document.Rows.Count - 1 || document.Rows[block.LastRow + 1].Kind == DiffLineKind.Unchanged);

            IEnumerable<AlignedRow> rows = Enumerable.Range(block.FirstRow, block.RowCount).Select(i => document.Rows[i]);
            foreach (int row in Enumerable.Range(block.FirstRow, block.RowCount))
            {
                Assert.NotEqual(DiffLineKind.Unchanged, document.Rows[row].Kind);
                covered.Add(row);
            }

            int[] leftLines = rows.Where(r => r.LeftLine is not null).Select(r => r.LeftLine!.Value).ToArray();
            int[] rightLines = rows.Where(r => r.RightLine is not null).Select(r => r.RightLine!.Value).ToArray();
            Assert.Equal(leftLines, Enumerable.Range(block.LeftLines.Start, block.LeftLines.Count).ToArray());
            Assert.Equal(rightLines, Enumerable.Range(block.RightLines.Start, block.RightLines.Count).ToArray());

            Assert.Equal(rows.Count(r => r.Kind == DiffLineKind.Inserted), block.InsertedCount);
            Assert.Equal(rows.Count(r => r.Kind == DiffLineKind.Deleted), block.DeletedCount);
            Assert.Equal(rows.Count(r => r.Kind == DiffLineKind.Modified), block.ModifiedCount);
            DiffLineKind expectedKind = block.DeletedCount == 0 && block.ModifiedCount == 0 ? DiffLineKind.Inserted
                : block.InsertedCount == 0 && block.ModifiedCount == 0 ? DiffLineKind.Deleted
                : DiffLineKind.Modified;
            Assert.Equal(expectedKind, block.Kind);
        }

        int[] changedRows = Enumerable.Range(0, document.Rows.Count).Where(i => document.Rows[i].Kind != DiffLineKind.Unchanged).ToArray();
        Assert.Equal(changedRows, covered.Order().ToArray());
    }

    [Fact]
    public void Invariant_4_an_empty_range_sits_where_the_next_line_of_that_side_would_go()
    {
        // "b" inserted between a and c on the right: the block's left range is empty at line 1.
        SideBySideDocument document = DiffDocumentBuilder.Build("a\nc\n", "a\nb\nc\n").Document;
        ChangeBlock block = Assert.Single(document.Blocks);
        Assert.Equal(DiffLineKind.Inserted, block.Kind);
        Assert.Equal(LineRange.Empty(1), block.LeftLines);
        Assert.Equal(new LineRange(1, 1), block.RightLines);

        // Deleted at the very start: the empty right range is at line 0.
        document = DiffDocumentBuilder.Build("x\na\n", "a\n").Document;
        block = Assert.Single(document.Blocks);
        Assert.Equal(new LineRange(0, 1), block.LeftLines);
        Assert.Equal(LineRange.Empty(0), block.RightLines);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Invariant_5_line_ending_variants_of_the_same_content_produce_identical_rows(string lineEnding)
    {
        (string left, string right) = Fixtures.SimilarPair(300, seed: 5);
        string leftVariant = left.Replace("\n", lineEnding, StringComparison.Ordinal);
        string rightVariant = right.Replace("\n", lineEnding, StringComparison.Ordinal);

        AlignedRow[] reference = [.. DiffDocumentBuilder.Build(left, right).Document.Rows];
        AlignedRow[] rows = [.. DiffDocumentBuilder.Build(leftVariant, rightVariant).Document.Rows];

        Assert.Equal(reference, rows);
        Assert.Contains(reference, r => r.Kind != DiffLineKind.Unchanged);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Invariant_6_padding_before_every_line_plus_trailing_equals_the_sides_null_rows(string name, string left, string right)
    {
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;

        foreach (DiffSide side in new[] { DiffSide.Left, DiffSide.Right })
        {
            int lines = document.Pane(side).Lines.Count;
            int sum = Enumerable.Range(0, lines).Sum(line => Padding.Before(document, side, line)) + Padding.Trailing(document, side);
            int nulls = document.Rows.Count(r => SideBySideDocument.LineOf(r, side) is null);
            Assert.Equal(nulls, sum);

            // Padding before a line is exactly the run of null rows immediately above its row.
            for (int line = 0; line < lines; line++)
            {
                int row = document.Pane(side).Lines[line].Row;
                int above = 0;
                for (int i = row - 1; i >= 0 && SideBySideDocument.LineOf(document.Rows[i], side) is null; i--)
                {
                    above++;
                }

                Assert.Equal(above, Padding.Before(document, side, line));
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Padding.Before(document, DiffSide.Left, document.Left.Lines.Count));
        _ = name;
    }

    [Fact]
    public void Invariant_7_below_the_floor_on_large_inputs_the_sides_are_concatenated_unaligned_with_the_warning()
    {
        (string left, string right) = Fixtures.UnrelatedPair(6000, seed: 3);
        DiffOptions options = new() { AlignmentSizeThreshold = 10_000, AlignmentSimilarityFloor = 0.1 };

        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, options);

        Assert.False(result.Diagnostics.Aligned);
        Assert.True(result.Diagnostics.Similarity < 0.01, $"similarity {result.Diagnostics.Similarity}");
        Assert.True(result.Has(DiffWarningCode.TooDifferentToAlign));

        SideBySideDocument document = result.Document;
        int leftCount = document.Left.Lines.Count;
        Assert.Equal(leftCount + document.Right.Lines.Count, document.Rows.Count);
        Assert.All(document.Rows.Take(leftCount), r => Assert.Equal(DiffLineKind.Deleted, r.Kind));
        Assert.All(document.Rows.Skip(leftCount), r => Assert.Equal(DiffLineKind.Inserted, r.Kind));
        Assert.Equal(Enumerable.Range(0, leftCount), document.Rows.Take(leftCount).Select(r => r.LeftLine!.Value));

        // One block covers the whole table with both full ranges.
        ChangeBlock block = Assert.Single(document.Blocks);
        Assert.Equal(new LineRange(0, leftCount), block.LeftLines);
        Assert.Equal(new LineRange(0, document.Right.Lines.Count), block.RightLines);

        // Forcing aligns; so does being under the size threshold.
        Assert.True(DiffDocumentBuilder.Build(left, right, options with { ForceAlignment = true }).Diagnostics.Aligned);
        Assert.True(DiffDocumentBuilder.Build(left, right, options with { AlignmentSizeThreshold = 100_000 }).Diagnostics.Aligned);
        (string smallLeft, string smallRight) = Fixtures.UnrelatedPair(50, seed: 3);
        Assert.True(DiffDocumentBuilder.Build(smallLeft, smallRight, options).Diagnostics.Aligned);
    }
}
