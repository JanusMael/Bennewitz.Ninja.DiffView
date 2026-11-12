using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Every button and every other input control the repository writes in markup carries a tooltip: Brian's rule,
/// held here since 2026-11-10, when the review window was found with seven buttons and not one tooltip.
/// </summary>
/// <remarks>
/// <para>
/// <c>BNXQ1011</c>, from <c>Bennewitz.Ninja.XamlQuality</c> <c>2026.4.1111</c>, which Inspections shipped on this
/// repository's asking and which took over from this class's own scan of the markup in open item 40. It reads the
/// seventeen input controls that scan read, by local name, and leaves out a <c>MenuItem</c>, whose header is its
/// label. A tooltip counts in any framework's spelling, as an attribute or as a property element that sets
/// something, and a blank one is reported as empty, which the scan it replaced passed. A control in a
/// <c>ControlTheme</c>'s template is checked where it is written, which is where every input control of the
/// library's is.
/// </para>
/// <para>
/// ⛔ <b>Only markup.</b> A control built in code, or a tooltip set in code, is not seen, as it was not before.
/// </para>
/// </remarks>
public sealed class TooltipCoverageTests
{
    /// <summary>
    /// The input controls in the library's markup: its find bar's, its status strip's and each view's banner action.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A floor at the population, as the scan this class held set it.</b> A control taken away trips it as
    /// surely as markup the scan stopped reaching, so the floor moves in the change that takes one away.
    /// </remarks>
    private const int LibraryFloor = 15;

    /// <summary>The review window's seven buttons: a floor of the same kind as <see cref="LibraryFloor"/>.</summary>
    private const int BaselineFloor = 7;

    [Fact]
    public void Every_input_control_the_repository_writes_in_markup_carries_a_tooltip()
    {
        // ⛔ Each subject is derived from the one root, and the three account for the whole scan, as in
        // AccessibilityCoverageTests: a floor is satisfied by a subject that has swallowed the scan, and the
        // accounting below is false of one.
        XamlScanContext all = XamlScanContext.Load(RepoPaths.Source("src"));
        InputTooltipRule rule = new();
        XamlRuleResult result = rule.Analyze(all);

        // ⛔ Skipped FIRST. A control with no tooltip of its own that a style or a theme in the scan may give one is
        // named there rather than checked, and a tooltip a selector may or may not deliver is not one to count on.
        Assert.True(
            result.Skipped.Count == 0,
            "BNXQ1011 found input controls with no tooltip of their own that a style or a theme may give one, which "
            + "markup cannot decide, so it could not check them. Give each a tooltip of its own:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Skipped.Select(s => "  " + s)));

        int library = rule.Analyze(XamlScanContext.Load(Path.Combine(all.Root, "DiffView.Avalonia"))).Inspected;
        Assert.True(
            library >= LibraryFloor,
            $"BNXQ1011 inspected {library} input controls in the library's markup, which holds {LibraryFloor}: the scan "
            + "is not reading what it should, and a clean result would mean nothing.");

        int baseline = rule.Analyze(XamlScanContext.Load(Path.Combine(all.Root, "AgentBaseline"))).Inspected;
        Assert.True(
            baseline >= BaselineFloor,
            $"BNXQ1011 inspected {baseline} input controls in the review window's markup, which holds {BaselineFloor}: "
            + "the scan is not reading what it should, and a clean result would mean nothing.");

        int demo = rule.Analyze(XamlScanContext.Load(Path.Combine(all.Root, "DiffView.Demo"))).Inspected;
        Assert.True(
            library + demo + baseline == result.Inspected,
            $"The library's {library} input controls, the demo's {demo} and the review window's {baseline} do not "
            + $"account for the {result.Inspected} the whole scan inspected. More means one subject has swallowed "
            + "another, which no floor can see; fewer means markup under src that no subject holds, which wants a "
            + "subject and a floor of its own.");

        Assert.True(
            result.Findings.Count == 0,
            "Input controls with no tooltip, or an empty one, so nothing tells the person pointing at them what they "
            + "do:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }
}
