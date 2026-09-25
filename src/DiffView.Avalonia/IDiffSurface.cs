using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Everything <see cref="DiffBuildController"/> has to ask the control it drives, and nothing
/// else. Two unrelated sibling controls share one build orchestration by each owning a controller
/// and implementing this; the controller holds the shared state and calls back through here for
/// the handful of things that genuinely differ between an editor and a viewer, plus the three
/// members the framework marks <c>protected</c>.
/// </summary>
/// <remarks>
/// <para>
/// Kept deliberately narrow. The shape this replaced — an extension block over an interface —
/// needed roughly ninety members, because a block declares no state and every shared private
/// field had to be named on the interface; a controller keeps those thirty-four fields private to
/// the thing that uses them, which is better encapsulation than the code had before the split.
/// </para>
/// <para>
/// Implemented <b>explicitly</b> by both controls, so none of it reaches their public surface.
/// Explicit implementations are private in IL, which is also why the surface gate cannot see them
/// and why the behaviour suite, not the gate, is what proves the split correct.
/// </para>
/// </remarks>
internal interface IDiffSurface
{
    // ── The framework's protected members, re-exposed ───────────────────────────────────────

    /// <summary>
    /// The control itself, for the value of a styled property, the resources and the visual.
    /// <c>InvalidateVisual</c> is public on <c>Visual</c> and needs no member of its own; the two
    /// below are not, and do.
    /// </summary>
    TemplatedControl Control { get; }

    /// <summary><see cref="StyledElement.PseudoClasses"/>, which is protected on the control.</summary>
    IPseudoClasses PseudoClasses { get; }

    /// <summary>
    /// <see cref="AvaloniaObject.SetAndRaise{T}"/>, which is protected. The controller passes a
    /// <c>ref</c> to its <b>own</b> field, so the state stays with the code that uses it while the
    /// notification is raised on the control the host is bound to.
    /// </summary>
    bool SetAndRaise<T>(DirectPropertyBase<T> property, ref T field, T value);

    // ── The ten variation points ────────────────────────────────────────────────────────────

    /// <summary>The template's parts are found and wired; the surface adds its own handlers. Editor: five find-bar and four context handlers. Viewer: nothing.</summary>
    void OnPartsAttached(TemplateAppliedEventArgs e);

    /// <summary>The parts are about to be dropped; the surface unwires the same handlers.</summary>
    void OnPartsDetaching();

    /// <summary>A pane is attached and configured. Editor: the context, copy-out and copy-selection handlers, and both read-only flags. Viewer: nothing.</summary>
    void OnPaneAttached(DiffPanePresenter pane, DiffSide side);

    /// <summary>A model has been applied to the panes and the out-of-pane surfaces. Editor: re-runs the open search against the new row space. Viewer: nothing.</summary>
    void OnModelApplied();

    /// <summary>A side's source was replaced and it has a new document. Editor: wires the four document handlers. Viewer: nothing.</summary>
    void OnSourceReplaced(DiffSide side, TextDocument document);

    /// <summary>The current change moved, or the change count did. Navigation is on both surfaces, so both answer this.</summary>
    void OnChangeSetMoved();

    /// <summary>
    /// The set of folded runs changed, so whatever the surface offers for folding has to be
    /// re-evaluated. Separate from <see cref="OnChangeSetMoved"/>: a fold moves no change, and a
    /// change moves no fold. Both surfaces fold, so both answer this.
    /// </summary>
    void OnFoldsChanged();

    /// <summary>
    /// Whether <paramref name="side"/> holds an unsaved edit, for the header's dirty marker and
    /// the strip's dirty lane. Distinct from <see cref="IsEdited"/>: a save clears this and leaves
    /// that one set, which is why they cannot be one point. Viewer: always <c>false</c>.
    /// </summary>
    bool IsDirty(DiffSide side);

    /// <summary>The find lane of the status strip, or <c>null</c> where there is no find bar.</summary>
    string? FindStripText();

    /// <summary>
    /// Whether <paramref name="side"/> has been typed into since its source was assigned, which is
    /// what makes the next build read the pane's live text instead. Viewer: always <c>false</c>.
    /// </summary>
    bool IsEdited(DiffSide side);

    /// <summary>
    /// A side's document was replaced. The controller owns the storage; raising the change is the
    /// control's, because the property is registered against the control's own type and a
    /// <c>DirectProperty</c> of one sibling is not in the other's registry. Editor: the public
    /// accessor's notification. Viewer: an internal one.
    /// </summary>
    void OnDocumentReplaced(DiffSide side, TextDocument oldValue, TextDocument newValue);

    // ── The three events, which only their declaring type may raise ─────────────────────────

    /// <summary>Raises the surface's <c>BuildCompleted</c>; CS0079 is why this is a method and not the event.</summary>
    void RaiseBuildCompleted(DiffBuildCompletedEventArgs e);

    /// <summary>Raises the surface's <c>BuildFailed</c>.</summary>
    void RaiseBuildFailed(DiffBuildFailedEventArgs e);

    /// <summary>Raises the surface's <c>RenderFault</c>.</summary>
    void RaiseRenderFault(RenderFaultEventArgs e);
}
