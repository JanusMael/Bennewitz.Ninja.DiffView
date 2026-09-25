using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// A side-by-side diff you scroll and look at: two <see cref="DiffPanePresenter"/>s over the
/// sources' own documents, headers above, a banner for what failed or was skipped, the connector
/// gutter and the overview map, and a status strip below — and none of the verbs. There is no find
/// bar, no context menu, no copy arrow, no save or revert, and no key binding beyond what a text
/// pane does with an arrow key. The panes are read-only, and stay focusable because that is how
/// keyboard scrolling and select-to-copy work.
/// </summary>
/// <remarks>
/// <para>
/// It builds, renders and changes state exactly as <see cref="SideBySideDiffView"/> does, through
/// the same internal controller: assigning <see cref="LeftSource"/> or <see cref="RightSource"/>
/// replaces that side's document and builds, changing an option rebuilds, builds run on a worker
/// and the latest wins, and the control is always in exactly one <see cref="State"/>. The two are
/// sibling controls rather than one deriving from the other, because a derived type cannot narrow:
/// a reference typed as the editor would reach every verb this control leaves out.
/// </para>
/// <para>
/// Navigation stays, because none of it touches content: the current change, the four walks and
/// their commands, <see cref="GoToChange"/> and <see cref="SelectChange"/>; the overview map and
/// the connector gutter move the current change on a click. Folding stays through
/// <see cref="UnchangedContextRows"/>, and a click on a fold's placeholder opens the run again.
/// </para>
/// <para>
/// Every property the two controls share is the editor's own registration, owned here through
/// <c>AddOwner</c> — a style, a binding or a property-changed handler written against one reaches
/// the other.
/// </para>
/// </remarks>
public class DiffViewer : TemplatedControl, IDiffSurface
{
    /// <summary>The template part hosting the left pane.</summary>
    public const string LeftPanePart = SideBySideDiffView.LeftPanePart;

    /// <summary>The template part hosting the right pane.</summary>
    public const string RightPanePart = SideBySideDiffView.RightPanePart;

    /// <summary>The template part hosting the left header.</summary>
    public const string LeftHeaderPart = SideBySideDiffView.LeftHeaderPart;

    /// <summary>The template part hosting the right header.</summary>
    public const string RightHeaderPart = SideBySideDiffView.RightHeaderPart;

    /// <summary>The template part hosting the status strip.</summary>
    public const string StatusStripPart = SideBySideDiffView.StatusStripPart;

    /// <summary>The template part hosting the banner's action button.</summary>
    public const string BannerActionPart = SideBySideDiffView.BannerActionPart;

    /// <summary>The banner, whose visibility this control drives through its pseudo-classes.</summary>
    public const string BannerPart = SideBySideDiffView.BannerPart;

    /// <summary>The template part holding the two headers, whose star columns follow <see cref="SplitRatio"/>.</summary>
    public const string HeadersPart = SideBySideDiffView.HeadersPart;

    /// <summary>The header slots standing in for the overview map, one at each end.</summary>
    public const string HeaderLeftSpacerPart = SideBySideDiffView.HeaderLeftSpacerPart;

    /// <summary>The header slot at the other end; exactly one of the two has the map's width.</summary>
    public const string HeaderRightSpacerPart = SideBySideDiffView.HeaderRightSpacerPart;

    /// <summary>The template part holding the panes, gutter and minimap, whose star columns follow <see cref="SplitRatio"/>.</summary>
    public const string PanesPart = SideBySideDiffView.PanesPart;

    /// <summary>The template part hosting the connector gutter.</summary>
    public const string GutterPart = SideBySideDiffView.GutterPart;

    /// <summary>The template part hosting the minimap.</summary>
    public const string MinimapPart = SideBySideDiffView.MinimapPart;

    /// <summary>How long a build runs before the strip shows progress.</summary>
    public static readonly TimeSpan SlowBuildThreshold = SideBySideDiffView.SlowBuildThreshold;

