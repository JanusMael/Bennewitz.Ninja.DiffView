using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 8, the snapshot: the find bar over the panes, every match highlighted above
/// the row fills and below the selection, the current match in its own brush, in both variants.
/// </summary>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
[LinuxBaseline]
public sealed class FindSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_find_bar_and_the_match_highlights_render(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            (string left, string right) = CompositeHost.SmallFixture();
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();
            await host.LoadAsync(left, right);
            host.View.OpenFind();
            await host.FindAsync("_name");
            host.View.CurrentFindMatchIndex = 1;
            CompositeHost.Layout();
            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.True(host.View.IsFindBarOpen);
            Assert.NotEmpty(host.Left.SearchRenderer.LastRectangles);
            Assert.NotEmpty(host.Right.SearchRenderer.LastRectangles);
            Assert.Contains(host.Right.SearchRenderer.LastRectangles, r => r.IsCurrent);
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
