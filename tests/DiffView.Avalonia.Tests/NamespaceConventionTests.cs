using System.Reflection;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00017's convention: every namespace this repository authors is under <see cref="Prefix"/>, and
/// nothing else in the shipped assemblies is anything but generated.
/// </summary>
/// <remarks>
/// <para>
/// The convention exists because of shadowing. C# resolves the first identifier of a qualified name by
/// walking outward through the enclosing namespaces before it reaches global, so inside
/// <c>Bennewitz.Ninja.DiffView.Avalonia</c> — which is what this library was called until plan 00017 —
/// the name <c>Avalonia</c> found *us* first, and <c>Avalonia.Media.Color</c> failed to compile with
/// CS0234. The tree carried the scar in <see cref="DiffCommand"/>, whose documentation needed
/// <c>global::</c> to name a <see cref="Avalonia.Input.KeyBinding"/> — a cref that now resolves without
/// it, which the compiler enforces rather than an assertion.
/// </para>
/// <para>
/// The shadowing check itself is <c>BNAQ1004</c>'s since plan 00028:
/// <see cref="AssemblyQualityTests.No_shipped_namespace_shadows_a_referenced_root"/>, which reads both
/// shipped assemblies, internal types included, and replaced the hand-rolled test that stood here. What
/// stays is what the rule does not assert: that the code it reads is all ours or generated.
/// </para>
/// </remarks>
public sealed class NamespaceConventionTests
{
    /// <summary>What every namespace this repository authors begins with.</summary>
    private const string Prefix = "Bennewitz.";

    /// <summary>
    /// The scoping is only honest if everything we author really is under <see cref="Prefix"/>.
    /// Anything else in the shipped assemblies has to be generated — and generated namespaces are
    /// named by a tool, never enclose our source, and are not ours to rename.
    /// </summary>
    [Fact]
    public void Everything_outside_the_repository_prefix_is_generated()
    {
        string[] generated = ["CompiledAvaloniaXaml", "XamlX"];
        Assembly[] ours = [typeof(SideBySideDiffView).Assembly, typeof(PaneSource).Assembly];

        List<string> unexpected = ours
            .SelectMany(a => a.GetTypes())
            .Select(t => t.Namespace)
            .Where(ns => ns is { Length: > 0 })
            .Select(ns => ns!.Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .Where(root => !Prefix.StartsWith(root + ".", StringComparison.Ordinal)
                           && !generated.Contains(root, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unexpected.Count == 0,
            "These root namespaces are neither ours nor known tool output, so the namespace conventions "
            + "BNAQ1004 holds are not being read against all of the code they claim to: "
            + string.Join(", ", unexpected));
    }
}