    /// <summary>Identifies the <see cref="LeftSource"/> property.</summary>
    public static readonly StyledProperty<PaneSource?> LeftSourceProperty =
        SideBySideDiffView.LeftSourceProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="RightSource"/> property.</summary>
    public static readonly StyledProperty<PaneSource?> RightSourceProperty =
        SideBySideDiffView.RightSourceProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="IgnoreWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> IgnoreWhitespaceProperty =
        SideBySideDiffView.IgnoreWhitespaceProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="IgnoreCase"/> property.</summary>
    public static readonly StyledProperty<bool> IgnoreCaseProperty =
        SideBySideDiffView.IgnoreCaseProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="WordDiff"/> property.</summary>
    public static readonly StyledProperty<WordDiffMode> WordDiffProperty =
        SideBySideDiffView.WordDiffProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="MaxWordDiffLineLength"/> property.</summary>
    public static readonly StyledProperty<int> MaxWordDiffLineLengthProperty =
        SideBySideDiffView.MaxWordDiffLineLengthProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ForceAlignment"/> property.</summary>
    public static readonly StyledProperty<bool> ForceAlignmentProperty =
        SideBySideDiffView.ForceAlignmentProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="SyncHorizontalScroll"/> property.</summary>
    public static readonly StyledProperty<bool> SyncHorizontalScrollProperty =
        SideBySideDiffView.SyncHorizontalScrollProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="IsCaretBlinkEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsCaretBlinkEnabledProperty =
        SideBySideDiffView.IsCaretBlinkEnabledProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="UseSyntaxHighlighting"/> property.</summary>
    public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
        SideBySideDiffView.UseSyntaxHighlightingProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="UnchangedContextRows"/> property.</summary>
    public static readonly StyledProperty<int?> UnchangedContextRowsProperty =
        SideBySideDiffView.UnchangedContextRowsProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ShowWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> ShowWhitespaceProperty =
        SideBySideDiffView.ShowWhitespaceProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ShowMinimap"/> property.</summary>
    public static readonly StyledProperty<bool> ShowMinimapProperty =
        SideBySideDiffView.ShowMinimapProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ShowHeaders"/> property.</summary>
    public static readonly StyledProperty<bool> ShowHeadersProperty =
        SideBySideDiffView.ShowHeadersProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ShowStatusStrip"/> property.</summary>
    public static readonly StyledProperty<bool> ShowStatusStripProperty =
        SideBySideDiffView.ShowStatusStripProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ShowBanner"/> property.</summary>
    public static readonly StyledProperty<bool> ShowBannerProperty =
        SideBySideDiffView.ShowBannerProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="MinimapPlacement"/> property.</summary>
    public static readonly StyledProperty<MinimapPlacement> MinimapPlacementProperty =
        SideBySideDiffView.MinimapPlacementProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="ShowLineEndings"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLineEndingsProperty =
        SideBySideDiffView.ShowLineEndingsProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="TabWidth"/> property. Coerced to at least 1.</summary>
    public static readonly StyledProperty<int> TabWidthProperty =
        SideBySideDiffView.TabWidthProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="PaneFontSize"/> property. <see cref="double.NaN"/> leaves the panes' own.</summary>
    public static readonly StyledProperty<double> PaneFontSizeProperty =
        SideBySideDiffView.PaneFontSizeProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="PaneFontFamily"/> property. <c>null</c> leaves the panes' own.</summary>
    public static readonly StyledProperty<FontFamily?> PaneFontFamilyProperty =
        SideBySideDiffView.PaneFontFamilyProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="LeftPaneName"/> property.</summary>
    public static readonly StyledProperty<string> LeftPaneNameProperty =
        SideBySideDiffView.LeftPaneNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="RightPaneName"/> property.</summary>
    public static readonly StyledProperty<string> RightPaneNameProperty =
        SideBySideDiffView.RightPaneNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="LeftHeaderName"/> property.</summary>
    public static readonly StyledProperty<string> LeftHeaderNameProperty =
        SideBySideDiffView.LeftHeaderNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="RightHeaderName"/> property.</summary>
    public static readonly StyledProperty<string> RightHeaderNameProperty =
        SideBySideDiffView.RightHeaderNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="StatusStripName"/> property.</summary>
    public static readonly StyledProperty<string> StatusStripNameProperty =
        SideBySideDiffView.StatusStripNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="GutterName"/> property.</summary>
    public static readonly StyledProperty<string> GutterNameProperty =
        SideBySideDiffView.GutterNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="MinimapName"/> property.</summary>
    public static readonly StyledProperty<string> MinimapNameProperty =
        SideBySideDiffView.MinimapNameProperty.AddOwner<DiffViewer>();

