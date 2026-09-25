using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 10, the rendered evidence: whitespace and line-ending glyphs over the diff
/// backgrounds, a wider tab, a larger pane font — and the focus accent under the header of the
/// pane that has focus — in both theme variants.
/// </summary>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
[LinuxBaseline]
public sealed class ViewOptionsSnapshotTests
{
    private const string Left = "\tone\t  two\nalpha\nbeta\n";
    private const string Right = "\tone\t  three\nalpha\ngamma\n";

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_view_options_and_the_focus_accent_render(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 900, height: 320);
            host.Show();
            await host.LoadAsync(Left, Right);
            host.View.ShowWhitespace = true;
            host.View.ShowLineEndings = true;
            host.View.TabWidth = 8;
            host.View.PaneFontSize = 16;
            host.Right.TextArea.Focus();
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.True(host.View.RightHeader!.IsPaneFocused);
            Assert.Equal(host.Left.TextArea.TextView.DocumentHeight, host.Right.TextArea.TextView.DocumentHeight, 3);
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
