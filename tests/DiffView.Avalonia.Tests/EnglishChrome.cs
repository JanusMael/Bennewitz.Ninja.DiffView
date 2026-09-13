using System.Globalization;
using System.Reflection;
using Bennewitz.Ninja.DiffView.Avalonia;
using Xunit.v3;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// Pins the library's text to English for one test and puts back whatever was there afterwards —
/// the claim that this test is about English words, written where a reviewer can see it.
/// </summary>
/// <remarks>
/// <para>
/// A committed frame is a picture of English chrome, so a snapshot rendered under
/// <c>DIFFVIEW_TEST_UI_CULTURE=de-DE</c> fails for a reason that is not a defect. The same is true
/// of an assertion whose subject really is the English sentence. Both want the same thing: the
/// library's text held at English while the thread's UI culture is whatever the leg set.
/// </para>
/// <para>
/// <b>The pin is per test, and that is the whole design.</b> Assigning
/// <c>DiffViewStrings.Localization</c> once at start-up was measured on 2026-09-13 and recovers 16
/// of the 94 failures: <see cref="LocalizationTests"/> assigns the seam directly and calls
/// <c>DiffViewStrings.ResetForTesting</c> in a <c>finally</c> — both the seam working as designed —
/// and because the Avalonia suite is serial every test after it in the run order reverts to the
/// machine's language. A scope re-established per test cannot be destroyed by what a previous test
/// did, which is why this hangs off <see cref="DiffViewStrings.Override"/> and its
/// <see cref="IDisposable"/> rather than an assignment.
/// </para>
/// <para>
/// Applied to a method by default. A class-level application is shorthand for <i>every test in this
/// class is a frame</i>, and where that holds it is correct but blunt: it also pins every test the
/// class gains afterwards, taking them out of the leg's reach before anyone has looked at them. The
/// set pinned is the set that was measured to need it.
/// </para>
/// <para>
/// <c>EditingSnapshotTests</c> is why the measuring came first. Its <c>Verify</c> sits behind
/// assertions that fail under the German leg, so its frame has never been compared there at all —
/// which is what makes the snapshot count a lower bound rather than a list.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class EnglishChromeAttribute : BeforeAfterTestAttribute
{
    /// <summary>The culture the baselines and the English literals were written in.</summary>
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// The live scope. A plain static rather than <c>AsyncLocal</c>: the Avalonia suite is serial by
    /// design (<c>xunit.runner.json</c>), which plan 00014 already weighed when it rejected
    /// <c>AsyncLocal</c> for the seam itself. <see cref="Before"/> throws rather than nest, so the
    /// assumption fails loudly if it ever stops holding.
    /// </summary>
    private static IDisposable? scope;

    /// <inheritdoc/>
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (scope is not null)
        {
            throw new InvalidOperationException(
                $"A pin was still open when {methodUnderTest.Name} started. The suite is serial by design; " +
                "a nested pin means two tests are running at once and the scope's lifetime no longer matches a test's.");
        }

        scope = DiffViewStrings.Override(new DiffViewLocalization { Culture = English });
    }

    /// <inheritdoc/>
    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        // Null when Before threw, and on any path where the runner calls After without Before.
        scope?.Dispose();
        scope = null;
    }
}
