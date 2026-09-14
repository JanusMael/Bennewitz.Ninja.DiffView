using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00005 §Phase 3, the rendered evidence: a run of same-kind rows sharing one chip, a lone
/// changed row as a badge, and a run scrolled off the top edge, in both theme variants.
/// </summary>
/// <remarks>
/// A chip is glyph-scale, and the comparer tolerates half a percent, so these frames cannot fail
/// on their own for a change this small — the assertions beside each capture are the guard, and
/// the PNG is what a reviewer looks at.
/// </remarks>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
public sealed class MarkerChipSnapshotTests
{
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

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_run_shares_one_chip_and_a_lone_row_gets_a_badge(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 700, height: 320);
            host.Show();
            await host.LoadAsync(Left, Right);

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
            double rowHeight = host.Left.TextArea.TextView.DefaultLineHeight;

            // Five deleted rows and one modified row: two chips, one tall and one a badge.
            Assert.Equal([DiffLineKind.Deleted, DiffLineKind.Modified], margin.LastChips.Select(c => c.Kind));
            Assert.Equal((5 * rowHeight) - 4, margin.LastChips[0].Bounds.Height, 0.5);
            Assert.Equal(rowHeight - 4, margin.LastChips[1].Bounds.Height, 0.5);
            Assert.Equal(16, margin.Bounds.Width, 0.5);
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_run_scrolled_off_the_top_is_not_rounded_there(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            // A long deleted run, so it can be scrolled into from the middle.
            string left = "alpha\n" + string.Join("\n", Enumerable.Range(1, 40).Select(i => $"gone {i}")) + "\nomega\n";
            using CompositeHost host = new(width: 700, height: 200);
            host.Show();
            await host.LoadAsync(left, "alpha\nomega\n");

            Presenter.PresenterHost.ScrollTo(host.Left, 12 * host.Left.TextArea.TextView.DefaultLineHeight);
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            ChangeMarkerMargin margin = host.Left.ChangeMarkerMargin;
            (Rect chip, DiffLineKind kind) = Assert.Single(margin.LastChips);
            Assert.Equal(DiffLineKind.Deleted, kind);

            // The run continues past both edges, so the chip does too and neither end is rounded
            // where the reader can see it. A full row above the edge, not merely a negative top:
            // the first visible row starts slightly above zero when scrolled mid-line, so `< 0`
            // alone holds whether or not the chip was extended, and proves nothing.
            double rowHeight = host.Left.TextArea.TextView.DefaultLineHeight;
            Assert.True(chip.Top <= -rowHeight + 1, $"the chip's top is {chip.Top:F1}; a run continuing above should start a full row ({rowHeight:F1}) past the edge");
            Assert.True(chip.Bottom > host.Left.TextArea.TextView.Bounds.Height, $"the chip's bottom is {chip.Bottom:F1}; it should be below the viewport");
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }
}
