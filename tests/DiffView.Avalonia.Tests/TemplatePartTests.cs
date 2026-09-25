using System.Reflection;
using System.Text.RegularExpressions;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Every template part a control looks up by name is declared in the theme that control ships
/// with. Half of that contract is code and half is markup, and neither half alone says whether
/// they agree — which is the class of defect <c>AGENTS.md</c> §6 records for
/// <c>SideBySideDiffView</c>'s parts.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b><c>.WithAssemblies(…)</c> is not optional and its absence is silent.</b>
/// <c>TemplatePartRule.Analyze</c> returns <c>Clean(0)</c> when the context carries no assemblies —
/// no exception, no warning, just a rule that inspected nothing and reports success. Copying the
/// accessibility gate's <c>XamlScanContext.Load(…)</c> without this call yields exactly that, which
/// is why the floor below is asserted and not merely the findings.
/// </para>
/// <para>
/// ⭐ <b>The rule reads parts from compiled code as of <c>1d9ccac</c>, shipped in
/// <c>2026.3.924</c>.</b> At <c>922</c> it read public string constants only, so an inlined literal —
/// <c>DiffPanePresenter</c>'s <c>NameScope.Find&lt;ScrollViewer&gt;("PART_ScrollViewer")</c> — was
/// invisible and the count was 33. Reading literals took it to <b>34</b>, and the floor needed no
/// edit for that, which is the point of setting it well below the population. Promoting the literal
/// to a <c>public const</c> would also have reached 34, at the price of permanent public surface on a
/// shipping control, and was declined for that reason.
/// </para>
/// <para>
/// ⚠ <b>Only one of the two assemblies is load-bearing.</b> The UI assembly alone yields the same 47;
/// the model assembly alone yields 0, having no templated controls. It is passed as forward cover —
/// the same reasoning that keeps inert names in the accessibility gate's set — and that gate holds its
/// forward cover to the <em>inverse</em> assertion, so this one does too rather than leaving the claim
/// as a remark nothing checks.
/// </para>
/// <para>
/// ⛔ <b>At <c>924</c> the blinding has a direct signal, and the floor is no longer the only guard.</b>
/// Handed no assemblies the rule used to return a silent <c>Clean(0)</c>; <c>52e481c</c> populates
/// <see cref="XamlRuleResult.Skipped"/> with every themed control it could not check.
/// <para>
/// ⚠ <b>But "<c>Skipped</c> is empty" is the wrong assertion.</b> A themed
/// control that declares no template parts is skipped legitimately, and two of this library's do. What
/// separates the blinding is <em>which</em> controls: eight skipped with no assemblies, only those two
/// in the real configuration — beside <see cref="DiffBuildController"/>, skipped for the opposite
/// reason: parts, and no theme of its own. So the assertion names all three, keyed on
/// <see cref="XamlSkip.Subject"/> rather than the prose in <see cref="XamlSkip.Reason"/>; the
/// controller's parts are then read out of that prose, the only place the rule reports them.
/// </para>
/// </para>
/// </remarks>
public sealed class TemplatePartTests
{
    /// <summary>
    /// Well under the 47 the rule inspects today — <c>DiffFindBar</c> 11, <c>InlineDiffView</c> 8,
    /// <c>SideBySideDiffView</c> 14, <c>DiffViewer</c> 13, and one literal inlined at its use site —
    /// because what this number guards is a zero, not a part count.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Deliberately slack, like the accessibility gate's stock floor.</b>
    /// <see cref="TemplatePartRule"/> returns <c>Clean(0)</c> when it is handed no assemblies, so the
    /// failure this number exists to catch is 47 dropping to 0 — and nothing else here can see that,
    /// because a re-rooted scan measures the same 47 (parts come from compiled code, not from the
    /// markup root). Set at its exact population it would instead demand a visible edit every time a
    /// part is legitimately added or removed, and it buys no detection to pay that: a partial loss of
    /// parts is caught by nothing either way.
    /// </remarks>
    private const int InspectedFloor = 20;

