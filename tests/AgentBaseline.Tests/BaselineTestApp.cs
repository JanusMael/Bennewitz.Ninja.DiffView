using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Bennewitz.Ninja.AgentBaseline.Tests;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Tests;
using Semi.Avalonia;

// Avalonia tests share one dispatcher; xunit.runner.json in this project turns collection
// parallelization off so they run serially.
[assembly: AvaloniaTestApplication(typeof(BaselineTestApp))]

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// The application this project's headless tests run under, configured as the library's
/// <c>HeadlessTestApp</c> is: Semi as the base theme, the DiffView styles on top, Skia so a pane's
/// text is really laid out, the bundled monospace font as the default family, and Avalonia's logger
/// routed into <see cref="TestLogSink"/> so a binding warning can fail a test.
/// </summary>
/// <remarks>
/// That class is internal to the library's test assembly, as <c>CompositeHost</c> is — plan 00034
/// step 5's own note — so this project carries its own rather than a reference into another test
/// assembly. Its culture pin is left out: nothing here asserts the library's text.
/// </remarks>
public sealed class BaselineTestApp : Application
{
    /// <summary>The bundled font, addressed through this assembly's resources.</summary>
    internal const string MonoFamilyName = "avares://AgentBaseline.Tests/Fonts#DejaVu Sans Mono";

    public override void Initialize()
    {
        Styles.Add(new SemiTheme { Locale = CultureInfo.GetCultureInfo("en-US") });
        Styles.Add(new StyleInclude(new Uri("avares://AgentBaseline.Tests/")) { Source = DiffViewResources.ThemeUri });
        Resources[DiffViewResources.MonospaceFontFamilyKey] = new FontFamily(MonoFamilyName);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        Logger.Sink = TestLogSink.Instance;

        return AppBuilder.Configure<BaselineTestApp>()
                         .UseSkia()
                         .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                         .With(new FontManagerOptions
                         {
                             DefaultFamilyName = MonoFamilyName,
                             FontFallbacks = [new FontFallback { FontFamily = new FontFamily(MonoFamilyName) }],
                         });
    }
}
