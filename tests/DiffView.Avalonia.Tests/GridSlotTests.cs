using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// No control declares a size larger than the <b>fixed</b> grid slot it sits in.
/// </summary>
/// <remarks>
/// <para>
/// <c>XQ1004</c>, from <c>Bennewitz.Ninja.XamlQuality</c>. ⭐ <b>The defect is in the automation tree,
/// not on screen.</b> A child whose own <c>MinHeight</c> exceeds its row's fixed height is arranged at
/// the size it asked for and keeps that size in the automation tree, so a UI harness or a screen reader
/// sees a pane the user cannot. Whether it is also <em>drawn</em> depends on the container —
/// <c>ContentControl</c> clips by default and <c>Border</c> does not — which is why the rule does not
/// ask that question.
/// </para>
/// <para>
/// ⚠ <b>Only fixed slot sizes are decidable</b>, which is most of what this repository's grids use
/// <c>*</c> and <c>Auto</c> for. A <c>*</c> row resolves against its siblings and the available space,
/// and <c>Auto</c> against its content, so neither can be judged from markup. The one fixed slot in the
/// library's own grids is the 16-pixel spacer column between the two panes.
/// </para>
/// <para>
/// ⭐ <b>Adopted at <c>2026.3.924</c>, the first published release carrying it</b>, where it measures
/// <b>22 inspected, 0 findings, 0 skipped</b>, all 22 in this library's own themes and none in the demo.
/// Declining an <em>inert</em> rule and declining a <em>live</em> one are different acts: <c>XQ1001</c>
/// inspects nothing here and could only report what a broken codebase also reports, where this one
/// inspects 22 real placements and can be made to fail.
/// </para>
/// <para>
/// ⛔ <b>The rule asks about SIZE, not about indices</b> — a child placed in a column its grid does not
/// define is not its concern. Measured: <c>Grid.Column="9"</c> on a five-column grid survives this gate.
/// A gate adopted on a description of a defect it cannot see is precisely what plan 00025 exists to
/// refuse, and only a mutation separates the two.
/// </para>
/// </remarks>
public sealed class GridSlotTests
{
    /// <summary>
    /// Well under the 22 the rule inspects, because what this number guards is a <b>zero</b>.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A blinding floor, like <see cref="TemplatePartTests"/>'s — not a population count.</b> This
    /// rule reads markup and no assemblies, so the way to blind it is to move the scan root somewhere
    /// with no markup, and then it reports 0 inspected and 0 findings, which is what a clean repository
    /// reports. Set at its exact population it would instead demand a visible edit every time a grid
    /// child is legitimately added or removed, and buy no detection for it: a partial loss of placements
    /// is caught by nothing either way.
    /// </remarks>
    private const int InspectedFloor = 12;

    [Fact]
    public void No_control_declares_a_size_larger_than_its_fixed_grid_slot()
    {
        XamlRuleResult result = new GridSlotOverflowRule().Analyze(XamlScanContext.Load(RepoPaths.Source("src")));

        Assert.True(
            result.Inspected >= InspectedFloor,
            $"XQ1004 inspected {result.Inspected} grid placements, below the floor of {InspectedFloor}. "
            + "This floor sits well under the population, so it has not been tripped by a child being "
            + "added or removed: the rule has largely stopped seeing placements. Zero means the scan "
            + "reached no markup at all, which is indistinguishable from a repository whose grids agree.");

        // ⚠ FORWARD COVER, and it cannot fail at this pin. Measured over bindings, resources, unparseable
        // and bound definitions, a bound index, a bound size and a span: the pinned rule never populates
        // Skipped — an undecidable placement is counted as inspected and reported clean instead. So this
        // guards the release that starts reporting grids it could not read, when the count above would
        // stop meaning what this test says it means. The marker below is what the mutation harness reads:
        // it excuses this one guard for as long as the pin is the one it was measured against, and not
        // a version longer. When the pin moves, scripts/xq1004-skips.sh re-measures the claim against
        // whatever is pinned: exit 0 means re-mark it with the new version, exit 1 means it is owed a
        // mutation instead.
        // inert-at-pin: Bennewitz.Ninja.XamlQuality 2026.3.924
        Assert.True(
            result.Skipped.Count == 0,
            "XQ1004 could not read some grids, so the placements inside them went unchecked:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, result.Skipped.Select(s => "  " + s)));

        Assert.True(
            result.Findings.Count == 0,
            "A control asks for more room than its fixed grid slot gives it. It is arranged at the size "
            + "it asked for and keeps that size in the automation tree, so a harness or a screen reader "
            + "sees a region the user cannot:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }
}
