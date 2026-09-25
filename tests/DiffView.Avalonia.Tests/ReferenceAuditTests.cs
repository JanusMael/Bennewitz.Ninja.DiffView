using Bennewitz.Ninja.XamlQuality.ThemeAudit;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The repository's own audit over the reference checkouts (plan 00001 §Phase 2): the committed
/// report and compat dictionaries equal a fresh run, so a pin bump that changes an inventory
/// forces regeneration; every AvaloniaEdit theme key resolves under every Semi variant with the
/// compat dictionaries and the gaps without them are exactly the known ones; and every
/// <c>DiffView.*</c> pair meets its floor under all ten targets. The checkouts are fetched by the
/// project's <c>EnsureReferenceSources</c> target; a missing consumer directory fails the run
/// naming what to fetch rather than skipping.
/// </summary>
/// <remarks>
/// ⚠ This is a test of DIFFVIEW, not of the audit. The analysis itself moved to
/// Bennewitz.Ninja.XamlQuality and is tested there; what stays here is the part that is about this
/// repository's own themes — that the committed compat dictionaries and report match a fresh run,
/// and that the <c>DiffView.*</c> palette clears its contrast floors. It reaches the analysis
/// through the published <c>Bennewitz.Ninja.XamlQuality</c> package rather than a local project.
/// </remarks>
[Trait("Category", "Reference")]
public sealed class ReferenceAuditTests
{
    private static readonly Lazy<AuditResult> Result = new(() => AuditRunner.Run(AuditConfig.Load(Path.Combine(RepoPaths.Root, "theme-audit.json"))));

    private static readonly string[] SemiVariants = ["Light", "Dark", "Aquatic", "Desert", "Dusk", "NightSky"];

    [Fact]
    public void The_committed_report_equals_a_fresh_run()
    {
        AuditResult result = Result.Value;
        string fresh = MarkdownReport.Render(result);
        string committedPath = result.Config.Resolve(result.Config.Report);

        Assert.True(File.Exists(committedPath), $"{committedPath} is missing; run `theme-audit report`.");
        string committed = Normalize(File.ReadAllText(committedPath));
        if (committed != Normalize(fresh))
        {
            string received = Path.ChangeExtension(committedPath, ".received.md");
            File.WriteAllText(received, fresh);
            Assert.Fail($"{result.Config.Relative(committedPath)} is stale: a fresh run differs (written to {result.Config.Relative(received)}). " +
                        "Regenerate with `theme-audit compat` then `theme-audit report`, and commit the result.");
        }
    }

    [Fact]
    public void The_committed_compat_dictionaries_equal_a_fresh_generation()
    {
        AuditResult result = Result.Value;
        Assert.NotEmpty(result.Compat);

        foreach (CompatOutcome outcome in result.Compat)
        {
            string committedPath = result.Config.Resolve(outcome.Config.Output);
            Assert.True(File.Exists(committedPath), $"{committedPath} is missing; run `theme-audit compat`.");
            string committed = Normalize(File.ReadAllText(committedPath));
            if (committed != Normalize(outcome.Generation.Xml))
            {
                string received = Path.ChangeExtension(committedPath, ".received.axaml");
                File.WriteAllText(received, outcome.Generation.Xml);
                Assert.Fail($"{result.Config.Relative(committedPath)} is stale: a fresh generation differs (written to {result.Config.Relative(received)}). " +
                            "Regenerate with `theme-audit compat` and commit the result.");
            }

            // Nothing had to fall back to a literal: every mapped and copied key closes over the target.
            Assert.DoesNotContain(outcome.Generation.Entries, e => e.How == CompatHow.Literal);
        }
    }

