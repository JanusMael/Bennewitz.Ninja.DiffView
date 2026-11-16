using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Presenter;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00035 step 3, the frame: a range marked on each side, in both variants, each ending part-way through a block
/// of changed lines, so the same colour shows inside the mark and beside it. The right's runs over two plain lines
/// into the first two of three inserted lines. The left's runs over two plain lines, a gap of padding that stays
/// unshaded, a heading, a modified line with its word pieces, and the first of two deleted lines.
/// </summary>
/// <remarks>
/// Class-level <see cref="EnglishChromeAttribute"/>: every test here is a frame, and a committed frame is a picture of
/// English chrome.
/// </remarks>
[EnglishChrome]
[LinuxBaseline]
public sealed class EmphasisSnapshotTests
{
    /// <summary>Three sections of rules, Build's three lines inserted on the right at its lines 7 to 9.</summary>
    private const string Left =
        "# Rules\n\n## Build\n\nRun the tests before a push.\nKeep the build green.\nNever skip a hook.\n\n"
        + "## Style\n\nWrite short sentences.\nPrefer the active voice.\nName things for what they do.\n\n"
        + "## Review\n\nRead the diff first.\nAsk before a large change.\n";

    /// <summary>Build gains three lines, Style rewords its first and drops the other two, and Review rewords its first.</summary>
    private const string Right =
        "# Rules\n\n## Build\n\nRun the tests before a push.\nKeep the build green.\nRun the linter too.\n"
        + "Fix every warning.\nCommit small changes.\nNever skip a hook.\n\n"
        + "## Style\n\nWrite short, plain sentences.\n\n"
        + "## Review\n\nRead the whole diff first.\nAsk before a large change.\n";

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_range_marked_on_each_side_renders(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();
            await host.LoadAsync(Left, Right);
            using WriteableBitmap unmarked = host.Capture();

            // The left's lines 5 to 12, the right's 5 to 8.
            host.View.LeftEmphasis = new LineRange(4, 8);
            host.View.RightEmphasis = new LineRange(4, 4);
            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            Assert.Equal([5, 6, 7, 8, 9, 10, 11, 12], host.Left.EmphasisRenderer.LastDrawn.Select(r => r.FirstLineNumber));
            Assert.Equal([5, 6, 7, 8], host.Right.EmphasisRenderer.LastDrawn.Select(r => r.FirstLineNumber));
            Assert.Equal(DiffLineKind.Inserted, host.Right.Metadata.KindOf(9));
            Assert.Equal(DiffLineKind.Deleted, host.Left.Metadata.KindOf(13));

            // Beside the picture, the property it shows, which a frame cannot be relied on for: line 5's row on the
            // left carries the shade over what was there; the three rows of padding above line 7 are untouched, and so
            // are the rows of the changed lines the marks stop short of, the right's line 9 and the left's 13.
            TextView left = host.Left.TextArea.TextView;
            double lineHeight = left.DefaultLineHeight;
            Point origin = left.TranslatePoint(new Point(0, 0), host.Window) ?? throw new InvalidOperationException("The left text view is not in the window.");
            int x = (int)(origin.X + left.Bounds.Width - 8);
            int y = (int)(origin.Y + RowTop(left, 5) + lineHeight / 2);
            Color expected = PresenterHost.Composite(PresenterHost.Token("DiffView.EmphasisBrush"), PixelProbe.At(unmarked, x, y));
            Color shaded = PixelProbe.At(frame, x, y);
            Assert.True(PresenterHost.Near(shaded, expected), $"the left's line 5: expected {expected}, got {shaded}");

            double paddingTop = origin.Y + left.GetVisualTopByDocumentLine(7) - left.ScrollOffset.Y;
            AssertUntouched(unmarked, frame, PixelProbe.Inside(origin.X, paddingTop, origin.X + left.Bounds.Width, paddingTop + 3 * lineHeight), "the padding above the left's line 7");
            AssertUntouched(unmarked, frame, RowOf(host, host.Left, 13), "the left's line 13, deleted and past the mark");
            AssertUntouched(unmarked, frame, RowOf(host, host.Right, 9), "the right's line 9, inserted and past the mark");
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    /// <summary>The top of 1-based <paramref name="line"/>'s own row in <paramref name="view"/>, below any padding above it.</summary>
    private static double RowTop(TextView view, int line)
    {
        VisualLine visual = view.GetVisualLine(line) ?? throw new InvalidOperationException($"Line {line} is not on screen.");
        return visual.VisualTop - view.ScrollOffset.Y + DiffLineBackgroundRenderer.PaddingOf(visual).Above * view.DefaultLineHeight;
    }

    /// <summary>The pixels of 1-based <paramref name="line"/>'s own row in <paramref name="pane"/>, inside its edges.</summary>
    private static PixelRect RowOf(CompositeHost host, DiffPanePresenter pane, int line)
    {
        TextView view = pane.TextArea.TextView;
        Point origin = view.TranslatePoint(new Point(0, 0), host.Window) ?? throw new InvalidOperationException("The text view is not in the window.");
        double top = origin.Y + RowTop(view, line);
        return PixelProbe.Inside(origin.X, top, origin.X + view.Bounds.Width, top + view.DefaultLineHeight);
    }

    private static void AssertUntouched(WriteableBitmap before, WriteableBitmap after, PixelRect area, string what)
    {
        Assert.True(area.Width > 0 && area.Height > 0, $"{what}: {area} is empty, so it would show nothing");
        (int moved, PixelPoint? first) = PixelProbe.Differing(before, after, area);
        Assert.True(moved == 0, $"{moved} pixels of {what} moved, the first at {first}");
    }
}
