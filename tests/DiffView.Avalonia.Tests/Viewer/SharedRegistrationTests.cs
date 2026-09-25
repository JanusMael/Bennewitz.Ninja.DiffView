using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// Plan 00021: every property the viewer carries is the editor's own registration, owned through
/// <c>AddOwner</c> — so one identity means the same thing on both controls, whether a style, a
/// binding or the controller's own dispatch is holding it.
/// </summary>
/// <remarks>
/// The controller raises and dispatches through the <em>editor's</em> identities —
/// <c>SetAndRaise(SideBySideDiffView.StateProperty, …)</c>, <c>change.Property ==
/// SideBySideDiffView.ShowMinimapProperty</c> — so a viewer property registered afresh compares
/// unequal, and its branch never runs, with no error anywhere. <c>StyledProperty.AddOwner</c>
/// returns the same instance; <c>DirectProperty.AddOwner</c> returns a new one that compares equal
/// through the id they share. Each kind is asserted as what it is.
/// </remarks>
public sealed class SharedRegistrationTests
{
    [Fact]
    public void Every_property_the_viewer_carries_is_the_editors_own_registration()
    {
        // Of any visibility: an internal registration added by hand is how a document once leaked,
        // and it is held to the same rule as a public one.
        AvaloniaProperty[] properties =
        [
            .. typeof(DiffViewer)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => typeof(AvaloniaProperty).IsAssignableFrom(f.FieldType))
                .Select(f => (AvaloniaProperty)f.GetValue(null)!),
        ];

        // Both kinds, because each has its own way of going wrong and a loop over neither proves nothing.
        Assert.Contains(properties, p => p.IsDirect);
        Assert.Contains(properties, p => !p.IsDirect);

        List<string> wrong = [];
        foreach (AvaloniaProperty mine in properties)
        {
            FieldInfo? field = typeof(SideBySideDiffView).GetField(mine.Name + "Property", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field?.GetValue(null) is not AvaloniaProperty theirs)
            {
                wrong.Add($"{mine.Name}: the editor has no property of that name, so nothing shares it");
            }
            else if (mine != theirs)
            {
                wrong.Add($"{mine.Name}: compares unequal to the editor's — registered afresh, so the controller's dispatch on the editor's identity never reaches it");
            }
            else if (!AvaloniaPropertyRegistry.Instance.IsRegistered(typeof(DiffViewer), mine))
            {
                wrong.Add($"{mine.Name}: the editor's identity, but not registered on the viewer — assigned rather than owned through AddOwner");
            }
            else if (!mine.IsDirect && !ReferenceEquals(mine, theirs))
            {
                wrong.Add($"{mine.Name}: a styled property owned through AddOwner is the editor's own instance, and this is another");
            }
        }

        Assert.True(
            wrong.Count == 0,
            "These properties of DiffViewer are not the editor's own registration:" + Environment.NewLine
            + string.Join(Environment.NewLine, wrong.Select(w => "  " + w)));
    }

    /// <summary>
    /// One rule styles both controls: a comma-union type selector, which needs no class on either,
    /// and one setter on the shared identity — which reaches the viewer only because that identity is
    /// the editor's own instance.
    /// </summary>
    /// <remarks>
    /// The setter's property is owner-qualified, and must be: a union's target type is the controls'
    /// nearest common base, <c>TemplatedControl</c>, which has no such property, so the unqualified
    /// spelling does not load at all. The hosting guide has to show the qualified form.
    /// </remarks>
    [AvaloniaFact]
    public void One_comma_union_rule_styles_both_controls_through_one_setter()
    {
        Assert.ThrowsAny<Exception>(() => UnionStyle("ShowMinimap"));

        DiffViewer viewer = new() { TimeProvider = new FakeTimeProvider() };
        SideBySideDiffView editor = new() { TimeProvider = new FakeTimeProvider() };
        Assert.True(viewer.ShowMinimap && editor.ShowMinimap, "both default to showing the map, so the rule is what turns it off");

        StackPanel panel = new() { Children = { viewer, editor } };
        panel.Styles.AddRange(UnionStyle("dv:SideBySideDiffView.ShowMinimap"));
        Window window = new() { Content = panel };
        window.Show();
        try
        {
            ViewerHost.Layout();
            Assert.False(editor.ShowMinimap);
            Assert.False(viewer.ShowMinimap);

            // Nothing either control opted into: the only entries in its classes are pseudo-classes.
            Assert.All(viewer.Classes, c => Assert.StartsWith(":", c, StringComparison.Ordinal));
            Assert.All(editor.Classes, c => Assert.StartsWith(":", c, StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    private static Styles UnionStyle(string property)
    {
        string xaml = $$"""
            <Styles xmlns="https://github.com/avaloniaui"
                    xmlns:dv="using:Bennewitz.Ninja.DiffView">
              <Style Selector="dv|DiffViewer, dv|SideBySideDiffView">
                <Setter Property="{{property}}" Value="False" />
              </Style>
            </Styles>
            """;
        return (Styles)AvaloniaRuntimeXamlLoader.Load(xaml, typeof(DiffViewer).Assembly);
    }
}
