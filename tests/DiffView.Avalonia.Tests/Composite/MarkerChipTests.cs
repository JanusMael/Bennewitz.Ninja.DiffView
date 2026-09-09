using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00005 §Phase 2, the chip under each marker glyph: one per run of same-kind rows, so a lone
/// changed line is a badge and a block is one band, and the glyph centred in whichever it gets.
/// </summary>
public sealed class MarkerChipTests
{
    /// <summary>Five deleted lines in a row, then a lone modified one further down.</summary>
    private const string Left = """
        alpha
        one
        two
        three
        four
        five
        beta
        gamma
        SEVEN
        delta
        """;

    private const string Right = """
        alpha
        beta
        gamma
        seven
        delta
        """;

    [AvaloniaFact]
    public async Task A_run_of_same_kind_rows_shares_one_chip()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(Left, Right);
        host.Capture().Dispose();

        ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;

        // Five deleted lines and one modified line: two runs, so two chips — not six.
        Assert.Equal(
            [DiffLineKind.Deleted, DiffLineKind.Modified],
            margin.LastChips.Select(c => c.Kind));

        double rowHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        (Rect deleted, _) = margin.LastChips[0];
        (Rect modified, _) = margin.LastChips[1];

        // The run's chip spans its five rows, less the inset at each end; the lone row's spans one.
        Assert.Equal((5 * rowHeight) - 4, deleted.Height, 0.5);
        Assert.Equal(rowHeight - 4, modified.Height, 0.5);
    }

    [AvaloniaFact]
    public async Task A_lone_changed_row_gets_a_badge_of_the_same_shape()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");
        host.Capture().Dispose();

        (Rect chip, DiffLineKind kind) = Assert.Single(host.Left.ChangeMarkerMargin.LastChips);
        Assert.Equal(DiffLineKind.Modified, kind);
        Assert.Equal(host.Left.TextArea.TextView.DefaultLineHeight - 4, chip.Height, 0.5);
    }

    [AvaloniaFact]
    public async Task The_chips_are_exactly_the_runs_the_model_describes()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 600);
        host.Show();
        await host.LoadAsync(left, right);
        host.Capture().Dispose();

        foreach (DiffPanePresenter pane in (DiffPanePresenter[])[host.Left, host.Right])
        {
            ChangeMarkerMargin margin = pane.ChangeMarkerMargin;
            double rowHeight = pane.TextArea.TextView.DefaultLineHeight;

            // The runs, derived from the kinds the margin reported rather than from the chips.
            List<(DiffLineKind Kind, int Rows)> runs = [];
            DiffLineKind? previous = null;
            foreach ((int _, DiffLineKind kind) in margin.LastRendered)
            {
                if (ChangeMarkerMargin.GlyphFor(kind) is null)
                {
                    previous = null;
                    continue;
                }

                if (previous == kind)
                {
                    runs[^1] = (kind, runs[^1].Rows + 1);
                }
                else
                {
                    runs.Add((kind, 1));
                }

                previous = kind;
            }

            Assert.NotEmpty(runs);
            Assert.Equal(runs.Select(r => r.Kind), margin.LastChips.Select(c => c.Kind));
            for (int i = 0; i < runs.Count; i++)
            {
                Assert.Equal((runs[i].Rows * rowHeight) - 4, margin.LastChips[i].Bounds.Height, 0.5);
            }
        }
    }

    [AvaloniaFact]
    public async Task The_glyph_is_centred_in_its_chip()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(Left, Right);

        using WriteableBitmap frame = host.Capture();
        ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
        Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;
        Assert.NotEmpty(margin.LastChips);

        foreach ((Rect chip, DiffLineKind kind) in margin.LastChips)
        {
            // The glyph's ink, weighed either side of the chip's centre line. The bug this test
            // exists for put the chip a pixel off the glyph, which no reported rectangle can show.
            Color marker = PresenterHost.Token(kind == DiffLineKind.Deleted
                ? "DiffView.MarkerDeletedBrush"
                : "DiffView.MarkerModifiedBrush");
            int left = Ink(frame, origin, new Rect(chip.Left, chip.Top, chip.Width / 2, chip.Height), marker);
            int right = Ink(frame, origin, new Rect(chip.Center.X, chip.Top, chip.Width / 2, chip.Height), marker);
            Assert.True(left > 0 && right > 0, $"{kind}: glyph ink was {left} and {right}");
            Assert.True(Math.Abs(left - right) <= Math.Max(left, right) / 3, $"{kind}: glyph ink is lopsided, {left} against {right}");
        }
    }

    [AvaloniaFact]
    public async Task The_chip_stops_where_the_modified_since_load_bar_begins()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\nTWO\nthree\n", "one\ntwo\nthree\n");

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = host.Left.Document.GetLineByNumber(2).Offset;
        CompositeHost.Layout();
        host.Window.KeyTextInput("Z");
        CompositeHost.Layout();

        using WriteableBitmap frame = host.Capture();
        ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
        (Rect chip, _) = Assert.Single(margin.LastChips);
        Assert.Equal([2], margin.LastModified);

        // Symmetric, and ending exactly where the bar starts: the two are adjacent, never stacked.
        Assert.Equal(margin.Bounds.Width - chip.Right, chip.Left, 0.5);
        Assert.Equal(margin.Bounds.Width - 2, chip.Right, 0.5);

        // Both are painted on that line.
        Point origin = margin.TranslatePoint(new Point(0, 0), host.Window)!.Value;
        double rowHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.True(Ink(frame, origin, new Rect(chip.Right, chip.Top, 2, rowHeight), PresenterHost.Token("DiffView.ModifiedSinceLoadBrush")) > 0, "the bar should still paint beside the chip");
        Assert.True(Ink(frame, origin, chip, PresenterHost.Token("DiffView.MarkerChipModifiedBrush")) > 0, "the chip should be painted");
    }

    [AvaloniaFact]
    public async Task The_chip_costs_the_margin_no_width()
    {
        using CompositeHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("one\ntwo\nthree\n", "one\ntwo\nthree\n");
        host.Capture().Dispose();
        double unchanged = host.Left.ChangeMarkerMargin.Bounds.Width;

        await host.LoadAsync(Left, Right);
        host.Capture().Dispose();

        Assert.NotEmpty(host.Left.ChangeMarkerMargin.LastChips);
        Assert.Equal(unchanged, host.Left.ChangeMarkerMargin.Bounds.Width, 0.5);
        Assert.Equal(16, host.Left.ChangeMarkerMargin.Bounds.Width, 0.5);
    }

    private static int Ink(WriteableBitmap frame, Point origin, Rect area, Color colour)
    {
        PixelRect probe = PixelProbe.Inside(origin.X + area.Left, origin.Y + area.Top, origin.X + area.Right, origin.Y + area.Bottom, inset: 0);
        return PixelProbe.Count(frame, probe, c => PresenterHost.Near(c, colour));
    }
}
