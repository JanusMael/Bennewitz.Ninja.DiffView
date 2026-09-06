using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>One match rectangle the search renderer drew, in text-view coordinates; read by tests.</summary>
internal readonly record struct SearchRectangle(int LineNumber, FindMatch Match, bool IsCurrent, Rect Rect);

/// <summary>
/// Highlights this pane's find matches: one rectangle per match over the row's text band, the
/// current match in its own brush. Draws on <see cref="KnownLayer.Selection"/> — above the row
/// fills, below the text — and is added to the text view before
/// <see cref="DiffSelectionRenderer"/>, which shares that layer, so a selected match still reads
/// as selected. Only the visible lines are touched: the pane's matches are ordered by line, so
/// each visual line costs one binary search.
/// </summary>
internal sealed class SearchMatchRenderer : GuardedBackgroundRenderer
{
    private readonly List<SearchRectangle> _lastRectangles = [];

    public SearchMatchRenderer(DiffPanePresenter owner)
        : base(owner, KnownLayer.Selection, nameof(SearchMatchRenderer))
    {
    }

    /// <summary>The match rectangles of the last frame, in line and column order.</summary>
    public IReadOnlyList<SearchRectangle> LastRectangles => _lastRectangles;

    /// <summary>
    /// The index of the first match on <paramref name="line"/> (0-based), or where one would be
    /// inserted. The pane's matches are ordered by line, then column.
    /// </summary>
    public static int FirstIndexOfLine(IReadOnlyList<FindMatch> matches, int line)
    {
        ArgumentNullException.ThrowIfNull(matches);
        int low = 0;
        int high = matches.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (matches[middle].Line < line)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    protected override void DrawCore(TextView textView, DrawingContext drawingContext)
    {
        _lastRectangles.Clear();
        IReadOnlyList<FindMatch> matches = Owner.SearchMatches;
        if (matches.Count == 0 || textView.Bounds.Width <= 0)
        {
            return;
        }

        double lineHeight = textView.DefaultLineHeight;
        Vector scroll = textView.ScrollOffset;
        DiffBrushes palette = Owner.Palette;
        FindMatch? current = Owner.CurrentSearchMatch;

        foreach (VisualLine line in textView.VisualLines)
        {
            // The model is 0-based and the editor 1-based; this is the only conversion here.
            int modelLine = line.FirstDocumentLine.LineNumber - 1;
            int index = FirstIndexOfLine(matches, modelLine);
            if (index >= matches.Count || matches[index].Line != modelLine)
            {
                continue;
            }

            double rowTop = line.VisualTop - scroll.Y + (DiffLineBackgroundRenderer.PaddingOf(line).Above * lineHeight);
            TextLine textLine = line.TextLines[0];
            int lineLength = line.FirstDocumentLine.Length;
            for (int i = index; i < matches.Count && matches[i].Line == modelLine; i++)
            {
                FindMatch match = matches[i];
                // The document may have changed since the snapshot the search ran over.
                int start = Math.Min(match.Column, lineLength);
                int end = Math.Min(match.Column + match.Length, lineLength);
                if (end <= start)
                {
                    continue;
                }

                double left = line.GetTextLineVisualXPosition(textLine, line.GetVisualColumn(start)) - scroll.X;
                double right = line.GetTextLineVisualXPosition(textLine, line.GetVisualColumn(end)) - scroll.X;
                bool isCurrent = current == match;
                Rect rect = new(left, rowTop, Math.Max(0, right - left), lineHeight);
                drawingContext.FillRectangle(palette[isCurrent ? DiffBrush.FindCurrentMatch : DiffBrush.FindMatch], rect);
                _lastRectangles.Add(new SearchRectangle(line.FirstDocumentLine.LineNumber, match, isCurrent, rect));
            }
        }
    }
}
