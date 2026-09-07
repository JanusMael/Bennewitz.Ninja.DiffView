namespace Bennewitz.Ninja.DiffView.Core.Tests;

/// <summary>
/// The unified reading of a model: what it emits, in what order, what it maps back, and that it
/// loses nothing a side-by-side reading shows.
/// </summary>
public sealed class InlineDocumentTests
{
    [Fact]
    public void Identical_inputs_emit_one_line_per_row_and_no_blocks()
    {
        (string left, _) = Fixtures.Small();
        SideBySideDocument model = DiffDocumentBuilder.Build(left, left).Document;

        InlineDocument inline = InlineDocument.Build(model);

        Assert.Equal(model.Rows.Count, inline.Lines.Count);
        Assert.All(inline.Lines, line => Assert.Equal(DiffLineKind.Unchanged, line.Kind));
        // A context row shows its left line; the right line of that row is not displayed.
        Assert.All(inline.Lines, line => Assert.Equal(DiffSide.Left, line.Side));
        Assert.Equal(model.Version, inline.Version);
        Assert.Same(model, inline.Model);
    }

    [Fact]
    public void Every_displayed_line_appears_exactly_once_and_the_counts_add_up()
    {
        (string left, string right) = Fixtures.Small();
        SideBySideDocument model = DiffDocumentBuilder.Build(left, right).Document;

        InlineDocument inline = InlineDocument.Build(model);

        // Every left line is shown; a right line is shown unless its row is unchanged, where the
        // left line already carries the text.
        int unchangedRows = model.Rows.Count(r => r.Kind == DiffLineKind.Unchanged);
        Assert.Equal(model.Left.Lines.Count + model.Right.Lines.Count - unchangedRows, inline.Lines.Count);

        HashSet<(DiffSide Side, int Line)> seen = [];
        foreach (InlineLine line in inline.Lines)
        {
            Assert.True(seen.Add((line.Side, line.SourceLine)), $"{line.Side} line {line.SourceLine} twice");
        }

        for (int i = 0; i < model.Left.Lines.Count; i++)
        {
            Assert.Equal(i, inline.Lines[inline.LineOf(DiffSide.Left, i)!.Value].SourceLine);
        }
    }

    [Fact]
    public void A_change_block_prints_its_removals_before_its_additions()
    {
        // Two lines replaced by three: one block, whose left lines come first as a group.
        SideBySideDocument model = DiffDocumentBuilder.Build("a\nOLD1\nOLD2\nz", "a\nNEW1\nNEW2\nNEW3\nz").Document;
        ChangeBlock block = Assert.Single(model.Blocks);

        InlineDocument inline = InlineDocument.Build(model);
        LineRange range = inline.LinesOfBlock(block.Index);

        List<InlineLine> lines = [.. inline.Lines.Skip(range.Start).Take(range.Count)];
        Assert.Equal(block.DeletedCount + block.InsertedCount + (2 * block.ModifiedCount), range.Count);
        int firstRight = lines.FindIndex(l => l.Side == DiffSide.Right);
        Assert.All(lines.Take(firstRight), l => Assert.Equal(DiffSide.Left, l.Side));
        Assert.All(lines.Skip(firstRight), l => Assert.Equal(DiffSide.Right, l.Side));

        // Each side's lines keep their own order inside the group.
        Assert.Equal(
            lines.Where(l => l.Side == DiffSide.Left).Select(l => l.SourceLine),
            lines.Where(l => l.Side == DiffSide.Left).Select(l => l.SourceLine).Order());
        Assert.Equal(
            lines.Where(l => l.Side == DiffSide.Right).Select(l => l.SourceLine),
            lines.Where(l => l.Side == DiffSide.Right).Select(l => l.SourceLine).Order());

        // The context lines are outside the block's range, and the range is contiguous.
        Assert.Equal(DiffLineKind.Unchanged, inline.Lines[range.Start - 1].Kind);
        Assert.Equal(DiffLineKind.Unchanged, inline.Lines[range.Start + range.Count].Kind);
        Assert.All(lines, l => Assert.NotEqual(DiffLineKind.Unchanged, l.Kind));
    }

