using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00027: the locale caveat reaches a consumer, and stops drifting. Eleven places state that
/// the eight shipped locales are machine-generated and unread — the package blurb, the README, the
/// hosting guide that is both packages' <c>PackageReadmeFile</c>, and the eight locale files' header
/// comments — and nothing held them in agreement.
/// </summary>
/// <remarks>
/// <para>
/// The README told its public readers for three and a half weeks that native review *gates the
/// first release*, a decision lifted on 2026-09-18, and nothing caught it. Each of the eight locale
/// headers ordered the same thing as an imperative. What is gated here is therefore two facts being
/// present and one claim being absent — never the wording, which spans three registers: a one-line
/// blurb, a guide written for a host, and an XML comment written for whoever edits a translation.
/// Forcing one voice on all three would fail on the first legitimate rewrite.
/// </para>
/// <para>
/// ⛔ <c>DECISIONS.md</c>, <c>PROGRESS.md</c> and everything under <c>plans/</c> are deliberately
/// outside the scanned set, and cannot be added to it. Each one *must* quote the claim: the
/// superseded-pointer blockquotes record the correction by naming what they supersede, an approved
/// plan is frozen history, and plan 00027 — this gate's own specification — quotes the lifted
/// imperative three times while explaining it. A gate over all markdown would fail on the documents
/// that record the fix.
/// </para>
/// </remarks>
public sealed class LocaleCaveatTests
{
    private const string Readme = "README.md";
    private const string Guide = "docs/hosting-diffview.md";
    private const string Blurb = "src/DiffView.Avalonia/DiffView.Avalonia.csproj";

    /// <summary>
    /// The phrasings that count as saying the translations are unreviewed, one per register. A
    /// rewrite that drops all of them fails; a rewrite that introduces a new phrasing extends this
    /// list, which is a deliberate act and the point of the gate rather than a cost of it.
    /// </summary>
    private static readonly string[] Unreviewed =
    [
        "not been read by a native speaker",
        "not been reviewed by native speakers",
        "pending native review",
    ];

    /// <summary>
    /// The lifted claim, in the affirmative only. English inflection is load-bearing here and the
    /// positive control below is why it can be trusted: the claim reads "review <b>gates</b> the
    /// release", while every correction reads "does not <b>gate</b> the release". Matching the
    /// substring "gate the release" would fail on the corrections themselves — which is how a gate
    /// comes to report a regression on the commit that fixed the thing.
    /// </summary>
    private static readonly string[] LiftedGate =
    [
        @"\bgates\s+the\s+(first\s+)?(release|publish)\b",
        @"\bblocks\s+the\s+(first\s+)?(release|publish)\b",
        @"\bdo\s+not\s+ship\s+a\s+release\b",
    ];

    private static string[] LocaleFiles()
    {
        string[] found = Directory.GetFiles(
            Path.Combine(RepoPaths.Root, "src", "DiffView.Avalonia", "Localization"),
            "Strings.*-*.resx");

        Assert.Equal(8, found.Length);
        return found;
    }

    /// <summary>
    /// Collapses every run of whitespace to one space, and strips the inline markdown markers that
    /// can sit inside a phrase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Whitespace, because the phrase that went stale was <b>wrapped across a line break</b> in
    /// <c>DECISIONS.md</c>, and a one-line grep over the whole repository reported it absent from
    /// the one file that had it — which is the search that let this survive. These documents
    /// hard-wrap near 100 columns, so any phrase over a few words is a coin toss unread.
    /// </para>
    /// <para>
    /// Emphasis, because the first run of this gate failed on the README, which reads
    /// "have <c>**</c>not<c>**</c> been read by a native speaker": the markers sit <b>inside</b> the
    /// phrase, so a literal match misses it while a human reads the sentence as plainly present.
    /// Adding an asterisked variant to <see cref="Unreviewed"/> would fix that one sentence and
    /// leave the next bolded word to break it again.
    /// </para>
    /// </remarks>
    private static string Normalized(string path)
    {
        return Strip(File.ReadAllText(path));
    }

    private static string Strip(string text)
    {
        return Regex.Replace(Regex.Replace(text, @"[*_`]", string.Empty), @"\s+", " ");
    }

    private static string BlurbText()
    {
        XElement description = XDocument
            .Load(RepoPaths.Source(Blurb))
            .Descendants("Description")
            .Single();

        return Strip(description.Value).Trim();
    }

    [Fact]
    public void Every_place_a_consumer_meets_the_caveat_states_it()
    {
        List<string> texts = [RepoPaths.Source(Readme), RepoPaths.Source(Guide), .. LocaleFiles()];
        Assert.Equal(10, texts.Count);

        foreach (string path in texts)
        {
            string text = Normalized(path);
            string name = Path.GetFileName(path);

            Assert.True(
                text.Contains("machine-generated", StringComparison.OrdinalIgnoreCase),
                $"'{name}' no longer says the translations are machine-generated.");

            Assert.True(
                Unreviewed.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase)),
                $"'{name}' no longer says the translations are unreviewed, in any phrasing this gate knows. "
                    + $"Either the caveat is gone, or a rewrite needs its phrasing adding to {nameof(Unreviewed)}.");
        }
    }

    [Fact]
    public void The_package_blurb_names_the_shipped_locales()
    {
        string blurb = BlurbText();

        // The blurb carries the surprise and not the caveat — the readme nuget.org renders directly
        // beneath it carries that — so what is asserted is that it says the locales are there, are
        // machine-made, and arrive without being asked for.
        Assert.Contains("eight", blurb, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("machine-generated", blurb, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without configuration", blurb, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_shipped_or_authored_text_says_native_review_gates_the_release()
    {
        // The positive control, per AGENTS.md §5: a reading that only asserts nothing wrong was
        // found passes when it reads nothing. These two run first, so a pattern set that has stopped
        // matching anything fails here rather than reporting eleven clean files.
        const string Affirmative = "native review gates the first release, so do not ship a release on these yet";
        Assert.True(
            LiftedGate.Any(pattern => Regex.IsMatch(Affirmative, pattern, RegexOptions.IgnoreCase)),
            "The patterns no longer match the claim they exist to catch, so this test now passes vacuously.");

        const string Corrected = "review is owed to the wording and does not gate the release";
        Assert.False(
            LiftedGate.Any(pattern => Regex.IsMatch(Corrected, pattern, RegexOptions.IgnoreCase)),
            "The patterns match a correction, so this gate would fail on the commit that fixed the drift.");

        List<string> texts =
            [RepoPaths.Source(Readme), RepoPaths.Source(Guide), RepoPaths.Source(Blurb), .. LocaleFiles()];
        Assert.Equal(11, texts.Count);

        foreach (string path in texts)
        {
            string text = Normalized(path);
            foreach (string pattern in LiftedGate)
            {
                Match hit = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                Assert.False(
                    hit.Success,
                    $"'{Path.GetFileName(path)}' says native review gates the release — lifted 2026-09-18 — "
                        + $"as \"{hit.Value}\". Open item 6 and DECISIONS.md have why it no longer does.");
            }
        }
    }
}
