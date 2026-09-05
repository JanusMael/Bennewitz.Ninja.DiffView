using System.Text.RegularExpressions;

namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>How the search reads a side: line count and line text, with no terminators. The editor supplies a snapshot; tests supply strings.</summary>
public interface IPaneText
{
    /// <summary>Lines on this side.</summary>
    int LineCount { get; }

    /// <summary>The text of <paramref name="line"/> without its terminator.</summary>
    string GetLine(int line);
}

/// <summary>An <see cref="IPaneText"/> over a string, split the way the model splits lines.</summary>
public sealed class StringPaneText : IPaneText
{
    private readonly string[] _lines;

    /// <param name="text">The side's text.</param>
    public StringPaneText(string text)
    {
        _lines = LineSplitter.Split(text);
    }

    /// <inheritdoc/>
    public int LineCount => _lines.Length;

    /// <inheritdoc/>
    public string GetLine(int line) => _lines[line];
}

/// <summary>Which side or sides a search covers.</summary>
public enum FindScope
{
    /// <summary>The left pane only.</summary>
    Left,

    /// <summary>The right pane only.</summary>
    Right,

    /// <summary>Both panes, walked in row order, left before right within a row.</summary>
    Both,
}

/// <summary>How a search runs.</summary>
public sealed record FindOptions
{
    /// <summary>The defaults: case-insensitive literal search over both sides, at most 10,000 matches.</summary>
    public static FindOptions Default { get; } = new();

    /// <summary>Letter case matters.</summary>
    public bool MatchCase { get; init; }

    /// <summary>A match must not be preceded or followed by a word character.</summary>
    public bool WholeWord { get; init; }

    /// <summary>The query is a .NET regular expression.</summary>
    public bool UseRegex { get; init; }

    /// <summary>Which side or sides.</summary>
    public FindScope Scope { get; init; } = FindScope.Both;

    /// <summary>Only lines on changed rows.</summary>
    public bool ChangedRowsOnly { get; init; }

    /// <summary>The cap; past it the result is <see cref="FindResult.Truncated"/>.</summary>
    public int MaxMatches { get; init; } = 10_000;

    /// <summary>The per-match timeout for a regular expression that cannot run without backtracking.</summary>
    public TimeSpan MatchTimeout { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>One hit: which side, which line, where in it.</summary>
public readonly record struct FindMatch(DiffSide Side, int Line, int Column, int Length);

/// <summary>The outcome of a search. A bad query is an <see cref="Error"/>, never an exception.</summary>
public sealed record FindResult(IReadOnlyList<FindMatch> Matches, int LeftCount, int RightCount, bool Truncated, string? Error)
{
    /// <summary>No matches.</summary>
    public static FindResult Empty { get; } = new([], 0, 0, false, null);

    /// <summary>A search that could not run.</summary>
    public static FindResult Failed(string error) => new([], 0, 0, false, error);
}

/// <summary>
/// Finds a query in one or both sides, in row order — left before right within a row, then by
/// column — so walking the matches never jumps backwards on screen. Literal search is
/// culture-invariant; regular expressions run non-backtracking when the pattern allows it and
/// with a match timeout otherwise. An invalid pattern or a timeout is <see cref="FindResult.Error"/>;
/// cancellation throws.
/// </summary>
public static class DiffSearch
{
    /// <summary>Runs the search.</summary>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public static FindResult Find(
        SideBySideDocument document,
        IPaneText left,
        IPaneText right,
        string query,
        FindOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        options ??= FindOptions.Default;

        if (string.IsNullOrEmpty(query))
        {
            return FindResult.Empty;
        }

        Matcher matcher;
        try
        {
            matcher = Matcher.Create(query, options);
        }
        catch (ArgumentException ex)
        {
            return FindResult.Failed("Invalid regular expression: " + ex.Message);
        }

        List<FindMatch> matches = [];
        int leftCount = 0;
        int rightCount = 0;
        bool truncated = false;

        try
        {
            for (int row = 0; row < document.Rows.Count && !truncated; row++)
            {
                if ((row & 0xFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                AlignedRow aligned = document.Rows[row];
                if (options.ChangedRowsOnly && aligned.Kind == DiffLineKind.Unchanged)
                {
                    continue;
                }

                if (options.Scope != FindScope.Right && aligned.LeftLine is { } l && l < left.LineCount)
                {
                    truncated = Collect(matcher, DiffSide.Left, l, left.GetLine(l), options.MaxMatches, matches, ref leftCount);
                }

                if (!truncated && options.Scope != FindScope.Left && aligned.RightLine is { } r && r < right.LineCount)
                {
                    truncated = Collect(matcher, DiffSide.Right, r, right.GetLine(r), options.MaxMatches, matches, ref rightCount);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return FindResult.Failed($"The pattern took longer than {options.MatchTimeout.TotalMilliseconds:N0} ms to match a line and was stopped.");
        }

        return new FindResult(matches, leftCount, rightCount, truncated, null);
    }

    /// <summary>Adds the line's matches; returns true when the cap was reached.</summary>
    private static bool Collect(Matcher matcher, DiffSide side, int line, string text, int max, List<FindMatch> matches, ref int sideCount)
    {
        foreach ((int column, int length) in matcher.Matches(text))
        {
            if (matches.Count >= max)
            {
                return true;
            }

            matches.Add(new FindMatch(side, line, column, length));
            sideCount++;
        }

        return false;
    }

    /// <summary>The literal or regular-expression matcher a query compiles to.</summary>
    private sealed class Matcher
    {
        private readonly string? _literal;
        private readonly StringComparison _comparison;
        private readonly bool _wholeWord;
        private readonly Regex? _regex;

        private Matcher(string? literal, StringComparison comparison, bool wholeWord, Regex? regex)
        {
            _literal = literal;
            _comparison = comparison;
            _wholeWord = wholeWord;
            _regex = regex;
        }

        /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
        public static Matcher Create(string query, FindOptions options)
        {
            if (!options.UseRegex)
            {
                return new Matcher(query, options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase, options.WholeWord, null);
            }

            string pattern = options.WholeWord ? @"\b(?:" + query + @")\b" : query;
            RegexOptions regexOptions = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);

            Regex regex;
            try
            {
                // Linear-time engine when the pattern has no backreferences or lookarounds.
                regex = new Regex(pattern, regexOptions | RegexOptions.NonBacktracking);
            }
            catch (NotSupportedException)
            {
                regex = new Regex(pattern, regexOptions, options.MatchTimeout);
            }

            return new Matcher(null, StringComparison.Ordinal, false, regex);
        }

        public IEnumerable<(int Column, int Length)> Matches(string text)
        {
            if (_regex is not null)
            {
                foreach (Match match in _regex.Matches(text))
                {
                    if (match.Length > 0)
                    {
                        yield return (match.Index, match.Length);
                    }
                }

                yield break;
            }

            string literal = _literal!;
            int from = 0;
            while (from <= text.Length - literal.Length)
            {
                int at = text.IndexOf(literal, from, _comparison);
                if (at < 0)
                {
                    yield break;
                }

                if (!_wholeWord || IsWholeWord(text, at, literal.Length))
                {
                    yield return (at, literal.Length);
                }

                from = at + Math.Max(1, literal.Length);
            }
        }

        private static bool IsWholeWord(string text, int at, int length)
        {
            bool before = at == 0 || !IsWordChar(text[at - 1]);
            bool after = at + length >= text.Length || !IsWordChar(text[at + length]);
            return before && after;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