    /// <summary>Identifies the <see cref="CurrentChangeIndex"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, int> CurrentChangeIndexProperty =
        SideBySideDiffView.CurrentChangeIndexProperty.AddOwner<DiffViewer>(o => o.CurrentChangeIndex, (o, v) => o.CurrentChangeIndex = v, unsetValue: -1);

    /// <summary>Identifies the <see cref="SplitRatio"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, double> SplitRatioProperty =
        SideBySideDiffView.SplitRatioProperty.AddOwner<DiffViewer>(o => o.SplitRatio, (o, v) => o.SplitRatio = v, unsetValue: 0.5);

    /// <summary>Identifies the <see cref="State"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, DiffViewState> StateProperty =
        SideBySideDiffView.StateProperty.AddOwner<DiffViewer>(o => o.State);

    /// <summary>Identifies the <see cref="StateMessage"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, string?> StateMessageProperty =
        SideBySideDiffView.StateMessageProperty.AddOwner<DiffViewer>(o => o.StateMessage);

    /// <summary>Identifies the <see cref="ChangeCount"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, int> ChangeCountProperty =
        SideBySideDiffView.ChangeCountProperty.AddOwner<DiffViewer>(o => o.ChangeCount);

    /// <summary>Identifies the <see cref="BannerKind"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, DiffBannerKind> BannerKindProperty =
        SideBySideDiffView.BannerKindProperty.AddOwner<DiffViewer>(o => o.BannerKind);

    /// <summary>Identifies the <see cref="BannerMessage"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, string?> BannerMessageProperty =
        SideBySideDiffView.BannerMessageProperty.AddOwner<DiffViewer>(o => o.BannerMessage);

    /// <summary>Identifies the <see cref="BannerActionText"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, string?> BannerActionTextProperty =
        SideBySideDiffView.BannerActionTextProperty.AddOwner<DiffViewer>(o => o.BannerActionText);

    /// <summary>Identifies the <see cref="IsStale"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, bool> IsStaleProperty =
        SideBySideDiffView.IsStaleProperty.AddOwner<DiffViewer>(o => o.IsStale);

    /// <summary>Identifies the <see cref="IsBuildingSlowly"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, bool> IsBuildingSlowlyProperty =
        SideBySideDiffView.IsBuildingSlowlyProperty.AddOwner<DiffViewer>(o => o.IsBuildingSlowly);

    /// <summary>Identifies the <see cref="FocusedSide"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, DiffSide?> FocusedSideProperty =
        SideBySideDiffView.FocusedSideProperty.AddOwner<DiffViewer>(o => o.FocusedSide);

    /// <summary>Identifies the <see cref="CaretLine"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, int> CaretLineProperty =
        SideBySideDiffView.CaretLineProperty.AddOwner<DiffViewer>(o => o.CaretLine);

    /// <summary>Identifies the <see cref="CaretColumn"/> property.</summary>
    public static readonly DirectProperty<DiffViewer, int> CaretColumnProperty =
        SideBySideDiffView.CaretColumnProperty.AddOwner<DiffViewer>(o => o.CaretColumn);

    // Document, Diagnostics, Warnings and the two documents are deliberately NOT registered here.
    // AvaloniaObject.GetValue resolves a direct property by its Id against the object's own type, so
    // a registration of any visibility makes the editor's PUBLIC identity read this control's value:
    // viewer.GetValue(SideBySideDiffView.LeftDocumentProperty) returned the live, editable document.
    // The controller does not need them: SetAndRaise through the editor's identity stores, raises and
    // dispatches on a control that never registered it (measured, Avalonia 12.1.2).

    private readonly DiffBuildController _controller;
    private readonly DelegateCommand _retry;
    private readonly DelegateCommand _force;
    private readonly DelegateCommand _nextChange;
    private readonly DelegateCommand _previousChange;
    private readonly DelegateCommand _firstChange;
    private readonly DelegateCommand _lastChange;

