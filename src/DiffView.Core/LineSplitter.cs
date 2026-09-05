namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>
/// Splits text into lines on <c>\r\n</c>, <c>\r</c> and <c>\n</c> — the three terminators
/// DiffPlex's <c>LineChunker</c> and AvaloniaEdit's <c>NewLineFinder</c> both recognise — so
/// the model, the search and the editor agree on line numbers. The empty text is one empty
/// line; a text ending in a terminator has a final empty line. No normalisation is applied.
/// </summary>
public static class LineSplitter
{
    /// <summary>The lines of <paramref name="text"/>, without their terminators.</summary>
    public static string[] Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<string> lines = new(capacity: Math.Max(16, text.Length / 32));
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                lines.Add(text[start..i]);
                start = i + 1;
            }
            else if (c == '\r')
            {
                lines.Add(text[start..i]);
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }
        }

        lines.Add(text[start..]);
        return [.. lines];
    }
}
