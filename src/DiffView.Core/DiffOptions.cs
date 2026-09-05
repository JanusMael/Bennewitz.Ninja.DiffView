namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>How word-level pieces are computed inside a modified row.</summary>
public enum WordDiffMode
{
    /// <summary>No word-level pieces.</summary>
    Off,

    /// <summary>Pieces are words and separator runs (<see cref="DiffOptions.WordSeparators"/>).</summary>
    Word,

    /// <summary>Pieces are single characters.</summary>
    Character,
}

/// <summary>Everything that shapes a build and the word-level pieces derived from it.</summary>
public sealed record DiffOptions
{
    /// <summary>
    /// DiffPlex's word separators (space, tab, <c>.(){},!?;</c>) extended with
    /// <c>= + - * / " ' [ ] &lt; &gt; : &amp; |</c>, so operators and brackets split code into pieces.
    /// Declared before <see cref="Default"/>: static initialisers run in textual order, and the
    /// default instance reads this list.
    /// </summary>
    public static IReadOnlyList<char> DefaultWordSeparators { get; } =
        [' ', '\t', '.', '(', ')', '{', '}', ',', '!', '?', ';', '=', '+', '-', '*', '/', '"', '\'', '[', ']', '<', '>', ':', '&', '|'];

    /// <summary>The defaults.</summary>
    public static DiffOptions Default { get; } = new();

    /// <summary>Leading and trailing whitespace of a line (or a word piece) does not count as a difference.</summary>
    public bool IgnoreWhitespace { get; init; }

    /// <summary>Letter case does not count as a difference.</summary>
    public bool IgnoreCase { get; init; }

    /// <summary>Word-level mode.</summary>
    public WordDiffMode WordDiff { get; init; } = WordDiffMode.Word;

    /// <summary>The characters that separate words in <see cref="WordDiffMode.Word"/>.</summary>
    public IReadOnlyList<char> WordSeparators { get; init; } = DefaultWordSeparators;

    /// <summary>A line longer than this gets no word-level pieces; such lines are counted in the <c>LongLinesSkipped</c> warning.</summary>
    public int MaxWordDiffLineLength { get; init; } = 20_000;

    /// <summary>
    /// Below this line-overlap similarity (0..1, Dice coefficient over line hashes), inputs
    /// above <see cref="AlignmentSizeThreshold"/> are not aligned: the Myers run cannot be
    /// cancelled and two unrelated large files would take minutes.
    /// </summary>
    public double AlignmentSimilarityFloor { get; init; } = 0.1;

    /// <summary>The combined line count from which the similarity gate applies; smaller inputs always align.</summary>
    public int AlignmentSizeThreshold { get; init; } = 10_000;

    /// <summary>Align regardless of similarity — the banner's "Force" — accepting the cost.</summary>
    public bool ForceAlignment { get; init; }
}
