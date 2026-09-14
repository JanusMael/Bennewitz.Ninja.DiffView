using System.Reflection;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00017: no namespace this repository declares carries a segment that shadows the root
/// namespace of something it references.
/// </summary>
/// <remarks>
/// <para>
/// C# resolves the first identifier of a qualified name by walking outward through the enclosing
/// namespaces before it reaches global. So inside <c>Bennewitz.Ninja.DiffView.Avalonia</c> — which
/// is what this library was called until plan 00017 — the name <c>Avalonia</c> found *us* first, and
/// <c>Avalonia.Media.Color</c> failed to compile with CS0234 while looking for
/// <c>Bennewitz.Ninja.DiffView.Avalonia.Media.Color</c>. The tree carried the scar in
/// <see cref="DiffCommand"/>, whose documentation needed <c>global::</c> to name a
/// <see cref="Avalonia.Input.KeyBinding"/> — a cref that now resolves without it, which is the other
/// half of this test and is enforced by the compiler rather than by an assertion.
/// </para>
/// <para>
/// Written against the segments rather than against the single name <c>Avalonia</c>: the defect is
/// the shape, not the word, and a future <c>.Media</c> or <c>.Controls</c> segment would be the same
/// bug with a different spelling.
/// </para>
/// </remarks>
public sealed class NamespaceConventionTests
{
    /// <summary>What every namespace this repository authors begins with.</summary>
    private const string Prefix = "Bennewitz.";

    /// <summary>
    /// The scoping above is only honest if everything we author really is under <see cref="Prefix"/>.
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
            "These root namespaces are neither ours nor known tool output, so the shadowing test above is "
            + "not looking at all of the code it claims to: " + string.Join(", ", unexpected));
    }

    [Fact]
    public void No_namespace_carries_a_segment_that_shadows_a_referenced_root()
    {
        Assembly[] ours = [typeof(SideBySideDiffView).Assembly, typeof(PaneSource).Assembly];
        HashSet<string> ourNames = new(ours.Select(a => a.GetName().Name!), StringComparer.Ordinal);

        // The root namespace of everything the shipped assemblies reference — Avalonia, System,
        // DiffPlex and the rest. Our own assemblies are skipped: Bennewitz is a root we declare,
        // and matching it against ourselves would report the convention as its own violation.
        HashSet<string> referencedRoots = new(StringComparer.Ordinal);
        foreach (Assembly assembly in ours)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (ourNames.Contains(reference.Name!))
                {
                    continue;
                }

                foreach (string root in RootNamespacesOf(reference))
                {
                    referencedRoots.Add(root);
                }
            }
        }

        Assert.NotEmpty(referencedRoots);

        // Every segment we declare, public and internal alike: internal code is shadowed just as
        // thoroughly as public code, and fixes it the same ugly way.
        //
        // Only namespaces we actually author, which is what Prefix selects. The Avalonia XAML
        // compiler emits a CompiledAvaloniaXaml namespace into this assembly and Avalonia ships one
        // too, so the naive form of this rule reports a collision on its first run — and it is not
        // one: that namespace is generated, cannot be renamed, and never encloses a line of our
        // source, so nothing we write resolves through it.
        Dictionary<string, string> segmentToNamespace = new(StringComparer.Ordinal);
        foreach (Assembly assembly in ours)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.Namespace is not { Length: > 0 } declared
                    || !declared.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string segment in declared.Split('.'))
                {
                    segmentToNamespace.TryAdd(segment, declared);
                }
            }
        }

        List<string> shadowing = segmentToNamespace
            .Where(pair => referencedRoots.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"'{pair.Key}' in {pair.Value}")
            .ToList();

        Assert.True(
            shadowing.Count == 0,
            "These namespace segments shadow the root namespace of a referenced assembly, so a qualified "
            + "reference to that root resolves to ours and fails to compile — every use needs global:: "
            + "until the segment is renamed: " + string.Join("; ", shadowing));
    }

    private static IEnumerable<string> RootNamespacesOf(AssemblyName reference)
    {
        Assembly loaded;
        try
        {
            loaded = Assembly.Load(reference);
        }
        catch (Exception exception) when (exception is FileNotFoundException or BadImageFormatException)
        {
            // A reference the test host cannot resolve contributes no roots. It also cannot be
            // shadowing anything we compile against, because we did compile.
            yield break;
        }

        foreach (Type type in loaded.GetExportedTypes())
        {
            if (type.Namespace is { Length: > 0 } declared)
            {
                yield return declared.Split('.')[0];
            }
        }
    }
}
