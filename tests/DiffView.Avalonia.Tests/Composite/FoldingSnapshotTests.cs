using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00013 phase 5, the rendered evidence: the same pair unfolded and folded, so the frames
/// say what a fold looks like — placeholders in both panes at the same rows, the connector's
/// polygons moved up with them, and the map measuring the document that is on screen.
/// </summary>
/// <remarks>
/// The pair is fixed here rather than shared with the other folding tests: a snapshot's fixture
/// is part of the frame, and a change to a fixture two tests away should not silently repaint six
/// baselines.
/// </remarks>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
public sealed class FoldingSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_folded_pair_renders(string variant)
    {
        MemoryStream png = await RenderAsync(variant, contextRows: 0);
        await Verifier.Verify(png, "png").UseParameters(variant);
    }

    [AvaloniaFact]
    public async Task The_same_pair_unfolded_renders()
    {
        MemoryStream png = await RenderAsync("Light", contextRows: null);
        await Verifier.Verify(png, "png");
    }

    [AvaloniaFact]
    public async Task A_pair_folded_with_context_renders()
    {
        MemoryStream png = await RenderAsync("Light", contextRows: DiffKeyMap.DefaultContextRows);
        await Verifier.Verify(png, "png");
    }

    private static async Task<MemoryStream> RenderAsync(string variant, int? contextRows)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();
            (string left, string right) = FoldingFixture.Pair();
            await host.LoadAsync(new PaneSource(left) { Title = "before.txt" }, new PaneSource(right) { Title = "after.txt" });

            host.View.UnchangedContextRows = contextRows;
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            Assert.Equal(900, frame.PixelSize.Width);
            frame.Save(png, PngBitmapEncoderOptions.Default);
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        return png;
    }
}