    /// <summary>Creates the control with its compiled theme merged into its own resources.</summary>
    public DiffViewer()
    {
        Resources.MergedDictionaries.Add(new DiffViewerTheme());
        // First, and before any command closure can run: every one of them reads state that
        // lives in the controller.
        _controller = new DiffBuildController(this);
        _retry = new DelegateCommand(Retry, () => State == DiffViewState.Failed);
        _force = new DelegateCommand(ForceAlign, () => BannerKind == DiffBannerKind.TooDifferentToAlign);
        _nextChange = new DelegateCommand(NextChange, () => ChangeCount > 0);
        _previousChange = new DelegateCommand(PreviousChange, () => ChangeCount > 0);
        _firstChange = new DelegateCommand(FirstChange, () => ChangeCount > 0);
        _lastChange = new DelegateCommand(LastChange, () => ChangeCount > 0);
        _controller.Initialize();
    }

    /// <summary>
    /// A build produced a document and the panes show it. Its <see cref="DiffBuildCompletedEventArgs.Result"/>
    /// is how a host reaches the model, the diagnostics and the warnings.
    /// </summary>
    public event EventHandler<DiffBuildCompletedEventArgs>? BuildCompleted;

    /// <summary>A build failed; the control is <see cref="DiffViewState.Failed"/>.</summary>
    public event EventHandler<DiffBuildFailedEventArgs>? BuildFailed;

    /// <summary>A pane's decorator threw and disabled itself; the control is <see cref="DiffViewState.Degraded"/>.</summary>
    public event EventHandler<RenderFaultEventArgs>? RenderFault;

    /// <summary>The left side's input; assigning it replaces the left document and builds.</summary>
    public PaneSource? LeftSource
    {
        get => GetValue(LeftSourceProperty);
        set => SetValue(LeftSourceProperty, value);
    }

    /// <summary>The right side's input; assigning it replaces the right document and builds.</summary>
    public PaneSource? RightSource
    {
        get => GetValue(RightSourceProperty);
        set => SetValue(RightSourceProperty, value);
    }

    /// <summary>Leading and trailing whitespace does not count as a difference. Changing it rebuilds.</summary>
    public bool IgnoreWhitespace
    {
        get => GetValue(IgnoreWhitespaceProperty);
        set => SetValue(IgnoreWhitespaceProperty, value);
    }

    /// <summary>Letter case does not count as a difference. Changing it rebuilds.</summary>
    public bool IgnoreCase
    {
        get => GetValue(IgnoreCaseProperty);
        set => SetValue(IgnoreCaseProperty, value);
    }

    /// <summary>The word-level mode. Changing it rebuilds.</summary>
    public WordDiffMode WordDiff
    {
        get => GetValue(WordDiffProperty);
        set => SetValue(WordDiffProperty, value);
    }

    /// <summary>A line longer than this gets no word-level pieces. Changing it rebuilds.</summary>
    public int MaxWordDiffLineLength
    {
        get => GetValue(MaxWordDiffLineLengthProperty);
        set => SetValue(MaxWordDiffLineLengthProperty, value);
    }

    /// <summary>Align regardless of similarity — the banner's Force. Changing it rebuilds.</summary>
    public bool ForceAlignment
    {
        get => GetValue(ForceAlignmentProperty);
        set => SetValue(ForceAlignmentProperty, value);
    }

    /// <summary>Whether the horizontal offsets are coupled as the vertical ones always are.</summary>
    public bool SyncHorizontalScroll
    {
        get => GetValue(SyncHorizontalScrollProperty);
        set => SetValue(SyncHorizontalScrollProperty, value);
    }

    /// <summary>Whether the focused pane's caret blinks.</summary>
    public bool IsCaretBlinkEnabled
    {
        get => GetValue(IsCaretBlinkEnabledProperty);
        set => SetValue(IsCaretBlinkEnabledProperty, value);
    }

    /// <summary>
    /// Whether both panes colour their text from a TextMate grammar, chosen from each side's
    /// <see cref="PaneSource.Path"/> — its <see cref="PaneSource.Title"/> when it has no path — by
    /// extension, with the theme following the variant. On by default; a side whose extension no
    /// grammar claims stays plain text. The diff highlighting is a separate layer either way.
    /// </summary>
    public bool UseSyntaxHighlighting
    {
        get => GetValue(UseSyntaxHighlightingProperty);
        set => SetValue(UseSyntaxHighlightingProperty, value);
    }

    /// <summary>Whether both panes draw spaces and tabs as glyphs. Off by default.</summary>
    public bool ShowWhitespace
    {
        get => GetValue(ShowWhitespaceProperty);
        set => SetValue(ShowWhitespaceProperty, value);
    }