    [Fact]
    public void AvaloniaEdit_theme_keys_resolve_under_every_Semi_variant_with_compat_and_the_gaps_without_are_the_known_ones()
    {
        AuditResult result = Result.Value;

        ConsumerThemeFindings Findings(string consumer, string theme) =>
            result.Findings.Single(f => f.Consumer.Config.Name == consumer && f.Theme.Config.Name == theme);

        // The compat dictionaries close every gap, under all six variants.
        Assert.Equal(SemiVariants, Findings("AvaloniaEdit Fluent theme", "Semi + compat").Theme.Variants);
        Assert.Empty(Findings("AvaloniaEdit Fluent theme", "Semi + compat").Undefined);
        Assert.Empty(Findings("AvaloniaEdit Simple theme", "Semi + compat").Undefined);

        // Without them, AvaloniaEdit's Fluent theme is missing exactly these six keys under every variant …
        IReadOnlyList<UndefinedKeyFinding> fluentGaps = Findings("AvaloniaEdit Fluent theme", "Semi").Undefined;
        Assert.Equal(
            ["ContentControlThemeFontFamily", "ControlContentThemeFontSize", "SystemAccentColor", "SystemBaseLowColor", "SystemChromeMediumColor", "ToolTipBorderThemeThickness"],
            fluentGaps.Select(f => f.Key).Distinct().Order(StringComparer.Ordinal).ToArray());
        Assert.All(fluentGaps.GroupBy(f => f.Key), g => Assert.Equal(SemiVariants, g.Select(f => f.DisplayName).ToArray()));

        // … and its Simple theme these nine, HighlightColor only where Semi's high-contrast variants do not define their own.
        IReadOnlyList<UndefinedKeyFinding> simpleGaps = Findings("AvaloniaEdit Simple theme", "Semi").Undefined;
        Assert.Equal(
            ["ContentControlThemeFontFamily", "FontSizeNormal", "HighlightColor", "ThemeBackgroundBrush", "ThemeBackgroundColor", "ThemeBorderLowColor", "ThemeBorderMidBrush", "ThemeBorderThickness", "ThemeForegroundColor"],
            simpleGaps.Select(f => f.Key).Distinct().Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["Light", "Dark"], simpleGaps.Where(f => f.Key == "HighlightColor").Select(f => f.DisplayName).ToArray());

        // Under their own themes, both files resolve completely.
        Assert.Empty(Findings("AvaloniaEdit Fluent theme", "Fluent").Undefined);
        Assert.Empty(Findings("AvaloniaEdit Simple theme", "Simple").Undefined);
    }

    [Fact]
    public void Every_DiffView_pair_meets_its_floor_under_all_ten_targets_in_both_palettes()
    {
        AuditResult result = Result.Value;

        foreach (string consumer in new[] { "DiffView", "DiffView (colour-blind palette)" })
        {
            List<ConsumerThemeFindings> findings = result.Findings.Where(f => f.Consumer.Config.Name == consumer).ToList();

            // Fluent 2 + Simple 2 + Semi 6 (+ Semi with compat, the same six surfaces).
            int targets = findings.Where(f => f.Theme.Config.Name != "Semi + compat").Sum(f => f.Theme.Variants.Count);
            Assert.Equal(10, targets);

            foreach (ConsumerThemeFindings finding in findings)
            {
                Assert.Equal(finding.Consumer.Pairs.Count * finding.Theme.Variants.Count, finding.Contrast.Count);
                Assert.All(finding.Contrast, c => Assert.True(
                    c.Status == ContrastStatus.Pass,
                    $"{consumer} under {finding.Theme.Config.Name}/{c.DisplayName}: {c.Pair.Foreground} on {c.Pair.Background} is {c.Status} ({c.Ratio?.ToString("0.00") ?? c.Reason})"));

                // DiffView references no host key: nothing undefined under any target.
                Assert.Empty(finding.Undefined);
            }
        }
    }

    [Fact]
    public void The_hosts_own_gaps_are_reported_not_hidden()
    {
        AuditResult result = Result.Value;

        // Fluent 12.1.2 references two keys it never defines; the compat dictionary cannot invent them.
        ConsumerThemeFindings fluentOnFluent = result.Findings.Single(f => f.Consumer.Config.Name == "Fluent controls" && f.Theme.Config.Name == "Fluent");
        Assert.Equal(["ScrollBarButtonBackgroundDisabled", "ToggleSwitchFillOffDisabled"], fluentOnFluent.Undefined.Select(f => f.Key).Distinct().Order(StringComparer.Ordinal).ToArray());
        ConsumerThemeFindings fluentOnCompat = result.Findings.Single(f => f.Consumer.Config.Name == "Fluent controls" && f.Theme.Config.Name == "Semi + compat");
        Assert.Equal(fluentOnFluent.Undefined.Select(f => f.Key).Distinct().Order(StringComparer.Ordinal), fluentOnCompat.Undefined.Select(f => f.Key).Distinct().Order(StringComparer.Ordinal));

        // Every theme walked completely.
        Assert.All(result.Themes, t => Assert.Empty(t.Inventory.Unresolved));
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
