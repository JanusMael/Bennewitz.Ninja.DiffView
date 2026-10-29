using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Bennewitz.Ninja.DiffView;
using Semi.Avalonia;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// The application the review window runs in, configured as the tests' <c>BaselineTestApp</c> is: Semi
/// as the base theme and the DiffView styles on top, so what a test asserts is the window the operator
/// sees.
/// </summary>
/// <remarks>
/// ⚠ Written in code, with no markup of its own, so it adds nothing to what the accessibility and grid
/// gates scan in <c>src</c>. The style include is not trimmed here: only the library's projects run the
/// trim analyser, and this app is neither trimmed nor published. ⛔ <see cref="Run"/> starts a platform
/// lifetime, which no headless test can, so it is the one part of the start-up a by-hand run judges.
/// </remarks>
internal sealed class App(StartupPlan plan) : Application
{
    /// <summary>Opens the review <paramref name="plan"/> describes and runs until its window closes.</summary>
    /// <returns>The lifetime's exit code.</returns>
    internal static int Run(StartupPlan plan) =>
        AppBuilder.Configure(() => new App(plan))
                  .UsePlatformDetect()
                  .StartWithClassicDesktopLifetime([]);

    public override void Initialize()
    {
        Styles.Add(new SemiTheme { Locale = CultureInfo.GetCultureInfo("en-US") });
        Styles.Add(new StyleInclude(new Uri("avares://AgentBaseline/")) { Source = DiffViewResources.ThemeUri });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Startup.Open(plan, new ReviewWindow());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