    /// <summary>
    /// Rows kept either side of every change, with the unchanged runs between them folded behind
    /// a placeholder. <c>null</c> — the default — folds nothing; <c>0</c> hides every matching
    /// row. A click on a placeholder opens its run again.
    /// </summary>
    public int? UnchangedContextRows
    {
        get => GetValue(UnchangedContextRowsProperty);
        set => SetValue(UnchangedContextRowsProperty, value);
    }

    /// <summary>
    /// Whether the overview map is shown beside the panes. On by default. Off, its column takes
    /// no width at all, so the panes get it back rather than looking at a gap.
    /// </summary>
    public bool ShowMinimap
    {
        get => GetValue(ShowMinimapProperty);
        set => SetValue(ShowMinimapProperty, value);
    }

    /// <summary>
    /// Whether the pane headers are shown. On by default. Off, their row takes no height, so the
    /// panes get it back — and the accent that says which pane has focus goes with them.
    /// </summary>
    public bool ShowHeaders
    {
        get => GetValue(ShowHeadersProperty);
        set => SetValue(ShowHeadersProperty, value);
    }

    /// <summary>
    /// Whether the status strip is shown. On by default. Off, it takes no height, and the
    /// transient message lane goes with it — so does the caret position.
    /// </summary>
    public bool ShowStatusStrip
    {
        get => GetValue(ShowStatusStripProperty);
        set => SetValue(ShowStatusStripProperty, value);
    }

    /// <summary>
    /// Whether the banner may be shown when there is something to say. On by default. Off, it
    /// never appears, and the retry and force-alignment affordances go with it — both verbs
    /// stay available as commands. A banner with nothing to say is hidden either way.
    /// </summary>
    public bool ShowBanner
    {
        get => GetValue(ShowBannerProperty);
        set => SetValue(ShowBannerProperty, value);
    }

    /// <summary>
    /// Which edge of the panes the overview map is docked against; <see cref="MinimapPlacement.Right"/>
    /// by default. The map's lanes do not follow this — a lane names a file, not an edge — but the
    /// current-block marker does.
    /// </summary>
    public MinimapPlacement MinimapPlacement
    {
        get => GetValue(MinimapPlacementProperty);
        set => SetValue(MinimapPlacementProperty, value);
    }

    /// <summary>Whether both panes draw a line terminator at the end of each line. Off by default.</summary>
    public bool ShowLineEndings
    {
        get => GetValue(ShowLineEndingsProperty);
        set => SetValue(ShowLineEndingsProperty, value);
    }

    /// <summary>
    /// Columns a tab advances to in both panes, 4 by default and never below 1. Rows keep their
    /// heights, so the panes stay aligned across a change.
    /// </summary>
    public int TabWidth
    {
        get => GetValue(TabWidthProperty);
        set => SetValue(TabWidthProperty, value);
    }

    /// <summary>
    /// The panes' font size, or <see cref="double.NaN"/> — the default — to leave the size their
    /// own theme sets. Changing it re-primes both panes, so the row heights and the two extents
    /// follow it.
    /// </summary>
    public double PaneFontSize
    {
        get => GetValue(PaneFontSizeProperty);
        set => SetValue(PaneFontSizeProperty, value);
    }

    /// <summary>
    /// The panes' font family, or <c>null</c> — the default — to leave the family their theme
    /// sets, which is the <c>DiffView.MonospaceFontFamily</c> stack a host can redefine. Changing
    /// it re-primes both panes.
    /// </summary>
    public FontFamily? PaneFontFamily
    {
        get => GetValue(PaneFontFamilyProperty);
        set => SetValue(PaneFontFamilyProperty, value);
    }

    /// <summary>The left pane's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string LeftPaneName
    {
        get => GetValue(LeftPaneNameProperty);
        set => SetValue(LeftPaneNameProperty, value);
    }

    /// <summary>The right pane's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string RightPaneName
    {
        get => GetValue(RightPaneNameProperty);
        set => SetValue(RightPaneNameProperty, value);
    }

    /// <summary>The left header's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string LeftHeaderName
    {
        get => GetValue(LeftHeaderNameProperty);
        set => SetValue(LeftHeaderNameProperty, value);
    }

    /// <summary>The right header's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string RightHeaderName
    {
        get => GetValue(RightHeaderNameProperty);
        set => SetValue(RightHeaderNameProperty, value);
    }

