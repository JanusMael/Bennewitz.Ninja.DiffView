using System.Reflection;
using Bennewitz.Ninja.AssemblyQuality;
using Bennewitz.Ninja.AssemblyQuality.Rules;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// What the shipped assemblies expose and what they bind against, read by reflection over the
/// compiled output rather than the source — which is the thing a consumer actually binds to.
/// </summary>
/// <remarks>
/// ⛔ <b>No rule's <c>Inspected</c> here can tell a live configuration from a dead one.</b>
/// <c>SurfaceLeakRule</c>, <c>ForbiddenReferenceRule</c> and <c>CancellationTokenRule</c> all
/// increment it *before* testing their predicate, so a misspelled prefix — or a predicate that has
/// stopped firing — reports the same numbers as a clean assembly. Each gate below therefore carries a
/// positive control that must produce a finding, using the **same rule instance** — a control built
/// from a different prefix proves only that the library works.
/// <para>
/// ⚠ <b>That holds of <c>AQ1001</c> as much as of the other two.</b> Measured reporting
/// <c>inspected=2 findings=1</c> over a fixture, its <c>Inspected</c> is a candidate count like the
/// others', so a dead predicate over <c>DiffView.Core</c> would report today's <c>2 / 0</c> exactly.
/// <see cref="TokenDefaultControl"/> is its control.
/// </para>
/// </remarks>
public sealed class AssemblyQualityTests
{
    /// <summary>The diff engine. Public on <c>DiffView.Core</c>'s surface it would bind every consumer to DiffPlex.</summary>
    private const string DiffEngine = "DiffPlex";

    /// <summary>The UI framework. <c>DiffView.Core</c> is the half that must not know about it.</summary>
    private const string UiFramework = "Avalonia";

    /// <summary>A scan of the one assembly with this simple name.</summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Each gate resolves its subject into one local and uses that local for every guard and
    /// for the scan.</b> Two weaker shapes were measured and both leave a gate green over the wrong
    /// assembly: a separate test asserting the two names, and a helper returning a checked
    /// <see cref="Assembly"/>. In each, the guard reads one expression and the rule reads another.
    /// </para>
    /// <para>
    /// ⚠ <b>What closes it is not this helper.</b> A helper validates whatever subject it is asked
    /// for, so <c>Analyze(Scan("DiffView.Avalonia"))</c> passes — measured. What makes the gates
    /// below unable to report on the wrong assembly is that each guards a property true of its
    /// subject and false of the other: <c>DiffView.Core</c> references DiffPlex and does not
    /// reference Avalonia. Change the local and those guards fail. The helper's job is only to make
    /// the subject appear once.
    /// </para>
    /// </remarks>
    private static AssemblyScanContext Scan(string simpleName)
    {
        Assembly? assembly = simpleName switch
        {
            "DiffView.Core" => typeof(FindScope).Assembly,
            "DiffView.Avalonia" => typeof(SideBySideDiffView).Assembly,
            _ => null,
        };

        // An assertion rather than a `throw`, so the mutation harness can attribute a failure to it.
        Assert.True(assembly is not null, $"'{simpleName}' is not an assembly this suite gates.");

        AssemblyScanContext scan = AssemblyScanContext.Of(assembly!);
        Assert.Equal([simpleName], scan.Assemblies.Select(a => a.GetName().Name));
        return scan;
    }

    /// <summary>
    /// A public member naming a DiffPlex type, so the gate below has something its own rule
    /// instance must find. Deleting <c>"DiffPlex"</c> from that instance turns this red.
    /// </summary>
    public static class LeakControl
    {
        /// <summary>Never called; its signature is the whole point.</summary>
        public static DiffPlex.Model.DiffResult? Leaks() => null;
    }

    /// <summary>
    /// A public method defaulting its <see cref="CancellationToken"/>, so <c>AQ1001</c>'s gate has
    /// something its own rule instance must find. Removing the default turns that control red.
    /// </summary>
    public static class TokenDefaultControl
    {
        /// <summary>Never called; its signature is the whole point.</summary>
        public static void Defaults(CancellationToken cancellationToken = default)
        {
        }
    }

