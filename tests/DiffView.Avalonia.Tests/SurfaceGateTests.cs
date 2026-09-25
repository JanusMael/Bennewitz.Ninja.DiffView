using System.Reflection;
using System.Text;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The stop condition for plan 00021: what a consumer can reach on <see cref="SideBySideDiffView"/>
/// does not change while its implementation moves out from under it.
/// </summary>
/// <remarks>
/// <para>
/// The measure is the <b>flattened</b> surface filtered to members this library declares. Flattened
/// rather than declared-only because members that move stay reachable — a declared-only dump would
/// shrink by construction and could never be compared against anything. Filtered because framework
/// members are not this library's surface and never move.
/// </para>
/// <para>
/// This gate is <b>weaker than it looks</b>, and the plan says so rather than dressing it up.
/// Explicit interface implementations are private in IL, so a <c>BindingFlags.Public</c> dump cannot
/// see <see cref="IDiffSurface"/> at all. What it proves is that nothing leaked <i>out</i>; that the
/// refactor is correct is carried by the behaviour suite, which is the whole of the rest of this
/// project.
/// </para>
/// </remarks>
public class SurfaceGateTests
{
    private const BindingFlags Flattened =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    [Fact]
    public void SideBySideDiffView_reachable_surface_matches_the_committed_baseline()
    {
        string baselinePath = RepoPaths.Source(Path.Combine("fixtures", "api", "sidebyside.txt"));
        string[] baseline = Normalise(File.ReadAllText(baselinePath));
        string[] actual = Dump(typeof(SideBySideDiffView));

        // Named before the sequence assertion, because "the count dropped by one" is the sentence
        // that says what happened and a first-differing-element message is not.
        Assert.Equal(baseline.Length, actual.Length);
        Assert.Equal(baseline, actual);
    }

    [Fact]
    public void Nothing_the_controller_shares_is_public()
    {
        Assert.False(typeof(DiffBuildController).IsPublic, "DiffBuildController must stay internal.");
        Assert.False(typeof(IDiffSurface).IsPublic, "IDiffSurface must stay internal.");

        // Every seam member is implemented EXPLICITLY, which in IL means the implementing method is
        // private. Asserting the name is absent from the surface would be wrong, not merely weak:
        // IsDirty and IsEdited are public verbs of the editor that the seam deliberately reuses.
        InterfaceMapping map = typeof(SideBySideDiffView).GetInterfaceMap(typeof(IDiffSurface));
        for (int i = 0; i < map.InterfaceMethods.Length; i++)
        {
            Assert.True(
                map.TargetMethods[i].IsPrivate,
                $"{map.InterfaceMethods[i].Name} is implemented implicitly and would reach the public surface.");
        }

        // There is deliberately no assertion that DiffBuildController never appears in a public
        // signature. It cannot: a public member naming an internal type is CS0050 or CS0051, which
        // a mutation confirmed before this comment was written. A check that cannot fail is worth
        // less than the sentence saying why.
    }

    private static string[] Dump(Type target)
    {
        Assembly library = target.Assembly;
        return [.. target
            .GetMembers(Flattened)
            .Where(m => m.DeclaringType?.Assembly == library)
            .Select(Describe)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Splits a committed dump into lines, tolerating the byte-order mark and the line terminator
    /// the file happens to carry. The baseline is compared by <b>content</b>: the spike's own file
    /// was 13,422 bytes against this tool's 13,419, all three of them the BOM.
    /// </summary>
    private static string[] Normalise(string text)
    {
        return [.. text
            .TrimStart('﻿')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => line.Length > 0)];
    }

    private static string Describe(MemberInfo member) => member switch
    {
        ConstructorInfo c => $"Constructor .ctor({Parameters(c.GetParameters())})",
        FieldInfo f => $"Field       {f.FieldType.Name} {f.Name}",
        EventInfo e => $"Event       event {e.EventHandlerType?.Name} {e.Name}",
        PropertyInfo p => $"Property    {p.PropertyType.Name} {p.Name} {Accessors(p)}".TrimEnd(),
        MethodInfo m => $"Method      {m.ReturnType.Name} {m.Name}({Parameters(m.GetParameters())})",
        Type t => $"NestedType  {t.Name}",
        _ => $"Other       {member.MemberType} {member.Name}",
    };

    private static string Accessors(PropertyInfo p)
    {
        List<string> parts = [];
        if (p.GetGetMethod() is not null)
        {
            parts.Add("get");
        }

        if (p.GetSetMethod() is not null)
        {
            parts.Add("set");
        }

        return string.Join(' ', parts);
    }

    private static string Parameters(ParameterInfo[] parameters) =>
        string.Join(", ", parameters.Select(p => p.ParameterType.Name));
}