    /// <summary>The status strip's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string StatusStripName
    {
        get => GetValue(StatusStripNameProperty);
        set => SetValue(StatusStripNameProperty, value);
    }

    /// <summary>The connector gutter's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string GutterName
    {
        get => GetValue(GutterNameProperty);
        set => SetValue(GutterNameProperty, value);
    }

    /// <summary>The minimap's automation name, from <see cref="DiffViewStrings"/>.</summary>
    public string MinimapName
    {
        get => GetValue(MinimapNameProperty);
        set => SetValue(MinimapNameProperty, value);
    }

    /// <summary>
    /// The current change block, -1 for none. Setting it clamps to the blocks, scrolls both
    /// panes so the block is centred, outlines it in the panes, the gutter and the minimap, and
    /// puts "change i of n" in the strip.
    /// </summary>
    public int CurrentChangeIndex
    {
        get => _controller.CurrentChangeIndex;
        set => _controller.SetCurrentChange(value, scroll: true);
    }

    /// <summary>The left pane's share of the panes' width, 0.1 to 0.9; a drag on the gutter changes it.</summary>
    public double SplitRatio
    {
        get => _controller.SplitRatio;
        set => _controller.SplitRatio = value;
    }

    /// <summary>The one state the control is in.</summary>
    public DiffViewState State => _controller.State;

    /// <summary>What the state means to the user: the empty prompt, the warning, or the failure.</summary>
    public string? StateMessage => _controller.StateMessage;

    /// <summary>Change blocks in the model.</summary>
    public int ChangeCount => _controller.ChangeCount;

    /// <summary>Which banner is shown above the panes.</summary>
    public DiffBannerKind BannerKind => _controller.BannerKind;

    /// <summary>The banner's text.</summary>
    public string? BannerMessage => _controller.BannerMessage;

    /// <summary>The banner's action label, or <c>null</c> when the banner offers none.</summary>
    public string? BannerActionText => _controller.BannerActionText;

    /// <summary>
    /// Whether the result on screen is about to be replaced by a running build — which is what
    /// tells a rebuild with a stale result still visible from a first build.
    /// </summary>
    public bool IsStale => _controller.IsStale;

    /// <summary>
    /// Whether the running build has passed <see cref="SlowBuildThreshold"/>. There is no event for
    /// a build starting, so this and <see cref="IsStale"/> are how a host learns that one is running long.
    /// </summary>
    public bool IsBuildingSlowly => _controller.IsBuildingSlowly;

    /// <summary>
    /// The pane with keyboard focus, or <c>null</c>. With <see cref="CaretLine"/> and
    /// <see cref="CaretColumn"/>, the only access to caret state: the panes are not public.
    /// </summary>
    public DiffSide? FocusedSide => _controller.FocusedSide;

    /// <summary>The focused pane's caret line, 1-based; 0 without focus.</summary>
    public int CaretLine => _controller.CaretLine;

    /// <summary>The focused pane's caret column, 1-based; 0 without focus.</summary>
    public int CaretColumn => _controller.CaretColumn;

    /// <summary>
    /// Creates the category loggers (<see cref="DiffViewLogCategories"/>) when set. State
    /// transitions log at <c>Information</c>, warnings at <c>Warning</c>, faults at <c>Error</c>
    /// with the exception, cancellation at <c>Debug</c>; never document text. This control never
    /// creates the find category, having no find.
    /// </summary>
    public ILoggerFactory? LoggerFactory
    {
        get => _controller.LoggerFactory;
        set => _controller.LoggerFactory = value;
    }

    /// <summary>
    /// The transient message lane; its typed helpers are the only way to emit one. The instance
    /// does not outlive the view's place in the visual tree: leaving it releases the controller
    /// and the next access builds a fresh one, so a host that stores the reference, sets a delay
    /// on it or subscribes to <see cref="StatusController.Changed"/> has to do so again after a
    /// re-attach.
    /// </summary>
    public StatusController Status => _controller.Status;

    /// <summary>Rebuilds after a failure.</summary>
    public ICommand RetryCommand => _retry;

    /// <summary>Sets <see cref="ForceAlignment"/> from the too-different banner.</summary>
    public ICommand ForceAlignmentCommand => _force;

    /// <summary>Moves to the next change.</summary>
    public ICommand NextChangeCommand => _nextChange;

