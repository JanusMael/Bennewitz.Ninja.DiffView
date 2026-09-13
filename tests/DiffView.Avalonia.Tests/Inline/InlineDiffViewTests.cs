using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Inline;

/// <summary>
/// Plan 00001 §Phase 11: the unified view over the same fixtures as the side-by-side — the text
/// it composes, the change counts, the kinds and numbers it draws, and the failure behaviour.
/// </summary>
public sealed class InlineDiffViewTests
{
    [AvaloniaTheory]
    [MemberData(nameof(ThemeTargets.All), MemberType = typeof(ThemeTargets))]
    public async Task Renders_under_every_theme_target_with_no_binding_or_resource_warnings(string theme, string variant)
    {
        using ThemeSwap swap = ThemeSwap.To(theme, variant);
        TestLogSink.Instance.Clear();
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        using WriteableBitmap frame = host.Capture();

        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.NotNull(host.View.LeftHeader);
        Assert.NotNull(host.View.StatusStrip);

        Color headerBackground = PresenterHost.Token("DiffView.HeaderBackgroundBrush");
        Point origin = host.View.LeftHeader!.TranslatePoint(new Point(0, 0), host.Window) ?? throw new InvalidOperationException("header not in tree");
        Color sampled = PixelProbe.At(frame, (int)(origin.X + host.View.LeftHeader.Bounds.Width - 3), (int)origin.Y + 2);
        Assert.True(PresenterHost.Near(sampled, headerBackground), $"{theme} {variant}: expected header {headerBackground}, got {sampled}");

        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    [AvaloniaFact]
    public async Task The_pane_holds_the_two_sides_unified_and_the_change_counts_match_the_side_by_side_view()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost inline = new();
        using CompositeHost sideBySide = new();
        inline.Show();
        sideBySide.Show();
        await inline.LoadAsync(left, right);
        await sideBySide.LoadAsync(left, right);

        // The same builder, so the same model: the counts a user reads are the same numbers.
        Assert.Equal(sideBySide.View.ChangeCount, inline.View.ChangeCount);
        Assert.Equal(sideBySide.View.Diagnostics!.RowCount, inline.View.Diagnostics!.RowCount);
        Assert.Equal(sideBySide.View.StatusStrip!.CountsText, inline.View.StatusStrip!.CountsText);
        Assert.Equal(sideBySide.View.StatusStrip.ChangesText, inline.View.StatusStrip.ChangesText);

        // The composed text is every line of the unified table, taken from the side it names.
        InlineDocument table = inline.View.Inline!;
        StringBuilder expected = new();
        foreach (InlineLine line in table.Lines)
        {
            string[] source = (line.Side == DiffSide.Left ? left : right).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            expected.Append(source[line.SourceLine]).Append('\n');
        }

        expected.Length--;
        Assert.Equal(expected.ToString(), inline.View.PaneDocument.Text);
        Assert.Equal(table.Lines.Count, inline.View.PaneDocument.LineCount);
        Assert.Same(inline.View.PaneDocument, inline.Pane.Document);
        Assert.Same(table, inline.Pane.InlineDocument);
        Assert.True(inline.Pane.IsUnified);
        Assert.True(inline.Pane.IsReadOnly);
    }

