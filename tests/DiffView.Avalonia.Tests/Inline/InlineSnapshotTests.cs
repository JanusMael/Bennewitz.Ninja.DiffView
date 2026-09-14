using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace Bennewitz.Ninja.DiffView.Tests.Inline;

/// <summary>
/// Plan 00001 §Phase 11, the rendered evidence: the same pair unified — removals then additions
/// inside each block, a number column per side, word-level pieces on a modified pair, and the
/// current block outlined — in both theme variants.
/// </summary>
/// <remarks>Every test here is a frame, and a frame is a picture of English chrome — see <see cref="EnglishChromeAttribute"/>.</remarks>
[EnglishChrome]
public sealed class InlineSnapshotTests
{
    private const string Left = "using System;\nnamespace Demo;\n\nclass Greeter\n{\n    public string Greet(string name) => $\"Hello {name}\";\n\n    public void Farewell() { }\n}\n";
    private const string Right = "using System;\nusing System.Text;\nnamespace Demo;\n\nclass Greeter\n{\n    public string Greet(string name) => $\"Hi, {name}!\";\n}\n";

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_unified_view_renders(string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            using InlineHost host = new(width: 900, height: 360);
            host.Show();
            await host.LoadAsync(Left, Right);
            host.View.FirstChange();
            InlineHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.Equal(DiffViewState.Ready, host.View.State);
            Assert.Equal(0, host.View.CurrentChangeIndex);
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