    /// <summary>Moves to the previous change.</summary>
    public ICommand PreviousChangeCommand => _previousChange;

    /// <summary>Moves to the first change.</summary>
    public ICommand FirstChangeCommand => _firstChange;

    /// <summary>Moves to the last change.</summary>
    public ICommand LastChangeCommand => _lastChange;

    /// <summary>The clock behind the transient messages' auto-clear and the slow-build threshold. Set it before the first build.</summary>
    internal TimeProvider TimeProvider
    {
        get => _controller.TimeProvider;
        set => _controller.TimeProvider = value;
    }

    /// <summary>The build routine; tests replace it to make a build slow or throw.</summary>
    internal Func<PaneSource, PaneSource, DiffOptions, CancellationToken, DiffBuildResult> Builder
    {
        get => _controller.Builder;
        set => _controller.Builder = value;
    }

    /// <summary>The in-flight build, completing when its outcome has been applied or discarded; <c>null</c> when idle.</summary>
    internal Task? CurrentBuild => _controller.CurrentBuild;

    /// <summary>The model the panes render, or <c>null</c> before the first build lands.</summary>
    internal SideBySideDocument? Document => _controller.Document;

    /// <summary>What the last successful build measured.</summary>
    internal DiffDiagnostics? Diagnostics => _controller.Diagnostics;

    /// <summary>The last successful build's warnings.</summary>
    internal IReadOnlyList<DiffWarning> Warnings => _controller.Warnings;

    /// <summary>The word-level lookup of the current model, bound to the options its build ran under.</summary>
    internal WordDiffLookup? WordDiffLookup => _controller.WordDiffLookup;

    /// <summary>The live left document: the source text, never padded.</summary>
    internal TextDocument LeftDocument => _controller.LeftDocument;

    /// <summary>The live right document: the source text, never padded.</summary>
    internal TextDocument RightDocument => _controller.RightDocument;

    internal ChangeConnectorGutter? Gutter => _controller.Gutter;

    internal DiffMinimap? Minimap => _controller.Minimap;

    internal DiffPanePresenter? LeftPane => _controller.LeftPane;

    internal DiffPanePresenter? RightPane => _controller.RightPane;

    internal DiffPaneHeader? LeftHeader => _controller.LeftHeader;

    internal DiffPaneHeader? RightHeader => _controller.RightHeader;

    internal DiffStatusStrip? StatusStrip => _controller.StatusStrip;

    internal StatusController? StatusOrNull => _controller.StatusOrNull;

    internal Button? BannerAction => _controller.BannerAction;

    internal Grid? HeadersGrid => _controller.HeadersGrid;

    internal Border? Banner => _controller.Banner;

    internal ScrollSync? Sync => _controller.Sync;

    /// <summary>Runs the build again with the current sources and options; the panes' faults are cleared.</summary>
    public void Retry() => _controller.Retry();

    /// <summary>Aligns the sides regardless of similarity: sets <see cref="ForceAlignment"/>, which rebuilds.</summary>
    public void ForceAlign() => _controller.ForceAlign();

    /// <summary>Moves to the next change; at the last one it stays and the strip says so.</summary>
    public void NextChange() => _controller.NextChange();

    /// <summary>Moves to the previous change; at the first one, or before any, it stays and the strip says so.</summary>
    public void PreviousChange() => _controller.PreviousChange();

    /// <summary>Moves to the first change.</summary>
    public void FirstChange() => _controller.FirstChange();

    /// <summary>Moves to the last change.</summary>
    public void LastChange() => _controller.LastChange();

    /// <summary>
    /// Makes block <paramref name="index"/> the current change and scrolls to it — what a click on
    /// the connector gutter does; an index the model does not have does nothing.
    /// </summary>
    public void GoToChange(int index) => _controller.GoToChange(index);

    /// <summary>Scrolls both panes so <paramref name="row"/> sits at the centre of the viewport.</summary>
    public void ScrollToRow(int row) => _controller.ScrollToRow(row);

    /// <summary>
    /// Selects block <paramref name="index"/>'s lines on <paramref name="side"/>, or on both
    /// sides where it is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// A side the block has no lines on — the near half of an insertion or a deletion — has its
    /// selection cleared rather than left standing: after "select this change" a selection
    /// elsewhere would be describing a different change.
    /// </remarks>
    public void SelectChange(int index, DiffSide? side) => _controller.SelectChange(index, side);

