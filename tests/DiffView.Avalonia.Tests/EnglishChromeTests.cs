using System.Globalization;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Avalonia;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// Plan 00015 §Phase 1: the pin, proven to fire. An attribute believed to pin while pinning nothing
/// is worse than no attribute — the suite would read as audited and stay green on an English runner
/// — so the mechanism is tested before it is applied to anything.
/// </summary>
public sealed class EnglishChromeTests
{
    /// <summary>A stand-in for the runner's argument: <see cref="EnglishChromeAttribute"/> reads only its name.</summary>
    private static MethodInfo SomeMethod => typeof(EnglishChromeTests).GetMethod(nameof(SomeMethodPlaceholder), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void SomeMethodPlaceholder()
    {
    }

    /// <summary>
    /// The load-bearing one: these are <c>[AvaloniaFact]</c> tests, and Avalonia's own test runner
    /// dispatches the body onto the headless UI thread through <c>AvaloniaTestRunner</c>. Whether
    /// xunit's before/after attributes survive that path is the assumption the whole plan rests on,
    /// and reading the runner's source is not the same as watching it happen.
    /// </summary>
    /// <remarks>
    /// Culture-independent by construction, so it means the same thing under either leg: the
    /// ambient value is <c>DiffViewLocalization.Default</c>, whose <c>Culture</c> is <c>null</c>.
    /// Deleting the attribute below must turn this red.
    /// </remarks>
    [AvaloniaFact]
    [EnglishChrome]
    public void The_pin_reaches_the_body_of_a_headless_test()
    {
        Assert.Equal(EnglishChromeAttribute.English, DiffViewStrings.Localization.Culture);
    }

    /// <summary>
    /// And that the pin does the thing it exists for rather than merely setting a property: text
    /// resolves English while the thread is asking for German, which is the arrangement the CI leg
    /// creates and the only one in which a pinned frame is the right frame.
    /// </summary>
    [AvaloniaFact]
    [EnglishChrome]
    public void Text_resolves_English_while_the_thread_asks_for_German()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            // Against the compiled table rather than a literal: the two sides cannot drift, and a
            // pin that failed returns the German satellite's word, which matches neither.
            Assert.Equal(DiffViewStrings.EnglishDefaults[DiffViewStrings.HeaderDirty], DiffViewStrings.Get(DiffViewStrings.HeaderDirty));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// The scope decision, stated as a test. An ambient pin — one assignment at start-up — is
    /// destroyed by any test that resets the seam, and <see cref="LocalizationTests"/> does exactly
    /// that in a <c>finally</c>, by design. A per-test scope re-establishes itself regardless.
    /// </summary>
    /// <remarks>
    /// Mutate <see cref="EnglishChromeAttribute"/> to pin once behind a flag and this is the test
    /// that fails; every other test in this file stays green.
    /// </remarks>
    [Fact]
    public void The_pin_is_re_established_after_a_test_reset_the_seam()
    {
        DiffViewLocalization before = DiffViewStrings.Localization;
        EnglishChromeAttribute attribute = new();
        try
        {
            attribute.Before(SomeMethod, test: null!);
            attribute.After(SomeMethod, test: null!);

            // What LocalizationTests does on its way out, and what an ambient pin does not survive.
            DiffViewStrings.ResetForTesting();

            attribute.Before(SomeMethod, test: null!);
            Assert.Equal(EnglishChromeAttribute.English, DiffViewStrings.Localization.Culture);
            attribute.After(SomeMethod, test: null!);
        }
        finally
        {
            DiffViewStrings.Localization = before;
        }
    }

    /// <summary>
    /// The pin is a test's, not the run's: what was there before comes back, so the next test reads
    /// the leg's culture and stays inside the audit.
    /// </summary>
    [Fact]
    public void The_pin_is_removed_when_the_test_ends()
    {
        DiffViewLocalization before = DiffViewStrings.Localization;
        EnglishChromeAttribute attribute = new();
        try
        {
            attribute.Before(SomeMethod, test: null!);
            Assert.NotSame(before, DiffViewStrings.Localization);

            attribute.After(SomeMethod, test: null!);
            Assert.Same(before, DiffViewStrings.Localization);
        }
        finally
        {
            DiffViewStrings.Localization = before;
        }
    }

    /// <summary>
    /// The static scope's assumption, asserted rather than commented: serial execution means one
    /// pin at a time, and if that ever stops holding the suite says so instead of silently leaving
    /// one test's pin open across another's.
    /// </summary>
    [Fact]
    public void A_second_pin_opened_over_the_first_is_refused()
    {
        DiffViewLocalization before = DiffViewStrings.Localization;
        EnglishChromeAttribute attribute = new();
        try
        {
            attribute.Before(SomeMethod, test: null!);
            Assert.Throws<InvalidOperationException>(() => attribute.Before(SomeMethod, test: null!));
        }
        finally
        {
            attribute.After(SomeMethod, test: null!);
            DiffViewStrings.Localization = before;
        }
    }
}
