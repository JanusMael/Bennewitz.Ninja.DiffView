using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>Plan 00001 §Phase 7, the snapshot: minimap, connectors and the current-block border on the small fixture, in both variants.</summary>
public sealed class NavigationSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Minimap_connectors_and_the_current_block_render(string variant)
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
            host.View.CurrentChangeIndex = 2;
            CompositeHost.Layout();
            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.Equal(2, host.View.CurrentChangeIndex);
            Assert.NotNull(host.Left.BackgroundRenderer.LastCurrentBlockBorder);
            Assert.NotEmpty(host.View.Gutter!.LastPolygons);
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