    [AvaloniaFact]
    public async Task Every_visible_row_is_filled_by_its_own_kind_and_none_of_them_is_padded()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 900);
        host.Show();
        await host.LoadAsync(left, right);
        using WriteableBitmap frame = host.Capture();

        IReadOnlyList<DrawnLine> drawn = host.Pane.BackgroundRenderer.LastDrawn;
        Assert.NotEmpty(drawn);
        InlineDocument table = host.View.Inline!;
        foreach (DrawnLine line in drawn)
        {
            Assert.Equal(table.Lines[line.LineNumber - 1].Kind, line.Kind);
            // A unified document holds every line it shows: there is nothing to pad.
            Assert.True(line.Padding.IsEmpty, $"line {line.LineNumber} carries padding");
        }

        Assert.Equal(0, host.Pane.PrimedLineCount);
        Assert.Contains(drawn, l => l.Kind == DiffLineKind.Deleted);
        Assert.Contains(drawn, l => l.Kind == DiffLineKind.Inserted);
        Assert.Contains(drawn, l => l.Kind == DiffLineKind.Unchanged);

        // The markers say the same thing as the fills, glyph for glyph.
        foreach ((int number, DiffLineKind kind) in host.Pane.ChangeMarkerMargin.LastRendered)
        {
            Assert.Equal(table.Lines[number - 1].Kind, kind);
        }
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task The_gutter_numbers_each_line_on_its_own_side_and_leaves_the_other_column_empty()
    {
        // A context line is in both files and carries both numbers; a removed or added line is
        // in one file only, and the column of the other stays empty.
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("keep\ngone\ntail\n", "keep\nadded\ntail\n");
        using WriteableBitmap frame = host.Capture();

        InlineDocument table = host.View.Inline!;
        IReadOnlyList<(int? Left, int? Right)> numbers = host.Pane.LineNumberMargin.LastSourceNumbers;
        Assert.Equal(table.Lines.Count, numbers.Count);
        for (int i = 0; i < numbers.Count; i++)
        {
            InlineLine line = table.Lines[i];
            (int? left, int? right) = numbers[i];
            int? other = host.Pane.Metadata.OtherLine(i + 1);
            if (line.Side == DiffSide.Left)
            {
                Assert.Equal(line.SourceLine + 1, left);
                Assert.Equal(line.Kind == DiffLineKind.Unchanged ? other : null, right);
            }
            else
            {
                Assert.Equal(line.SourceLine + 1, right);
                Assert.Equal(line.Kind == DiffLineKind.Unchanged ? other : null, left);
            }

            Assert.Equal(line.Kind == DiffLineKind.Unchanged, left is not null && right is not null);
        }

        // The tooltip names the side, because the line above may be the other one.
        int deleted = table.Lines.ToList().FindIndex(l => l.Kind == DiffLineKind.Deleted || l.Kind == DiffLineKind.Modified);
        Assert.NotEqual(-1, deleted);
        string? tooltip = host.Pane.LineNumberMargin.TooltipFor(deleted + 1);
        Assert.Contains("on the left", tooltip, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_modified_row_shows_both_of_its_lines_with_the_word_pieces_of_the_side_each_belongs_to()
    {
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("alpha beta gamma\n", "alpha DELTA gamma\n");
        using WriteableBitmap frame = host.Capture();

        InlineDocument table = host.View.Inline!;
        List<InlineLine> modified = [.. table.Lines.Where(l => l.Kind == DiffLineKind.Modified)];
        Assert.Equal(2, modified.Count);
        Assert.Equal(DiffSide.Left, modified[0].Side);
        Assert.Equal(DiffSide.Right, modified[1].Side);

        IReadOnlyList<WordRectangle> pieces = host.Pane.BackgroundRenderer.LastWordRectangles;
        Assert.NotEmpty(pieces);

        // Each half is highlighted over its own word: "beta" on the removed line, "DELTA" on the
        // added one, so the rectangles' columns come from different sides of the same row.
        foreach (WordRectangle piece in pieces)
        {
            DocumentLine line = host.View.PaneDocument.GetLineByNumber(piece.LineNumber);
            string text = host.View.PaneDocument.GetText(line.Offset, line.Length);
            string covered = text[piece.Piece.Start..Math.Min(piece.Piece.End, text.Length)];
            Assert.Equal(text.Contains("DELTA", StringComparison.Ordinal) ? "DELTA" : "beta", covered);
        }

        Assert.Contains(pieces, p => p.LineNumber == 1);
        Assert.Contains(pieces, p => p.LineNumber == 2);
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task F7_walks_the_blocks_and_the_border_covers_the_block_own_unified_lines()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        Assert.Equal(-1, host.View.CurrentChangeIndex);
        Assert.True(host.View.ChangeCount > 1);
        // F7, Shift+F7, Ctrl+F, F3, Shift+F3, Escape — one fewer than the side-by-side view,
        // which also binds F6 to switch panes.
        Assert.Equal(6, host.View.KeyBindings.Count);

        host.Pane.TextArea.Focus();
        InlineHost.Layout();
        Press(host, Key.F7);
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Assert.Equal($"change 1 of {host.View.ChangeCount}", host.View.StatusStrip!.ChangesText);

        // A block holding a modified row, where the unified lines and the model's rows have
        // parted: a modified pair prints as two lines, so a border drawn over the rows would be
        // both too short and in the wrong place.
        ChangeBlock block = host.View.Document!.Blocks.Last(b => b.ModifiedCount > 0);
        host.View.CurrentChangeIndex = block.Index;
        InlineHost.Layout();
        LineRange lines = host.View.Inline!.LinesOfBlock(block.Index);
        Assert.Same(block, host.Pane.CurrentBlock);
        Assert.True(lines.Count > block.RowCount, "a modified pair takes two unified lines");
        Assert.NotEqual(block.FirstRow, lines.Start);

        double lineHeight = host.Pane.TextArea.TextView.DefaultLineHeight;
        using WriteableBitmap frame = host.Capture();
        Rect border = host.Pane.BackgroundRenderer.LastCurrentBlockBorder
                      ?? throw new InvalidOperationException("the current block was not drawn");
        double scroll = host.Pane.TextArea.TextView.ScrollOffset.Y;
        Assert.Equal((lines.Start * lineHeight) - scroll, border.Top, 1);
        Assert.Equal(lines.Count * lineHeight, border.Height, 1);

        host.View.FirstChange();
        Press(host, Key.F7, RawInputModifiers.Shift);
        Assert.Equal(0, host.View.CurrentChangeIndex);
        Assert.Equal("No previous change", host.View.StatusStrip.TransientText);
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task A_binary_side_fails_the_build_and_the_banner_offers_a_retry()
    {
        using InlineHost host = new();
        host.Show();
        List<DiffViewState> states = [];
        host.View.PropertyChanged += (_, e) =>
        {
            if (e.Property == InlineDiffView.StateProperty)
            {
                states.Add(host.View.State);
            }
        };
        int failed = 0;
        host.View.BuildFailed += (_, _) => failed++;

        await host.LoadAsync(new PaneSource("text\n"), PaneSource.FromBytes(BinaryBytes(), "image.png"));

        Assert.Equal(1, failed);
        Assert.Equal(DiffViewState.Failed, host.View.State);
        Assert.Equal([DiffViewState.Building, DiffViewState.Failed], states.Distinct());
        Assert.Equal(DiffBannerKind.Error, host.View.BannerKind);
        Assert.Equal("Retry", host.View.BannerActionText);
        Assert.Null(host.View.Document);
        Assert.Null(host.View.Inline);
        Assert.Equal(string.Empty, host.View.PaneDocument.Text);
        Assert.Equal("Failed", host.View.StatusStrip!.StateText);
        Assert.Equal(StatusKind.Failure, host.View.StatusStrip.TransientKind);
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task A_throwing_decorator_degrades_the_control_and_the_text_still_renders()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.Equal(DiffViewState.Ready, host.View.State);

        List<RenderFaultEventArgs> faults = [];
        host.View.RenderFault += (_, e) => faults.Add(e);
        host.Pane.PaddingSourceForTesting = _ => throw new InvalidOperationException("bad padding");
        host.Pane.TextArea.TextView.Redraw();
        InlineHost.Layout();
        InlineHost.Layout();

        Assert.Single(faults);
        Assert.Equal(DiffViewState.Degraded, host.View.State);
        Assert.Equal("Degraded", host.View.StatusStrip!.StateText);
        Assert.True(host.Pane.IsDegraded);
        // The generator disabled itself; the text layer is untouched.
        Assert.NotEmpty(host.Pane.BackgroundRenderer.LastDrawn);
        Assert.Equal(host.View.Inline!.Lines.Count, host.View.PaneDocument.LineCount);

        // The log names the pane it happened on, which is neither side.
        Assert.Contains(host.Logs.Records, r => r.Category == DiffViewLogCategories.Render && r.Message.Contains("unified", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task An_option_change_rebuilds_the_unified_text_and_keeps_the_control_ready()
    {
        // Two lines that differ only in leading whitespace: ignoring it turns a modified row into
        // a context row, so the unified text loses a line.
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync("  same\nother\n", "same\nother\n");

        int before = host.View.PaneDocument.LineCount;
        Assert.Equal(1, host.View.ChangeCount);

        host.View.IgnoreWhitespace = true;
        await host.WaitForBuildAsync();

        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.Equal(0, host.View.ChangeCount);
        Assert.Equal(before - 1, host.View.PaneDocument.LineCount);
        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.False(host.View.IsStale);
        // The context row shows the left side's text, whitespace and all.
        Assert.StartsWith("  same", host.View.PaneDocument.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task The_caret_lane_names_the_line_on_its_own_side_not_the_unified_one()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new(width: 900, height: 400);
        host.Show();
        await host.LoadAsync(left, right);

        host.Pane.TextArea.Focus();
        InlineHost.Layout();
        Assert.True(host.View.IsPaneFocused);

        // A line well past the first block, where the unified numbering and the side's have parted.
        InlineDocument table = host.View.Inline!;
        int unified = table.Lines.ToList().FindLastIndex(l => l.Kind == DiffLineKind.Unchanged);
        Assert.True(unified > 0);
        host.Pane.TextArea.Caret.Line = unified + 1;
        InlineHost.Layout();

        Assert.Equal(table.Lines[unified].SourceLine + 1, host.View.CaretLine);
        Assert.NotEqual(unified + 1, host.View.CaretLine);
        Assert.Contains($"Ln {host.View.CaretLine}", host.View.StatusStrip!.CaretText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Every_decorator_the_unified_view_builds_carries_an_automation_name()
    {
        (string left, string right) = InlineHost.SmallFixture();
        using InlineHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        host.View.OpenFind();
        InlineHost.Layout();

        List<Control> decorators =
        [
            host.Pane,
            host.Pane.LineNumberMargin,
            host.Pane.ChangeMarkerMargin,
            host.View.StatusStrip ?? throw new InvalidOperationException("no status strip"),
            host.View.FindBar ?? throw new InvalidOperationException("no find bar"),
        ];

        foreach (Control decorator in decorators)
        {
            string? name = AutomationProperties.GetName(decorator);
            Assert.False(string.IsNullOrWhiteSpace(name), $"{decorator.GetType().Name} carries no automation name");
        }
    }

    private static byte[] BinaryBytes()
    {
        List<byte> bytes = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        bytes.AddRange(Encoding.ASCII.GetBytes("IHDR and then some text so it is not all NULs\n"));
        return [.. bytes];
    }

    private static void Press(InlineHost host, Key key, RawInputModifiers raw = RawInputModifiers.None)
    {
        host.Window.KeyPress(key, raw, PhysicalKeyOf(key), null);
        host.Window.KeyRelease(key, raw, PhysicalKeyOf(key), null);
        InlineHost.Layout();
    }

    private static PhysicalKey PhysicalKeyOf(Key key)
    {
        return key switch
        {
            Key.F3 => PhysicalKey.F3,
            Key.F7 => PhysicalKey.F7,
            Key.Escape => PhysicalKey.Escape,
            Key.F => PhysicalKey.F,
            _ => PhysicalKey.None,
        };
    }
}
