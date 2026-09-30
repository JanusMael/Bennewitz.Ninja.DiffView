using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// No control declares a size larger than the <b>fixed</b> grid slot it sits in, and none is hidden by a
/// slot of no size alone.
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
/// and <c>Auto</c> against its content, so neither can be judged from markup. The fixed slots in the
/// library's own grids are the 16-pixel spacer columns between the two panes and between their headers,
/// in both two-sided views, and the unified view's 1-pixel divider between its headers.
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
/// <para>
/// <c>BNXQ1008</c>, adopted in plan 00026, is <c>BNXQ1004</c>'s other half by XamlQuality's account: a
/// control that asks for <em>no</em> size, in a slot that has none. ⭐ <b>A slot of no size hides a control
/// from people, not from a harness.</b> The control is arranged empty, stays effectively visible, and its
/// peer stays in the automation tree, so a harness finds a part nobody can see. What leaves the tree is a
/// control hidden through <c>IsVisible</c>, which is how every part this library shows only on request is
/// hidden. It reads the library's markup: 5 controls sized at <c>2026.3.928</c> — the ones in the fixed
/// slots above, <c>Auto</c> and <c>*</c> stating no size — and 0 findings. Unlike <c>BNXQ1004</c> it
/// counts a control in a slot with room, so its own count is what shows it read the markup.
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

    /// <summary>
    /// Well under the 5 controls whose slot <c>BNXQ1008</c> sizes in the library's markup, because what this
    /// number guards is a <b>zero</b>: a scan that reached no markup.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>A blinding floor, like <see cref="GridChildrenFloor"/> — not a population count.</b> The five are
    /// the controls in the library's fixed slots, so a spacer added or taken away moves the count as an
    /// ordinary product change, and a floor at the population would fire on it naming a cause it does not
    /// establish.
    /// </remarks>
    private const int SizedSlotsFloor = 2;

    [Fact]
    public void No_control_is_hidden_by_a_slot_of_no_size_alone()
    {
        // ⚠ The library's markup, as plan 00026 adopts the rule, and spelled apart from the scan above: that
        // one's markup-free-ground mutation matches its path exactly once.
        XamlScanContext library = XamlScanContext.Load(RepoPaths.Source(Path.Combine("src", "DiffView.Avalonia")));
        XamlRuleResult result = new ZeroSizeSlotRule().Analyze(library);

        // ⛔ Skipped FIRST. A slot whose size markup cannot evaluate is named there rather than checked, and a
        // control the rule could not size is not a control it found hidden properly.
        Assert.True(
            result.Skipped.Count == 0,
            "BNXQ1008 could not evaluate the size of the slots some controls sit in, so it could not check "
            + "them:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Skipped.Select(s => "  " + s)));

        Assert.True(
            result.Inspected >= SizedSlotsFloor,
            $"BNXQ1008 sized the slots of {result.Inspected} controls in the library's markup, below the floor "
            + $"of {SizedSlotsFloor}. The floor sits well under the population, so no spacer was merely added or "
            + "removed: the scan has lost the markup it reads, and a clean result would mean nothing.");

        Assert.True(
            result.Findings.Count == 0,
            "A control sits in a grid slot of no size and is not hidden. Nobody can see it, but its peer stays "
            + "in the automation tree, so a harness finds a part that is not there — hide it with IsVisible:"
            + Environment.NewLine
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