    [Fact]
    public void The_model_does_not_expose_the_diff_engine_on_its_public_surface()
    {
        AssemblyScanContext model = Scan("DiffView.Core");
        SurfaceLeakRule rule = new([DiffEngine]);

        // ⭐ Two jobs. A misspelled prefix would report clean over a clean assembly, which is
        // indistinguishable from success — and because this reads the same local the rule reads,
        // pointing the gate at the UI assembly fails here first: that one does not reference
        // DiffPlex. Measured both ways.
        Assert.Contains(DiffEngine, model.Assemblies[0].GetReferencedAssemblies().Select(a => a.Name));

        // ⛔ But that guard is a coincidence rather than a property. The UI assembly does not
        // reference DiffPlex *today*; DiffPlex is already a declared dependency of its package, so one
        // `using DiffPlex.Model;` there emits the reference with no csproj change, and the line above
        // stops telling the two subjects apart while this gate stays green. What cannot drift that way
        // is the negative, because AQ1003 in this same file is what holds it: the model references no
        // UI framework, and the assembly a swap would reach references three.
        Assert.DoesNotContain(
            model.Assemblies[0].GetReferencedAssemblies(),
            a => a.Name?.StartsWith(UiFramework, StringComparison.Ordinal) == true);

        AssemblyRuleResult result = rule.Analyze(model);
        Assert.True(result.Inspected > 0, "AQ1002 examined no signatures at all.");
        Assert.True(
            result.Findings.Count == 0,
            "The model's public surface names a DiffPlex type:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));

        // The control: the same instance, over an assembly that does leak one.
        // ⚠ Anchored to the fixture, not merely non-empty. The rule concatenates its stock
        // namespaces, so some future public test helper naming System.Text.Json.Nodes would satisfy
        // a count-only control with LeakControl deleted, and the fixture would rot unnoticed.
        AssemblyRuleResult control = rule.Analyze(AssemblyScanContext.Of(typeof(LeakControl).Assembly));
        Assert.Contains(
            control.Findings,
            f => f.Subject.Contains(nameof(LeakControl), StringComparison.Ordinal));
    }

    [Fact]
    public void The_model_layer_binds_against_no_ui_framework()
    {
        AssemblyScanContext model = Scan("DiffView.Core");
        AssemblyScanContext ui = Scan("DiffView.Avalonia");
        ForbiddenReferenceRule rule = new([UiFramework]);

        // The prefix must name something the control's scope really references, or the control
        // proves nothing about the spelling.
        Assert.Contains(ui.Assemblies[0].GetReferencedAssemblies(), a => a.Name?.StartsWith(UiFramework, StringComparison.Ordinal) == true);

        AssemblyRuleResult result = rule.Analyze(model);
        Assert.True(result.Inspected > 0, "AQ1003 examined no references at all.");
        Assert.True(
            result.Findings.Count == 0,
            "The model layer references the UI framework:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));

        // The control: the same instance over the layer that legitimately *is* Avalonia.
        AssemblyRuleResult control = rule.Analyze(ui);
        Assert.True(
            control.Findings.Count > 0,
            $"The positive control found nothing, so '{UiFramework}' is not a prefix this rule can act on.");
    }

    /// <summary>
    /// ⚠ <b>Reports today, and that is the point of the gate.</b> Adopting <c>AQ1001</c> means the
    /// two public entry points take a <see cref="CancellationToken"/> the caller had to write.
    /// </summary>
    [Fact]
    public void No_public_entry_point_defaults_its_cancellation_token()
    {
        AssemblyScanContext model = Scan("DiffView.Core");

        // ⛔ Not `Inspected > 0` alone. That catches a swapped subject only while the UI assembly
        // happens to expose no public method taking a token — a coincidence of today's API, not a
        // property. One ordinary `RefreshAsync(CancellationToken)` on a view and the swap survives.
        Assert.DoesNotContain(
            model.Assemblies[0].GetReferencedAssemblies(),
            a => a.Name?.StartsWith(UiFramework, StringComparison.Ordinal) == true);

        CancellationTokenRule rule = new();
        AssemblyRuleResult result = rule.Analyze(model);

        Assert.True(result.Inspected > 0, "AQ1001 examined no tokens at all — it has stopped looking.");
        Assert.True(
            result.Findings.Count == 0,
            "A public method defaults its CancellationToken:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));

        // ⛔ The standing control. AQ1001's Inspected is a candidate count like the other two rules', so
        // a predicate that has stopped firing reports today's 2 / 0 exactly — and the risk is a pin bump,
        // which a one-time mutation such as "a new method with a defaulted token" never sees again.
        AssemblyRuleResult control = rule.Analyze(AssemblyScanContext.Of(typeof(TokenDefaultControl).Assembly));
        Assert.Contains(
            control.Findings,
            f => f.Subject.Contains(nameof(TokenDefaultControl), StringComparison.Ordinal));
    }
}
