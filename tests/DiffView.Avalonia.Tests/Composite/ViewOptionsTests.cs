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
