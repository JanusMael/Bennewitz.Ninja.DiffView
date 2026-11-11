using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.DiffView.Tests.Viewer;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Open item 26, layout F: a header's title and its focus underline start where its pane's line numbers do, at the
/// first digit of the widest number, so the title stands over the numbers it names and the underline clears the
/// find bar's buttons above it.
/// </summary>
/// <remarks>
/// The first digit is read off the margin's last frame, <see cref="DiffLineNumberMargin.LastColumnLeft"/>, and never
/// computed here: a header held to a number copied from the margin's padding would pass beside a margin that had
/// moved its numbers. An underline is drawn only while its pane holds the keyboard, so each header is read with its
/// own pane focused. The unified view's one pane lights neither of its headers, and only its left header stands over
/// the gutter, so that view is asked about one title.
/// </remarks>
public sealed class HeaderAlignmentTests
{
    /// <summary>
    /// A pixel: layout rounding puts the margin's width and the header's edges on whole pixels, which can move the
    /// first digit by a fraction of one from the padding it is measured from. The layout this replaced put the title
    /// 2 px out and the underline 6.
    /// </summary>
    private const double Tolerance = 1;

    [AvaloniaFact]
    public async Task Each_editor_header_starts_its_title_and_its_underline_at_its_panes_first_digit()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left) { Title = "before.cs" }, new PaneSource(right) { Title = "after.cs" });

        AssertStartsAtFirstDigit("the left header", host.Window, host.Left, host.View.LeftHeader!, host.Capture, underline: true);
        AssertStartsAtFirstDigit("the right header", host.Window, host.Right, host.View.RightHeader!, host.Capture, underline: true);
        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Each_viewer_header_starts_its_title_and_its_underline_at_its_panes_first_digit()
    {
        using ViewerHost host = new();
        host.Show();
        (string left, string right) = ViewerHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left) { Title = "before.cs" }, new PaneSource(right) { Title = "after.cs" });

        AssertStartsAtFirstDigit("the left header", host.Window, host.Left, host.View.LeftHeader!, host.Capture, underline: true);
        AssertStartsAtFirstDigit("the right header", host.Window, host.Right, host.View.RightHeader!, host.Capture, underline: true);
        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_unified_views_left_header_starts_its_title_at_the_left_sides_first_digit()
    {
        using InlineHost host = new();
        host.Show();
        (string left, string right) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left) { Title = "before.cs" }, new PaneSource(right) { Title = "after.cs" });

        AssertStartsAtFirstDigit("the left header", host.Window, host.Pane, host.View.LeftHeader!, host.Capture, underline: false);
        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    private static void AssertStartsAtFirstDigit(
        string which,
        Window window,
        DiffPanePresenter pane,
        DiffPaneHeader header,
        Func<WriteableBitmap> capture,
        bool underline)
    {
        if (underline)
        {
            pane.TextArea.Focus();
            Dispatcher.UIThread.RunJobs();
        }

        // A frame, because the margin says where it drew its numbers only once it has drawn them.
        using (capture())
        {
        }

        DiffLineNumberMargin margin = pane.LineNumberMargin;
        Assert.True(margin.LastColumnLeft > 0, $"{which}: the margin reports no first digit, so there is nothing to hold the header to");
        double firstDigit = XOf(margin, window) + margin.LastColumnLeft;

        double title = XOf(Part<TextBlock>(header, "PART_Title"), window);
        Assert.True(
            Math.Abs(title - firstDigit) <= Tolerance,
            $"{which}'s title starts at {title:0.#} px, where its pane's first digit is at {firstDigit:0.#}");

        if (!underline)
        {
            return;
        }

        Border accent = Part<Border>(header, "PART_FocusAccent");
        Assert.True(accent.IsVisible, $"{which}'s underline is not drawn while its pane holds the keyboard");
        double start = XOf(accent, window);
        Assert.True(
            Math.Abs(start - firstDigit) <= Tolerance,
            $"{which}'s underline starts at {start:0.#} px, where its pane's first digit is at {firstDigit:0.#}");
    }

    private static T Part<T>(DiffPaneHeader header, string name)
        where T : Control
    {
        return header.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);
    }

    private static double XOf(Visual visual, Window window)
    {
        return visual.TranslatePoint(new Point(0, 0), window)?.X
               ?? throw new InvalidOperationException("The part is not in the window's visual tree.");
    }
}
