using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Reads the colours a built line carries. Plain text is one foreground for the whole line; a
/// grammar gives its tokens several, which is what "the pane is colourised" means here — and,
/// unlike a pixel, it is available the moment the line is rebuilt.
/// </summary>
internal static class SyntaxProbe
{
    /// <summary>The distinct foregrounds the runs of <paramref name="lineNumber"/> carry; empty when it is not built.</summary>
    public static HashSet<Color> Colours(DiffPanePresenter pane, int lineNumber)
    {
        HashSet<Color> colours = [];
        TextView view = pane.TextArea.TextView;
        if (!view.VisualLinesValid || view.GetVisualLine(lineNumber) is not { } visual)
        {
            return colours;
        }

        foreach (TextLine textLine in visual.TextLines)
        {
            foreach (TextRun run in textLine.TextRuns)
            {
                if (run.Properties?.ForegroundBrush is ISolidColorBrush brush)
                {
                    colours.Add(brush.Color);
                }
            }
        }

        return colours;
    }

    /// <summary>How many distinct foregrounds <paramref name="lineNumber"/> carries; 0 when it is not built.</summary>
    public static int Foregrounds(DiffPanePresenter pane, int lineNumber)
    {
        return Colours(pane, lineNumber).Count;
    }

    /// <summary>
    /// The foreground of the first run of <paramref name="lineNumber"/> — the first token's own
    /// colour, which a grammar sets and the pane's palette does not, so it says which syntax
    /// theme is in force. <c>null</c> when the line is not built.
    /// </summary>
    public static Color? FirstColour(DiffPanePresenter pane, int lineNumber)
    {
        TextView view = pane.TextArea.TextView;
        if (!view.VisualLinesValid || view.GetVisualLine(lineNumber) is not { } visual)
        {
            return null;
        }

        foreach (TextLine textLine in visual.TextLines)
        {
            foreach (TextRun run in textLine.TextRuns)
            {
                if (run.Properties?.ForegroundBrush is ISolidColorBrush brush)
                {
                    return brush.Color;
                }
            }
        }

        return null;
    }

    /// <summary>Whether a grammar has coloured <paramref name="lineNumber"/> — more than one foreground on it.</summary>
    public static bool IsColoured(DiffPanePresenter pane, int lineNumber)
    {
        return Foregrounds(pane, lineNumber) > 1;
    }
}
