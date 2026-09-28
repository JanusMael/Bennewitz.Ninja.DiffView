using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 10, the view options: whitespace and line-ending glyphs, the tab width, the
/// pane font — each reaching both panes, and none of them costing the row alignment the padding
/// buys. The mixed-line-ending notice is here too: the builder raises it, and the strip carries
/// it.
/// </summary>
public sealed class ViewOptionsTests
{
    /// <summary>A pair whose first line is indented with a tab and a run of spaces.</summary>
    private const string TabbedLeft = "\tone\t  two\nplain\n";
    private const string TabbedRight = "\tone\t  three\nplain\n";

    [AvaloniaFact]
    public async Task Whitespace_and_line_ending_glyphs_reach_both_panes_and_put_more_ink_on_the_page()
    {
        using CompositeHost host = new(width: 600, height: 200);
        host.Show();
        await host.LoadAsync(TabbedLeft, TabbedRight);

        Assert.False(host.Left.Options.ShowSpaces);
        Assert.False(host.Left.Options.ShowTabs);
        Assert.False(host.Left.Options.ShowEndOfLine);
        int plain = Ink(host);

        host.View.ShowWhitespace = true;
        host.View.ShowLineEndings = true;
        CompositeHost.Layout();

        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            Assert.True(pane.ShowWhitespace);
            Assert.True(pane.ShowLineEndings);
            Assert.True(pane.Options.ShowSpaces);
            Assert.True(pane.Options.ShowTabs);
            Assert.True(pane.Options.ShowEndOfLine);
        }

        Assert.True(Ink(host) > plain, "the glyphs should paint more than the text alone");

