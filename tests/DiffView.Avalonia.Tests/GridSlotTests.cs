using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// No control declares a size larger than the <b>fixed</b> grid slot it sits in.
/// </summary>
/// <remarks>
/// <para>
/// <c>BNXQ1004</c>, from <c>Bennewitz.Ninja.XamlQuality</c>. ⭐ <b>The defect is in the automation tree,
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
/// ⭐ <b>Adopted at <c>2026.3.924</c>, the first published release carrying it</b>, where it measured
/// 22 inspected, 0 findings, 0 skipped. From <c>2026.3.925</c> it counts a placement only when it
/// measured it against a fixed slot, and none here is — the spacer's children declare no size — so it
/// reads <b>0 inspected</b> on today's markup, which is also what a scan that reached no markup reads.
/// ⛔ <b>Reading 0 is not being unable to fail.</b> A child that asks the spacer for more room is still
/// caught — 1 inspected, 1 finding — and one whose size markup cannot evaluate is named in
/// <see cref="XamlRuleResult.Skipped"/>; both are measured by mutations. What 925 took away is only the
/// footing of the blinding floor, so the floor moved from the rule's count to the markup the scan holds.
/// Declining an <em>inert</em> rule and declining a <em>live</em> one are different acts:
/// <c>BNXQ1001</c> has nothing here to read at all, where this one has a fixed slot and its children.
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
    /// Well under the 37 grid children the scan holds, because what this number guards is a <b>zero</b>:
    /// a scan that reached no markup.
    /// </summary>
    /// <remarks>
    /// A grid child is a direct element child of a <c>Grid</c> that is not a property element —
    /// <c>Grid.ColumnDefinitions</c> and its like are not children — counted in every file the scan parsed,
    /// which is the markup the rule reads. ⛔ <b>A blinding floor, like <see cref="TemplatePartTests"/>'s —
    /// not a population count.</b> From 925 the rule's own count reads 0 on a clean repository, so what
    /// shows it had something to read is the scan's markup. Set at its exact population it would demand
    /// a visible edit every time a grid child is legitimately added or removed, and buy no detection for
    /// it.
    /// </remarks>
    private const int GridChildrenFloor = 20;

    [Fact]
    public void No_control_declares_a_size_larger_than_its_fixed_grid_slot()
    {
        XamlScanContext context = XamlScanContext.Load(RepoPaths.Source("src"));
        int gridChildren = GridChildren(context);

        Assert.True(
            gridChildren >= GridChildrenFloor,
            $"The scan holds {gridChildren} grid children, below the floor of {GridChildrenFloor}. This floor "
            + "sits well under the population, so it has not been tripped by a child being added or removed: "
            + "the scan has largely stopped reaching the markup. Zero means it reached none, where BNXQ1004 "
            + "reports 0 inspected and 0 findings — which is what a repository whose grids agree reports too.");

        XamlRuleResult result = new GridSlotOverflowRule().Analyze(context);

        // Live from 2026.3.925: a child of a fixed slot whose size markup cannot evaluate is named here
        // rather than counted as checked.
        Assert.True(
            result.Skipped.Count == 0,
            "BNXQ1004 could not evaluate the size some children of a fixed slot declare, so it could not "
            + "check them:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Skipped.Select(s => "  " + s)));

        Assert.True(
            result.Findings.Count == 0,
            "A control asks for more room than its fixed grid slot gives it. It is arranged at the size "
            + "it asked for and keeps that size in the automation tree, so a harness or a screen reader "
            + "sees a region the user cannot:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }

    /// <summary>The grid children in the markup the scan parsed: every direct element child of a <c>Grid</c> that is not a property element.</summary>
    private static int GridChildren(XamlScanContext context) =>
        context.ParsedFiles
            .Select(f => f.Document)
            .Where(d => d is not null)
            .SelectMany(d => d!.Descendants())
            .Count(e => e.Parent is { } parent && parent.Name.LocalName == "Grid" && !e.Name.LocalName.Contains('.'));
}
