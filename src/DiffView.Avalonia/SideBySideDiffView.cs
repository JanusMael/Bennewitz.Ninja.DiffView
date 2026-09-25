using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;
using TextInfo = Bennewitz.Ninja.DiffView.Core.TextInfo;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The side-by-side diff: two <see cref="DiffPanePresenter"/>s over the sources' own documents,
/// headers above, a banner for what failed or was skipped, a status strip below, and vertical
/// scrolling coupled 1:1. Assigning <see cref="LeftSource"/> or <see cref="RightSource"/>
/// replaces that side's document and builds; changing an option rebuilds and swaps the model
/// only — the documents, and with them caret, selection, scroll and undo, are untouched. Builds
/// run on a worker, latest wins; the control is always in exactly one <see cref="State"/>, and
/// every transition, warning and fault is logged through <see cref="LoggerFactory"/> without a
/// character of document text.
/// </summary>
public class SideBySideDiffView : TemplatedControl, IDiffSurface
{
    /// <summary>The template part hosting the left pane.</summary>
    public const string LeftPanePart = "PART_LeftPane";

    /// <summary>The template part hosting the right pane.</summary>
    public const string RightPanePart = "PART_RightPane";

    /// <summary>The template part hosting the left header.</summary>
    public const string LeftHeaderPart = "PART_LeftHeader";

    /// <summary>The template part hosting the right header.</summary>
    public const string RightHeaderPart = "PART_RightHeader";

    /// <summary>The template part hosting the status strip.</summary>
    public const string StatusStripPart = "PART_StatusStrip";

    /// <summary>The template part hosting the banner's action button.</summary>
    public const string BannerActionPart = "PART_BannerAction";

    /// <summary>The banner, whose visibility this control drives through its pseudo-classes.</summary>
    public const string BannerPart = "PART_Banner";

    /// <summary>The template part holding the two headers, whose star columns follow <see cref="SplitRatio"/>.</summary>
    public const string HeadersPart = "PART_Headers";

    /// <summary>The header slots standing in for the overview map, one at each end.</summary>
    public const string HeaderLeftSpacerPart = "PART_HeaderLeftSpacer";

    /// <summary>The header slot at the other end; exactly one of the two has the map's width.</summary>
    public const string HeaderRightSpacerPart = "PART_HeaderRightSpacer";

    /// <summary>The template part holding the panes, gutter and minimap, whose star columns follow <see cref="SplitRatio"/>.</summary>
    public const string PanesPart = "PART_Panes";

    /// <summary>The template part hosting the connector gutter.</summary>
    public const string GutterPart = "PART_Gutter";

    /// <summary>The template part hosting the minimap.</summary>
    public const string MinimapPart = "PART_Minimap";

    /// <summary>The template part hosting the find bar.</summary>
    public const string FindBarPart = "PART_FindBar";

    /// <summary>How long a build runs before the strip shows progress.</summary>
    public static readonly TimeSpan SlowBuildThreshold = TimeSpan.FromMilliseconds(100);

    /// <summary>How long the query and the find options rest before the search runs.</summary>
    public static readonly TimeSpan FindDebounce = TimeSpan.FromMilliseconds(150);

    /// <summary>How long an edited pane rests before the re-diff its edit triggered runs.</summary>
    public static readonly TimeSpan ReDiffDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>Identifies the <see cref="LeftSource"/> property.</summary>
    public static readonly StyledProperty<PaneSource?> LeftSourceProperty =
        AvaloniaProperty.Register<SideBySideDiffView, PaneSource?>(nameof(LeftSource));

    /// <summary>Identifies the <see cref="RightSource"/> property.</summary>
    public static readonly StyledProperty<PaneSource?> RightSourceProperty =
        AvaloniaProperty.Register<SideBySideDiffView, PaneSource?>(nameof(RightSource));

