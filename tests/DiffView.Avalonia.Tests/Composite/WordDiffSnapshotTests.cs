using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>Plan 00001 §Phase 6, the snapshot: a modified row with only its changed words highlighted, in both variants.</summary>
public sealed class WordDiffSnapshotTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_modified_row_shows_only_the_changed_words(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 900, height: 240);
            host.Show();
            await host.LoadAsync(
                "alpha\nThe quick brown fox jumps over the lazy dog\nreturn Compute(value, 12) + offset;\nomega\n",
                "alpha\nThe quick red fox leaps over the lazy dog\nreturn Compute(value, 42) - offset;\nomega\n");
            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.NotEmpty(host.Left.BackgroundRenderer.LastWordRectangles);
            Assert.NotEmpty(host.Right.BackgroundRenderer.LastWordRectangles);
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
