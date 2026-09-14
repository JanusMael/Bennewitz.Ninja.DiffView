using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView.Tests.Presenter;

/// <summary>
/// Plan 00001 §Phase 4, the coarse snapshot: two presenters over the small fixture, every row
/// visible, in both theme variants under Semi. The pixel assertions gate; this catches what
/// they do not.
/// </summary>
public sealed class PresenterSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Small_fixture_renders(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using PresenterHost host = PresenterHost.Small(width: 900, height: 600);
            host.Show();
            using WriteableBitmap frame = host.Capture();
            Assert.Equal(900, frame.PixelSize.Width);
            Assert.Equal(600, frame.PixelSize.Height);
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.Empty(host.Left.Faults);
            Assert.Empty(host.Right.Faults);
        }
        finally
        {
            // Restored before the await: the continuation after it may run off the UI thread.
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant);
    }
}
