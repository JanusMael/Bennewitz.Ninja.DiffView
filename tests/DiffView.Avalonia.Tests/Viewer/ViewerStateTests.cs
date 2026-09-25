using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// Plan 00021: what the viewer shares with the editor through the controller — the state it
/// publishes as pseudo-classes — and what the seam answers differently for it.
/// </summary>
public sealed class ViewerStateTests
{
    /// <summary>
    /// The nine pseudo-classes are the only way a host styles on control state through a selector,
    /// so all three views carry the same ones through the same states. Two of the three share them
    /// through the controller; the unified view sets its own, and this is what keeps them one set.
    /// </summary>
    [AvaloniaFact]
    public async Task All_three_views_carry_the_same_pseudo_classes_through_the_same_states()
    {
        using CompositeHost editor = new();
        using ViewerHost viewer = new();
        using InlineHost unified = new();
        View[] views =
        [
            new("the editor", editor.View, editor.Show, (l, r) => { editor.View.LeftSource = l; editor.View.RightSource = r; }, editor.WaitForBuildAsync,
                on => editor.View.ShowBanner = on, () => editor.View.Builder = CompositeHost.FailingBuilder),
            new("the viewer", viewer.View, viewer.Show, (l, r) => { viewer.View.LeftSource = l; viewer.View.RightSource = r; }, viewer.WaitForBuildAsync,
                on => viewer.View.ShowBanner = on, () => viewer.View.Builder = CompositeHost.FailingBuilder),
            new("the unified view", unified.View, unified.Show, (l, r) => { unified.View.LeftSource = l; unified.View.RightSource = r; }, unified.WaitForBuildAsync,
                on => unified.View.ShowBanner = on, () => unified.View.Builder = CompositeHost.FailingBuilder),
        ];
        (string left, string right) = CompositeHost.SmallFixture();
        string unrelatedLeft = string.Join("\n", Enumerable.Range(0, 6000).Select(i => $"L{i} {(i * 7919) % 1000}"));
        string unrelatedRight = string.Join("\n", Enumerable.Range(0, 6000).Select(i => $"R{i} {(i * 104729) % 1000}"));

        async Task LoadAll(string l, string r)
        {
            foreach (View view in views)
            {
                view.Assign(l, r);
                await view.Wait();
            }
        }

        foreach (View view in views)
        {
            view.Show();
        }

        AssertOneSet(views, "shown with nothing to compare", ":empty", ":banner-none");

        // Assigned and not yet applied: the build is on a worker until the dispatcher is pumped.
        foreach (View view in views)
        {
            view.Assign(left, right);
        }

        AssertOneSet(views, "building", ":building", ":banner-none");
        foreach (View view in views)
        {
            await view.Wait();
        }

        AssertOneSet(views, "ready", ":ready", ":banner-none");

        await LoadAll(left, left);
        AssertOneSet(views, "identical", ":ready", ":banner-identical");

        // The toggle is a disjunct inside :banner-none, so switched off it is both.
        foreach (View view in views)
        {
            view.ShowBanner(false);
        }

        AssertOneSet(views, "the banner switched off", ":ready", ":banner-none", ":banner-identical");
        foreach (View view in views)
        {
            view.ShowBanner(true);
        }

        await LoadAll(unrelatedLeft, unrelatedRight);
        AssertOneSet(views, "too different to align", ":degraded", ":banner-degraded");

        foreach (View view in views)
        {
            view.Fail();
        }

        await LoadAll(left, right);
        AssertOneSet(views, "failed", ":failed", ":banner-error");
    }