    [Fact]
    public void A_modified_row_keeps_its_kind_on_both_halves_and_a_pure_delete_or_insert_does_not()
    {
        SideBySideDocument model = DiffDocumentBuilder.Build("keep\nchanged left\ngone\n", "keep\nchanged right\nadded\n").Document;

        InlineDocument inline = InlineDocument.Build(model);

        foreach (InlineLine line in inline.Lines)
        {
            DiffLineKind rowKind = model.Rows[line.Row].Kind;
            DiffLineKind expected = rowKind switch
            {
                DiffLineKind.Modified => DiffLineKind.Modified,
                DiffLineKind.Unchanged => DiffLineKind.Unchanged,
                _ => line.Side == DiffSide.Left ? DiffLineKind.Deleted : DiffLineKind.Inserted,
            };
            Assert.Equal(expected, line.Kind);
        }

        // Both halves of a modified row point at the same row, which is what carries the pieces.
        List<InlineLine> modified = [.. inline.Lines.Where(l => l.Kind == DiffLineKind.Modified)];
        Assert.NotEmpty(modified);
        Assert.All(modified.GroupBy(l => l.Row), group => Assert.Equal(2, group.Count()));
    }

    [Fact]
    public void The_right_line_of_a_context_row_is_not_displayed_and_maps_to_nothing()
    {
        SideBySideDocument model = DiffDocumentBuilder.Build("same\nleft only\n", "same\nright only\n").Document;

        InlineDocument inline = InlineDocument.Build(model);

        AlignedRow context = model.Rows.First(r => r.Kind == DiffLineKind.Unchanged);
        Assert.NotNull(inline.LineOf(DiffSide.Left, context.LeftLine!.Value));
        Assert.Null(inline.LineOf(DiffSide.Right, context.RightLine!.Value));
    }

    [Fact]
    public void Out_of_range_lines_and_blocks_map_to_nothing_rather_than_throwing()
    {
        SideBySideDocument model = DiffDocumentBuilder.Build("a\n", "b\n").Document;

        InlineDocument inline = InlineDocument.Build(model);

        Assert.Null(inline.LineOf(DiffSide.Left, -1));
        Assert.Null(inline.LineOf(DiffSide.Left, model.Left.Lines.Count));
        Assert.Null(inline.LineOf(DiffSide.Right, int.MaxValue));
        Assert.True(inline.LinesOfBlock(-1).IsEmpty);
        Assert.True(inline.LinesOfBlock(model.Blocks.Count).IsEmpty);
    }

    [Fact]
    public void Every_block_maps_to_a_contiguous_range_holding_exactly_its_own_rows()
    {
        (string left, string right) = Fixtures.SimilarPair(600, seed: 11);
        SideBySideDocument model = DiffDocumentBuilder.Build(left, right).Document;

        InlineDocument inline = InlineDocument.Build(model);

        Assert.NotEmpty(model.Blocks);
        int previousEnd = -1;
        foreach (ChangeBlock block in model.Blocks)
        {
            LineRange range = inline.LinesOfBlock(block.Index);
            Assert.False(range.IsEmpty);
            Assert.True(range.Start > previousEnd, "the blocks' ranges are ordered and disjoint");
            previousEnd = range.End - 1;

            for (int line = range.Start; line < range.End; line++)
            {
                int row = inline.Lines[line].Row;
                Assert.True(row >= block.FirstRow && row <= block.LastRow, $"line {line} is on row {row}, outside block {block.Index}");
            }
        }

        // Nothing outside a block's range belongs to a changed row.
        HashSet<int> inBlocks = [];
        foreach (ChangeBlock block in model.Blocks)
        {
            LineRange range = inline.LinesOfBlock(block.Index);
            for (int line = range.Start; line < range.End; line++)
            {
                inBlocks.Add(line);
            }
        }

        for (int line = 0; line < inline.Lines.Count; line++)
        {
            Assert.Equal(inBlocks.Contains(line), inline.Lines[line].Kind != DiffLineKind.Unchanged);
        }
    }

    [Fact]
    public void An_unaligned_pair_prints_every_left_line_then_every_right_line()
    {
        // Past the size threshold with nothing in common, the gate refuses to align and the
        // builder concatenates: one block, every deletion before every insertion.
        (string left, string right) = Fixtures.UnrelatedPair(6_000, seed: 3);
        DiffBuildResult result = DiffDocumentBuilder.Build(left, right);
        Assert.False(result.Diagnostics.Aligned);

        InlineDocument inline = InlineDocument.Build(result.Document);

        Assert.Equal(result.Document.Left.Lines.Count + result.Document.Right.Lines.Count, inline.Lines.Count);
        int firstRight = inline.Lines.ToList().FindIndex(l => l.Side == DiffSide.Right);
        Assert.Equal(result.Document.Left.Lines.Count, firstRight);
        Assert.All(inline.Lines.Take(firstRight), l => Assert.Equal(DiffSide.Left, l.Side));
    }

    [Fact]
    public void Build_rejects_a_null_model()
    {
        Assert.Throws<ArgumentNullException>(() => InlineDocument.Build(null!));
    }
}