        host.View.ShowWhitespace = false;
        host.View.ShowLineEndings = false;
        CompositeHost.Layout();
        Assert.False(host.Right.Options.ShowSpaces);
        Assert.Equal(plain, Ink(host));
    }

    [AvaloniaFact]
    public async Task A_tab_width_change_moves_text_sideways_and_leaves_the_rows_and_the_extents_alone()
    {
        using CompositeHost host = new(width: 600, height: 200);
        host.Show();
        await host.LoadAsync(TabbedLeft, TabbedRight);
        Assert.Equal(4, host.View.TabWidth);

        double narrow = FirstTextX(host.Left);
        double rowHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        Assert.True(narrow > 0);

        host.View.TabWidth = 8;
        CompositeHost.Layout();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Equal(8, host.Left.TabWidth);
        Assert.Equal(8, host.Left.Options.IndentationSize);
        Assert.True(FirstTextX(host.Left) > narrow, "a wider tab should push the first text column right");
        Assert.Equal(rowHeight, host.Left.TextArea.TextView.DefaultLineHeight);
        AssertExtentsEqual(host);

        // Below one is not a width at all; the property floors it rather than throwing.
        host.View.TabWidth = 0;
        CompositeHost.Layout();
        Assert.Equal(1, host.View.TabWidth);
        Assert.Equal(1, host.Left.Options.IndentationSize);
    }

    [AvaloniaFact]
    public async Task A_font_size_change_re_primes_both_panes_and_leaves_their_extents_equal()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        AssertExtentsEqual(host);

        double before = host.Left.TextArea.TextView.DefaultLineHeight;
        double extentBefore = host.Left.TextArea.TextView.DocumentHeight;
        Assert.True(double.IsNaN(host.View.PaneFontSize));

        host.View.PaneFontSize = 22;
        CompositeHost.Layout();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        CompositeHost.Layout();
        Assert.Equal(22, host.Left.FontSize);
        Assert.Equal(22, host.Right.FontSize);
        Assert.True(host.Left.TextArea.TextView.DefaultLineHeight > before, "a larger font should give taller rows");
        Assert.True(host.Left.TextArea.TextView.DocumentHeight > extentBefore, "and a taller document");
        AssertExtentsEqual(host);

        // Unset again: the panes go back to the size their own theme sets.
        host.View.PaneFontSize = double.NaN;
        CompositeHost.Layout();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        CompositeHost.Layout();
        Assert.Equal(before, host.Left.TextArea.TextView.DefaultLineHeight);
        AssertExtentsEqual(host);
    }

    [AvaloniaFact]
    public async Task A_pane_font_family_change_reaches_both_panes_and_clears_back_to_the_theme()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();
        await host.LoadAsync(left, right);
        FontFamily themeFamily = host.Left.FontFamily;

        host.View.PaneFontFamily = FontFamily.Default;
        CompositeHost.Layout();
        Assert.Equal(FontFamily.Default, host.Left.FontFamily);
        Assert.Equal(FontFamily.Default, host.Right.FontFamily);
        AssertExtentsEqual(host);

        host.View.PaneFontFamily = null;
        CompositeHost.Layout();
        Assert.Equal(themeFamily, host.Left.FontFamily);
        AssertExtentsEqual(host);
    }

    [AvaloniaFact]
    [EnglishChrome]
    public async Task Mixed_line_endings_are_noticed_in_the_state_and_the_strip()
    {
        using CompositeHost host = new(width: 700, height: 200);
        host.Show();
        await host.LoadAsync("alpha\r\nbeta\ngamma\n", "alpha\r\nbeta\ndelta\n");

        Assert.Contains(host.View.Warnings, w => w.Code == DiffWarningCode.MixedLineEndings);
        Assert.Equal(DiffViewState.Degraded, host.View.State);
        Assert.Contains("line endings", host.View.StateMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StatusKind.Warning, host.View.Status.Kind);
        Assert.Contains("line endings", host.View.StatusStrip!.TransientText!, StringComparison.OrdinalIgnoreCase);

        // The header names the convention per side, so the notice says where to look.
        Assert.Contains("mixed", host.View.LeftHeader!.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A padded line keeps its padding whatever its first character is. AvaloniaEdit creates its own
    /// single-character generator in the text view's constructor, ahead of every generator a pane
    /// adds, and where two generators want the same offset the first to build an element with a
    /// length wins it. The padding has no length, so it must be asked first: behind that generator, a
    /// padded line beginning with a character it draws — a space or a tab while whitespace is shown,
    /// a control character always — lost its rows, and every row below it lost its alignment. Found
    /// by hand in the demo, where the copy arrow drawn in the missing padding landed on the next
    /// line's number.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Showing_whitespace_keeps_the_padding_of_a_line_that_begins_with_it(bool beforeTheLoad)
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(width: 900, height: 300);
        host.Show();

        // Before the load is how a host that remembers the setting starts; after it is the View menu.
        if (beforeTheLoad)
        {
            host.View.ShowWhitespace = true;
        }

        await host.LoadAsync(left, right);
        if (!beforeTheLoad)
        {
            host.View.ShowWhitespace = true;
            CompositeHost.Layout();
        }

        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            Assert.True(pane.Options.ShowSpaces);

            // The fixture must hold the case, or the assertion after it passes over nothing.
            Assert.Contains(' ', LeadingCharactersOfPaddedLines(pane));
            Assert.Empty(PaddingThatDidNotRender(pane));
        }

        AssertExtentsEqual(host);
    }

    /// <summary>
    /// The same rule for the other two characters that generator draws: a tab, while whitespace is
    /// shown, and a control character, which it boxes by default with whitespace hidden.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("one\n\ttwo\n", "one\ninserted\n\ttwo\n", true, '\t')]
    [InlineData("one\n\ftwo\n", "one\ninserted\n\ftwo\n", false, '\f')]
    public async Task A_padded_line_keeps_its_padding_whatever_character_it_begins_with(
        string left, string right, bool showWhitespace, char leading)
    {
        using CompositeHost host = new(width: 600, height: 200);
        host.Show();
        host.View.ShowWhitespace = showWhitespace;
        await host.LoadAsync(left, right);

        char first = Assert.Single(LeadingCharactersOfPaddedLines(host.Left));
        Assert.Equal(leading, first);
        Assert.Empty(PaddingThatDidNotRender(host.Left));
        AssertExtentsEqual(host);
    }

    /// <summary>The first character of every line the model pads; an empty line contributes none.</summary>
    private static char[] LeadingCharactersOfPaddedLines(DiffPanePresenter pane)
    {
        int count = pane.Document.LineCount;
        return
        [
            .. pane.Metadata.PaddedLineNumbers(count)
                .Select(number => pane.Document.GetLineByNumber(number))
                .Where(line => line.Length > 0)
                .Select(line => pane.Document.GetCharAt(line.Offset)),
        ];
    }

    /// <summary>
    /// Every padded line whose visual line carries other padding than the model gives it — read from
    /// the element the line was built with, as the background renderer reads it.
    /// </summary>
    private static string[] PaddingThatDidNotRender(DiffPanePresenter pane)
    {
        TextView view = pane.TextArea.TextView;
        int count = pane.Document.LineCount;
        return
        [
            .. pane.Metadata.PaddedLineNumbers(count)
                .Select(number => (
                    Number: number,
                    Expected: pane.Metadata.PaddingFor(number, count),
                    Actual: DiffLineBackgroundRenderer.PaddingOf(view.GetOrConstructVisualLine(pane.Document.GetLineByNumber(number)))))
                .Where(line => line.Actual != line.Expected)
                .Select(line => $"line {line.Number}: the model pads it {line.Expected}, its visual line carries {line.Actual}"),
        ];
    }

    /// <summary>The panes agree on how tall the document is; the padding is what makes that true.</summary>
    private static void AssertExtentsEqual(CompositeHost host)
    {
        Assert.Equal(host.Left.TextArea.TextView.DocumentHeight, host.Right.TextArea.TextView.DocumentHeight, 3);
    }

    /// <summary>Pixels in the left pane that are not its background: the ink on the page.</summary>
    private static int Ink(CompositeHost host)
    {
        using WriteableBitmap frame = host.Capture();
        TextView view = host.Left.TextArea.TextView;
        Point origin = view.TranslatePoint(new Point(0, 0), host.Window)
                       ?? throw new InvalidOperationException("the text view is not in the window");
        PixelRect area = PixelProbe.Inside(origin.X, origin.Y, origin.X + view.Bounds.Width, origin.Y + view.Bounds.Height);
        Color background = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        return PixelProbe.Count(frame, area, c => !PresenterHost.Near(c, background, tolerance: 24));
    }

    /// <summary>Where the first glyph after the leading tab sits, in text-view coordinates.</summary>
    private static double FirstTextX(DiffPanePresenter pane)
    {
        VisualLine visual = pane.TextArea.TextView.GetVisualLine(1)
                            ?? throw new InvalidOperationException("the first line is not visible");
        return visual.GetTextLineVisualXPosition(visual.TextLines[0], visual.GetVisualColumn(1));
    }
}