    /// <summary>
    /// The seam's answers, each control against the same controller: the editor made to have one to
    /// give — an edit on its right side and its find bar open — and the viewer, which has none.
    /// </summary>
    /// <remarks>
    /// Of the seam's eleven variation points — the plan's ten and <c>OnFoldsChanged</c> — this holds
    /// the ones that answer a question: the pane's configuration (3), the dirty decoration (7), the
    /// strip's find lane (8), the pending edit (9) and the documents (10). The rest are observed where
    /// they act: the parts and their menus (1, 2) by
    /// <see cref="ViewerVerbTests.No_surface_opens_a_menu_where_the_editor_opens_one"/>, the search a
    /// new model re-runs (4) by there being no search, the document handlers (5) by
    /// <see cref="ViewerVerbTests.Each_pane_selects_and_copies_and_neither_takes_a_keystroke_or_a_paste"/>,
    /// the change set moving (6) by
    /// <see cref="ViewerNavigationTests.The_walks_move_the_current_change_and_their_commands_follow_the_change_count"/>,
    /// and the folds moving — which re-evaluates the editor's folding commands, of which the viewer has
    /// none — by folding still working, in
    /// <see cref="ViewerNavigationTests.The_option_folds_both_panes_alike_and_a_placeholder_click_gives_a_run_back"/>.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_seam_answers_for_its_own_control()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost editor = new();
        using ViewerHost viewer = new();
        editor.Show();
        viewer.Show();

        // (10) Replacing a side's source replaces its document. The editor publishes the new one;
        // the viewer notifies nobody, because the notification would carry the document itself.
        List<AvaloniaPropertyChangedEventArgs> editorChanges = [];
        List<AvaloniaPropertyChangedEventArgs> viewerChanges = [];
        editor.View.PropertyChanged += (_, e) => editorChanges.Add(e);
        viewer.View.PropertyChanged += (_, e) => viewerChanges.Add(e);
        await editor.LoadAsync(left, right);
        await viewer.LoadAsync(left, right);
        Assert.Contains(editorChanges, e => e.Property == SideBySideDiffView.LeftDocumentProperty && e.NewValue is TextDocument);
        Assert.DoesNotContain(viewerChanges, e => e.NewValue is TextDocument || e.OldValue is TextDocument);

        editor.View.RightReadOnly = false;
        editor.Right.TextArea.Focus();
        editor.Right.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        editor.Window.KeyTextInput("x");
        CompositeHost.Layout();
        editor.View.OpenFind();
        await editor.FindAsync("using");

        IDiffSurface editorSeam = editor.View;
        IDiffSurface viewerSeam = viewer.View;
        Assert.True(editorSeam.IsDirty(DiffSide.Right));
        Assert.True(editorSeam.IsEdited(DiffSide.Right));
        Assert.NotNull(editorSeam.FindStripText());
        Assert.True(editor.View.RightHeader!.IsDirty);
        Assert.True(editor.Left.CanCopyOut);

        foreach (DiffSide side in new[] { DiffSide.Left, DiffSide.Right })
        {
            DiffPanePresenter pane = viewer.View.Pane(side)!;
            Assert.True(pane.IsReadOnly, $"the viewer's {side} pane takes edits");
            Assert.False(pane.CanCopyOut, $"the viewer's {side} pane offers a copy arrow");
            Assert.False(viewerSeam.IsDirty(side));
            Assert.False(viewerSeam.IsEdited(side));
        }

        Assert.Null(viewerSeam.FindStripText());
        Assert.False(viewer.View.LeftHeader!.IsDirty);
        Assert.False(viewer.View.RightHeader!.IsDirty);
        Assert.Null(viewer.View.StatusStrip!.FindText);
    }

    private static void AssertOneSet(View[] views, string step, params string[] expected)
    {
        string[] first = PseudoClassesOf(views[0].Control);
        foreach (string pseudoClass in expected)
        {
            Assert.True(first.Contains(pseudoClass), $"{step}: {views[0].Name} carries [{string.Join(' ', first)}], without {pseudoClass}");
        }

        foreach (View view in views.Skip(1))
        {
            string[] set = PseudoClassesOf(view.Control);
            Assert.True(
                set.SequenceEqual(first),
                $"{step}: {view.Name} carries [{string.Join(' ', set)}] where {views[0].Name} carries [{string.Join(' ', first)}]");
        }
    }

    private static string[] PseudoClassesOf(StyledElement element)
    {
        return [.. element.Classes.Where(c => c.StartsWith(':')).OrderBy(c => c, StringComparer.Ordinal)];
    }

    /// <summary>One view driven through the journey, whichever of the three it is.</summary>
    private sealed record View(
        string Name,
        StyledElement Control,
        Action Show,
        Action<string, string> Assign,
        Func<Task> Wait,
        Action<bool> ShowBanner,
        Action Fail);
}
