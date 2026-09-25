using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.DiffView.Tests.Viewer;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00021 phase 3: every control theme this library ships sits in a compiled dictionary of its
/// own, merged by the control it themes. So no theme of ours is found twice on the way up from any
/// element, the viewer — which has no find — carries no theme for a find bar, and the three views'
/// themes, written as copies of one another, style the same pseudo-classes.
/// </summary>
/// <remarks>
/// ⚠ <b>"Keyed once" is a statement about the resource path, not about the files.</b> Before the
/// split every key already sat in exactly one compiled dictionary — <c>SideBySideDiffViewTheme</c>
/// held four of them — so a check over the dictionaries alone passes on the code the split replaced.
/// What was doubled was the path: each header, the status strip and the find bar merged that
/// four-theme dictionary for their own, beneath a view that had merged it as well.
/// </remarks>
public sealed class ControlThemeTests
{
    private static readonly Assembly Library = typeof(SideBySideDiffView).Assembly;

    /// <summary>A pseudo-class in a selector: a colon and a name.</summary>
    private static readonly Regex PseudoClass = new(@":[A-Za-z][A-Za-z0-9-]*", RegexOptions.Compiled);

    [AvaloniaFact]
    public async Task No_theme_of_ours_is_found_twice_on_the_way_up_from_any_element()
    {
        TestLogSink.Instance.Clear();
        (string left, string right) = CompositeHost.SmallFixture();
        HashSet<Type> shipped = [.. ShippedThemeTargets()];
        List<string> faults = [];
        HashSet<Type> checkedOnScreen = [];
        List<string> walked = [];
        HashSet<string> climbed = [];

        // Each view as it looks with everything it can show on screen: the find bar open in the two
        // that have one.
        using (CompositeHost editor = new())
        {
            editor.Show();
            await editor.LoadAsync(left, right);
            editor.View.OpenFind();
            CompositeHost.Layout();
            WalkThePathsUp("the editor", editor.View, shipped, faults, checkedOnScreen, walked, climbed);
        }

        using (InlineHost unified = new())
        {
            unified.Show();
            await unified.LoadAsync(left, right);
            unified.View.OpenFind();
            InlineHost.Layout();
            WalkThePathsUp("the unified view", unified.View, shipped, faults, checkedOnScreen, walked, climbed);
        }

        using (ViewerHost viewer = new())
        {
            viewer.Show();
            await viewer.LoadAsync(left, right);
            ViewerHost.Layout();
            WalkThePathsUp("the viewer", viewer.View, shipped, faults, checkedOnScreen, walked, climbed);
        }

        Assert.True(
            faults.Count == 0,
            "A control theme of ours is found more than once, or not at all, on the way up from an element. "
            + "Every control merges its own theme and nothing else's, so each key has one home on any path:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, faults.Distinct(StringComparer.Ordinal).Select(f => "  " + f)));

        // ⛔ A walk that reaches nothing finds nothing twice. Every theme the library ships must have
        // been checked on screen, in the control it themes, at least once.
        Assert.True(shipped.Count > 0, "The library's compiled dictionaries define no control theme by this test's reading.");
        List<string> unreached = [.. shipped.Where(t => !checkedOnScreen.Contains(t)).Select(t => t.Name).Order(StringComparer.Ordinal)];
        Assert.True(
            unreached.Count == 0,
            "These controls have a theme of ours and none of them was on screen in the walk, so a theme "
            + "doubled on their path would pass: " + string.Join(", ", unreached));

        // ⛔ And a walk that never leaves the element finds nothing twice either: in every view, some
        // element's way up must cross a second control that carries a theme of ours.
        List<string> flat = [.. walked.Where(v => !climbed.Contains(v))];
        Assert.True(
            walked.Count > 0 && flat.Count == 0,
            "No element's way up crossed more than one control carrying a theme, so nothing could have "
            + "been found twice, in: " + (walked.Count == 0 ? "(no view walked)" : string.Join(", ", flat)));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task The_viewer_carries_no_theme_for_the_find_bar_it_does_not_have()
    {
        using ViewerHost viewer = new();
        viewer.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await viewer.LoadAsync(left, right);
        ViewerHost.Layout();

        List<string> carriers = [];
        HashSet<Type> read = [];
        foreach (Visual element in SelfAndDescendants(viewer.View))
        {
            List<object> keys = [.. ThemeKeysOn(element)];
            if (keys.Count > 0)
            {
                read.Add(element.GetType());
            }

            if (keys.Contains(typeof(DiffFindBar)))
            {
                carriers.Add(Describe(element));
            }
        }

        // ⛔ The carriers were the headers and the strip, each merging the editor's four-theme
        // dictionary for its own; a walk that never read their resources could not find it there.
        Assert.True(
            read.Contains(typeof(DiffPaneHeader)) && read.Contains(typeof(DiffStatusStrip)),
            "The walk read no theme from a header or from the strip — only from "
            + string.Join(", ", read.Select(t => t.Name).Order(StringComparer.Ordinal))
            + " — so a find bar's theme carried by either would go unseen.");

        Assert.True(
            carriers.Count == 0,
            "The viewer has no find bar, and these elements of its tree carry a theme for one: "
            + string.Join(", ", carriers));
    }

    [AvaloniaFact]
    public void Every_pseudo_class_one_view_theme_styles_every_view_theme_styles()
    {
        // The views are the controls with a banner — the three that publish the same pseudo-classes
        // (ViewerStateTests). Derived, so a fourth joins the moment it has one; the three named here
        // are only what the derivation must not lose.
        Type[] views =
        [
            .. Library.GetExportedTypes()
                .Where(t => typeof(TemplatedControl).IsAssignableFrom(t) && !t.IsAbstract)
                .Where(t => t.GetProperty(nameof(SideBySideDiffView.BannerKind), BindingFlags.Public | BindingFlags.Instance)?.PropertyType == typeof(DiffBannerKind))
                .OrderBy(t => t.Name, StringComparer.Ordinal),
        ];
        List<string> lost = [.. new[] { typeof(SideBySideDiffView), typeof(DiffViewer), typeof(InlineDiffView) }
            .Where(t => !views.Contains(t)).Select(t => t.Name)];
        Assert.True(lost.Count == 0, "The derivation of the views has lost " + string.Join(", ", lost) + ", so their themes go unchecked.");

        Dictionary<Type, SortedSet<string>> styled = views.ToDictionary(v => v, StyledPseudoClasses);
        SortedSet<string> union = new(styled.Values.SelectMany(s => s), StringComparer.Ordinal);

        // Five of the nine pseudo-classes are styled by no theme, and that is allowed: this is about
        // the themes agreeing. But a reading that found nothing in any of them would agree as well.
        Assert.True(union.Count > 0, "No view's theme styles any pseudo-class by this test's reading, so the comparison below compares nothing.");

        foreach ((Type view, SortedSet<string> set) in styled)
        {
            Assert.True(
                set.SetEquals(union),
                $"{view.Name}'s theme styles [{string.Join(' ', set)}] where the views' themes together style "
                + $"[{string.Join(' ', union)}]. A state one view shows and another does not is drift: missing "
                + $"[{string.Join(' ', union.Except(set))}].");
        }
    }

    /// <summary>
    /// For every element of <paramref name="root"/>'s tree, the themes of ours found on its way up —
    /// the element and each visual ancestor to the window. Resource lookup follows the logical parent,
    /// which for a template's content is the templated control, itself a visual ancestor; so this is
    /// that path and a little more.
    /// </summary>
    private static void WalkThePathsUp(
        string view,
        Visual root,
        HashSet<Type> shipped,
        List<string> faults,
        HashSet<Type> checkedOnScreen,
        List<string> walked,
        HashSet<string> climbed)
    {
        walked.Add(view);
        foreach (Visual element in SelfAndDescendants(root))
        {
            List<(Visual Owner, object Key)> found = [];
            foreach (Visual step in (IEnumerable<Visual>)[element, .. element.GetVisualAncestors()])
            {
                found.AddRange(ThemeKeysOn(step).Select(key => (step, key)));
            }

            if (found.Select(f => f.Owner).Distinct().Count() > 1)
            {
                climbed.Add(view);
            }

            foreach (IGrouping<object, (Visual Owner, object Key)> key in found.GroupBy(f => f.Key))
            {
                // Named by where the copies are rather than by the element that found them: every
                // element below the innermost owner finds the same ones, and the report is read once.
                if (key.Count() > 1)
                {
                    faults.Add(
                        $"{view}: {KeyName(key.Key)}'s theme is found {key.Count()} times on one way up, in "
                        + string.Join(", ", key.Select(k => Describe(k.Owner))));
                }
            }

            Type type = element.GetType();
            if (!shipped.Contains(type))
            {
                continue;
            }

            int own = found.Count(f => Equals(f.Key, type));
            if (own == 0)
            {
                faults.Add($"{view}: {Describe(element)} finds no theme of its own on the way up");
            }
            else if (own == 1)
            {
                checkedOnScreen.Add(type);
            }
        }
    }

    /// <summary>
    /// <see cref="ThemeKeysOf"/> for an element's own resources, read only where it has some: the
    /// <see cref="StyledElement.Resources"/> getter creates a dictionary on an element that had none,
    /// and a walk over every element would otherwise leave one behind on each.
    /// </summary>
    private static IEnumerable<object> ThemeKeysOn(StyledElement element) =>
        ((IResourceNode)element).HasResources ? ThemeKeysOf(element.Resources) : [];

    /// <summary>
    /// The keys of every control theme in <paramref name="resources"/> and the dictionaries merged into
    /// it at any depth that is ours: keyed by a type of this library, or held by a compiled dictionary
    /// of this library — the latter being how the text area's theme, keyed by a string, is counted too.
    /// </summary>
    private static IEnumerable<object> ThemeKeysOf(IResourceDictionary resources)
    {
        foreach (IResourceDictionary dictionary in SelfAndMerged(resources))
        {
            bool ourDictionary = dictionary.GetType().Assembly == Library;
            foreach (object key in dictionary.Keys.ToList())
            {
                bool ourKey = key is Type type && type.Assembly == Library;
                if ((ourKey || ourDictionary)
                    && dictionary.TryGetResource(key, null, out object? value)
                    && value is ControlTheme)
                {
                    yield return key;
                }
            }
        }
    }

    private static IEnumerable<IResourceDictionary> SelfAndMerged(IResourceDictionary resources)
    {
        yield return resources;
        foreach (IResourceProvider merged in resources.MergedDictionaries)
        {
            IResourceDictionary? inner = merged switch
            {
                IResourceDictionary dictionary => dictionary,
                ResourceInclude include => include.Loaded,
                _ => null,
            };

            if (inner is null)
            {
                continue;
            }

            foreach (IResourceDictionary nested in SelfAndMerged(inner))
            {
                yield return nested;
            }
        }
    }

    /// <summary>The types the library's compiled dictionaries carry a control theme for.</summary>
    private static Type[] ShippedThemeTargets() =>
    [
        .. Library.GetExportedTypes()
            .Where(t => typeof(ResourceDictionary).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null)
            .SelectMany(t => ThemeKeysOf((ResourceDictionary)Activator.CreateInstance(t)!))
            .OfType<Type>()
            .Distinct()
            .OrderBy(t => t.Name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// The pseudo-classes <paramref name="view"/>'s own theme styles the control on: the ':'-names of
    /// each nested rule's first segment, the one that starts at '^' and so is the control itself.
    /// </summary>
    private static SortedSet<string> StyledPseudoClasses(Type view)
    {
        Control control = (Control)Activator.CreateInstance(view)!;
        Assert.True(
            control.TryFindResource(view, out object? found) && found is ControlTheme,
            $"{view.Name} finds no control theme of its own, so there is nothing of it to compare.");

        SortedSet<string> set = new(StringComparer.Ordinal);
        foreach (Style style in ((ControlTheme)found!).Children.OfType<Style>())
        {
            string first = (style.Selector?.ToString() ?? string.Empty).Split(' ', 2)[0];
            if (!first.StartsWith('^'))
            {
                continue;
            }

            foreach (Match match in PseudoClass.Matches(first))
            {
                set.Add(match.Value);
            }
        }

        return set;
    }

    private static IEnumerable<Visual> SelfAndDescendants(Visual root) => [root, .. root.GetVisualDescendants()];

    private static string Describe(StyledElement element) =>
        element.Name is { Length: > 0 } name ? $"{element.GetType().Name} '{name}'" : element.GetType().Name;

    private static string KeyName(object key) => key is Type type ? type.Name : key.ToString() ?? "(null)";
}
