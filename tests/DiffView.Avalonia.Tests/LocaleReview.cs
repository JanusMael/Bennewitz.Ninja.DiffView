using System.Text;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Reads a <c>docs/locale-review/&lt;culture&gt;.md</c> document back and checks that every row
/// still describes the string it claims to.
/// </summary>
/// <remarks>
/// <para>
/// The document is generated and never edited, so the drift that matters is the strings moving
/// underneath it: a translation corrected in the <c>.resx</c>, an English default reworded, a
/// summary rewritten. A review document that describes the strings of six weeks ago is worse than
/// no review document, because a reviewer signs off on what they read.
/// </para>
/// <para>
/// This reads the committed markdown rather than re-rendering it, for the same reason
/// <c>LocalizationTests</c> reads the committed neutral resx rather than re-running the generator:
/// a gate that re-derives its subject from the same code that produced it tests very little. What
/// it checks is content — key, context, placeholders, English, translation — and not layout, since
/// a layout change is harmless and a content change is the whole risk.
/// </para>
/// </remarks>
internal static class LocaleReview
{
    /// <summary>A row of the review table, cells already unescaped.</summary>
    internal readonly record struct Row(string Key, string Context, string Holds, string English, string Translation);

    /// <summary>Something the document says that the strings no longer agree with.</summary>
    internal readonly record struct Violation(string Culture, string Key, string What)
    {
        public override string ToString()
        {
            return $"{Culture} '{Key}': {What}";
        }
    }

    internal const string Directory = "docs/locale-review";

    /// <summary>The table rows of a review document, in the order they appear.</summary>
    internal static List<Row> Parse(string markdown)
    {
        List<Row> rows = [];
        foreach (string line in markdown.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (!trimmed.StartsWith("| `", StringComparison.Ordinal))
            {
                // The header row and the separator start with "| Key" and "|---", and the prose
                // above the table does not start with a pipe at all.
                continue;
            }

            List<string> cells = Cells(trimmed);
            if (cells.Count != 5)
            {
                continue;
            }

            rows.Add(new Row(cells[0].Trim('`'), cells[1], cells[2], cells[3], cells[4]));
        }

        return rows;
    }

    /// <summary>Every way the document and the strings can disagree.</summary>
    internal static List<Violation> Check(
        string culture,
        IReadOnlyList<Row> rows,
        IReadOnlyDictionary<string, string> english,
        IReadOnlyDictionary<string, string> translated,
        IReadOnlyDictionary<string, string> summaries)
    {
        List<Violation> violations = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (Row row in rows)
        {
            if (!seen.Add(row.Key))
            {
                violations.Add(new Violation(culture, row.Key, "appears in the document twice"));
                continue;
            }

            if (!english.TryGetValue(row.Key, out string? englishText))
            {
                violations.Add(new Violation(culture, row.Key, "has a row but is not a key the library declares"));
                continue;
            }

            if (!string.Equals(row.English, englishText, StringComparison.Ordinal))
            {
                violations.Add(new Violation(culture, row.Key,
                    $"the document shows English '{row.English}' and the library says '{englishText}'"));
            }

            if (translated.TryGetValue(row.Key, out string? translatedText)
                && !string.Equals(row.Translation, translatedText, StringComparison.Ordinal))
            {
                violations.Add(new Violation(culture, row.Key,
                    $"the document shows '{row.Translation}' and the locale says '{translatedText}'"));
            }

            if (summaries.TryGetValue(row.Key, out string? summary)
                && !string.Equals(row.Context, summary, StringComparison.Ordinal))
            {
                violations.Add(new Violation(culture, row.Key,
                    "the context column is not what the key's summary now says"));
            }

            string holds = Holds(englishText);
            if (!string.Equals(row.Holds, holds, StringComparison.Ordinal))
            {
                violations.Add(new Violation(culture, row.Key,
                    $"the document says it holds {row.Holds} and the string takes {holds}"));
            }
        }

        foreach (string key in english.Keys)
        {
            if (!seen.Contains(key))
            {
                violations.Add(new Violation(culture, key, "has no row, so a reviewer never sees it"));
            }
        }

        return violations;
    }

    /// <summary>The <c>Holds</c> cell the generator writes for a string.</summary>
    internal static string Holds(string value)
    {
        HashSet<int> indices = LocaleParity.IndicesIn(value);
        return indices.Count == 0
            ? "—"
            : string.Join(" ", indices.Order().Select(i => "`{" + i.ToString(provider: null) + "}`"));
    }

    /// <summary>U+2423 OPEN BOX: what the document shows where a string starts or ends with a space.</summary>
    internal const char Space = '␣';

    /// <summary>
    /// The cells of one table row, with <c>\|</c> read back as a literal pipe and <c>␣</c> back as
    /// the space it stands for. Splitting on every pipe would tear a cell in half at exactly the
    /// character the generator escapes, and trimming without decoding <c>␣</c> would report every
    /// padded string as drifted — the two halves of the round trip a test is most likely to get
    /// wrong.
    /// </summary>
    private static List<string> Cells(string line)
    {
        List<string> cells = [];
        StringBuilder current = new();

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\' && i + 1 < line.Length && line[i + 1] == '|')
            {
                current.Append('|');
                i++;
                continue;
            }

            if (line[i] == '|')
            {
                cells.Add(current.ToString().Trim().Replace(Space, ' '));
                current.Clear();
                continue;
            }

            current.Append(line[i]);
        }

        cells.Add(current.ToString().Trim().Replace(Space, ' '));

        // A row is "| a | b |", so the split leaves an empty cell at each end.
        if (cells.Count >= 2)
        {
            cells.RemoveAt(cells.Count - 1);
            cells.RemoveAt(0);
        }

        return cells;
    }
}