    /// <summary>The pane for <paramref name="side"/>, once the template has applied.</summary>
    internal DiffPanePresenter? Pane(DiffSide side) => _controller.Pane(side);

    /// <summary>Applies a folding option without going through the property, for the suite.</summary>
    internal RowProjection ApplyFolds(int? contextRows, int minimumFoldedRows = FoldPlan.DefaultMinimumFoldedRows)
    {
        return _controller.ApplyFolds(contextRows, minimumFoldedRows);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _controller.ApplyTemplate(e);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _controller.OnDetached();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChangeCountProperty)
        {
            RaiseNavigationCanExecuteChanged();
        }

        if (change.Property == StateProperty
            || change.Property == BannerKindProperty
            || change.Property == ShowBannerProperty)
        {
            // The controller sets the pseudo-classes from the same three, and these two commands
            // are this control's to re-evaluate.
            _retry.RaiseCanExecuteChanged();
            _force.RaiseCanExecuteChanged();
        }

        _controller.OnPropertyChanged(change);
    }

    private void RaiseNavigationCanExecuteChanged()
    {
        _nextChange.RaiseCanExecuteChanged();
        _previousChange.RaiseCanExecuteChanged();
        _firstChange.RaiseCanExecuteChanged();
        _lastChange.RaiseCanExecuteChanged();
    }

    // ── The controller's seam ──────────────────────────────────────────────────────────────

    // Implemented explicitly, so none of it reaches this control's public surface. Where the
    // editor wires a verb or reports a piece of editing state, this answers that it has none.

    /// <inheritdoc/>
    TemplatedControl IDiffSurface.Control => this;

    /// <inheritdoc/>
    IPseudoClasses IDiffSurface.PseudoClasses => PseudoClasses;

    /// <inheritdoc/>
    bool IDiffSurface.SetAndRaise<T>(DirectPropertyBase<T> property, ref T field, T value)
    {
        return SetAndRaise(property, ref field, value);
    }

    /// <inheritdoc/>
    void IDiffSurface.OnPartsAttached(TemplateAppliedEventArgs e)
    {
        // No find bar and no menus: nothing to wire.
    }

    /// <inheritdoc/>
    void IDiffSurface.OnPartsDetaching()
    {
        // Nothing was wired.
    }

    /// <inheritdoc/>
    void IDiffSurface.OnPaneAttached(DiffPanePresenter pane, DiffSide side)
    {
        // A pane is read-only and offers no copy arrow by default, and without a handler its
        // context request opens nothing — so a viewer's pane is left exactly as it comes.
    }

    /// <inheritdoc/>
    void IDiffSurface.OnModelApplied()
    {
        // No search to re-run.
    }

    /// <inheritdoc/>
    void IDiffSurface.OnSourceReplaced(DiffSide side, TextDocument document)
    {
        // Nothing listens to a document here: the panes cannot be typed into.
    }

    /// <inheritdoc/>
    void IDiffSurface.OnChangeSetMoved() => RaiseNavigationCanExecuteChanged();

    /// <inheritdoc/>
    void IDiffSurface.OnFoldsChanged()
    {
        // Folding is a property here, not a set of commands to re-evaluate.
    }

    /// <inheritdoc/>
    bool IDiffSurface.IsDirty(DiffSide side) => false;

    /// <inheritdoc/>
    string? IDiffSurface.FindStripText() => null;

    /// <inheritdoc/>
    bool IDiffSurface.IsEdited(DiffSide side) => false;

    /// <inheritdoc/>
    void IDiffSurface.OnDocumentReplaced(DiffSide side, TextDocument oldValue, TextDocument newValue)
    {
        // Nothing: a notification would carry the document to every PropertyChanged listener.
    }

    /// <inheritdoc/>
    void IDiffSurface.RaiseBuildCompleted(DiffBuildCompletedEventArgs e) => BuildCompleted?.Invoke(this, e);

    /// <inheritdoc/>
    void IDiffSurface.RaiseBuildFailed(DiffBuildFailedEventArgs e) => BuildFailed?.Invoke(this, e);

    /// <inheritdoc/>
    void IDiffSurface.RaiseRenderFault(RenderFaultEventArgs e) => RenderFault?.Invoke(this, e);
}
