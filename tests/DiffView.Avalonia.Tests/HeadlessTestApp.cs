using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Bennewitz.Ninja.DiffView.Avalonia;
using Bennewitz.Ninja.DiffView.Avalonia.Tests;
using Semi.Avalonia;

// Avalonia tests share one dispatcher; xunit.runner.json in this project turns collection
// parallelization off so they run serially.
[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// The application every headless test runs under: Semi as the base theme (the intended host's),
/// the DiffView styles on top, Skia rendering so frames can be captured, the bundled monospace
/// font as the default family so frames are deterministic, and Avalonia's logger routed into
/// <see cref="TestLogSink"/> so a binding warning can fail a test.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new SemiTheme { Locale = CultureInfo.GetCultureInfo("en-US") });
        Styles.Add(new StyleInclude(new Uri("avares://DiffView.Avalonia.Tests/")) { Source = DiffViewResources.ThemeUri });
        // The panes' font token, overridden with the bundled font: application resources beat
        // the theme include's, so rendered frames never depend on the machine's installed fonts.
        Resources[DiffViewResources.MonospaceFontFamilyKey] = new FontFamily(TestFonts.MonoFamilyName);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        Logger.Sink = TestLogSink.Instance;

        return AppBuilder.Configure<HeadlessTestApp>()
                         .UseSkia()
                         .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                         .With(new FontManagerOptions
                         {
                             DefaultFamilyName = TestFonts.MonoFamilyName,
                             FontFallbacks = [new FontFallback { FontFamily = new FontFamily(TestFonts.MonoFamilyName) }],
                         });
    }
}

/// <summary>The bundled font, addressed through this assembly's resources.</summary>
internal static class TestFonts
{
    public const string MonoFamilyName = "avares://DiffView.Avalonia.Tests/Fonts#DejaVu Sans Mono";
}