    /// <summary>Identifies the <see cref="LeftReadOnly"/> property.</summary>
    public static readonly StyledProperty<bool> LeftReadOnlyProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(LeftReadOnly), defaultValue: true);

    /// <summary>Identifies the <see cref="RightReadOnly"/> property.</summary>
    public static readonly StyledProperty<bool> RightReadOnlyProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(RightReadOnly), defaultValue: true);

    /// <summary>Identifies the <see cref="LiveReDiff"/> property.</summary>
    public static readonly StyledProperty<bool> LiveReDiffProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(LiveReDiff), defaultValue: true);

    /// <summary>Identifies the <see cref="ReDiffDelay"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> ReDiffDelayProperty =
        AvaloniaProperty.Register<SideBySideDiffView, TimeSpan>(nameof(ReDiffDelay), defaultValue: ReDiffDebounce);

    /// <summary>Identifies the <see cref="IgnoreWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> IgnoreWhitespaceProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(IgnoreWhitespace));

    /// <summary>Identifies the <see cref="IgnoreCase"/> property.</summary>
    public static readonly StyledProperty<bool> IgnoreCaseProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(IgnoreCase));

    /// <summary>Identifies the <see cref="WordDiff"/> property.</summary>
    public static readonly StyledProperty<WordDiffMode> WordDiffProperty =
        AvaloniaProperty.Register<SideBySideDiffView, WordDiffMode>(nameof(WordDiff), WordDiffMode.Word);

    /// <summary>Identifies the <see cref="MaxWordDiffLineLength"/> property.</summary>
    public static readonly StyledProperty<int> MaxWordDiffLineLengthProperty =
        AvaloniaProperty.Register<SideBySideDiffView, int>(nameof(MaxWordDiffLineLength), DiffOptions.Default.MaxWordDiffLineLength);

    /// <summary>Identifies the <see cref="ForceAlignment"/> property.</summary>
    public static readonly StyledProperty<bool> ForceAlignmentProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ForceAlignment));

    /// <summary>Identifies the <see cref="SyncHorizontalScroll"/> property.</summary>
    public static readonly StyledProperty<bool> SyncHorizontalScrollProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(SyncHorizontalScroll));

    /// <summary>Identifies the <see cref="IsCaretBlinkEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsCaretBlinkEnabledProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(IsCaretBlinkEnabled), defaultValue: true);

    /// <summary>Identifies the <see cref="UseSyntaxHighlighting"/> property.</summary>
    public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(UseSyntaxHighlighting), defaultValue: true);

    /// <summary>Identifies the <see cref="UnchangedContextRows"/> property.</summary>
    public static readonly StyledProperty<int?> UnchangedContextRowsProperty =
        AvaloniaProperty.Register<SideBySideDiffView, int?>(
            nameof(UnchangedContextRows),
            coerce: static (_, value) => value is { } rows ? Math.Max(0, rows) : null);

    /// <summary>Identifies the <see cref="ShowWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> ShowWhitespaceProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowWhitespace));

    /// <summary>Identifies the <see cref="ShowMinimap"/> property.</summary>
    public static readonly StyledProperty<bool> ShowMinimapProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowMinimap), defaultValue: true);

    /// <summary>Identifies the <see cref="ShowHeaders"/> property.</summary>
    public static readonly StyledProperty<bool> ShowHeadersProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowHeaders), defaultValue: true);

    /// <summary>Identifies the <see cref="ShowStatusStrip"/> property.</summary>
    public static readonly StyledProperty<bool> ShowStatusStripProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowStatusStrip), defaultValue: true);

    /// <summary>Identifies the <see cref="ShowBanner"/> property.</summary>
    public static readonly StyledProperty<bool> ShowBannerProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowBanner), defaultValue: true);

    /// <summary>Identifies the <see cref="MinimapPlacement"/> property.</summary>
    public static readonly StyledProperty<MinimapPlacement> MinimapPlacementProperty =
        AvaloniaProperty.Register<SideBySideDiffView, MinimapPlacement>(nameof(MinimapPlacement));

    /// <summary>Identifies the <see cref="ShowLineEndings"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLineEndingsProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowLineEndings));

    /// <summary>Identifies the <see cref="TabWidth"/> property. Coerced to at least 1.</summary>
    public static readonly StyledProperty<int> TabWidthProperty =
        AvaloniaProperty.Register<SideBySideDiffView, int>(
            nameof(TabWidth),
            defaultValue: 4,
            coerce: static (_, value) => Math.Max(1, value));

    /// <summary>Identifies the <see cref="PaneFontSize"/> property. <see cref="double.NaN"/> leaves the panes' own.</summary>
    public static readonly StyledProperty<double> PaneFontSizeProperty =
        AvaloniaProperty.Register<SideBySideDiffView, double>(nameof(PaneFontSize), defaultValue: double.NaN);

    /// <summary>Identifies the <see cref="PaneFontFamily"/> property. <c>null</c> leaves the panes' own.</summary>
    public static readonly StyledProperty<FontFamily?> PaneFontFamilyProperty =
        AvaloniaProperty.Register<SideBySideDiffView, FontFamily?>(nameof(PaneFontFamily));

    /// <summary>Identifies the <see cref="LeftPaneName"/> property.</summary>
    public static readonly StyledProperty<string> LeftPaneNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(LeftPaneName), string.Empty);

    /// <summary>Identifies the <see cref="RightPaneName"/> property.</summary>
    public static readonly StyledProperty<string> RightPaneNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(RightPaneName), string.Empty);

    /// <summary>Identifies the <see cref="LeftHeaderName"/> property.</summary>
    public static readonly StyledProperty<string> LeftHeaderNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(LeftHeaderName), string.Empty);

    /// <summary>Identifies the <see cref="RightHeaderName"/> property.</summary>
    public static readonly StyledProperty<string> RightHeaderNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(RightHeaderName), string.Empty);

    /// <summary>Identifies the <see cref="StatusStripName"/> property.</summary>
    public static readonly StyledProperty<string> StatusStripNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(StatusStripName), string.Empty);

    /// <summary>Identifies the <see cref="GutterName"/> property.</summary>
    public static readonly StyledProperty<string> GutterNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(GutterName), string.Empty);

    /// <summary>Identifies the <see cref="MinimapName"/> property.</summary>
    public static readonly StyledProperty<string> MinimapNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(MinimapName), string.Empty);

    /// <summary>Identifies the <see cref="CurrentChangeIndex"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, int> CurrentChangeIndexProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, int>(nameof(CurrentChangeIndex), o => o.CurrentChangeIndex, (o, v) => o.CurrentChangeIndex = v, unsetValue: -1);

    /// <summary>Identifies the <see cref="SplitRatio"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, double> SplitRatioProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, double>(nameof(SplitRatio), o => o.SplitRatio, (o, v) => o.SplitRatio = v, unsetValue: 0.5);

    /// <summary>Identifies the <see cref="IsFindBarOpen"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, bool> IsFindBarOpenProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, bool>(nameof(IsFindBarOpen), o => o.IsFindBarOpen, (o, v) => o.IsFindBarOpen = v);

    /// <summary>Identifies the <see cref="FindQuery"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, string> FindQueryProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, string>(nameof(FindQuery), o => o.FindQuery, (o, v) => o.FindQuery = v, unsetValue: "");

    /// <summary>Identifies the <see cref="FindOptions"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, FindOptions> FindOptionsProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, FindOptions>(nameof(FindOptions), o => o.FindOptions, (o, v) => o.FindOptions = v);

    /// <summary>Identifies the <see cref="FindResult"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, FindResult?> FindResultProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, FindResult?>(nameof(FindResult), o => o.FindResult);

    /// <summary>Identifies the <see cref="CurrentFindMatchIndex"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, int> CurrentFindMatchIndexProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, int>(nameof(CurrentFindMatchIndex), o => o.CurrentFindMatchIndex, (o, v) => o.CurrentFindMatchIndex = v, unsetValue: -1);

    /// <summary>Identifies the <see cref="State"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, DiffViewState> StateProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, DiffViewState>(nameof(State), o => o.State);

    /// <summary>Identifies the <see cref="StateMessage"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, string?> StateMessageProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, string?>(nameof(StateMessage), o => o.StateMessage);

    /// <summary>Identifies the <see cref="Document"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, SideBySideDocument?> DocumentProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, SideBySideDocument?>(nameof(Document), o => o.Document);

    /// <summary>Identifies the <see cref="Diagnostics"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, DiffDiagnostics?> DiagnosticsProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, DiffDiagnostics?>(nameof(Diagnostics), o => o.Diagnostics);

    /// <summary>Identifies the <see cref="Warnings"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, IReadOnlyList<DiffWarning>> WarningsProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, IReadOnlyList<DiffWarning>>(nameof(Warnings), o => o.Warnings);

    /// <summary>Identifies the <see cref="ChangeCount"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, int> ChangeCountProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, int>(nameof(ChangeCount), o => o.ChangeCount);

    /// <summary>Identifies the <see cref="BannerKind"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, DiffBannerKind> BannerKindProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, DiffBannerKind>(nameof(BannerKind), o => o.BannerKind);

    /// <summary>Identifies the <see cref="BannerMessage"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, string?> BannerMessageProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, string?>(nameof(BannerMessage), o => o.BannerMessage);

    /// <summary>Identifies the <see cref="BannerActionText"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, string?> BannerActionTextProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, string?>(nameof(BannerActionText), o => o.BannerActionText);

    /// <summary>Identifies the <see cref="IsStale"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, bool> IsStaleProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, bool>(nameof(IsStale), o => o.IsStale);

    /// <summary>Identifies the <see cref="IsBuildingSlowly"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, bool> IsBuildingSlowlyProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, bool>(nameof(IsBuildingSlowly), o => o.IsBuildingSlowly);

    /// <summary>Identifies the <see cref="LeftDocument"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, TextDocument> LeftDocumentProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, TextDocument>(nameof(LeftDocument), o => o.LeftDocument);

    /// <summary>Identifies the <see cref="RightDocument"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, TextDocument> RightDocumentProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, TextDocument>(nameof(RightDocument), o => o.RightDocument);

    /// <summary>Identifies the <see cref="FocusedSide"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, DiffSide?> FocusedSideProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, DiffSide?>(nameof(FocusedSide), o => o.FocusedSide);

    /// <summary>Identifies the <see cref="CaretLine"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, int> CaretLineProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, int>(nameof(CaretLine), o => o.CaretLine);

    /// <summary>Identifies the <see cref="CaretColumn"/> property.</summary>
    public static readonly DirectProperty<SideBySideDiffView, int> CaretColumnProperty =
        AvaloniaProperty.RegisterDirect<SideBySideDiffView, int>(nameof(CaretColumn), o => o.CaretColumn);

    private readonly DiffBuildController _controller;
    private readonly DelegateCommand _retry;
    private readonly DelegateCommand _force;
    private readonly DelegateCommand _nextChange;
    private readonly DelegateCommand _previousChange;
    private readonly DelegateCommand _firstChange;
    private readonly DelegateCommand _lastChange;
    private readonly DelegateCommand _switchPane;
    private readonly DelegateCommand _openFind;
    private readonly DelegateCommand _closeFind;
    private readonly DelegateCommand _findNext;
    private readonly DelegateCommand _findPrevious;
    private readonly DelegateCommand _copyToLeft;
    private readonly DelegateCommand _copyToRight;
    private readonly DelegateCommand _copyBlockToLeft;
    private readonly DelegateCommand _copyBlockToRight;
    private readonly DelegateCommand _goToChange;
    private readonly DelegateCommand _selectBlock;
    private readonly DelegateCommand _showAllRows;
    private readonly DelegateCommand _showDifferencesOnly;
    private readonly DelegateCommand _showContext;
    private readonly DelegateCommand _expandFold;
    private bool _isFindBarOpen;
    private string _findQuery = string.Empty;
    private FindOptions _findOptions = FindOptions.Default;
    private FindResult? _findResult;
    private int _currentFindMatchIndex = -1;
    private ILogger? _findLogger;
    private DiffFindBar? _findBar;
    private DiffSide _findReturnSide = DiffSide.Left;
    private int _findGeneration;
    private CancellationTokenSource? _findCts;
    private ITimer? _findTimer;
    private ITimer? _reDiffTimer;
    private bool _leftEdited;
    private bool _rightEdited;
    private readonly HashSet<int> _leftModifiedLines = [];
    private readonly HashSet<int> _rightModifiedLines = [];
    private bool _leftDirty;
    private bool _rightDirty;
    private bool _suppressTextChanged;
    private (DateTime WriteTimeUtc, long Length)? _leftStamp;
    private (DateTime WriteTimeUtc, long Length)? _rightStamp;
    private bool _syncingFindBar;

    private readonly DiffKeyBindings _bindings;

    private DiffKeyMap _keyMap = new();

    /// <summary>Creates the control with its compiled theme merged into its own resources.</summary>
    public SideBySideDiffView()
    {
        Resources.MergedDictionaries.Add(new SideBySideDiffViewTheme());
        // First, and before any command closure can run: every one of them reads state that
        // now lives in the controller.
        _controller = new DiffBuildController(this);
        _retry = new DelegateCommand(Retry, () => State == DiffViewState.Failed);
        _force = new DelegateCommand(ForceAlign, () => BannerKind == DiffBannerKind.TooDifferentToAlign);
        _nextChange = new DelegateCommand(NextChange, () => ChangeCount > 0);
        _previousChange = new DelegateCommand(PreviousChange, () => ChangeCount > 0);
        _firstChange = new DelegateCommand(FirstChange, () => ChangeCount > 0);
        _lastChange = new DelegateCommand(LastChange, () => ChangeCount > 0);
        _switchPane = new DelegateCommand(SwitchPane);
        _copyToLeft = new DelegateCommand(() => CopyToward(DiffSide.Left), () => CanCopyToward(DiffSide.Left));
        _copyToRight = new DelegateCommand(() => CopyToward(DiffSide.Right), () => CanCopyToward(DiffSide.Right));
        _copyBlockToLeft = new DelegateCommand(() => CopyCurrentBlock(DiffSide.Left), () => CanCopyBlock(CurrentChangeIndex, DiffSide.Left));
        _copyBlockToRight = new DelegateCommand(() => CopyCurrentBlock(DiffSide.Right), () => CanCopyBlock(CurrentChangeIndex, DiffSide.Right));
        // The gesture forms of plan 00012's two block verbs, which mean the *current* block —
        // the only reading a keyboard has. Their menu entries carry the block that was clicked
        // instead, the way the copy entries already do.
        _goToChange = new DelegateCommand(() => GoToChange(CurrentChangeIndex), () => ChangeCount > 0);
        _selectBlock = new DelegateCommand(() => SelectChange(CurrentChangeIndex, FocusedSide), () => ChangeCount > 0);
        // The three folding modes are the one option written three ways, and each is disabled
        // where it is already in force: a menu that offers the state you are in is noise.
        _showAllRows = new DelegateCommand(
            () => SetCurrentValue(UnchangedContextRowsProperty, null),
            () => UnchangedContextRows is not null);
        _showDifferencesOnly = new DelegateCommand(
            () => SetCurrentValue(UnchangedContextRowsProperty, 0),
            () => UnchangedContextRows != 0);
        _showContext = new DelegateCommand(
            () => SetCurrentValue(UnchangedContextRowsProperty, DiffKeyMap.DefaultContextRows),
            () => UnchangedContextRows != DiffKeyMap.DefaultContextRows);
        _expandFold = new DelegateCommand(_controller.ExpandFoldAtCaret, _controller.CanExpandFoldAtCaret);
        _openFind = new DelegateCommand(OpenFind);
        _closeFind = new DelegateCommand(CloseFind, () => IsFindBarOpen);
        _findNext = new DelegateCommand(FindNext, () => IsFindBarOpen);
        _findPrevious = new DelegateCommand(FindPrevious, () => IsFindBarOpen);
        Searcher = static (document, left, right, query, options, token) => DiffSearch.Find(document, left, right, query, token, options);

        // The default key bindings come from the map; a host rebinds, unbinds or clears them.
        // Escape and F3 execute only while the find bar is open, and a binding that does not
        // execute leaves the key unhandled, so Escape still reaches the rest of the application.
        _bindings = new DiffKeyBindings(this, CommandFor);
        KeyMap = DiffKeyMap.Default();

        _controller.Initialize();
    }

    /// <summary>A build produced a document and the panes show it.</summary>
    public event EventHandler<DiffBuildCompletedEventArgs>? BuildCompleted;

    /// <summary>A build failed; the control is <see cref="DiffViewState.Failed"/>.</summary>
    public event EventHandler<DiffBuildFailedEventArgs>? BuildFailed;

    /// <summary>A pane's decorator threw and disabled itself; the control is <see cref="DiffViewState.Degraded"/>.</summary>
    public event EventHandler<RenderFaultEventArgs>? RenderFault;

    /// <summary>A search finished and its matches are on screen; a bad pattern arrives here too, as <see cref="Core.FindResult.Error"/>.</summary>
    public event EventHandler<DiffFindCompletedEventArgs>? FindCompleted;

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

    /// <summary>Whether the left pane rejects typing. Default true.</summary>
    public bool LeftReadOnly
    {
        get => GetValue(LeftReadOnlyProperty);
        set => SetValue(LeftReadOnlyProperty, value);
    }

    /// <summary>Whether the right pane rejects typing. Default true.</summary>
    public bool RightReadOnly
    {
        get => GetValue(RightReadOnlyProperty);
        set => SetValue(RightReadOnlyProperty, value);
    }

    /// <summary>
    /// Whether an edit re-diffs on its own after <see cref="ReDiffDelay"/>. Clearing it leaves the
    /// model as it was until <see cref="ReDiffNow"/> is called, which is the escape hatch for a
    /// pair large enough that rebuilding on a debounce costs more than it is worth.
    /// </summary>
    public bool LiveReDiff
    {
        get => GetValue(LiveReDiffProperty);
        set => SetValue(LiveReDiffProperty, value);
    }

    /// <summary>How long an edited pane rests before its re-diff runs; <see cref="ReDiffDebounce"/> by default.</summary>
    public TimeSpan ReDiffDelay
    {
        get => GetValue(ReDiffDelayProperty);
        set => SetValue(ReDiffDelayProperty, value);
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
    /// row. Beyond Compare calls the three <i>Show All</i>, <i>Show Differences</i> and
    /// <i>Show Context</i>.
    /// </summary>
    /// <remarks>
    /// One property rather than a flag and a count, because a pair can express "folding off with
    /// three context rows" — a state with no meaning, and therefore a state to document, test and
    /// get wrong.
    /// </remarks>
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
    /// Whether the pane headers are shown. On by default. Off, their row takes no height, so
    /// the panes get it back — and the header context menu goes with them, as does the accent
    /// that says which pane has focus.
    /// </summary>
    public bool ShowHeaders
    {
        get => GetValue(ShowHeadersProperty);
        set => SetValue(ShowHeadersProperty, value);
    }

    /// <summary>
    /// Whether the status strip is shown. On by default. Off, it takes no height, and the
    /// transient message lane goes with it — so do the save outcomes and the caret position.
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
    /// by default, which is where it has always been. The map's lanes do not follow this — a lane
    /// names a file, not an edge — but the current-block marker and the find ticks do.
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

    /// <summary>
    /// Whether the find bar is open. Opening it pre-fills the query from the focused pane's
    /// selection and puts the caret in the query box; closing it drops the highlights and
    /// returns focus to the pane that had it.
    /// </summary>
    public bool IsFindBarOpen
    {
        get => _isFindBarOpen;
        set
        {
            if (value)
            {
                OpenFind();
            }
            else
            {
                CloseFind();
            }
        }
    }

    /// <summary>What to search for; the search runs after <see cref="FindDebounce"/>, cancelling the one in flight.</summary>
    public string FindQuery
    {
        get => _findQuery;
        set
        {
            if (SetAndRaise(FindQueryProperty, ref _findQuery, value ?? string.Empty))
            {
                UpdateFindBar();
                RequestFind();
            }
        }
    }

    /// <summary>How the search runs: the scope, the toggles and the cap. Changing it re-runs the search.</summary>
    public FindOptions FindOptions
    {
        get => _findOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetAndRaise(FindOptionsProperty, ref _findOptions, value))
            {
                UpdateFindBar();
                RequestFind();
            }
        }
    }

    /// <summary>The last search's outcome, or <c>null</c> when nothing has been searched for.</summary>
    public FindResult? FindResult
    {
        get => _findResult;
        private set => SetAndRaise(FindResultProperty, ref _findResult, value);
    }

    /// <summary>
    /// The current match, -1 for none. Setting it scrolls both panes to the match's row, selects
    /// it in its own pane and gives that pane focus. A fresh result leaves it at -1: typing must
    /// not take focus out of the query box, so the first <see cref="FindNext"/> lands on match 1.
    /// </summary>
    public int CurrentFindMatchIndex
    {
        get => _currentFindMatchIndex;
        set => SetCurrentFindMatch(value, scroll: true);
    }

    /// <summary>The one state the control is in.</summary>
    public DiffViewState State => _controller.State;

    /// <summary>What the state means to the user: the empty prompt, the warning, or the failure.</summary>
    public string? StateMessage => _controller.StateMessage;

    /// <summary>The model the panes render, or <c>null</c> before the first build lands.</summary>
    public SideBySideDocument? Document => _controller.Document;

    /// <summary>What the last successful build measured.</summary>
    public DiffDiagnostics? Diagnostics => _controller.Diagnostics;

    /// <summary>The last successful build's warnings.</summary>
    public IReadOnlyList<DiffWarning> Warnings => _controller.Warnings;

    /// <summary>Change blocks in the model.</summary>
    public int ChangeCount => _controller.ChangeCount;

    /// <summary>Which banner is shown above the panes.</summary>
    public DiffBannerKind BannerKind => _controller.BannerKind;

    /// <summary>The banner's text.</summary>
    public string? BannerMessage => _controller.BannerMessage;

    /// <summary>The banner's action label, or <c>null</c> when the banner offers none.</summary>
    public string? BannerActionText => _controller.BannerActionText;

    /// <summary>Whether the result on screen is about to be replaced by a running build.</summary>
    public bool IsStale => _controller.IsStale;

    /// <summary>Whether the running build has passed <see cref="SlowBuildThreshold"/>.</summary>
    public bool IsBuildingSlowly => _controller.IsBuildingSlowly;

    /// <summary>The live left document: the source text, never padded.</summary>
    public TextDocument LeftDocument => _controller.LeftDocument;

    /// <summary>The live right document: the source text, never padded.</summary>
    public TextDocument RightDocument => _controller.RightDocument;

    /// <summary>The pane with keyboard focus, or <c>null</c>.</summary>
    public DiffSide? FocusedSide => _controller.FocusedSide;

    /// <summary>The focused pane's caret line, 1-based; 0 without focus.</summary>
    public int CaretLine => _controller.CaretLine;

    /// <summary>The focused pane's caret column, 1-based; 0 without focus.</summary>
    public int CaretColumn => _controller.CaretColumn;

    /// <summary>
    /// Creates the four category loggers (<see cref="DiffViewLogCategories"/>) when set. State
    /// transitions log at <c>Information</c>, warnings at <c>Warning</c>, faults at <c>Error</c>
    /// with the exception, cancellation at <c>Debug</c>; never document text.
    /// </summary>
    public ILoggerFactory? LoggerFactory
    {
        get => _controller.LoggerFactory;
        set
        {
            // Build and render belong to the shared half and are made where they are used; find
            // is this control's alone, and the viewer will never create one.
            _controller.LoggerFactory = value;
            _findLogger = value?.CreateLogger(DiffViewLogCategories.Find);
        }
    }

    /// <summary>The clock behind the transient messages' auto-clear and the slow-build threshold. Set it before the first build.</summary>
    public TimeProvider TimeProvider
    {
        get => _controller.TimeProvider;
        set => _controller.TimeProvider = value;
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

    /// <summary>Moves to the next change; F7 by default.</summary>
    public ICommand NextChangeCommand => _nextChange;

    /// <summary>Moves to the previous change; Shift+F7 by default.</summary>
    public ICommand PreviousChangeCommand => _previousChange;

    /// <summary>Moves to the first change.</summary>
    public ICommand FirstChangeCommand => _firstChange;

    /// <summary>Moves to the last change.</summary>
    public ICommand LastChangeCommand => _lastChange;

    /// <summary>Moves keyboard focus to the other pane; F6 by default.</summary>
    public ICommand SwitchPaneCommand => _switchPane;

    /// <summary>Copies the current block onto the left side; Alt+Left by default.</summary>
    public ICommand CopyToLeftCommand => _copyToLeft;

    /// <summary>Copies the current block onto the right side; Alt+Right by default.</summary>
    public ICommand CopyToRightCommand => _copyToRight;

    /// <summary>Opens the find bar; Ctrl+F by default.</summary>
    public ICommand OpenFindCommand => _openFind;

    /// <summary>Closes the find bar; Escape by default.</summary>
    public ICommand CloseFindCommand => _closeFind;

    /// <summary>Moves to the next match; F3 or Enter by default.</summary>
    public ICommand FindNextCommand => _findNext;

    /// <summary>Moves to the previous match; Shift+F3 or Shift+Enter by default.</summary>
    public ICommand FindPreviousCommand => _findPrevious;

    /// <summary>The build routine; tests replace it to make a build slow or throw.</summary>
    internal Func<PaneSource, PaneSource, DiffOptions, CancellationToken, DiffBuildResult> Builder
    {
        get => _controller.Builder;
        set => _controller.Builder = value;
    }

    /// <summary>The search routine; tests replace it to hold a search open or watch its thread.</summary>
    internal Func<SideBySideDocument, IPaneText, IPaneText, string, FindOptions, CancellationToken, FindResult> Searcher { get; set; }

    /// <summary>Above this many rows the search runs on a worker; at or below it, inline.</summary>
    internal int FindWorkerRowThreshold { get; set; } = 2_000;

    /// <summary>The in-flight build, completing when its outcome has been applied or discarded; <c>null</c> when idle.</summary>
    internal Task? CurrentBuild => _controller.CurrentBuild;

    /// <summary>The in-flight search, completing when its outcome has been applied or discarded; <c>null</c> when idle.</summary>
    internal Task? CurrentFind { get; private set; }

    internal DiffFindBar? FindBar => _findBar;

    /// <summary>The word-level lookup of the current model, bound to the options its build ran under; <c>null</c> without a model.</summary>
    public WordDiffLookup? WordDiffLookup => _controller.WordDiffLookup;

    internal ChangeConnectorGutter? Gutter => _controller.Gutter;

    internal DiffMinimap? Minimap => _controller.Minimap;

    internal DiffPanePresenter? LeftPane => _controller.LeftPane;

    internal DiffPanePresenter? RightPane => _controller.RightPane;

    internal DiffPaneHeader? LeftHeader => _controller.LeftHeader;

    internal DiffPaneHeader? RightHeader => _controller.RightHeader;

    internal DiffStatusStrip? StatusStrip => _controller.StatusStrip;

    // The controller as it stands, without building one: the public Status getter is lazy,
    // so asking it whether the field was released would create what it was asked about.
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
    public void NextChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        if (CurrentChangeIndex >= ChangeCount - 1)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoNext));
            return;
        }

        _controller.SetCurrentChange(CurrentChangeIndex + 1, scroll: true);
    }

    /// <summary>Moves to the previous change; at the first one, or before any, it stays and the strip says so.</summary>
    public void PreviousChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        if (CurrentChangeIndex <= 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoPrevious));
            return;
        }

        _controller.SetCurrentChange(CurrentChangeIndex - 1, scroll: true);
    }

    /// <summary>Moves to the first change.</summary>
    public void FirstChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        _controller.SetCurrentChange(0, scroll: true);
    }

    /// <summary>Moves to the last change.</summary>
    public void LastChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        _controller.SetCurrentChange(ChangeCount - 1, scroll: true);
    }

    /// <summary>Moves keyboard focus to the other pane; to the left one when neither has it.</summary>
    public void SwitchPane()
    {
        DiffSide target = FocusedSide == DiffSide.Left ? DiffSide.Right : DiffSide.Left;
        Pane(target)?.TextArea.Focus();
    }

    /// <summary>Scrolls both panes so <paramref name="row"/> sits at the centre of the viewport.</summary>
    public void ScrollToRow(int row) => _controller.ScrollToRow(row);

    /// <summary>
    /// Opens the find bar, pre-fills the query from the focused pane's selection when it is a
    /// single line, and puts the caret in the query box. Already open, it re-focuses and
    /// re-selects the query, so Ctrl+F twice is a way back to the box.
    /// </summary>
    public void OpenFind()
    {
        if (!IsFindBarOpen)
        {
            // Escape hands focus back to whichever pane had it when the bar opened.
            _findReturnSide = FocusedSide ?? DiffSide.Left;
            SetAndRaise(IsFindBarOpenProperty, ref _isFindBarOpen, true);
            RaiseFindCanExecuteChanged();
        }

        if (SelectionOfFocusedPane() is { } selection)
        {
            FindQuery = selection;
        }

        UpdateFindBar();
        _controller.UpdateStrip();

        // The bar has only just become visible; an unmeasured control cannot take focus, so a
        // failed attempt is retried below the layout pass's priority.
        if (_findBar is { } bar && !bar.FocusQuery())
        {
            Dispatcher.UIThread.Post(() => _findBar?.FocusQuery(), DispatcherPriority.Input);
        }

        RequestFind();
    }

    /// <summary>Closes the find bar, drops the highlights and returns focus to the pane that had it.</summary>
    public void CloseFind()
    {
        if (!IsFindBarOpen)
        {
            return;
        }

        CancelFind();
        SetAndRaise(IsFindBarOpenProperty, ref _isFindBarOpen, false);
        ApplyFindResult(null);
        UpdateFindBar();
        _controller.UpdateStrip();
        Pane(_findReturnSide)?.TextArea.Focus();
    }

    /// <summary>Moves to the next match, wrapping at the end; with no matches the strip says so.</summary>
    public void FindNext()
    {
        MoveFindMatch(1);
    }

    /// <summary>Moves to the previous match, wrapping at the start; with no matches the strip says so.</summary>
    public void FindPrevious()
    {
        MoveFindMatch(-1);
    }

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
        if (change.Property == LeftReadOnlyProperty)
        {
            if (_controller.LeftPane is not null)
            {
                _controller.LeftPane.IsReadOnly = LeftReadOnly;
            }

            // A pane offers an arrow when the *other* side can receive the copy, so the left
            // side's flag drives the right pane's arrows.
            if (_controller.RightPane is not null)
            {
                _controller.RightPane.CanCopyOut = !LeftReadOnly;
            }

            RaiseNavigationCanExecuteChanged();
        }
        else if (change.Property == RightReadOnlyProperty)
        {
            if (_controller.RightPane is not null)
            {
                _controller.RightPane.IsReadOnly = RightReadOnly;
            }

            if (_controller.LeftPane is not null)
            {
                _controller.LeftPane.CanCopyOut = !RightReadOnly;
            }

            RaiseNavigationCanExecuteChanged();
        }
        else if (change.Property == ChangeCountProperty)
        {
            RaiseNavigationCanExecuteChanged();
        }

        if (change.Property == StateProperty
            || change.Property == BannerKindProperty
            || change.Property == ShowBannerProperty)
        {
            // Not an else-branch: the controller sets the pseudo-classes from the same three, and
            // these two commands are this control's to re-evaluate.
            _retry.RaiseCanExecuteChanged();
            _force.RaiseCanExecuteChanged();
        }

        _controller.OnPropertyChanged(change);
    }

    // ── Sources and builds ─────────────────────────────────────────────────────────────────

    /// <summary>Whether <paramref name="side"/> has been edited since its source was assigned.</summary>
    public bool IsEdited(DiffSide side) => side == DiffSide.Left ? _leftEdited : _rightEdited;

    private void OnLeftDocumentChanged(object? sender, DocumentChangeEventArgs e) => OnDocumentChanged(DiffSide.Left, e);

    private void OnRightDocumentChanged(object? sender, DocumentChangeEventArgs e) => OnDocumentChanged(DiffSide.Right, e);

    /// <summary>
    /// Keeps the modified-since-load set true across an edit that moves lines. Only the lines the
    /// edit touched are added; everything below it shifts by the number of lines the edit gained
    /// or lost, because a line's number is not its identity once something above it changes.
    /// </summary>
    private void OnDocumentChanged(DiffSide side, DocumentChangeEventArgs e)
    {
        if (_suppressTextChanged)
        {
            return;
        }

        HashSet<int> modified = side == DiffSide.Left ? _leftModifiedLines : _rightModifiedLines;
        TextDocument document = side == DiffSide.Left ? LeftDocument : RightDocument;
        int startLine = document.GetLineByOffset(Math.Clamp(e.Offset, 0, document.TextLength)).LineNumber;
        int removed = CountLineBreaks(e.RemovedText?.Text);
        int inserted = CountLineBreaks(e.InsertedText?.Text);
        int delta = inserted - removed;

        if (delta != 0 && modified.Count > 0)
        {
            List<int> shifted = new(modified.Count);
            foreach (int line in modified)
            {
                shifted.Add(line > startLine ? line + delta : line);
            }

            modified.Clear();
            foreach (int line in shifted)
            {
                if (line >= 1)
                {
                    modified.Add(line);
                }
            }
        }

        for (int i = 0; i <= inserted; i++)
        {
            modified.Add(startLine + i);
        }

        PushModifiedLines(side);
    }

    private static int CountLineBreaks(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
            else if (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n'))
            {
                count++;
            }
        }

        return count;
    }

    private void PushModifiedLines(DiffSide side)
    {
        DiffPanePresenter? pane = Pane(side);
        if (pane is not null)
        {
            pane.ModifiedLines = side == DiffSide.Left ? _leftModifiedLines : _rightModifiedLines;
        }
    }

    /// <summary>The lines edited on <paramref name="side"/> since its source was assigned.</summary>
    public IReadOnlySet<int> ModifiedLines(DiffSide side)
    {
        return side == DiffSide.Left ? _leftModifiedLines : _rightModifiedLines;
    }

    private void OnLeftTextChanged(object? sender, EventArgs e) => OnPaneTextChanged(DiffSide.Left);

    private void OnRightTextChanged(object? sender, EventArgs e) => OnPaneTextChanged(DiffSide.Right);

    /// <summary>
    /// A pane's document changed. Only the user can have done it: a build swaps metadata and
    /// never touches a document, and a source assignment replaces the document rather than
    /// editing it, unsubscribing first.
    /// </summary>
    private void OnPaneTextChanged(DiffSide side)
    {
        if (_suppressTextChanged)
        {
            // A revert is putting the source's own text back; that is not the user editing.
            return;
        }

        if (side == DiffSide.Left)
        {
            _leftEdited = true;
            _leftDirty = true;
        }
        else
        {
            _rightEdited = true;
            _rightDirty = true;
        }

        _controller.UpdateHeaders();
        _controller.UpdateStrip();

        // The matches were offsets into text that has just moved under them, so they are wrong
        // now rather than merely stale. The search re-runs with the build.
        CancelFind();
        ApplyFindResult(null);
        ScheduleReDiff();
    }

    /// <summary>
    /// Schedules the re-diff an edit triggered: the pane rests for <see cref="ReDiffDelay"/>, and
    /// every further keystroke restarts the wait. The model already on screen stays until the new
    /// one lands, so during the wait the panes are misaligned by whatever the edit changed and
    /// the padding catches up when the build arrives.
    /// </summary>
    private void ScheduleReDiff()
    {
        _reDiffTimer?.Dispose();
        _reDiffTimer = null;
        if (!LiveReDiff)
        {
            return;
        }

        _reDiffTimer = TimeProvider.CreateTimer(
            _ => Dispatcher.UIThread.Post(() =>
            {
                _reDiffTimer?.Dispose();
                _reDiffTimer = null;
                _controller.ReDiff(keepModel: true);
            }),
            state: null,
            ReDiffDelay,
            Timeout.InfiniteTimeSpan);
    }

    /// <summary>Whether <paramref name="side"/> holds edits that are not on disk.</summary>
    public bool IsDirty(DiffSide side) => side == DiffSide.Left ? _leftDirty : _rightDirty;

    /// <summary>Whether <see cref="Save"/> would write: the side is dirty and came from a file.</summary>
    public bool CanSave(DiffSide side)
    {
        PaneSource? source = side == DiffSide.Left ? LeftSource : RightSource;
        return IsDirty(side) && !string.IsNullOrEmpty(source?.Path);
    }

    /// <summary>
    /// Writes <paramref name="side"/> back to the file it was read from, with that file's encoding,
    /// byte-order mark and line-terminator convention. Reports rather than throws; the message
    /// reaches the banner and the strip the way a build failure does.
    /// </summary>
    public SaveOutcome Save(DiffSide side)
    {
        if (!IsDirty(side))
        {
            return SaveOutcome.NotDirty;
        }

        PaneSource? source = side == DiffSide.Left ? LeftSource : RightSource;
        string name = DiffBuildController.HeaderTitle(side, source);
        if (source?.Path is not { Length: > 0 } path)
        {
            ReportSave(DiffViewStrings.Format(DiffViewStrings.SaveNoPath, name));
            return SaveOutcome.NoPath;
        }

        // Someone else's write must never be lost to ours.
        if (HasChangedOnDisk(side, path))
        {
            ReportSave(DiffViewStrings.Format(DiffViewStrings.SaveChangedOnDisk, name));
            return SaveOutcome.ChangedOnDisk;
        }

        TextInfo? info = side == DiffSide.Left ? _controller.LeftInfo : _controller.RightInfo;
        TextDocument document = side == DiffSide.Left ? LeftDocument : RightDocument;
        try
        {
            PaneWriter.Write(path, document.Text, source.Encoding, info?.LineEnding ?? LineEnding.Lf);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ReportSave(DiffViewStrings.Format(DiffViewStrings.SaveFailed, name, ex.Message));
            return SaveOutcome.Failed;
        }

        // The file now matches the pane, so the side is clean — but still *edited*, because the
        // assigned source's text is what it always was and the next build still reads the document.
        if (side == DiffSide.Left)
        {
            _leftDirty = false;
            _leftStamp = StampOf(path);
        }
        else
        {
            _rightDirty = false;
            _rightStamp = StampOf(path);
        }

        Status.SetSuccess(DiffViewStrings.Format(DiffViewStrings.SaveSucceeded, name));
        _controller.UpdateHeaders();
        _controller.UpdateStrip();
        return SaveOutcome.Saved;
    }

    /// <summary>
    /// Puts <paramref name="side"/> back to the text of its assigned source and rebuilds. This is
    /// a verb of its own because re-assigning the source cannot do it: <see cref="PaneSource"/> is
    /// a record, so an equal source raises no property change and nothing happens.
    /// </summary>
    public void Revert(DiffSide side)
    {
        PaneSource? source = side == DiffSide.Left ? LeftSource : RightSource;
        if (source is null || !IsEdited(side))
        {
            return;
        }

        TextDocument document = side == DiffSide.Left ? LeftDocument : RightDocument;
        _suppressTextChanged = true;
        try
        {
            // The same document instance, so the caret and the scroll offset are kept; the undo
            // stack keeps the revert too, which is the behaviour an editor should have.
            document.Text = source.Text;
        }
        finally
        {
            _suppressTextChanged = false;
        }

        if (side == DiffSide.Left)
        {
            _leftEdited = false;
            _leftDirty = false;
            _leftModifiedLines.Clear();
        }
        else
        {
            _rightEdited = false;
            _rightDirty = false;
            _rightModifiedLines.Clear();
        }

        PushModifiedLines(side);
        _controller.UpdateHeaders();
        _controller.UpdateStrip();
        ReDiffNow();
    }

    /// <summary>
    /// Whether block <paramref name="index"/> could be copied onto <paramref name="toSide"/>:
    /// the model has that block and the target is editable.
    /// </summary>
    public bool CanCopyBlock(int index, DiffSide toSide)
    {
        bool readOnly = toSide == DiffSide.Left ? LeftReadOnly : RightReadOnly;
        return !readOnly && Document is { } model && index >= 0 && index < model.Blocks.Count;
    }

    /// <summary>
    /// Replaces block <paramref name="index"/>'s lines on <paramref name="toSide"/> with the
    /// other side's lines of the same block, so the block collapses when the re-diff lands. The
    /// edit goes through the editor's own document, so undo takes it back like any other.
    /// </summary>
    /// <returns>Whether anything was written.</returns>
    public bool CopyBlock(int index, DiffSide toSide)
    {
        if (!CanCopyBlock(index, toSide))
        {
            return false;
        }

        ChangeBlock block = Document!.Blocks[index];
        DiffSide fromSide = Other(toSide);
        return CopyLines(fromSide, block.LinesFor(fromSide), toSide, block.LinesFor(toSide));
    }

    /// <summary>Copies the current change block onto <paramref name="toSide"/>.</summary>
    public bool CopyCurrentBlock(DiffSide toSide) => CopyBlock(CurrentChangeIndex, toSide);

    /// <summary>
    /// Copies toward <paramref name="toSide"/> the way the gutter says it will: the focused pane's
    /// selected lines when there is a selection, and the current block otherwise.
    /// </summary>
    /// <remarks>
    /// Plan 00006 made a selection arrow take a block arrow's cell — a selection being the more
    /// specific and the more recent intent — while the chord went on copying the block, so the two
    /// disagreed whenever a selection was up. This is the rule that makes them agree, and it is
    /// the one cut, copy and delete follow everywhere. <see cref="CopyCurrentBlock"/> is still the
    /// block whatever is selected.
    /// </remarks>
    public bool CopyToward(DiffSide toSide)
    {
        DiffSide fromSide = Other(toSide);
        return CanCopySelection(fromSide) ? CopySelection(fromSide) : CopyCurrentBlock(toSide);
    }

    /// <summary>Whether <see cref="CopyToward"/> would write anything.</summary>
    public bool CanCopyToward(DiffSide toSide)
    {
        DiffSide fromSide = Other(toSide);
        return CanCopySelection(fromSide) || CanCopyBlock(CurrentChangeIndex, toSide);
    }

    /// <summary>
    /// What key each command is on. Assigning a map, or changing one in place, rebuilds the
    /// bindings this control owns and leaves any a host added alone.
    /// </summary>
    public DiffKeyMap KeyMap
    {
        get => _keyMap;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_keyMap, value))
            {
                return;
            }

            _keyMap.Changed -= OnKeyMapChanged;
            _keyMap = value;
            _keyMap.Changed += OnKeyMapChanged;
            RebuildKeyBindings();
        }
    }

    /// <summary>The gesture <paramref name="command"/> is on, or <c>null</c> when it is unbound.</summary>
    public KeyGesture? GestureFor(DiffCommand command) => _keyMap[command];

    /// <summary>
    /// A pane's context menu is about to open, with what was clicked and the items that will be
    /// shown, which a handler may insert into, remove from or retitle in place.
    /// </summary>
    /// <remarks>Not raised while <see cref="PaneContextMenu"/> is set: there is nothing of ours to amend.</remarks>
    public event EventHandler<DiffPaneContextMenuEventArgs>? PaneContextMenuOpening;

    /// <summary>
    /// A menu to open in place of the control's own. The context reaches it as its
    /// <c>DataContext</c>, which is how a host's XAML binds to what was clicked. Null — the
    /// default — leaves the control's own menu in charge.
    /// </summary>
    public ContextMenu? PaneContextMenu { get; set; }

    /// <summary>
    /// A header's context menu is about to open, with which side and file was clicked and the
    /// items that will be shown, which a handler may insert into, remove from or retitle in place.
    /// </summary>
    /// <remarks>
    /// A second event rather than a second region on the first, because a header's subject differs
    /// in kind from a line's — see <see cref="DiffHeaderContext"/>. Not raised while
    /// <see cref="HeaderContextMenu"/> is set, for the same reason its pane counterpart is not.
    /// </remarks>
    public event EventHandler<DiffHeaderContextMenuEventArgs>? HeaderContextMenuOpening;

    /// <summary>
    /// A menu to open in place of the control's own header menu. The
    /// <see cref="DiffHeaderContext"/> reaches it as its <c>DataContext</c>.
    /// </summary>
    public ContextMenu? HeaderContextMenu { get; set; }

    /// <summary>
    /// Describes <paramref name="side"/>'s header — what is on screen above that pane, and the
    /// state of the file behind it. Public for the same reason <c>ContextAt</c> is: without it a
    /// host replacing the menu has nothing to write one against.
    /// </summary>
    public DiffHeaderContext HeaderContextAt(DiffSide side)
    {
        DiffPaneHeader? header = side == DiffSide.Left ? _controller.LeftHeader : _controller.RightHeader;
        return new DiffHeaderContext(
            side,
            header?.Title ?? string.Empty,
            header?.Detail,
            IsDirty(side),
            ReadOnly(side));
    }

    /// <summary>
    /// A header's menu: the file's two verbs, and nothing else. **No copy and no navigate** — a
    /// header is not a position, and the entries are already named per side, which is the only
    /// thing distinguishing the two headers' menus.
    /// </summary>
    private List<DiffMenuItem> HeaderMenuItems(DiffHeaderContext context)
    {
        List<DiffMenuItem> items = [];
        AddFileVerbs(items, context.Side);
        return items;
    }

    /// <summary>
    /// Save and revert for <paramref name="side"/>, shared by the header's menu and the text's
    /// rather than written twice — §7's usual reason, and these two are the pair most likely to
    /// drift, being the only entries that write to disk.
    /// </summary>
    private void AddFileVerbs(List<DiffMenuItem> items, DiffSide side)
    {
        items.Add(new DiffMenuItem
        {
            Header = DiffViewStrings.MenuSave(side),
            Command = new DelegateCommand(() => Save(side), () => CanSave(side)),
            IsEnabled = CanSave(side),
        });
        items.Add(new DiffMenuItem
        {
            Header = DiffViewStrings.MenuRevert(side),
            Command = new DelegateCommand(() => Revert(side), () => IsEdited(side)),
            IsEnabled = IsEdited(side),
        });
    }

    /// <summary>
    /// The items the menu shows for <paramref name="context"/>. The shape never changes with the
    /// state — every entry is always here, enabled or not — because a host's "insert after this
    /// item" has to mean the same thing on every open.
    /// </summary>
    /// <remarks>
    /// One method per surface rather than one method with five branches through it: what each
    /// menu is <em>about</em> is the whole design, and a reader should be able to see one menu's
    /// list without reading the other four's conditions.
    /// </remarks>
    internal List<DiffMenuItem> MenuItemsFor(DiffPaneContext context)
    {
        return context.Region switch
        {
            DiffPaneRegion.ConnectorGutter => ConnectorMenuItems(context),
            DiffPaneRegion.OverviewMap => OverviewMapMenuItems(context),
            _ => PaneMenuItems(context),
        };
    }

    /// <summary>
    /// The connector's menu: a polygon <em>is</em> a change block, so every entry is about that
    /// block and there is nothing else to be about. Both copy directions, because the column
    /// belongs to neither side — which is also why selecting from here selects in both panes.
    /// No find and no navigate: this is not a position in a file.
    /// </summary>
    private List<DiffMenuItem> ConnectorMenuItems(DiffPaneContext context)
    {
        List<DiffMenuItem> items = [];
        int block = context.Block?.Index ?? -1;
        foreach (DiffSide toSide in (DiffSide[])[DiffSide.Left, DiffSide.Right])
        {
            DiffSide target = toSide;
            DiffMenuItem? copy = DiffPaneMenu.Verb(
                DiffViewStrings.MenuCopyChange(target),
                target == DiffSide.Left ? DiffCommand.CopyBlockToLeft : DiffCommand.CopyBlockToRight,
                CommandOrNull,
                GestureFor,
                CanCopyBlock(block, target));
            if (copy is not null)
            {
                copy.Command = new DelegateCommand(() => CopyBlock(block, target), () => CanCopyBlock(block, target));
                copy.Icon = DiffMenuIcons.CopyArrow(target);
                items.Add(copy);
            }
        }

        AddBlockVerbs(items, context);

        // The connector's column is where a run's absence is most visible — a polygon with a long
        // gap under it — so the folding modes belong here as well as on the margins. The map's
        // menu does not get them: it is a navigation surface whose menu plan 00012 kept short on
        // purpose, and four more entries would double it.
        DiffPaneMenu.AddFolding(items, CommandOrNull, GestureFor, ExpandHere(context.Row));
        return items;
    }

    /// <summary>
    /// The map's menu: a row, the change at it if there is one, and the map's own way off screen.
    /// Short by nature — a left-click already does its main verb — and kept anyway, because a
    /// seam that answers every surface but one is a seam a host has to special-case.
    /// </summary>
    private List<DiffMenuItem> OverviewMapMenuItems(DiffPaneContext context)
    {
        int? row = context.Row;
        List<DiffMenuItem> items =
        [
            new DiffMenuItem
            {
                Header = DiffViewStrings.Get(DiffViewStrings.MenuGoToRow),
                Command = new DelegateCommand(() => ScrollToRow(row ?? 0), () => row is not null),
                IsEnabled = row is not null,
            },
        ];

        AddBlockVerbs(items, context, select: false);
        items.Add(DiffMenuItem.Separator());
        items.Add(new DiffMenuItem
        {
            Header = DiffViewStrings.Get(DiffViewStrings.MenuHideOverviewMap),
            Command = new DelegateCommand(() => ShowMinimap = false, () => ShowMinimap),
            IsEnabled = ShowMinimap,
        });
        return items;
    }

    /// <summary>
    /// A pane's menu, and its two gutters'. The copy items are the same three surfaces' business;
    /// what differs is the block verbs the gutters carry and the file verbs only the text does.
    /// </summary>
    private List<DiffMenuItem> PaneMenuItems(DiffPaneContext context)
    {
        DiffSide side = context.Side ?? DiffSide.Left;
        DiffSide toSide = Other(side);
        int block = context.Block?.Index ?? CurrentChangeIndex;
        List<DiffMenuItem> items = [];

        // The marker margin's chip names a run, so the run's verbs lead: this gutter's subject is
        // the change, and the copy items below it are what you then do with one. The line-number
        // margin reads the other way round — its glyph is a copy arrow — so its go-to entry comes
        // after the copies instead, added below.
        if (context.Region is DiffPaneRegion.ChangeMarkerMargin)
        {
            AddBlockVerbs(items, context);
        }

        // Two scopes, each named for exactly what it copies. The wording is the menu's own and
        // shorter than the gutter's — a tooltip can afford the words, a menu row sits beside its
        // accelerator — and each direction is a whole sentence, so a translator is never handed a
        // sentence with someone else's word dropped into it. The block copied is the one **under
        // the pointer**, not the current change: a context menu that acted somewhere else would
        // not be one.
        DiffMenuItem? selection = DiffPaneMenu.Verb(
            DiffViewStrings.MenuCopySelection(toSide),
            toSide == DiffSide.Left ? DiffCommand.CopyToLeft : DiffCommand.CopyToRight,
            CommandOrNull,
            GestureFor,
            CanCopySelection(side));
        if (selection is not null)
        {
            selection.Command = new DelegateCommand(() => CopySelection(side), () => CanCopySelection(side));
            selection.Icon = DiffMenuIcons.CopyArrow(toSide);
            items.Add(selection);
        }

        DiffMenuItem? whole = DiffPaneMenu.Verb(
            DiffViewStrings.MenuCopyChange(toSide),
            toSide == DiffSide.Left ? DiffCommand.CopyBlockToLeft : DiffCommand.CopyBlockToRight,
            CommandOrNull,
            GestureFor,
            CanCopyBlock(block, toSide));
        if (whole is not null)
        {
            whole.Command = new DelegateCommand(() => CopyBlock(block, toSide), () => CanCopyBlock(block, toSide));
            whole.Icon = DiffMenuIcons.CopyArrow(toSide);
            items.Add(whole);
        }

        if (context.Region is DiffPaneRegion.LineNumberMargin)
        {
            AddBlockVerbs(items, context, select: false);
        }

        items.Add(DiffMenuItem.Separator());
        DiffPaneMenu.AddNavigation(items, CommandOrNull, GestureFor, ChangeCount);

        // Save and revert are the **file's** verbs, not the line's, and a gutter is a position.
        // They are absent from a margin's menu rather than greyed in it — the same rule plan
        // 00010 set for a verb a view does not have, where the unified view's menu has no copy
        // items at all instead of four disabled ones. "Disable, do not hide" governs one menu
        // changing with state; these are different menus. The header's own menu is where the
        // file's verbs belong, and as of phase 3 that menu exists — the same two entries, from
        // the same AddFileVerbs, so the two places that offer to write a file cannot drift.
        if (context.Region is DiffPaneRegion.Text)
        {
            items.Add(DiffMenuItem.Separator());
            AddFileVerbs(items, side);
        }

        // Last on every surface that has it. Folding is a view option rather than something done
        // to what is under the pointer, and a group that is last in one menu and in the middle of
        // another is a group a host's "insert after" has to find twice. Its expand entry is the
        // run under the pointer — the entry says "here", and a menu that meant the caret would be
        // answering a question nobody asked it.
        DiffPaneMenu.AddFolding(items, CommandOrNull, GestureFor, ExpandHere(context.Row));
        return items;
    }

    /// <summary>
    /// Plan 00012's two block verbs, about the block <em>under the pointer</em> rather than the
    /// current one — the rule the copy entries already follow, because a context menu that acted
    /// somewhere else would not be one. Present and disabled outside a change rather than absent:
    /// whether the pointer is in a change is state, and 00010's shape rule is that a host's
    /// "insert after this item" means the same thing on every open.
    /// </summary>
    /// <param name="items">The list being built, appended to in place.</param>
    /// <param name="context">What was under the pointer; its <c>Block</c> is the subject.</param>
    /// <param name="select">
    /// Whether the selection verb is offered. The map does not offer it: its subject is a row,
    /// and the lines a block covers are in the panes rather than anywhere the map can show them.
    /// </param>
    private void AddBlockVerbs(List<DiffMenuItem> items, DiffPaneContext context, bool select = true)
    {
        int block = context.Block?.Index ?? -1;

        // The change's own kind, drawn as the marker margin's own operator. Both entries carry it
        // rather than one, because both are about the same change and a reader should be able to
        // see at a glance which two rows those are. Off a change it is null, which is the
        // reserved column doing the job it was reserved for.
        //
        // A fresh control per entry, never one shared: a visual has one parent, so assigning the
        // same instance to two items would take it away from the first.
        DiffLineKind kind = context.Block?.Kind ?? DiffLineKind.Unchanged;

        DiffMenuItem? goTo = DiffPaneMenu.Verb(
            DiffViewStrings.Get(DiffViewStrings.MenuGoToChange),
            DiffCommand.GoToChange,
            CommandOrNull,
            GestureFor,
            block >= 0);
        if (goTo is not null)
        {
            goTo.Command = new DelegateCommand(() => GoToChange(block), () => block >= 0);
            goTo.Icon = DiffMenuIcons.Operator(kind);
            items.Add(goTo);
        }

        if (!select)
        {
            return;
        }

        // Which panes the selection lands in is the *surface's* answer, not the focus's. A margin
        // belongs to one pane and selects there; the connector belongs to neither, and the block
        // it draws spans both files, so it selects in both rather than picking a side.
        DiffSide? side = context.Region is DiffPaneRegion.ConnectorGutter ? null : context.Side;
        DiffMenuItem? selectBlock = DiffPaneMenu.Verb(
            DiffViewStrings.Get(DiffViewStrings.MenuSelectChange),
            DiffCommand.SelectBlock,
            CommandOrNull,
            GestureFor,
            block >= 0);
        if (selectBlock is not null)
        {
            selectBlock.Command = new DelegateCommand(() => SelectChange(block, side), () => block >= 0);
            selectBlock.Icon = DiffMenuIcons.Operator(kind);
            items.Add(selectBlock);
        }
    }

    /// <summary>
    /// Makes block <paramref name="index"/> the current change and scrolls to it. What the
    /// connector's left-click has always done, given a name so that a menu can offer it and a
    /// host can bind it; an index the model does not have does nothing.
    /// </summary>
    public void GoToChange(int index)
    {
        if (Document is { } model && index >= 0 && index < model.Blocks.Count)
        {
            _controller.SetCurrentChange(index, scroll: true);
        }
    }

    /// <summary>
    /// Selects block <paramref name="index"/>'s lines on <paramref name="side"/>, or on both
    /// sides where it is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// A side the block has no lines on — the near half of an insertion or a deletion — has its
    /// selection cleared rather than left standing. After "select this change" a selection
    /// elsewhere would be describing a different change, and the copy arrows read the selection.
    /// </remarks>
    public void SelectChange(int index, DiffSide? side)
    {
        if (Document is not { } model || index < 0 || index >= model.Blocks.Count)
        {
            return;
        }

        ChangeBlock block = model.Blocks[index];
        foreach (DiffSide each in (DiffSide[])[DiffSide.Left, DiffSide.Right])
        {
            if (side is { } only && only != each)
            {
                continue;
            }

            SelectLines(each, block.LinesFor(each));
        }
    }

    /// <summary>
    /// Selects whole lines <paramref name="lines"/> — the model's 0-based counting — on
    /// <paramref name="side"/>, clearing the selection for an empty range.
    /// </summary>
    private void SelectLines(DiffSide side, LineRange lines)
    {
        if (Pane(side) is not { } pane || pane.Document is not { } document)
        {
            return;
        }

        // The model can be ahead of a document an edit has shortened, so the range is clamped
        // rather than trusted; a range entirely past the end reads as empty.
        int first = lines.Start + 1;
        int last = Math.Min(lines.End, document.LineCount);
        if (lines.IsEmpty || first > last)
        {
            pane.TextArea.ClearSelection();
            return;
        }

        int start = document.GetLineByNumber(first).Offset;
        pane.Select(start, document.GetLineByNumber(last).EndOffset - start);
    }

    /// <summary>Whether <paramref name="side"/> refuses typing.</summary>
    private bool ReadOnly(DiffSide side) => side == DiffSide.Left ? LeftReadOnly : RightReadOnly;

    /// <summary>
    /// The command behind <paramref name="command"/>. This view has every verb, so it never
    /// answers <c>null</c>; the signature matches the unified view's so the menu builder can ask
    /// both the same question.
    /// </summary>
    private ICommand? CommandOrNull(DiffCommand command) => CommandFor(command);

    /// <summary>
    /// The menu the last request opened, or <c>null</c> where none did. A test seam, like the
    /// margins' <c>LastCopyArrows</c>: a menu that did not open leaves no mark a frame could show.
    /// </summary>
    internal ContextMenu? LastMenu { get; private set; }

    private void OnPaneContextMenuRequested(object? sender, DiffPanePresenter.PaneContextRequest e)
    {
        if (sender is DiffPanePresenter pane)
        {
            e.Opened = OpenMenu(pane, e.Context, e.Pointer);
        }
    }

    /// <summary>
    /// Raises <see cref="PaneContextMenuOpening"/> over <paramref name="items"/> and says whether
    /// a handler cancelled. The seam takes this rather than the event itself, because the header's
    /// event carries a context of a different type and the two cannot share one delegate.
    /// </summary>
    private bool RaisePaneMenuOpening(DiffPaneContext context, IList<DiffMenuItem> items)
    {
        DiffPaneContextMenuEventArgs args = new(context, items);
        PaneContextMenuOpening?.Invoke(this, args);
        return args.Cancel;
    }

    /// <summary>The same for <see cref="HeaderContextMenuOpening"/>.</summary>
    private bool RaiseHeaderMenuOpening(DiffHeaderContext context, IList<DiffMenuItem> items)
    {
        DiffHeaderContextMenuEventArgs args = new(context, items);
        HeaderContextMenuOpening?.Invoke(this, args);
        return args.Cancel;
    }

    /// <summary>The command object behind <paramref name="command"/>, for a host that wants to invoke it.</summary>
    public ICommand CommandFor(DiffCommand command)
    {
        return command switch
        {
            DiffCommand.NextChange => _nextChange,
            DiffCommand.PreviousChange => _previousChange,
            DiffCommand.SwitchPane => _switchPane,
            DiffCommand.OpenFind => _openFind,
            DiffCommand.FindNext => _findNext,
            DiffCommand.FindPrevious => _findPrevious,
            DiffCommand.CloseFind => _closeFind,
            DiffCommand.CopyToLeft => _copyToLeft,
            DiffCommand.CopyToRight => _copyToRight,
            DiffCommand.GoToChange => _goToChange,
            DiffCommand.SelectBlock => _selectBlock,
            DiffCommand.CopyBlockToLeft => _copyBlockToLeft,
            DiffCommand.CopyBlockToRight => _copyBlockToRight,
            DiffCommand.ShowAllRows => _showAllRows,
            DiffCommand.ShowDifferencesOnly => _showDifferencesOnly,
            DiffCommand.ShowContext => _showContext,
            DiffCommand.ExpandFold => _expandFold,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
        };
    }

    private void OnKeyMapChanged(object? sender, EventArgs e) => RebuildKeyBindings();

    private void RebuildKeyBindings() => _bindings.Rebuild(_keyMap, _controller.RenderLogger);

    /// <summary>
    /// Whether the selection in <paramref name="fromSide"/>'s pane could be copied to the other
    /// side: that pane holds a selection and the side receiving it is editable.
    /// </summary>
    public bool CanCopySelection(DiffSide fromSide)
    {
        DiffSide toSide = Other(fromSide);
        bool readOnly = toSide == DiffSide.Left ? LeftReadOnly : RightReadOnly;
        return !readOnly && Document is not null && Pane(fromSide)?.SelectedLines is not null;
    }

    /// <summary>
    /// Replaces the other side's lines in the rows <paramref name="fromSide"/>'s selection
    /// occupies with the selection's own whole lines. It is the block copy's rule over a
    /// different range: the selection picks the rows, and the rows pick the target. Where the
    /// other side has no lines in those rows at all, the copy inserts rather than replaces, as a
    /// one-sided block's copy already does. The edit goes through the editor's own document, so
    /// undo takes it back like any other.
    /// </summary>
    /// <returns>Whether anything was written.</returns>
    public bool CopySelection(DiffSide fromSide)
    {
        if (!CanCopySelection(fromSide) || Pane(fromSide)?.SelectedLines is not { } fromRange)
        {
            return false;
        }

        DiffSide toSide = Other(fromSide);
        return CopyLines(fromSide, fromRange, toSide, AlignedLinesOf(fromSide, fromRange, toSide));
    }

    /// <summary>
    /// The lines <paramref name="toSide"/> holds in the rows <paramref name="fromRange"/>
    /// occupies — the run a copy of that range replaces. Empty where every one of those rows is
    /// padding on that side, and then positioned where the insertion belongs: after the last line
    /// that side has above the run, which is the convention <see cref="ChangeBlock"/>'s own empty
    /// ranges already carry.
    /// </summary>
    private LineRange AlignedLinesOf(DiffSide fromSide, LineRange fromRange, DiffSide toSide)
    {
        SideBySideDocument model = Document!;
        IReadOnlyList<DiffLine> lines = model.Pane(fromSide).Lines;
        if (lines.Count == 0)
        {
            return LineRange.Empty(0);
        }

        // Between a keystroke and the next build the document has lines the model does not; the
        // rows this copy can align to are the ones the model knows.
        int firstRow = lines[Math.Min(fromRange.Start, lines.Count - 1)].Row;
        int lastRow = lines[Math.Min(fromRange.End - 1, lines.Count - 1)].Row;

        int? first = null;
        int last = 0;
        for (int row = firstRow; row <= lastRow; row++)
        {
            if (SideBySideDocument.LineOf(model.Rows[row], toSide) is { } line)
            {
                first ??= line;
                last = line;
            }
        }

        if (first is { } start)
        {
            return new LineRange(start, last - start + 1);
        }

        for (int row = firstRow - 1; row >= 0; row--)
        {
            if (SideBySideDocument.LineOf(model.Rows[row], toSide) is { } line)
            {
                return LineRange.Empty(line + 1);
            }
        }

        return LineRange.Empty(0);
    }

    /// <summary>The other side of <paramref name="side"/>.</summary>
    private static DiffSide Other(DiffSide side) => side == DiffSide.Left ? DiffSide.Right : DiffSide.Left;

    /// <summary>
    /// Writes <paramref name="fromRange"/>'s whole lines over <paramref name="toRange"/>, an
    /// empty target being an insertion at its position. Shared by every copy the control makes,
    /// so a block and a selection differ in the range they name and in nothing else.
    /// </summary>
    /// <returns>Whether anything was written.</returns>
    private bool CopyLines(DiffSide fromSide, LineRange fromRange, DiffSide toSide, LineRange toRange)
    {
        TextDocument from = fromSide == DiffSide.Left ? LeftDocument : RightDocument;
        TextDocument to = toSide == DiffSide.Left ? LeftDocument : RightDocument;

        string text = LinesOf(from, fromRange);
        (int offset, int length) = RegionOf(to, toRange);
        string newLine = NewLineOf(toSide);

        // Whole lines in, whole lines out: an insertion in the middle of a document needs the
        // terminator the copied run's last line may not carry, and a replacement that reaches the
        // end must not leave one behind that the target never had.
        // The copy makes the target's lines the source's lines exactly, terminator included: the
        // point is for the difference to collapse, and trimming the source's own trailing
        // terminator would leave the very difference the copy was meant to remove.
        bool atEnd = offset + length >= to.TextLength;

        // Appending past a last line that carries no terminator needs one put in front, or the
        // copied run joins onto it.
        if (length == 0 && atEnd && offset > 0 && text.Length > 0 && !EndsWithNewLine(to.GetText(offset - 1, 1)))
        {
            text = newLine + text;
        }

        if (length == 0 && text.Length == 0)
        {
            return false;
        }

        to.Replace(offset, length, text);
        return true;
    }

    /// <summary>The text of <paramref name="range"/>, terminators included; empty for an empty range.</summary>
    private static string LinesOf(TextDocument document, LineRange range)
    {
        if (range.IsEmpty || range.Start >= document.LineCount)
        {
            return string.Empty;
        }

        DocumentLine first = document.GetLineByNumber(range.Start + 1);
        DocumentLine last = document.GetLineByNumber(Math.Min(range.End, document.LineCount));
        int start = first.Offset;
        return document.GetText(start, last.Offset + last.TotalLength - start);
    }

    /// <summary>Where <paramref name="range"/> sits in the document; a zero length for an insertion point.</summary>
    private static (int Offset, int Length) RegionOf(TextDocument document, LineRange range)
    {
        if (range.IsEmpty)
        {
            // An empty range past the last line is an append; otherwise it is the head of its line.
            return range.Start >= document.LineCount
                ? (document.TextLength, 0)
                : (document.GetLineByNumber(range.Start + 1).Offset, 0);
        }

        DocumentLine first = document.GetLineByNumber(range.Start + 1);
        DocumentLine last = document.GetLineByNumber(Math.Min(range.End, document.LineCount));
        int offset = first.Offset;
        return (offset, last.Offset + last.TotalLength - offset);
    }

    private static bool EndsWithNewLine(string text) => text.EndsWith('\n') || text.EndsWith('\r');

    /// <summary>The terminator a copied run should carry on <paramref name="side"/>.</summary>
    private string NewLineOf(DiffSide side)
    {
        TextInfo? info = side == DiffSide.Left ? _controller.LeftInfo : _controller.RightInfo;
        return info?.LineEnding switch
        {
            LineEnding.CrLf => "\r\n",
            LineEnding.Cr => "\r",
            _ => "\n",
        };
    }

    /// <summary>The file's identity when it came from one: enough to notice someone else's write.</summary>
    private static (DateTime WriteTimeUtc, long Length)? StampOf(PaneSource? source)
    {
        return source?.Path is { Length: > 0 } path ? StampOf(path) : null;
    }

    private static (DateTime WriteTimeUtc, long Length)? StampOf(string path)
    {
        try
        {
            FileInfo info = new(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the file moved under us. A side whose stamp was never taken — the file did not
    /// exist when it was read — is not treated as changed, or a first save could never happen.
    /// </summary>
    private bool HasChangedOnDisk(DiffSide side, string path)
    {
        (DateTime WriteTimeUtc, long Length)? loaded = side == DiffSide.Left ? _leftStamp : _rightStamp;
        return loaded is { } was && StampOf(path) is { } now && (now.WriteTimeUtc != was.WriteTimeUtc || now.Length != was.Length);
    }

    private void ReportSave(string message)
    {
        Status.SetWarning(message);
        _controller.UpdateStrip();
    }

    /// <summary>Rebuilds now from the panes' live text, whatever the debounce was doing.</summary>
    public void ReDiffNow()
    {
        _reDiffTimer?.Dispose();
        _reDiffTimer = null;
        _controller.ReDiff(keepModel: true);
    }

    // ── State, banner, strip, headers ──────────────────────────────────────────────────────

    // ── Panes ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The menu's own <see cref="DiffCommand.ExpandFold"/>: the run under the pointer rather than
    /// the one at the caret, per plan 00010's rule that a menu entry carries what was clicked.
    /// </summary>
    private ICommand? ExpandHere(int? row)
    {
        if (row is not { } modelRow)
        {
            return null;
        }

        return new DelegateCommand(
            () => _controller.ExpandFoldContaining(modelRow),
            () => _controller.Projection.FoldContaining(modelRow) >= 0);
    }

    private void OnPaneCopyOutRequested(object? sender, int blockIndex)
    {
        // The arrow is in the pane the block is copied *from*, so the target is the other side.
        DiffSide from = sender is DiffPanePresenter pane ? pane.Side : DiffSide.Left;
        CopyBlock(blockIndex, from == DiffSide.Left ? DiffSide.Right : DiffSide.Left);
    }

    private void OnPaneCopySelectionRequested(object? sender, EventArgs e)
    {
        // As with a block: the arrow is in the pane the lines are copied *from*.
        if (sender is DiffPanePresenter pane)
        {
            CopySelection(pane.Side);
        }
    }

    /// <summary>
    /// A right-click on the connector. The block is the one the polygon under the pointer draws,
    /// from the same hit-test the left-click uses — not the block nearest the pointer's row,
    /// which is a different answer where a polygon is tall. Off every polygon there is no block
    /// and so no menu: the empty column is the splitter, and a drag is its only verb.
    /// </summary>
    /// <remarks>
    /// The click itself is untouched. <c>OnPointerPressed</c> there already returns unless the
    /// left button is down, so a right-click navigates nothing, and this handler does not call
    /// <see cref="GoToChange"/> — offering the verb is not performing it.
    /// </remarks>
    private void OnConnectorContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _controller.Gutter is null || Document is not { } model)
        {
            return;
        }

        // The gutter is not focusable, so every request that reaches it carries a pointer.
        if (!e.TryGetPosition(_controller.Gutter, out Point point) || _controller.Gutter.PolygonAt(point) is not { } polygon)
        {
            return;
        }

        ChangeBlock block = model.Blocks[polygon.BlockIndex];
        int row = Math.Clamp(_controller.Gutter.RowAt(point.Y) ?? block.FirstRow, block.FirstRow, block.LastRow);
        e.Handled = OpenMenu(_controller.Gutter, RowContext(DiffPaneRegion.ConnectorGutter, model, row, side: null, block), point);
    }

    /// <summary>
    /// A right-click on the overview map. The row is the one a left-click there would jump to, so
    /// the menu's <em>go to this row</em> and the click cannot come to different answers, and the
    /// side is the lane under the pointer — <c>null</c> over the marker column the lanes share.
    /// </summary>
    /// <remarks>
    /// As on the connector, the click is untouched: the map's <c>OnPointerPressed</c> returns
    /// unless the left button is down, so a right-click neither scrolls nor jumps.
    /// </remarks>
    private void OnMinimapContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || _controller.Minimap is null || Document is not { } model || model.Rows.Count == 0)
        {
            return;
        }

        if (!e.TryGetPosition(_controller.Minimap, out Point point))
        {
            return;
        }

        int row = Math.Clamp(_controller.Minimap.RowForClick(point.Y), 0, model.Rows.Count - 1);
        ChangeBlock? block = _controller.LeftPane?.Metadata.BlockAtRow(row);
        e.Handled = OpenMenu(_controller.Minimap, RowContext(DiffPaneRegion.OverviewMap, model, row, _controller.Minimap.LaneAt(point.X), block), point);
    }

    /// <summary>
    /// The context for a surface whose subject is a row rather than a line.
    /// <paramref name="side"/> is what the surface itself says — the lane on the map, and nothing
    /// on the connector, which belongs to neither pane.
    /// </summary>
    /// <remarks>
    /// A row always has a line on at least one side, so the line here is real rather than a
    /// stand-in: the surface's own side where that side has one, and the left then the right
    /// where it names none or pads. <c>SourceSide</c> says which, exactly as it does for the
    /// unified view's composed document. The selection is the lane's pane's; the connector names
    /// no pane, so it reports none rather than one pane's chosen arbitrarily.
    /// </remarks>
    private DiffPaneContext RowContext(DiffPaneRegion region, SideBySideDocument model, int row, DiffSide? side, ChangeBlock? block)
    {
        AlignedRow aligned = model.Rows[row];
        DiffSide sourceSide = side is { } lane && aligned.LineOf(lane) is not null ? lane
            : aligned.LeftLine is not null ? DiffSide.Left
            : DiffSide.Right;
        int line = (aligned.LineOf(sourceSide) ?? 0) + 1;

        return new DiffPaneContext(
            region,
            side,
            line,
            sourceSide,
            line,
            row,
            block,
            aligned.Kind,
            side is { } lane2 ? Pane(lane2)?.SelectedLines : null,
            IsUnified: false,
            ReadOnly(sourceSide));
    }

    /// <summary>
    /// Opens a menu for a surface that is not a pane, through the one implementation the whole
    /// library shares — so the replacement property and the opening event mean the same thing
    /// here as they do in the text.
    /// </summary>
    /// <returns>
    /// Whether a menu opened, which is what marks the routed event handled: a request answered
    /// with nothing still reaches a <c>ContextMenu</c> a host put on an ancestor.
    /// </returns>
    private bool OpenMenu(Control owner, DiffPaneContext context, Point? pointer)
    {
        LastMenu = DiffPaneMenu.Request(
            owner,
            context,
            pointer,
            PaneContextMenu,
            () => MenuItemsFor(context),
            items => RaisePaneMenuOpening(context, items));
        return LastMenu is not null;
    }

    /// <summary>The same for a header, whose context and whose two host hooks are its own.</summary>
    private bool OpenMenu(Control owner, DiffHeaderContext context, Point? pointer)
    {
        LastMenu = DiffPaneMenu.Request(
            owner,
            context,
            pointer,
            HeaderContextMenu,
            () => HeaderMenuItems(context),
            items => RaiseHeaderMenuOpening(context, items));
        return LastMenu is not null;
    }

    private void OnLeftHeaderContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        OnHeaderContextRequested(DiffSide.Left, sender, e);
    }

    private void OnRightHeaderContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        OnHeaderContextRequested(DiffSide.Right, sender, e);
    }

    /// <summary>A right-click on <paramref name="side"/>'s header: the file's menu, about that file.</summary>
    /// <remarks>
    /// <para>
    /// The side comes from <em>which handler was attached</em> rather than from a test on the
    /// event. Phase 1's margin handler is attached to the pane, so it has several possible
    /// sources and has to resolve one — including a margin the library did not draw, which it
    /// leaves alone. A header handler is attached to the header, so it has exactly one, and there
    /// is correspondingly no foreign-header guard to write: a header this view did not build
    /// never reaches here at all.
    /// </para>
    /// <para>
    /// The event bubbles, so a click on the title or the detail line <em>inside</em> the header
    /// arrives here as a click on the header, which is what makes the whole strip answer rather
    /// than the gaps between its text. A header is not focusable, so a request here always
    /// carries a pointer; a keyboard user asking about "here" is asking about the pane.
    /// </para>
    /// </remarks>
    private void OnHeaderContextRequested(DiffSide side, object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || sender is not Control header)
        {
            return;
        }

        Point? pointer = e.TryGetPosition(header, out Point point) ? point : null;
        e.Handled = OpenMenu(header, HeaderContextAt(side), pointer);
    }

    private void RaiseNavigationCanExecuteChanged()
    {
        _nextChange.RaiseCanExecuteChanged();
        _previousChange.RaiseCanExecuteChanged();
        _firstChange.RaiseCanExecuteChanged();
        _lastChange.RaiseCanExecuteChanged();

        // Copying targets the current block, so its availability moves with the navigation and
        // with either side's read-only flag.
        _copyToLeft.RaiseCanExecuteChanged();
        _copyToRight.RaiseCanExecuteChanged();

        // The folding modes turn on which one is in force, and opening a run on where the caret
        // is — both of which move for reasons the navigation does not always cause, so they are
        // refreshed here and again whenever the folds are recomputed.
        RaiseFoldingCanExecuteChanged();
    }

    private void RaiseFoldingCanExecuteChanged()
    {
        _showAllRows.RaiseCanExecuteChanged();
        _showDifferencesOnly.RaiseCanExecuteChanged();
        _showContext.RaiseCanExecuteChanged();
        _expandFold.RaiseCanExecuteChanged();
    }

    // ── Find ───────────────────────────────────────────────────────────────────────────────

    /// <summary>The focused pane's selection when it is one line of text; <c>null</c> otherwise.</summary>
    private string? SelectionOfFocusedPane()
    {
        if (FocusedSide is not { } side || Pane(side) is not { } pane)
        {
            return null;
        }

        string text = pane.SelectedText;
        return string.IsNullOrEmpty(text) || text.Contains('\n') || text.Contains('\r') ? null : text;
    }

    /// <summary>Walks the matches by <paramref name="delta"/>, wrapping at either end.</summary>
    private void MoveFindMatch(int delta)
    {
        IReadOnlyList<FindMatch> matches = FindResult?.Matches ?? [];
        if (matches.Count == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.FindNoMatches));
            return;
        }

        // The walk wraps: stopping at the end would cost a second key to start over, and the
        // matches are in row order, so wrapping is the only backwards jump on screen.
        int index = CurrentFindMatchIndex < 0
            ? (delta > 0 ? 0 : matches.Count - 1)
            : (((CurrentFindMatchIndex + delta) % matches.Count) + matches.Count) % matches.Count;
        SetCurrentFindMatch(index, scroll: true);
    }

    private void SetCurrentFindMatch(int index, bool scroll)
    {
        IReadOnlyList<FindMatch> matches = FindResult?.Matches ?? [];
        int clamped = matches.Count == 0 ? -1 : Math.Clamp(index, -1, matches.Count - 1);
        SetAndRaise(CurrentFindMatchIndexProperty, ref _currentFindMatchIndex, clamped);

        FindMatch? current = clamped < 0 ? null : matches[clamped];
        foreach (DiffSide side in new[] { DiffSide.Left, DiffSide.Right })
        {
            if (Pane(side) is { } pane)
            {
                // A match belongs to one pane; the other draws no current match.
                pane.CurrentSearchMatch = current is { } match && match.Side == side ? match : null;
            }
        }

        if (scroll && current is { } chosen)
        {
            RevealMatch(chosen);
        }

        UpdateFindBar();
        _controller.UpdateStrip();
    }

    /// <summary>Selects the match in its own pane, focuses that pane, and centres its row in both.</summary>
    private void RevealMatch(FindMatch match)
    {
        if (Pane(match.Side) is not { } pane)
        {
            return;
        }

        TextDocument text = match.Side == DiffSide.Left ? LeftDocument : RightDocument;
        if (match.Line >= 0 && match.Line < text.LineCount)
        {
            DocumentLine line = text.GetLineByNumber(match.Line + 1);
            int start = Math.Min(line.Offset + match.Column, line.EndOffset);
            pane.Select(start, Math.Min(match.Length, line.EndOffset - start));
        }

        pane.TextArea.Focus();

        // Last, so the centring wins over any scroll the caret brought about; both panes move
        // because the rows are aligned.
        if (Document is { } document)
        {
            IReadOnlyList<DiffLine> lines = document.Pane(match.Side).Lines;
            if (match.Line >= 0 && match.Line < lines.Count)
            {
                // The run first, then the scroll: a row that is still folded projects to its
                // placeholder, and the pane would stop somewhere the match is not.
                _controller.RevealRow(lines[match.Line].Row);
                _controller.ScrollToRows(lines[match.Line].Row, 1);
            }
        }
    }

    /// <summary>Schedules a search: the one in flight is cancelled and the query rests for <see cref="FindDebounce"/>.</summary>
    private void RequestFind()
    {
        _findTimer?.Dispose();
        _findTimer = null;
        CancelFind();

        if (!IsFindBarOpen || Document is null || string.IsNullOrEmpty(FindQuery))
        {
            ApplyFindResult(null);
            return;
        }

        _findTimer = TimeProvider.CreateTimer(
            _ => Dispatcher.UIThread.Post(RunFind),
            state: null,
            FindDebounce,
            Timeout.InfiniteTimeSpan);
    }

    private void RunFind()
    {
        if (!IsFindBarOpen || Document is not { } document || string.IsNullOrEmpty(FindQuery))
        {
            return;
        }

        int generation = ++_findGeneration;
        CancellationTokenSource cts = new();
        _findCts = cts;
        string query = FindQuery;
        FindOptions options = FindOptions;
        // Captured here, on the UI thread: the worker sees an immutable snapshot and a copy of
        // the line table, never a TextDocument.
        DocumentPaneText left = DocumentPaneText.Capture(LeftDocument);
        DocumentPaneText right = DocumentPaneText.Capture(RightDocument);
        DiffViewLog.FindStarted(_findLogger, generation, query.Length, options);
        CurrentFind = RunFindAsync(generation, document, left, right, query, options, cts.Token);
    }

    private async Task RunFindAsync(
        int generation,
        SideBySideDocument document,
        IPaneText left,
        IPaneText right,
        string query,
        FindOptions options,
        CancellationToken token)
    {
        long started = Stopwatch.GetTimestamp();
        FindResult? result = null;
        Exception? failure = null;
        bool cancelled = false;
        try
        {
            result = document.Rows.Count > FindWorkerRowThreshold
                ? await Task.Run(() => Searcher(document, left, right, query, options, token), token).ConfigureAwait(false)
                : Searcher(document, left, right, query, options, token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            // DiffSearch reports a bad query as FindResult.Error; anything else is a bug, and it
            // must not take the control down or leave the task faulted.
            failure = ex;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        if (Dispatcher.UIThread.CheckAccess())
        {
            CompleteFind(generation, result, failure, cancelled, elapsed);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => CompleteFind(generation, result, failure, cancelled, elapsed));
        }
    }

    private void CompleteFind(int generation, FindResult? result, Exception? failure, bool cancelled, TimeSpan elapsed)
    {
        if (generation != _findGeneration)
        {
            // Latest wins, as builds do: a newer search superseded this one.
            DiffViewLog.FindSuperseded(_findLogger, generation, _findGeneration);
            return;
        }

        CurrentFind = null;
        if (cancelled)
        {
            DiffViewLog.FindCancelled(_findLogger, generation);
            return;
        }

        FindResult found;
        if (failure is not null)
        {
            DiffViewLog.FindFailed(_findLogger, generation, failure);
            found = Core.FindResult.Failed(DiffViewStrings.Get(DiffViewStrings.FindFailedMessage));
        }
        else
        {
            found = result!;
            if (found.Error is null)
            {
                DiffViewLog.FindCompleted(_findLogger, generation, found, elapsed);
            }
            else
            {
                DiffViewLog.FindFailed(_findLogger, generation, null);
            }
        }

        ApplyFindResult(found);
        if (found.Truncated)
        {
            Status.SetWarning(DiffViewStrings.Format(DiffViewStrings.FindTruncated, found.Matches.Count.ToString("N0", CultureInfo.CurrentCulture)));
        }

        FindCompleted?.Invoke(this, new DiffFindCompletedEventArgs(found));
    }

    /// <summary>Puts a result — or no result — on the panes, the minimap, the bar and the strip.</summary>
    private void ApplyFindResult(FindResult? result)
    {
        FindResult = result;
        // A fresh result has no current match: typing must not pull focus out of the query box,
        // so the first Next or Enter is what lands on a match.
        SetAndRaise(CurrentFindMatchIndexProperty, ref _currentFindMatchIndex, -1);

        List<FindMatch> left = [];
        List<FindMatch> right = [];
        List<int> rows = [];
        SideBySideDocument? document = Document;
        // A failed query highlights nothing; its matches are empty anyway.
        foreach (FindMatch match in result?.Matches ?? [])
        {
            (match.Side == DiffSide.Left ? left : right).Add(match);
            IReadOnlyList<DiffLine> lines = document?.Pane(match.Side).Lines ?? [];
            if (match.Line >= 0 && match.Line < lines.Count)
            {
                rows.Add(lines[match.Line].Row);
            }
        }

        if (_controller.LeftPane is not null)
        {
            _controller.LeftPane.SearchMatches = left;
            _controller.LeftPane.CurrentSearchMatch = null;
        }

        if (_controller.RightPane is not null)
        {
            _controller.RightPane.SearchMatches = right;
            _controller.RightPane.CurrentSearchMatch = null;
        }

        if (_controller.Minimap is not null)
        {
            _controller.Minimap.MatchRows = rows.Count == 0 ? null : rows;
        }

        UpdateFindBar();
        _controller.UpdateStrip();
        RaiseFindCanExecuteChanged();
    }

    private void CancelFind()
    {
        if (_findCts is { } cts)
        {
            _findCts = null;
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void UpdateFindBar()
    {
        if (_findBar is not { } bar)
        {
            return;
        }

        // A collapsed bar takes no height, which is why the template's find row is Auto.
        bar.IsVisible = IsFindBarOpen;
        _syncingFindBar = true;
        try
        {
            FindOptions options = FindOptions;
            bar.Query = FindQuery;
            bar.MatchCase = options.MatchCase;
            bar.WholeWord = options.WholeWord;
            bar.UseRegex = options.UseRegex;
            bar.ChangedRowsOnly = options.ChangedRowsOnly;
            bar.Scope = options.Scope;
            bar.CountText = FindCountText();
            bar.ErrorText = FindResult?.Error;
            bar.NoticeText = FindResult is { Truncated: true } truncated
                ? DiffViewStrings.Format(DiffViewStrings.FindTruncated, truncated.Matches.Count.ToString("N0", CultureInfo.CurrentCulture))
                : null;
        }
        finally
        {
            _syncingFindBar = false;
        }
    }

    private string? FindCountText()
    {
        if (FindResult is not { Error: null } result || string.IsNullOrEmpty(FindQuery))
        {
            return null;
        }

        if (result.Matches.Count == 0)
        {
            return DiffViewStrings.Get(DiffViewStrings.FindNoMatches);
        }

        string total = result.Matches.Count.ToString("N0", CultureInfo.CurrentCulture);
        string leftCount = result.LeftCount.ToString("N0", CultureInfo.CurrentCulture);
        string rightCount = result.RightCount.ToString("N0", CultureInfo.CurrentCulture);
        return CurrentFindMatchIndex >= 0
            ? DiffViewStrings.Format(DiffViewStrings.FindMatchOf, (CurrentFindMatchIndex + 1).ToString("N0", CultureInfo.CurrentCulture), total, leftCount, rightCount)
            : DiffViewStrings.Format(DiffViewStrings.FindMatches, total, leftCount, rightCount);
    }

    /// <summary>The strip's find lane: the count and the scope, only while the bar is open.</summary>
    private string? FindStripText()
    {
        if (!IsFindBarOpen)
        {
            return null;
        }

        string scope = DiffViewStrings.Get(FindOptions.Scope switch
        {
            FindScope.Left => DiffViewStrings.SideLeft,
            FindScope.Right => DiffViewStrings.SideRight,
            _ => DiffViewStrings.FindScopeBothWord,
        });

        if (FindResult is not { Error: null } result || string.IsNullOrEmpty(FindQuery))
        {
            return DiffViewStrings.Format(DiffViewStrings.StatusFindScope, scope);
        }

        string total = result.Matches.Count.ToString("N0", CultureInfo.CurrentCulture);
        string count = result.Matches.Count == 0
            ? DiffViewStrings.Get(DiffViewStrings.FindNoMatches)
            : CurrentFindMatchIndex >= 0
                ? DiffViewStrings.Format(DiffViewStrings.StatusFindMatchOf, (CurrentFindMatchIndex + 1).ToString("N0", CultureInfo.CurrentCulture), total)
                : DiffViewStrings.Format(DiffViewStrings.StatusFindMatches, total);
        return DiffViewStrings.Format(DiffViewStrings.StatusFind, count, scope);
    }

    private void RaiseFindCanExecuteChanged()
    {
        _closeFind.RaiseCanExecuteChanged();
        _findNext.RaiseCanExecuteChanged();
        _findPrevious.RaiseCanExecuteChanged();
    }

    private void OnFindBarQueryChanged(object? sender, EventArgs e)
    {
        if (_syncingFindBar || _findBar is not { } bar)
        {
            return;
        }

        FindQuery = bar.Query;
    }

    private void OnFindBarOptionsChanged(object? sender, EventArgs e)
    {
        if (_syncingFindBar || _findBar is not { } bar)
        {
            return;
        }

        FindOptions = FindOptions with
        {
            MatchCase = bar.MatchCase,
            WholeWord = bar.WholeWord,
            UseRegex = bar.UseRegex,
            ChangedRowsOnly = bar.ChangedRowsOnly,
            Scope = bar.Scope,
        };
    }

    private void OnFindBarNextRequested(object? sender, EventArgs e)
    {
        FindNext();
    }

    private void OnFindBarPreviousRequested(object? sender, EventArgs e)
    {
        FindPrevious();
    }

    private void OnFindBarCloseRequested(object? sender, EventArgs e)
    {
        CloseFind();
    }


    // ── The controller's seam ──────────────────────────────────────────────────────────────

    // Implemented explicitly, so none of it reaches this control's public surface. What the
    // editor answers here is what a viewer would answer differently: every one of these is a
    // verb it has, or a piece of state only editing produces.

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
        _findBar = e.NameScope.Find<DiffFindBar>(FindBarPart);

        if (_controller.Gutter is not null)
        {
            _controller.Gutter.ContextRequested += OnConnectorContextRequested;
        }

        if (_controller.LeftHeader is not null)
        {
            _controller.LeftHeader.ContextRequested += OnLeftHeaderContextRequested;
        }

        if (_controller.RightHeader is not null)
        {
            _controller.RightHeader.ContextRequested += OnRightHeaderContextRequested;
        }

        if (_controller.Minimap is not null)
        {
            _controller.Minimap.ContextRequested += OnMinimapContextRequested;
        }

        if (_findBar is not null)
        {
            _findBar.QueryChanged += OnFindBarQueryChanged;
            _findBar.OptionsChanged += OnFindBarOptionsChanged;
            _findBar.NextRequested += OnFindBarNextRequested;
            _findBar.PreviousRequested += OnFindBarPreviousRequested;
            _findBar.CloseRequested += OnFindBarCloseRequested;
        }

        // Before the controller writes the strip, which reads this control's find lane.
        UpdateFindBar();
    }

    /// <inheritdoc/>
    void IDiffSurface.OnPartsDetaching()
    {
        if (_controller.Gutter is not null)
        {
            _controller.Gutter.ContextRequested -= OnConnectorContextRequested;
        }

        if (_controller.LeftHeader is not null)
        {
            _controller.LeftHeader.ContextRequested -= OnLeftHeaderContextRequested;
        }

        if (_controller.RightHeader is not null)
        {
            _controller.RightHeader.ContextRequested -= OnRightHeaderContextRequested;
        }

        if (_controller.Minimap is not null)
        {
            _controller.Minimap.ContextRequested -= OnMinimapContextRequested;
        }

        if (_findBar is not null)
        {
            _findBar.QueryChanged -= OnFindBarQueryChanged;
            _findBar.OptionsChanged -= OnFindBarOptionsChanged;
            _findBar.NextRequested -= OnFindBarNextRequested;
            _findBar.PreviousRequested -= OnFindBarPreviousRequested;
            _findBar.CloseRequested -= OnFindBarCloseRequested;
        }
    }

    /// <inheritdoc/>
    void IDiffSurface.OnPaneAttached(DiffPanePresenter pane, DiffSide side)
    {
        pane.IsReadOnly = side == DiffSide.Left ? LeftReadOnly : RightReadOnly;
        // The other side's flag: a pane offers a copy arrow when the side it would copy to is
        // editable, not when it is itself.
        pane.CanCopyOut = side == DiffSide.Left ? !RightReadOnly : !LeftReadOnly;
        pane.ModifiedLines = side == DiffSide.Left ? _leftModifiedLines : _rightModifiedLines;
        pane.CopyOutRequested += OnPaneCopyOutRequested;
        pane.CopySelectionRequested += OnPaneCopySelectionRequested;
        pane.ContextMenuRequested += OnPaneContextMenuRequested;
    }

    /// <inheritdoc/>
    void IDiffSurface.OnModelApplied()
    {
        ApplyFindResult(null);
        RequestFind();
    }

    /// <inheritdoc/>
    void IDiffSurface.OnSourceReplaced(DiffSide side, TextDocument document)
    {
        // A re-diff armed by an edit to the document being replaced describes text that is about
        // to stop existing; the build the controller requests supersedes it anyway.
        _reDiffTimer?.Dispose();
        _reDiffTimer = null;

        // The controller has not assigned the new document yet, so these still read the old one —
        // which is the only moment its handlers can be taken off.
        if (side == DiffSide.Left)
        {
            _controller.LeftDocument.TextChanged -= OnLeftTextChanged;
            _controller.LeftDocument.Changed -= OnLeftDocumentChanged;
            _leftModifiedLines.Clear();
            _leftEdited = false;
            _leftDirty = false;
            _leftStamp = StampOf(LeftSource);
            document.TextChanged += OnLeftTextChanged;
            document.Changed += OnLeftDocumentChanged;
        }
        else
        {
            _controller.RightDocument.TextChanged -= OnRightTextChanged;
            _controller.RightDocument.Changed -= OnRightDocumentChanged;
            _rightModifiedLines.Clear();
            _rightEdited = false;
            _rightDirty = false;
            _rightStamp = StampOf(RightSource);
            document.TextChanged += OnRightTextChanged;
            document.Changed += OnRightDocumentChanged;
        }
    }

    /// <inheritdoc/>
    void IDiffSurface.OnChangeSetMoved() => RaiseNavigationCanExecuteChanged();

    /// <inheritdoc/>
    void IDiffSurface.OnFoldsChanged() => RaiseFoldingCanExecuteChanged();

    /// <inheritdoc/>
    bool IDiffSurface.IsDirty(DiffSide side) => IsDirty(side);

    /// <inheritdoc/>
    string? IDiffSurface.FindStripText() => FindStripText();

    /// <inheritdoc/>
    bool IDiffSurface.IsEdited(DiffSide side) => IsEdited(side);

    /// <inheritdoc/>
    void IDiffSurface.OnDocumentReplaced(DiffSide side, TextDocument oldValue, TextDocument newValue)
    {
        // The storage is the controller's and the notification is this type's: a DirectProperty
        // registered against this control is not in a sibling's registry, so SetAndRaise cannot
        // reach across and the change is raised here instead.
        RaisePropertyChanged(
            side == DiffSide.Left ? LeftDocumentProperty : RightDocumentProperty,
            oldValue,
            newValue);
    }

    /// <inheritdoc/>
    void IDiffSurface.RaiseBuildCompleted(DiffBuildCompletedEventArgs e) => BuildCompleted?.Invoke(this, e);

    /// <inheritdoc/>
    void IDiffSurface.RaiseBuildFailed(DiffBuildFailedEventArgs e) => BuildFailed?.Invoke(this, e);

    /// <inheritdoc/>
    void IDiffSurface.RaiseRenderFault(RenderFaultEventArgs e) => RenderFault?.Invoke(this, e);
}
