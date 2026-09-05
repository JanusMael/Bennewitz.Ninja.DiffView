using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Bennewitz.Ninja.DiffView.Demo;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// Proves the harness end to end: the headless host renders real pixels, the demo window comes
/// up under the test theme in both variants with its two panes over the bundled fixture,
/// nothing binds badly, and the frame matches its committed snapshot within the comparer's
/// tolerance.
/// </summary>
public sealed class SmokeSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Demo_window_renders(string variant)
    {
        TestLogSink.Instance.Clear();
        Application.Current!.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        MainWindow window = new() { Width = 800, Height = 500 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(800, frame.PixelSize.Width);
        Assert.Equal(500, frame.PixelSize.Height);

        using MemoryStream png = new();
        frame.Save(png, PngBitmapEncoderOptions.Default);
        png.Position = 0;

        window.Close();
        TestLogSink.AssertNoWarnings(LogArea.Binding);

        await Verifier.Verify(png, "png").UseParameters(variant);
    }
}
