using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The stop condition for plan 00021: what a consumer can reach on <see cref="SideBySideDiffView"/>
/// does not change while its implementation moves out from under it — and what a consumer can reach
/// on <see cref="DiffViewer"/> is exactly the viewer's API, and never its documents.
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

    /// <summary>
    /// The viewer's surface is its allow-list, <c>fixtures/api/viewer.txt</c>: plan 00021's Appendix A
    /// Keep table, the constructor, and plan 00025's two header names.
    /// </summary>
    /// <remarks>
    /// Equal, not merely a subset as the plan's Testing table words it. A subset lets a member vanish
    /// with the gate green, and from the first publish a vanished member is the break; equality makes
    /// a removal the same deliberate edit to the fixture that an addition already was.
    /// </remarks>
    [Fact]
    public void DiffViewer_reachable_surface_matches_its_allow_list()
    {
        string allowListPath = RepoPaths.Source(Path.Combine("fixtures", "api", "viewer.txt"));
        string[] allowed = Normalise(File.ReadAllText(allowListPath));
        string[] actual = Dump(typeof(DiffViewer));

        Assert.Equal(allowed.Length, actual.Length);
        Assert.Equal(allowed, actual);
    }

    [Fact]
    public void Nothing_the_controller_shares_is_public()
    {
        Assert.False(typeof(DiffBuildController).IsPublic, "DiffBuildController must stay internal.");
        Assert.False(typeof(IDiffSurface).IsPublic, "IDiffSurface must stay internal.");

        // Every control the controller drives, derived rather than listed — the same derivation
        // TemplatePartTests uses — so a third host is held to this the moment it exists. The
        // Contains is the anti-vacuity half: an empty derivation would satisfy the loop.
        Type[] hosts =
        [
            .. typeof(SideBySideDiffView).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDiffSurface).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal),
        ];
        Assert.Contains(typeof(SideBySideDiffView), hosts);
        Assert.Contains(typeof(DiffViewer), hosts);

        // Every seam member is implemented EXPLICITLY, which in IL means the implementing method is
        // private. Asserting the name is absent from the surface would be wrong, not merely weak:
        // IsDirty and IsEdited are public verbs of the editor that the seam deliberately reuses.
        foreach (Type host in hosts)
        {
            InterfaceMapping map = host.GetInterfaceMap(typeof(IDiffSurface));
            for (int i = 0; i < map.InterfaceMethods.Length; i++)
            {
                Assert.True(
                    map.TargetMethods[i].IsPrivate,
                    $"{host.Name} implements {map.InterfaceMethods[i].Name} implicitly, so it would reach the public surface.");
            }
        }

        // There is deliberately no assertion that DiffBuildController never appears in a public
        // signature. It cannot: a public member naming an internal type is CS0050 or CS0051, which
        // a mutation confirmed before this comment was written. A check that cannot fail is worth
        // less than the sentence saying why.
    }

    /// <summary>
    /// A document is what would make the read-only control writable, so the viewer's type hands out
    /// none — through its own members, and through anyone else's property identity.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>The second half is the one that leaked.</b> <c>AvaloniaObject.GetValue</c> resolves a
    /// direct property by its id against the object's own type, so an <em>internal</em> registration
    /// made through <c>AddOwner</c> let the editor's <em>public</em>
    /// <see cref="SideBySideDiffView.LeftDocumentProperty"/> read the viewer's live document, and an
    /// insert through it succeeded. The first half passed throughout: no member of the viewer's type
    /// named a document.
    /// </remarks>
    [AvaloniaFact]
    public void The_viewer_hands_out_no_TextDocument()
    {
        string[] naming =
        [
            .. typeof(DiffViewer).GetMembers(Flattened)
                .Where(m => Mentions(TypeOf(m), typeof(TextDocument)))
                .Select(Describe),
        ];
        Assert.True(
            naming.Length == 0,
            "These public members of DiffViewer hand out a TextDocument:" + Environment.NewLine
            + string.Join(Environment.NewLine, naming.Select(n => "  " + n)));

        // Every public property identity that carries a document, in this library and the editor
        // component it is built on, derived rather than listed.
        AvaloniaProperty[] identities =
        [
            .. new[] { typeof(DiffViewer).Assembly, typeof(TextEditor).Assembly }
                .SelectMany(a => a.GetExportedTypes())
                .Where(t => !t.ContainsGenericParameters)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(f => typeof(AvaloniaProperty).IsAssignableFrom(f.FieldType))
                .Select(f => (AvaloniaProperty)f.GetValue(null)!)
                .Where(p => Mentions(p.PropertyType, typeof(TextDocument))),
        ];
        Assert.Contains(SideBySideDiffView.LeftDocumentProperty, identities);
        Assert.Contains(SideBySideDiffView.RightDocumentProperty, identities);

        DiffViewer viewer = new();
        TextDocument[] own = [viewer.LeftDocument, viewer.RightDocument];
        string[] reading =
        [
            .. identities
                .Where(p => ReadOrNull(viewer, p) is { } value && own.Any(d => ReferenceEquals(d, value)))
                .Select(p => $"{p.OwnerType.Name}.{p.Name}"),
        ];
        Assert.True(
            reading.Length == 0,
            "These public property identities read the viewer's own document back out of it:" + Environment.NewLine
            + string.Join(Environment.NewLine, reading.Select(n => "  " + n)));
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

    /// <summary>What a member hands a caller: a property's or field's value, a method's return, an event's handler.</summary>
    private static Type? TypeOf(MemberInfo member) => member switch
    {
        PropertyInfo p => p.PropertyType,
        FieldInfo f => f.FieldType,
        MethodInfo m => m.ReturnType,
        EventInfo e => e.EventHandlerType,
        _ => null,
    };

    /// <summary>Whether <paramref name="type"/> is <paramref name="target"/>, or carries it as an element or a type argument.</summary>
    private static bool Mentions(Type? type, Type target)
    {
        return type is not null
               && (target.IsAssignableFrom(type)
                   || (type.HasElementType && Mentions(type.GetElementType(), target))
                   || (type.IsGenericType && type.GetGenericArguments().Any(a => Mentions(a, target))));
    }

    /// <summary>
    /// The value <paramref name="identity"/> reads from <paramref name="target"/>, or <c>null</c> where
    /// the object carries no such property: an identity its type never registered throws, and a throw
    /// hands nothing out.
    /// </summary>
    private static object? ReadOrNull(AvaloniaObject target, AvaloniaProperty identity)
    {
        try
        {
            return target.GetValue(identity);
        }
        catch (ArgumentException)
        {
            return null;
        }
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
