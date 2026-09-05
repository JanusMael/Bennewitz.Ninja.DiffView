using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

public sealed class WordDiffCacheTests
{
    private static readonly SideBySideDocument Document = DiffDocumentBuilder.Build("a\nb\nc\nd", "A\nB\nC\nD").Document;

    [Fact]
    public void Pieces_mark_the_changed_words_and_cover_both_lines()
    {
        WordDiffCache cache = new(DiffOptions.Default);

        WordDiffPieces pieces = cache.GetPieces(Document, 0, "int count = items.Length;", "int total = items.Count;");

        Assert.Equal(
            [new PieceRange(0, 4, PieceKind.Unchanged), new PieceRange(4, 5, PieceKind.Deleted), new PieceRange(9, 9, PieceKind.Unchanged), new PieceRange(18, 6, PieceKind.Deleted), new PieceRange(24, 1, PieceKind.Unchanged)],
            pieces.Left);
        Assert.Equal(
            [new PieceRange(0, 4, PieceKind.Unchanged), new PieceRange(4, 5, PieceKind.Inserted), new PieceRange(9, 9, PieceKind.Unchanged), new PieceRange(18, 5, PieceKind.Inserted), new PieceRange(23, 1, PieceKind.Unchanged)],
            pieces.Right);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "new text")]
    [InlineData("old text", "")]
    [InlineData("   ", "\t\t")]
    [InlineData("a(b)", "a(c)")]
    [InlineData("x = y + z;", "x = y - z;")]
    [InlineData("trailing ", "trailing")]
    [InlineData("=+-*/\"'[]<>:&|", "=+-*/\"'[]<>:&|!")]
    public void Pieces_concatenate_to_their_lines_in_word_and_character_mode(string left, string right)
    {
        foreach (WordDiffMode mode in new[] { WordDiffMode.Word, WordDiffMode.Character })
        {
            WordDiffCache cache = new(new DiffOptions { WordDiff = mode });
            WordDiffPieces pieces = cache.GetPieces(Document, 0, left, right);
            AssertCovers(pieces.Left, left, PieceKind.Deleted);
            AssertCovers(pieces.Right, right, PieceKind.Inserted);
        }
    }

    [Fact]
    public void Character_mode_splits_inside_a_word()
    {
        WordDiffCache cache = new(new DiffOptions { WordDiff = WordDiffMode.Character });

        WordDiffPieces pieces = cache.GetPieces(Document, 0, "colour", "color");

        Assert.Equal([new PieceRange(0, 4, PieceKind.Unchanged), new PieceRange(4, 1, PieceKind.Deleted), new PieceRange(5, 1, PieceKind.Unchanged)], pieces.Left);
        Assert.Equal([new PieceRange(0, 5, PieceKind.Unchanged)], pieces.Right);
    }

    [Fact]
    public void The_options_apply_to_pieces_too()
    {
        WordDiffPieces exact = new WordDiffCache(DiffOptions.Default).GetPieces(Document, 0, "Alpha beta", "alpha  beta");
        Assert.Contains(exact.Left, p => p.Kind == PieceKind.Deleted);

        WordDiffPieces relaxed = new WordDiffCache(new DiffOptions { IgnoreCase = true, IgnoreWhitespace = true }).GetPieces(Document, 0, "Alpha beta", "alpha  beta");
        Assert.All(relaxed.Left, p => Assert.Equal(PieceKind.Unchanged, p.Kind));
        AssertCovers(relaxed.Right, "alpha  beta", PieceKind.Inserted);
    }

    [Fact]
    public void A_second_request_returns_the_same_instance()
    {
        WordDiffCache cache = new(DiffOptions.Default);

        WordDiffPieces first = cache.GetPieces(Document, 1, "one two", "one three");
        WordDiffPieces second = cache.GetPieces(Document, 1, "one two", "one three");

        Assert.Same(first, second);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Evicts_the_least_recently_used_row()
    {
        WordDiffCache cache = new(DiffOptions.Default, capacity: 2);

        WordDiffPieces row0 = cache.GetPieces(Document, 0, "a b", "a c");
        cache.GetPieces(Document, 1, "d e", "d f");
        cache.GetPieces(Document, 0, "a b", "a c");   // row 0 is now the most recently used
        cache.GetPieces(Document, 2, "g h", "g i");   // evicts row 1

        Assert.True(cache.Contains(Document, 0));
        Assert.False(cache.Contains(Document, 1));
        Assert.True(cache.Contains(Document, 2));
        Assert.Equal(2, cache.Count);
        Assert.Same(row0, cache.GetPieces(Document, 0, "a b", "a c"));
    }

    [Fact]
    public void A_rebuilt_document_never_serves_the_old_documents_pieces()
    {
        WordDiffCache cache = new(DiffOptions.Default);
        SideBySideDocument rebuilt = DiffDocumentBuilder.Build("a\nb\nc\nd", "A\nB\nC\nD").Document;
        Assert.NotEqual(Document.Version, rebuilt.Version);

        WordDiffPieces old = cache.GetPieces(Document, 0, "a b", "a c");
        Assert.False(cache.Contains(rebuilt, 0));
        WordDiffPieces fresh = cache.GetPieces(rebuilt, 0, "a b", "a d");

        Assert.NotSame(old, fresh);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Word_level_off_yields_no_pieces_and_caches_nothing()
    {
        WordDiffCache cache = new(new DiffOptions { WordDiff = WordDiffMode.Off });

        Assert.Same(WordDiffPieces.None, cache.GetPieces(Document, 0, "a b", "a c"));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Clear_drops_everything_and_resets_the_long_line_count()
    {
        WordDiffCache cache = new(new DiffOptions { MaxWordDiffLineLength = 3 });
        cache.GetPieces(Document, 0, "abcd", "ab");
        Assert.Equal(1, cache.LongLinesSkipped);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.LongLinesSkipped);
    }

    private static void AssertCovers(IReadOnlyList<PieceRange> pieces, string line, PieceKind changedKind)
    {
        int expected = 0;
        foreach (PieceRange piece in pieces)
        {
            Assert.Equal(expected, piece.Start);
            Assert.True(piece.Length > 0);
            Assert.True(piece.Kind == PieceKind.Unchanged || piece.Kind == changedKind);
            expected = piece.End;
        }

        Assert.Equal(line.Length, expected);
    }
}
