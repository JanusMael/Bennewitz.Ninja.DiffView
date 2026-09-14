using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 6: word-level rectangles over modified rows, computed on first render and
/// cached only for rendered rows; the long-line fixture without pieces and its tooltip; the
/// options reaching the model and the strip.
/// </summary>
public sealed class WordDiffTests
{
    private const double Tolerance = 1e-6;

    /// <summary>Twenty unchanged lines, then a sentence with two words changed on each side, then one more line.</summary>
    private static (string Left, string Right) FoxPair(int unchangedLinesBefore = 20)
    {
        string prefix = string.Concat(Enumerable.Range(1, unchangedLinesBefore).Select(i => $"line {i}\n"));
        return (prefix + "The quick brown fox jumps over the lazy dog\nomega\n", prefix + "The quick red fox leaps over the lazy dog\nomega\n");
    }

    [AvaloniaFact]
    public async Task Word_rectangles_cover_exactly_the_piece_columns_in_both_panes_and_the_cache_holds_only_rendered_rows()
    {
        (string left, string right) = FoxPair();
        using CompositeHost host = new(width: 1200, height: 180);
        host.Show();
        await host.LoadAsync(left, right);
        SideBySideDocument document = host.View.Document!;
        WordDiffLookup lookup = host.View.WordDiffLookup!;
        int modifiedRow = document.Rows.ToList().FindIndex(r => r.Kind == DiffLineKind.Modified);
        Assert.Equal(20, modifiedRow);

        // The modified row lies below a 180 px window: nothing has been computed or drawn.
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Equal(0, lookup.Cache.Count);
        Assert.Empty(host.Left.BackgroundRenderer.LastWordRectangles);
        Assert.Empty(host.Right.BackgroundRenderer.LastWordRectangles);

        // Scrolling it into view computes its pieces once, for that row only.
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        host.Left.PaneScrollViewer!.Offset = new Vector(0, modifiedRow * lineHeight);
        CompositeHost.Layout();
        using WriteableBitmap frame = host.Capture();
        Assert.Equal(1, lookup.Cache.Count);
        Assert.True(lookup.Cache.Contains(document, modifiedRow));
        WordDiffPieces pieces = lookup.PiecesFor(modifiedRow);
        Assert.Equal(1, lookup.Cache.Count);

        Color paneBackground = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        Color modifiedRowColour = PresenterHost.Composite(PresenterHost.Token("DiffView.ModifiedBrush"), paneBackground);
        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            AlignedRow row = document.Rows[modifiedRow];
            int lineNumber = (pane.Side == DiffSide.Left ? row.LeftLine : row.RightLine)!.Value + 1;
            IReadOnlyList<PieceRange> sidePieces = pane.Side == DiffSide.Left ? pieces.Left : pieces.Right;
            PieceKind changedKind = pane.Side == DiffSide.Left ? PieceKind.Deleted : PieceKind.Inserted;
            List<PieceRange> expected = sidePieces.Where(p => p.Kind != PieceKind.Unchanged).ToList();
            Assert.Equal(2, expected.Count);
            Assert.All(expected, p => Assert.Equal(changedKind, p.Kind));

            // The pieces concatenate to the line, so their offsets are trustworthy.
            string lineText = pane.Document.GetText(pane.Document.GetLineByNumber(lineNumber));
            Assert.Equal(lineText.Length, sidePieces.Sum(p => p.Length));
            Assert.Equal(pane.Side == DiffSide.Left ? ["brown", "jumps"] : ["red", "leaps"], expected.Select(p => lineText.Substring(p.Start, p.Length)));

            // Each drawn rectangle spans exactly its piece's columns, over the full row height.
            VisualLine visual = pane.TextArea.TextView.GetVisualLine(lineNumber) ?? throw new InvalidOperationException("the modified line is not visible");
            TextLine textLine = visual.TextLines[0];
            Vector scroll = pane.TextArea.TextView.ScrollOffset;
            List<WordRectangle> drawn = pane.BackgroundRenderer.LastWordRectangles.Where(r => r.LineNumber == lineNumber).ToList();
            Assert.Equal(expected, drawn.Select(r => r.Piece));
            foreach (WordRectangle rectangle in drawn)
            {
                double x1 = visual.GetTextLineVisualXPosition(textLine, visual.GetVisualColumn(rectangle.Piece.Start)) - scroll.X;
                double x2 = visual.GetTextLineVisualXPosition(textLine, visual.GetVisualColumn(rectangle.Piece.End)) - scroll.X;
                Assert.Equal(x1, rectangle.Rect.Left, Tolerance);
                Assert.Equal(x2, rectangle.Rect.Right, Tolerance);
                Assert.Equal(lineHeight, rectangle.Rect.Height, Tolerance);
                Assert.Equal(visual.VisualTop - scroll.Y, rectangle.Rect.Top, Tolerance);
            }

            // Pixels: the word brush inside the first rectangle, the plain row tint just outside it.
            string wordKey = pane.Side == DiffSide.Left ? "DiffView.WordDeletedBrush" : "DiffView.WordInsertedBrush";
            Color wordColour = PresenterHost.Composite(PresenterHost.Token(wordKey), modifiedRowColour);
            Point origin = pane.TextArea.TextView.TranslatePoint(new Point(0, 0), host.Window) ?? throw new InvalidOperationException("text view not in tree");
            Rect first = drawn[0].Rect;
            PixelRect inside = PixelProbe.Inside(origin.X + first.Left, origin.Y + first.Top, origin.X + first.Right, origin.Y + first.Top + 3, inset: 0);
            PixelRect outside = PixelProbe.Inside(origin.X + first.Right + 2, origin.Y + first.Top, origin.X + first.Right + 12, origin.Y + first.Top + 3, inset: 0);
            Assert.True(PixelProbe.Count(frame, inside, c => PresenterHost.Near(c, wordColour)) > 0.5 * inside.Width * inside.Height, $"{pane.Side}: the word rectangle should carry {wordColour}");
            Assert.Equal(0, PixelProbe.Count(frame, outside, c => PresenterHost.Near(c, wordColour)));
        }
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task The_one_megabyte_single_line_renders_without_pieces_and_the_marker_tooltip_says_so()
    {
        const int size = 1_000_000;
        StringBuilder builder = new(size + 16);
        while (builder.Length < size)
        {
            builder.Append("lorem ipsum dolor ");
        }

        builder.Length = size;
        string left = builder.ToString();
        string right = left[..^5] + "amet!";
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        Stopwatch stopwatch = Stopwatch.StartNew();
        await host.LoadAsync(left, right);
        using WriteableBitmap frame = host.Capture();
        TestContext.Current.TestOutputHelper?.WriteLine($"1 MB single line: build, prime and first frame in {stopwatch.ElapsedMilliseconds} ms.");

        Assert.Equal(DiffViewState.Degraded, host.View.State);
        Assert.Contains(host.View.Warnings, w => w.Code == DiffWarningCode.LongLinesSkipped);
        WordDiffLookup lookup = host.View.WordDiffLookup!;
        Assert.True(lookup.IsLongLine(0));
        Assert.Same(WordDiffPieces.None, lookup.PiecesFor(0));
        Assert.Equal(1, lookup.Cache.LongLinesSkipped);
        Assert.Empty(host.Left.BackgroundRenderer.LastWordRectangles);
        Assert.Empty(host.Right.BackgroundRenderer.LastWordRectangles);
        Assert.Contains(host.Left.BackgroundRenderer.LastDrawn, d => d.LineNumber == 1 && d.Kind == DiffLineKind.Modified);

        // The marker's tooltip explains it, following the pointer over the row and leaving with it.
        ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
        string expected = margin.TooltipFor(1) ?? throw new InvalidOperationException("no tooltip for the long line");
        Assert.Contains("skipped", expected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20,000", expected, StringComparison.Ordinal);
        Point over = margin.TranslatePoint(new Point(margin.Bounds.Width / 2, 6), host.Window) ?? throw new InvalidOperationException("margin not in tree");
        host.Window.MouseMove(over);
        CompositeHost.Layout();
        Assert.Equal(expected, ToolTip.GetTip(margin));
        host.Window.MouseMove(new Point(host.Window.Width - 5, host.Window.Height - 5));
        CompositeHost.Layout();
        Assert.Null(ToolTip.GetTip(margin));
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task Toggling_IgnoreWhitespace_removes_whitespace_only_diffs_and_the_strip_reflects_the_option()
    {
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync("alpha\n  beta\ngamma\n", "alpha\nbeta  \ngamma\n");
        Assert.Equal(1, host.View.ChangeCount);
        Assert.Equal(DiffLineKind.Modified, host.View.Document!.Rows[1].Kind);
        Assert.Null(host.View.StatusStrip!.OptionsText);
        Assert.Equal("1 change", host.View.StatusStrip.ChangesText);

        host.View.IgnoreWhitespace = true;
        await host.WaitForBuildAsync();
        Assert.Equal(0, host.View.ChangeCount);
        Assert.True(host.View.Diagnostics!.Identical);
        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.Equal("no changes", host.View.StatusStrip.ChangesText);
        Assert.Equal("ignore whitespace", host.View.StatusStrip.OptionsText);
        Assert.True(host.View.WordDiffLookup!.Cache.Options.IgnoreWhitespace);

        host.View.IgnoreWhitespace = false;
        await host.WaitForBuildAsync();
        Assert.Equal(1, host.View.ChangeCount);
        Assert.Null(host.View.StatusStrip.OptionsText);
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task Word_diff_off_draws_nothing_and_character_mode_reaches_the_cache()
    {
        (string left, string right) = FoxPair(unchangedLinesBefore: 0);
        using CompositeHost host = new(width: 1200, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.NotEmpty(host.Left.BackgroundRenderer.LastWordRectangles);

        host.View.WordDiff = WordDiffMode.Off;
        await host.WaitForBuildAsync();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Equal(WordDiffMode.Off, host.View.WordDiffLookup!.Cache.Options.WordDiff);
        Assert.Same(WordDiffPieces.None, host.View.WordDiffLookup.PiecesFor(0));
        Assert.Empty(host.Left.BackgroundRenderer.LastWordRectangles);
        Assert.Equal("no word diff", host.View.StatusStrip!.OptionsText);

        host.View.WordDiff = WordDiffMode.Character;
        await host.WaitForBuildAsync();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Equal(WordDiffMode.Character, host.View.WordDiffLookup!.Cache.Options.WordDiff);
        Assert.NotEmpty(host.Right.BackgroundRenderer.LastWordRectangles);
        Assert.Equal("character diff", host.View.StatusStrip.OptionsText);
    }
}
