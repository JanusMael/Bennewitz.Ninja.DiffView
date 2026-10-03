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
/// ⚠ <b>Two of the three assemblies are load-bearing.</b> The UI assembly alone yields 47, and
/// AvaloniaEdit's adds the one part its own code looks up on a template this library ships, for 48;
/// the model assembly alone yields 0, having no templated controls. The model is passed as forward
/// cover — the same reasoning that keeps inert names in the accessibility gate's set — and that gate
/// holds its forward cover to the <em>inverse</em> assertion, so this one does too rather than leaving
/// the claim as a remark nothing checks.
/// </para>
/// <para>
/// ⛔ <b>At <c>924</c> the blinding has a direct signal, and the floor is no longer the only guard.</b>
/// Handed no assemblies the rule used to return a silent <c>Clean(0)</c>; <c>52e481c</c> populates
/// <see cref="XamlRuleResult.Skipped"/> with every themed control it could not check.
/// <para>
/// ⚠ <b>But "<c>Skipped</c> is empty" is the wrong assertion.</b> A themed
/// control that declares no template parts is skipped legitimately, and two of this library's do. What
/// separates the blinding is <em>which</em> controls: every themed control is skipped with no
/// assemblies; in the real configuration only those two are, beside five of AvaloniaEdit's whose themes
/// ship with AvaloniaEdit rather than in this markup. So the assertion names all seven, keyed on
/// <see cref="XamlSkip.Subject"/> rather than the prose in <see cref="XamlSkip.Reason"/>.
/// </para>
/// </para>
/// <para>
/// ⭐ <b>From <c>2026.3.925</c> the rule credits a lookup to the control whose template it is made
/// on</b> (XamlQuality #30, proposed from here). <see cref="DiffBuildController"/> looks its parts up on
/// its host's template, so each of those lookups is now checked against each host's own theme, and a
/// part the viewer's theme or the editor's lacks is a finding like any other. At <c>924</c> the rule
/// credited the lookup to the controller, which has no theme, skipped it, and this test held every
/// host's constants against the controller's parts by hand; that assertion retired with the pin.
/// </para>
/// <para>
/// ⭐ <b>AvaloniaEdit's assembly is handed in too, so the parts AvaloniaEdit's own code looks up on this
/// library's templates are checked.</b> The rule credits a lookup that a base class makes in its
/// <c>OnApplyTemplate</c> to the control and checks it against that control's theme, but it reads only
/// the code it is handed. The theme <see cref="DiffPanePresenter"/> applies to its text area is this
/// library's, and AvaloniaEdit's <c>TextArea</c> looks up <c>PART_CP</c> on it — the presenter its text
/// view is shown in, behind a null check, so a theme without it shows no text and says nothing. Measured
/// at <c>928</c> over a text area theme that drops <c>PART_CP</c>: one finding with AvaloniaEdit's
/// assembly, none without it.
/// </para>
/// </remarks>
public sealed class TemplatePartTests
{
    /// <summary>
    /// Well under the 48 the rule inspects today — <c>DiffFindBar</c> 11, <c>InlineDiffView</c> 8,
    /// <c>SideBySideDiffView</c> 14, <c>DiffViewer</c> 13, one literal inlined at its use site, and the
    /// part AvaloniaEdit's <c>TextArea</c> looks up on the text area theme — because what this number
    /// guards is a zero, not a part count.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Deliberately slack, like the accessibility gate's stock floor.</b>
    /// <see cref="TemplatePartRule"/> returns <c>Clean(0)</c> when it is handed no assemblies, so the
    /// failure this number exists to catch is 48 dropping to 0 — and nothing else here can see that,
    /// because a re-rooted scan measures the same 48 (parts come from compiled code, not from the
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
            .WithAssemblies(
                typeof(FindScope).Assembly,
                typeof(SideBySideDiffView).Assembly,
                typeof(AvaloniaEdit.TextEditor).Assembly);

        XamlRuleResult result = new TemplatePartRule().Analyze(context);

        Assert.True(
            result.Inspected >= InspectedFloor,
            $"BNXQ1003 inspected {result.Inspected} template parts, below the floor of {InspectedFloor}. "
            + "This floor sits well under the population, so it has not been tripped by a part being "
            + "added or removed: the rule has largely stopped seeing parts. Zero in particular means "
            + "the scan carries no assemblies — TemplatePartRule reports Clean(0) in that case, which "
            + "is indistinguishable from a codebase whose parts all agree.");

        // ⛔ The precise form of the same guard, available since 2026.3.924 — and ⚠ NOT "Skipped is
        // empty", which is wrong. Measured: a themed control with no template parts
        // at all is skipped legitimately, and two of ours are. What distinguishes the blinding is WHICH
        // controls: handed no assemblies the rule skips every themed control, where the real
        // configuration skips only those two, beside five of AvaloniaEdit's that declare parts but whose
        // themes ship with AvaloniaEdit rather than in this markup. Keyed on Subject rather than on
        // Reason, which is prose.
        //
        // These are a written expectation like a floor, but a far narrower one. DiffPaneHeader and
        // DiffStatusStrip are each the separately checkable claim "this control has a ControlTheme and
        // declares no parts". A new name appearing means a control lost its parts, the scan lost an
        // assembly, or the model gained a control with no theme — measured; a name disappearing means one
        // gained parts, which is a real change and should need a visible edit. AvaloniaEdit's five vanish
        // together when its assembly is not handed in — the floor cannot see that, its one part being
        // well inside the slack — and an AvaloniaEdit bump that adds a control declaring parts adds a name.
        Assert.Equal(
            [
                nameof(AvaloniaEdit.CodeCompletion.CompletionList),
                nameof(AvaloniaEdit.CodeCompletion.CompletionListBox),
                nameof(DiffPaneHeader),
                nameof(DiffStatusStrip),
                nameof(AvaloniaEdit.CodeCompletion.OverloadViewer),
                nameof(AvaloniaEdit.Search.SearchPanel),
                nameof(AvaloniaEdit.TextEditor),
            ],
            result.Skipped.Select(s => s.Subject).OrderBy(s => s, StringComparer.Ordinal));

        // The controller's lookups are findings here too: from 2026.3.925 the rule checks each one
        // against the theme of every control whose template it runs on. So are AvaloniaEdit's, on the
        // templates this library ships.
        Assert.True(
            result.Findings.Count == 0,
            "A control looks up a template part its own theme does not declare:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }
}