    [Fact]
    public void Every_template_part_a_control_looks_up_is_declared_in_its_theme()
    {
        // ⚠ The inverse half first: the model assembly is forward cover and contributes nothing today.
        // If it ever does, it has stopped being free and the floor below no longer measures what this
        // test says it measures. It comes FIRST because anything that makes the model contribute a part
        // also puts an undeclared part in the main scan, and the findings check would stop there first.
        //
        // ⛔ This does NOT make dropping the assembly detectable, and no assertion can: a thing that
        // contributes nothing contributes nothing whether or not it is passed. That is what forward cover
        // is, here and in the accessibility gate, where dropping an inert name is equally invisible. What
        // is detectable — and what both gates assert — is the moment it stops being inert.
        XamlRuleResult modelOnly = new TemplatePartRule().Analyze(
            XamlScanContext.Load(RepoPaths.Source("src")).WithAssemblies(typeof(FindScope).Assembly));

        Assert.True(
            modelOnly.Inspected == 0,
            $"The model assembly now declares {modelOnly.Inspected} template part(s). It is passed below as "
            + "forward cover, on the understanding that it declares none — so it is no longer inert and "
            + "the floor no longer accounts for what it contributes.");

        XamlScanContext context = XamlScanContext
            .Load(RepoPaths.Source("src"))
            .WithAssemblies(typeof(FindScope).Assembly, typeof(SideBySideDiffView).Assembly);

        XamlRuleResult result = new TemplatePartRule().Analyze(context);

        Assert.True(
            result.Inspected >= InspectedFloor,
            $"XQ1003 inspected {result.Inspected} template parts, below the floor of {InspectedFloor}. "
            + "This floor sits well under the population, so it has not been tripped by a part being "
            + "added or removed: the rule has largely stopped seeing parts. Zero in particular means "
            + "the scan carries no assemblies — TemplatePartRule reports Clean(0) in that case, which "
            + "is indistinguishable from a codebase whose parts all agree.");

        // ⛔ The precise form of the same guard, available since 2026.3.924 — and ⚠ NOT "Skipped is
        // empty", which is wrong. Measured: a themed control with no template parts
        // at all is skipped legitimately, and two of ours are. What distinguishes the blinding is WHICH
        // controls: handed no assemblies the rule skips all eight themed controls, where the real
        // configuration skips only those two. Keyed on Subject rather than on Reason, which is prose.
        //
        // These are a written expectation like a floor, but a far narrower one. DiffPaneHeader and
        // DiffStatusStrip are each the separately checkable claim "this control has a ControlTheme and
        // declares no parts". A new name appearing means a control lost its parts, the scan lost an
        // assembly, or the model gained a control with no theme — measured; a name disappearing means one
        // gained parts, which is a real change and should need a visible edit.
        //
        // DiffBuildController is a skip of the other kind: parts and no theme. It looks parts up on its
        // HOST's template, and the rule takes every lookup to be on the looking type's own, so it finds
        // parts, looks for a ControlTheme targeting the controller, finds none, and names the parts it
        // therefore did not check.
        Assert.Equal(
            [nameof(DiffBuildController), nameof(DiffPaneHeader), nameof(DiffStatusStrip)],
            result.Skipped.Select(s => s.Subject).OrderBy(s => s, StringComparer.Ordinal));

        // ⛔ What makes that skip harmless, held rather than assumed: every part the controller looks up
        // is a constant of EVERY control that hosts it, and the rule checks each host's constants against
        // that host's own theme. The controller's lookups run on whichever template it was handed, so a
        // part one host declares and another does not is looked up in a theme nobody checked. The parts
        // are read from the skip itself — the rule's reading of the compiled lookups, not a list of ours —
        // and the hosts from the assembly, as whatever implements the seam. The two counts are the
        // anti-vacuity half: should a later rule stop naming the parts in its reason, or the derivation
        // find no host, an empty set would satisfy the rest.
        string[] controllerParts = PartsNamedIn(result.Skipped.Single(s => s.Subject == nameof(DiffBuildController)));
        Type[] hosts = HostsOfTheController();
        string[] gaps =
        [
            .. hosts.SelectMany(host => controllerParts
                .Except(PartConstantsOf(host), StringComparer.Ordinal)
                .Select(part => $"{host.Name} does not declare {part}")),
        ];
        Assert.True(
            controllerParts.Length > 0 && hosts.Length > 0 && gaps.Length == 0,
            $"DiffBuildController looks up [{string.Join(", ", controllerParts)}] on the template of each of "
            + $"[{string.Join(", ", hosts.Select(h => h.Name))}]. A part it looks up that a host does not "
            + "declare as a constant is checked against no theme: the rule credits it to the controller, "
            + "which has none. Declare it on the host:" + Environment.NewLine
            + string.Join(Environment.NewLine, gaps.Select(g => "  " + g)));

        Assert.True(
            result.Findings.Count == 0,
            "A control looks up a template part its own theme does not declare:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }

    /// <summary>Every control the library builds on <see cref="DiffBuildController"/>: whatever implements its seam.</summary>
    private static Type[] HostsOfTheController() =>
        [.. typeof(SideBySideDiffView).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDiffSurface).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// <summary>The part names a skip's reason carries — the only place the rule reports them.</summary>
    private static string[] PartsNamedIn(XamlSkip skip) =>
        [.. Regex.Matches(skip.Reason, @"\bPART_\w+").Select(m => m.Value).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The part-name constants a type declares, public or not — the source the rule credits to that
    /// type, and so checks against that type's theme, wherever the lookups themselves are made.
    /// </summary>
    private static string[] PartConstantsOf(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => v.Length > TemplatePartRule.PartPrefix.Length
                && v.StartsWith(TemplatePartRule.PartPrefix, StringComparison.Ordinal))];
}
