using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 5, the snapshots: headers and status strip over the small fixture in both
/// theme variants and both palettes; the identical banner and the error banner once each.
/// </summary>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
[LinuxBaseline]
public sealed class CompositeSnapshotTests
{
    private static readonly Uri BaseUri = new("avares://DiffView.Avalonia.Tests/");

    [AvaloniaTheory]
    [InlineData("Light", "Default")]
    [InlineData("Dark", "Default")]
    [InlineData("Light", "ColorBlind")]
    [InlineData("Dark", "ColorBlind")]
    public async Task Headers_and_status_strip_render(string variant, string palette)
    {
        (string left, string right) = CompositeHost.SmallFixture();
        MemoryStream png = await RenderAsync(variant, palette, host => host.LoadAsync(new PaneSource(left) { Title = "before.cs" }, new PaneSource(right) { Title = "after.cs" }));
        await Verifier.Verify(png, "png").UseParameters(variant, palette);
    }

    [AvaloniaFact]
    public async Task The_identical_banner_renders()
    {
        (string left, _) = CompositeHost.SmallFixture();
        MemoryStream png = await RenderAsync("Light", "Default", host => host.LoadAsync(left, left));
        await Verifier.Verify(png, "png");
    }

    [AvaloniaFact]
    public async Task The_error_banner_renders()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        MemoryStream png = await RenderAsync("Dark", "Default", host =>
        {
            host.View.Builder = (_, _, _, _) => throw new InvalidOperationException("The diff engine threw a deliberate test failure.");
            return host.LoadAsync(left, right);
        });
        await Verifier.Verify(png, "png");
    }

    private static async Task<MemoryStream> RenderAsync(string variant, string palette, Func<CompositeHost, Task> load)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        ResourceInclude? colourBlind = palette == "ColorBlind" ? new ResourceInclude(BaseUri) { Source = DiffViewResources.ColorBlindTokensUri } : null;
        if (colourBlind is not null)
        {
            app.Resources.MergedDictionaries.Add(colourBlind);
        }

        MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();
            await load(host);
            using WriteableBitmap frame = host.Capture();
            Assert.Equal(900, frame.PixelSize.Width);
            frame.Save(png, PngBitmapEncoderOptions.Default);
        }
        finally
        {
            // Restored before the caller awaits Verify: that continuation may run off the UI thread.
            if (colourBlind is not null)
            {
                app.Resources.MergedDictionaries.Remove(colourBlind);
            }

            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        return png;
    }
}
