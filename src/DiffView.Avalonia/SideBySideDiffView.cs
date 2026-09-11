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

namespace Bennewitz.Ninja.DiffView.Avalonia;

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
public class SideBySideDiffView : TemplatedControl
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

    /// <summary>Identifies the <see cref="ShowWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> ShowWhitespaceProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowWhitespace));

    /// <summary>Identifies the <see cref="ShowMinimap"/> property.</summary>
    public static readonly StyledProperty<bool> ShowMinimapProperty =
        AvaloniaProperty.Register<SideBySideDiffView, bool>(nameof(ShowMinimap), defaultValue: true);

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
    private bool _isFindBarOpen;
    private string _findQuery = string.Empty;
    private FindOptions _findOptions = FindOptions.Default;
    private FindResult? _findResult;
    private int _currentFindMatchIndex = -1;
    private int _currentChangeIndex = -1;
    private double _splitRatio = 0.5;
    private Grid? _headersGrid;
    private Grid? _panesGrid;
    private ChangeConnectorGutter? _gutter;
    private DiffMinimap? _minimap;
    private Border? _headerLeftSpacer;
    private Border? _headerRightSpacer;

    /// <summary>
    /// The columns `PART_Panes` and `PART_Headers` share: a map slot at either end, the two star
    /// columns the split ratio is written into, and the connector gutter between them. The two
    /// grids are laid out alike so a header is exactly as wide as its pane by construction.
    /// </summary>
    private const int LeftMinimapColumn = 0;
    private const int LeftPaneColumn = 1;
    private const int RightPaneColumn = 3;
    private const int RightMinimapColumn = 4;
    private DiffViewState _state = DiffViewState.Empty;
    private string? _stateMessage;
    private SideBySideDocument? _document;
    private DiffDiagnostics? _diagnostics;
    private IReadOnlyList<DiffWarning> _warnings = [];
    private int _changeCount;
    private DiffBannerKind _bannerKind;
    private string? _bannerMessage;
    private string? _bannerActionText;
    private bool _isStale;
    private bool _isBuildingSlowly;
    private TextDocument _leftDocument = new();
    private TextDocument _rightDocument = new();
    private DiffSide? _focusedSide;
    private int _caretLine;
    private int _caretColumn;
    private TextInfo? _leftInfo;
    private TextInfo? _rightInfo;

    private StatusController? _status;
    private ILoggerFactory? _loggerFactory;
    private ILogger? _buildLogger;
    private ILogger? _renderLogger;
    private ILogger? _findLogger;
    private int _generation;
    private CancellationTokenSource? _buildCts;
    private ITimer? _slowTimer;

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

    private DiffPanePresenter? _leftPane;
    private DiffPanePresenter? _rightPane;
    private DiffPaneHeader? _leftHeader;
    private DiffPaneHeader? _rightHeader;
    private DiffStatusStrip? _statusStrip;
    private Button? _bannerAction;
    private ScrollSync? _sync;

    private readonly DiffKeyBindings _bindings;

    private DiffKeyMap _keyMap = new();

    /// <summary>Creates the control with its compiled theme merged into its own resources.</summary>
    public SideBySideDiffView()
    {
        Resources.MergedDictionaries.Add(new SideBySideDiffViewTheme());
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
        _openFind = new DelegateCommand(OpenFind);
        _closeFind = new DelegateCommand(CloseFind, () => IsFindBarOpen);
        _findNext = new DelegateCommand(FindNext, () => IsFindBarOpen);
        _findPrevious = new DelegateCommand(FindPrevious, () => IsFindBarOpen);
        Builder = static (left, right, options, token) => DiffDocumentBuilder.Build(left, right, options, token);
        Searcher = static (document, left, right, query, options, token) => DiffSearch.Find(document, left, right, query, options, token);

        // The default key bindings come from the map; a host rebinds, unbinds or clears them.
        // Escape and F3 execute only while the find bar is open, and a binding that does not
        // execute leaves the key unhandled, so Escape still reaches the rest of the application.
        _bindings = new DiffKeyBindings(this, CommandFor);
        KeyMap = DiffKeyMap.Default();

        LayoutUpdated += OnLayoutUpdated;
        RefreshStrings();
        UpdatePseudoClasses();
        SetStateCore(DiffViewState.Empty, DiffViewStrings.Get(DiffViewStrings.StateEmptyMessage), log: false);
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
    /// Whether the overview map is shown beside the panes. On by default. Off, its column takes
    /// no width at all, so the panes get it back rather than looking at a gap.
    /// </summary>
    public bool ShowMinimap
    {
        get => GetValue(ShowMinimapProperty);
        set => SetValue(ShowMinimapProperty, value);
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
        get => _currentChangeIndex;
        set => SetCurrentChange(value, scroll: true);
    }

    /// <summary>The left pane's share of the panes' width, 0.1 to 0.9; a drag on the gutter changes it.</summary>
    public double SplitRatio
    {
        get => _splitRatio;
        set
        {
            double clamped = Math.Clamp(value, 0.1, 0.9);
            if (SetAndRaise(SplitRatioProperty, ref _splitRatio, clamped))
            {
                ApplySplit();
            }
        }
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
    public DiffViewState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <summary>What the state means to the user: the empty prompt, the warning, or the failure.</summary>
    public string? StateMessage
    {
        get => _stateMessage;
        private set => SetAndRaise(StateMessageProperty, ref _stateMessage, value);
    }

    /// <summary>The model the panes render, or <c>null</c> before the first build lands.</summary>
    public SideBySideDocument? Document
    {
        get => _document;
        private set => SetAndRaise(DocumentProperty, ref _document, value);
    }

    /// <summary>What the last successful build measured.</summary>
    public DiffDiagnostics? Diagnostics
    {
        get => _diagnostics;
        private set => SetAndRaise(DiagnosticsProperty, ref _diagnostics, value);
    }

    /// <summary>The last successful build's warnings.</summary>
    public IReadOnlyList<DiffWarning> Warnings
    {
        get => _warnings;
        private set => SetAndRaise(WarningsProperty, ref _warnings, value);
    }

    /// <summary>Change blocks in the model.</summary>
    public int ChangeCount
    {
        get => _changeCount;
        private set => SetAndRaise(ChangeCountProperty, ref _changeCount, value);
    }

    /// <summary>Which banner is shown above the panes.</summary>
    public DiffBannerKind BannerKind
    {
        get => _bannerKind;
        private set => SetAndRaise(BannerKindProperty, ref _bannerKind, value);
    }

    /// <summary>The banner's text.</summary>
    public string? BannerMessage
    {
        get => _bannerMessage;
        private set => SetAndRaise(BannerMessageProperty, ref _bannerMessage, value);
    }

    /// <summary>The banner's action label, or <c>null</c> when the banner offers none.</summary>
    public string? BannerActionText
    {
        get => _bannerActionText;
        private set => SetAndRaise(BannerActionTextProperty, ref _bannerActionText, value);
    }

    /// <summary>Whether the result on screen is about to be replaced by a running build.</summary>
    public bool IsStale
    {
        get => _isStale;
        private set => SetAndRaise(IsStaleProperty, ref _isStale, value);
    }

    /// <summary>Whether the running build has passed <see cref="SlowBuildThreshold"/>.</summary>
    public bool IsBuildingSlowly
    {
        get => _isBuildingSlowly;
        private set => SetAndRaise(IsBuildingSlowlyProperty, ref _isBuildingSlowly, value);
    }

    /// <summary>The live left document: the source text, never padded.</summary>
    public TextDocument LeftDocument
    {
        get => _leftDocument;
        private set => SetAndRaise(LeftDocumentProperty, ref _leftDocument, value);
    }

    /// <summary>The live right document: the source text, never padded.</summary>
    public TextDocument RightDocument
    {
        get => _rightDocument;
        private set => SetAndRaise(RightDocumentProperty, ref _rightDocument, value);
    }

    /// <summary>The pane with keyboard focus, or <c>null</c>.</summary>
    public DiffSide? FocusedSide
    {
        get => _focusedSide;
        private set => SetAndRaise(FocusedSideProperty, ref _focusedSide, value);
    }

    /// <summary>The focused pane's caret line, 1-based; 0 without focus.</summary>
    public int CaretLine
    {
        get => _caretLine;
        private set => SetAndRaise(CaretLineProperty, ref _caretLine, value);
    }

    /// <summary>The focused pane's caret column, 1-based; 0 without focus.</summary>
    public int CaretColumn
    {
        get => _caretColumn;
        private set => SetAndRaise(CaretColumnProperty, ref _caretColumn, value);
    }

    /// <summary>
    /// Creates the four category loggers (<see cref="DiffViewLogCategories"/>) when set. State
    /// transitions log at <c>Information</c>, warnings at <c>Warning</c>, faults at <c>Error</c>
    /// with the exception, cancellation at <c>Debug</c>; never document text.
    /// </summary>
    public ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set
        {
            _loggerFactory = value;
            _buildLogger = value?.CreateLogger(DiffViewLogCategories.Build);
            _renderLogger = value?.CreateLogger(DiffViewLogCategories.Render);
            _findLogger = value?.CreateLogger(DiffViewLogCategories.Find);
            if (_leftPane is not null)
            {
                _leftPane.Logger = _renderLogger;
            }

            if (_rightPane is not null)
            {
                _rightPane.Logger = _renderLogger;
            }
        }
    }

    /// <summary>The clock behind the transient messages' auto-clear and the slow-build threshold. Set it before the first build.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>The transient message lane; its typed helpers are the only way to emit one.</summary>
    public StatusController Status
    {
        get
        {
            if (_status is null)
            {
                _status = new StatusController(TimeProvider);
                _status.Changed += OnStatusChanged;
            }

            return _status;
        }
    }

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
    internal Func<PaneSource, PaneSource, DiffOptions, CancellationToken, DiffBuildResult> Builder { get; set; }

    /// <summary>The search routine; tests replace it to hold a search open or watch its thread.</summary>
    internal Func<SideBySideDocument, IPaneText, IPaneText, string, FindOptions, CancellationToken, FindResult> Searcher { get; set; }

    /// <summary>Above this many rows the search runs on a worker; at or below it, inline.</summary>
    internal int FindWorkerRowThreshold { get; set; } = 2_000;

    /// <summary>The in-flight build, completing when its outcome has been applied or discarded; <c>null</c> when idle.</summary>
    internal Task? CurrentBuild { get; private set; }

    /// <summary>The in-flight search, completing when its outcome has been applied or discarded; <c>null</c> when idle.</summary>
    internal Task? CurrentFind { get; private set; }

    internal DiffFindBar? FindBar => _findBar;

    /// <summary>The word-level lookup of the current model, bound to the options its build ran under; <c>null</c> without a model.</summary>
    public WordDiffLookup? WordDiffLookup { get; private set; }

    internal ChangeConnectorGutter? Gutter => _gutter;

    internal DiffMinimap? Minimap => _minimap;

    internal DiffPanePresenter? LeftPane => _leftPane;

    internal DiffPanePresenter? RightPane => _rightPane;

    internal DiffPaneHeader? LeftHeader => _leftHeader;

    internal DiffPaneHeader? RightHeader => _rightHeader;

    internal DiffStatusStrip? StatusStrip => _statusStrip;

    internal Button? BannerAction => _bannerAction;

    internal ScrollSync? Sync => _sync;

    /// <summary>Runs the build again with the current sources and options; the panes' faults are cleared.</summary>
    public void Retry()
    {
        RequestBuild(keepModel: true);
    }

    /// <summary>Aligns the sides regardless of similarity: sets <see cref="ForceAlignment"/>, which rebuilds.</summary>
    public void ForceAlign()
    {
        ForceAlignment = true;
    }

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

        SetCurrentChange(CurrentChangeIndex + 1, scroll: true);
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

        SetCurrentChange(CurrentChangeIndex - 1, scroll: true);
    }

    /// <summary>Moves to the first change.</summary>
    public void FirstChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        SetCurrentChange(0, scroll: true);
    }

    /// <summary>Moves to the last change.</summary>
    public void LastChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        SetCurrentChange(ChangeCount - 1, scroll: true);
    }

    /// <summary>Moves keyboard focus to the other pane; to the left one when neither has it.</summary>
    public void SwitchPane()
    {
        DiffSide target = FocusedSide == DiffSide.Left ? DiffSide.Right : DiffSide.Left;
        Pane(target)?.TextArea.Focus();
    }

    /// <summary>Scrolls both panes so <paramref name="row"/> sits at the centre of the viewport.</summary>
    public void ScrollToRow(int row)
    {
        ScrollToRows(row, 1);
    }

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
        UpdateStrip();

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
        UpdateStrip();
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
    internal DiffPanePresenter? Pane(DiffSide side)
    {
        return side == DiffSide.Left ? _leftPane : _rightPane;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _leftPane = e.NameScope.Find<DiffPanePresenter>(LeftPanePart);
        _rightPane = e.NameScope.Find<DiffPanePresenter>(RightPanePart);
        _leftHeader = e.NameScope.Find<DiffPaneHeader>(LeftHeaderPart);
        _rightHeader = e.NameScope.Find<DiffPaneHeader>(RightHeaderPart);
        _statusStrip = e.NameScope.Find<DiffStatusStrip>(StatusStripPart);
        _bannerAction = e.NameScope.Find<Button>(BannerActionPart);
        _headersGrid = e.NameScope.Find<Grid>(HeadersPart);
        _headerLeftSpacer = e.NameScope.Find<Border>(HeaderLeftSpacerPart);
        _headerRightSpacer = e.NameScope.Find<Border>(HeaderRightSpacerPart);
        _panesGrid = e.NameScope.Find<Grid>(PanesPart);
        _gutter = e.NameScope.Find<ChangeConnectorGutter>(GutterPart);
        _minimap = e.NameScope.Find<DiffMinimap>(MinimapPart);
        _findBar = e.NameScope.Find<DiffFindBar>(FindBarPart);

        AttachPane(_leftPane, DiffSide.Left);
        AttachPane(_rightPane, DiffSide.Right);
        if (_statusStrip is not null)
        {
            _statusStrip.DismissRequested += OnDismissRequested;
        }

        if (_bannerAction is not null)
        {
            _bannerAction.Click += OnBannerActionClicked;
        }

        if (_gutter is not null)
        {
            _gutter.Document = Document;
            _gutter.CurrentChangeIndex = CurrentChangeIndex;
            _gutter.BlockClicked += OnGutterBlockClicked;
            _gutter.ResizeDragged += OnGutterResizeDragged;
            _gutter.ContextRequested += OnConnectorContextRequested;
        }

        if (_minimap is not null)
        {
            _minimap.Document = Document;
            _minimap.CurrentChangeIndex = CurrentChangeIndex;
            // Both paths, because a host that sets the flag in XAML is wired here and never
            // reaches the property-change handler — the gap plan 00004 had to fix for CanCopyOut.
            _minimap.IsVisible = ShowMinimap;
            ApplyMinimapPlacement();
            _minimap.JumpRequested += OnMinimapJumpRequested;
            _minimap.ContextRequested += OnMinimapContextRequested;
        }

        if (_findBar is not null)
        {
            _findBar.QueryChanged += OnFindBarQueryChanged;
            _findBar.OptionsChanged += OnFindBarOptionsChanged;
            _findBar.NextRequested += OnFindBarNextRequested;
            _findBar.PreviousRequested += OnFindBarPreviousRequested;
            _findBar.CloseRequested += OnFindBarCloseRequested;
        }

        ApplySplit();
        TryWireScrollSync();
        UpdateHeaders();
        UpdateFindBar();
        UpdateStrip();
        UpdateBanner();
        UpdateOverview();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LeftSourceProperty)
        {
            OnSourceChanged(DiffSide.Left, change.GetNewValue<PaneSource?>());
        }
        else if (change.Property == RightSourceProperty)
        {
            OnSourceChanged(DiffSide.Right, change.GetNewValue<PaneSource?>());
        }
        else if (change.Property == LeftReadOnlyProperty)
        {
            if (_leftPane is not null)
            {
                _leftPane.IsReadOnly = LeftReadOnly;
            }

            // A pane offers an arrow when the *other* side can receive the copy, so the left
            // side's flag drives the right pane's arrows.
            if (_rightPane is not null)
            {
                _rightPane.CanCopyOut = !LeftReadOnly;
            }

            RaiseNavigationCanExecuteChanged();
        }
        else if (change.Property == RightReadOnlyProperty)
        {
            if (_rightPane is not null)
            {
                _rightPane.IsReadOnly = RightReadOnly;
            }

            if (_leftPane is not null)
            {
                _leftPane.CanCopyOut = !RightReadOnly;
            }

            RaiseNavigationCanExecuteChanged();
        }
        else if (change.Property == IgnoreWhitespaceProperty
                 || change.Property == IgnoreCaseProperty
                 || change.Property == WordDiffProperty
                 || change.Property == MaxWordDiffLineLengthProperty
                 || change.Property == ForceAlignmentProperty)
        {
            RequestBuild(keepModel: true);
        }
        else if (change.Property == SyncHorizontalScrollProperty && _sync is not null)
        {
            _sync.SyncHorizontal = SyncHorizontalScroll;
            _sync.Align();
        }
        else if (change.Property == IsCaretBlinkEnabledProperty)
        {
            ForEachPane(pane => pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled);
        }
        else if (change.Property == UseSyntaxHighlightingProperty)
        {
            ForEachPane(pane => pane.UseSyntaxHighlighting = UseSyntaxHighlighting);
        }
        else if (change.Property == ShowMinimapProperty)
        {
            if (_minimap is not null)
            {
                _minimap.IsVisible = ShowMinimap;
            }

            ApplyMinimapPlacement();
        }
        else if (change.Property == MinimapPlacementProperty)
        {
            ApplyMinimapPlacement();
        }
        else if (change.Property == ShowWhitespaceProperty
                 || change.Property == ShowLineEndingsProperty
                 || change.Property == TabWidthProperty)
        {
            ForEachPane(ApplyDisplayOptions);
        }
        else if (change.Property == PaneFontSizeProperty || change.Property == PaneFontFamilyProperty)
        {
            ForEachPane(ApplyPaneFont);
        }
        else if (change.Property == StateProperty || change.Property == BannerKindProperty)
        {
            UpdatePseudoClasses();
            _retry.RaiseCanExecuteChanged();
            _force.RaiseCanExecuteChanged();
        }
        else if (change.Property == ChangeCountProperty)
        {
            RaiseNavigationCanExecuteChanged();
        }
    }

    // ── Sources and builds ─────────────────────────────────────────────────────────────────

    private void OnSourceChanged(DiffSide side, PaneSource? source)
    {
        DiffViewLog.SourceAssigned(_buildLogger, side, source);
        TextInfo? info = source is null ? null : TextProbe.Probe(source);
        TextDocument document = new(source?.Text ?? string.Empty);

        // A re-diff armed by an edit to the document being replaced describes text that is about
        // to stop existing; the build this method requests supersedes it anyway.
        _reDiffTimer?.Dispose();
        _reDiffTimer = null;

        if (side == DiffSide.Left)
        {
            _leftDocument.TextChanged -= OnLeftTextChanged;
            _leftDocument.Changed -= OnLeftDocumentChanged;
            _leftModifiedLines.Clear();
            _leftInfo = info;
            _leftEdited = false;
            _leftDirty = false;
            _leftStamp = StampOf(source);
            LeftDocument = document;
            document.TextChanged += OnLeftTextChanged;
            document.Changed += OnLeftDocumentChanged;
        }
        else
        {
            _rightDocument.TextChanged -= OnRightTextChanged;
            _rightDocument.Changed -= OnRightDocumentChanged;
            _rightModifiedLines.Clear();
            _rightInfo = info;
            _rightEdited = false;
            _rightDirty = false;
            _rightStamp = StampOf(source);
            RightDocument = document;
            document.TextChanged += OnRightTextChanged;
            document.Changed += OnRightDocumentChanged;
        }

        DiffPanePresenter? pane = Pane(side);
        if (pane is not null)
        {
            pane.Document = document;
            // The grammar follows the file, not the build, so it is chosen here.
            pane.SyntaxFileName = SyntaxFileNameOf(source);
        }

        // The old model described the old text; nothing of it applies to the new document.
        RequestBuild(keepModel: false);
    }

    /// <summary>What the grammar is chosen from: the file the side came from, or what it is called.</summary>
    private static string? SyntaxFileNameOf(PaneSource? source)
    {
        return source?.Path ?? source?.Title;
    }

    /// <summary>Whether <paramref name="side"/> has been edited since its source was assigned.</summary>
    public bool IsEdited(DiffSide side) => side == DiffSide.Left ? _leftEdited : _rightEdited;

    /// <summary>
    /// What the next build compares: the assigned source, or the pane's live text once the user
    /// has edited it. The encoding, the path and the title come from the source either way —
    /// they are what a save writes back with, and typing does not change them.
    /// </summary>
    private PaneSource? EffectiveSource(DiffSide side)
    {
        PaneSource? source = side == DiffSide.Left ? LeftSource : RightSource;
        if (source is null || !IsEdited(side))
        {
            return source;
        }

        // Read on the UI thread, like every other text the worker sees.
        TextDocument document = side == DiffSide.Left ? LeftDocument : RightDocument;
        return new PaneSource(document.Text)
        {
            Encoding = source.Encoding,
            Path = source.Path,
            Title = source.Title,
        };
    }

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

        UpdateHeaders();
        UpdateStrip();

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
                RequestBuild(keepModel: true);
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
        string name = HeaderTitle(side, source);
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

        TextInfo? info = side == DiffSide.Left ? _leftInfo : _rightInfo;
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
        UpdateHeaders();
        UpdateStrip();
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
        UpdateHeaders();
        UpdateStrip();
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
    /// The items the menu shows for <paramref name="context"/>. The shape never changes with the
    /// state — every entry is always here, enabled or not — because a host's "insert after this
    /// item" has to mean the same thing on every open.
    /// </summary>
    /// <remarks>
    /// One method per surface rather than one method with five branches through it: what each
    /// menu is <em>about</em> is the whole design, and a reader should be able to see one menu's
    /// list without reading the other four's conditions.
    /// </remarks>
    private List<DiffMenuItem> MenuItemsFor(DiffPaneContext context)
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
                items.Add(copy);
            }
        }

        AddBlockVerbs(items, context);
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
        // file's verbs belong, and phase 3 is where it arrives.
        if (context.Region is DiffPaneRegion.Text)
        {
            items.Add(DiffMenuItem.Separator());
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

        DiffMenuItem? goTo = DiffPaneMenu.Verb(
            DiffViewStrings.Get(DiffViewStrings.MenuGoToChange),
            DiffCommand.GoToChange,
            CommandOrNull,
            GestureFor,
            block >= 0);
        if (goTo is not null)
        {
            goTo.Command = new DelegateCommand(() => GoToChange(block), () => block >= 0);
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
            SetCurrentChange(index, scroll: true);
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
    internal ContextMenu? LastPaneMenu { get; private set; }

    private void OnPaneContextMenuRequested(object? sender, DiffPanePresenter.PaneContextRequest e)
    {
        if (sender is DiffPanePresenter pane)
        {
            LastPaneMenu = DiffPaneMenu.Request(pane, e.Context, e.Pointer, PaneContextMenu, MenuItemsFor, args => PaneContextMenuOpening?.Invoke(this, args));
            e.Opened = LastPaneMenu is not null;
        }
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
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
        };
    }

    private void OnKeyMapChanged(object? sender, EventArgs e) => RebuildKeyBindings();

    private void RebuildKeyBindings() => _bindings.Rebuild(_keyMap, _renderLogger);

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
        TextInfo? info = side == DiffSide.Left ? _leftInfo : _rightInfo;
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

    /// <summary>
    /// What to call a side in a save message: the same name its header shows. The banner is left
    /// alone deliberately — it reports what the *build* did, and a save is not a build.
    /// </summary>
    private static string HeaderTitle(DiffSide side, PaneSource? source)
    {
        return source?.Title
               ?? (source?.Path is { } path ? System.IO.Path.GetFileName(path) : null)
               ?? DiffViewStrings.Get(side == DiffSide.Left ? DiffViewStrings.LeftTitle : DiffViewStrings.RightTitle);
    }

    private void ReportSave(string message)
    {
        Status.SetWarning(message);
        UpdateStrip();
    }

    /// <summary>Rebuilds now from the panes' live text, whatever the debounce was doing.</summary>
    public void ReDiffNow()
    {
        _reDiffTimer?.Dispose();
        _reDiffTimer = null;
        RequestBuild(keepModel: true);
    }

    private void RequestBuild(bool keepModel)
    {
        PaneSource? left = EffectiveSource(DiffSide.Left);
        PaneSource? right = EffectiveSource(DiffSide.Right);
        if (left is null || right is null)
        {
            CancelBuild();
            ApplyModel(null, [], null, null);
            SetState(DiffViewState.Empty, DiffViewStrings.Get(DiffViewStrings.StateEmptyMessage));
            SetBanner(DiffBannerKind.None, null, null);
            IsStale = false;
            _status?.Dismiss();
            UpdateHeaders();
            UpdateStrip();
            return;
        }

        CancelBuild();
        int generation = ++_generation;
        CancellationTokenSource cts = new();
        _buildCts = cts;
        DiffOptions options = ComposeOptions();

        if (!keepModel)
        {
            ApplyModel(null, [], null, null);
        }

        _leftPane?.ResetFaults();
        _rightPane?.ResetFaults();
        IsStale = Document is not null;
        SetState(DiffViewState.Building, null);
        SetBanner(DiffBannerKind.None, null, null);
        Status.SetActive(DiffViewStrings.Get(DiffViewStrings.BuildRunning));
        StartSlowTimer(generation);
        DiffViewLog.BuildStarted(_buildLogger, generation, options);
        UpdateHeaders();
        UpdateStrip();

        CurrentBuild = RunBuildAsync(generation, left, right, options, cts.Token);
    }

    private async Task RunBuildAsync(int generation, PaneSource left, PaneSource right, DiffOptions options, CancellationToken token)
    {
        DiffBuildResult? result = null;
        DiffBuildException? failure = null;
        bool cancelled = false;
        try
        {
            // Text was captured on the UI thread; the worker never sees a TextDocument.
            result = await Task.Run(() => Builder(left, right, options, token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (DiffBuildException ex)
        {
            failure = ex;
        }
        catch (Exception ex)
        {
            failure = new DiffBuildException(DiffBuildErrorCode.DiffFailed, ex.Message, ex);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Complete(generation, result, failure, cancelled, options);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => Complete(generation, result, failure, cancelled, options));
        }
    }

    private void Complete(int generation, DiffBuildResult? result, DiffBuildException? failure, bool cancelled, DiffOptions options)
    {
        if (generation != _generation)
        {
            // Latest wins: a newer build superseded this one; its outcome is discarded silently.
            DiffViewLog.BuildSuperseded(_buildLogger, generation, _generation);
            return;
        }

        StopSlowTimer();
        CurrentBuild = null;
        if (cancelled)
        {
            DiffViewLog.BuildCancelled(_buildLogger, generation);
            return;
        }

        if (failure is not null)
        {
            ApplyFailure(generation, failure);
        }
        else
        {
            ApplyResult(generation, result!, options);
        }
    }

    private void ApplyResult(int generation, DiffBuildResult result, DiffOptions options)
    {
        // A decorator that faulted while this build ran — a grammar that would not install, say —
        // keeps the control Degraded once it lands. It is read before the model is applied,
        // because applying one re-enables every decorator and forgets its faults.
        RenderFaultEventArgs? fault = PaneFault();
        ApplyModel(result.Document, result.Warnings, result.Diagnostics, options);
        IsStale = false;
        foreach (DiffWarning warning in result.Warnings)
        {
            DiffViewLog.BuildWarning(_buildLogger, warning);
        }

        DiffViewLog.BuildCompleted(_buildLogger, generation, result.Diagnostics, result.Warnings.Count);

        DiffWarning? tooDifferent = result.Warnings.FirstOrDefault(w => w.Code == DiffWarningCode.TooDifferentToAlign);
        if (tooDifferent is not null)
        {
            SetBanner(DiffBannerKind.TooDifferentToAlign, tooDifferent.Message, DiffViewStrings.Get(DiffViewStrings.BannerForce));
        }
        else if (result.Diagnostics.Identical)
        {
            SetBanner(DiffBannerKind.Identical, DiffViewStrings.Get(DiffViewStrings.BannerIdentical), null);
        }
        else
        {
            SetBanner(DiffBannerKind.None, null, null);
        }

        string? message = result.Warnings.Count == 0 ? fault?.Message : string.Join(" ", result.Warnings.Select(w => w.Message));
        SetState(message is null ? DiffViewState.Ready : DiffViewState.Degraded, message);

        if (result.Warnings.Count > 0)
        {
            Status.SetWarning(message!);
        }
        else if (fault is not null)
        {
            Status.SetFailure(message!);
        }
        else if (result.Diagnostics.Identical)
        {
            Status.SetSuccess(DiffViewStrings.Get(DiffViewStrings.BuildIdentical));
        }
        else
        {
            Status.SetSuccess(DiffViewStrings.Format(
                DiffViewStrings.BuildCompleted,
                result.Diagnostics.RowCount.ToString("N0", CultureInfo.CurrentCulture),
                result.Diagnostics.BuildTime.TotalMilliseconds.ToString("F0", CultureInfo.CurrentCulture)));
        }

        UpdateHeaders();
        UpdateStrip();
        BuildCompleted?.Invoke(this, new DiffBuildCompletedEventArgs(result));
    }

    private void ApplyFailure(int generation, DiffBuildException failure)
    {
        DiffViewLog.BuildFailed(_buildLogger, generation, failure);
        IsStale = false;
        SetBanner(DiffBannerKind.Error, failure.Message, DiffViewStrings.Get(DiffViewStrings.BannerRetry));
        SetState(DiffViewState.Failed, failure.Message);
        Status.SetFailure(failure.Message);
        UpdateHeaders();
        UpdateStrip();
        BuildFailed?.Invoke(this, new DiffBuildFailedEventArgs(failure));
    }

    /// <summary>
    /// The first fault either pane is still carrying: one its decorators raised since they were
    /// last re-enabled, or the one that turned its syntax highlighting off, which outlives a
    /// rebuild because a rebuild is not what would fix it.
    /// </summary>
    private RenderFaultEventArgs? PaneFault()
    {
        return _leftPane?.Faults.FirstOrDefault()
               ?? _rightPane?.Faults.FirstOrDefault()
               ?? _leftPane?.SyntaxFault
               ?? _rightPane?.SyntaxFault;
    }

    private void ApplyModel(SideBySideDocument? document, IReadOnlyList<DiffWarning> warnings, DiffDiagnostics? diagnostics, DiffOptions? options)
    {
        Document = document;
        Warnings = warnings;
        Diagnostics = diagnostics;
        ChangeCount = document?.Blocks.Count ?? 0;
        // One lookup per result, bound to the options the build ran under, over the live documents.
        WordDiffLookup = document is null || options is null
            ? null
            : new WordDiffLookup(new WordDiffCache(options), LeftDocument, RightDocument, document);
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is null)
            {
                continue;
            }

            pane.DiffDocument = document;
            pane.WordDiffLookup = WordDiffLookup;
        }

        if (_gutter is not null)
        {
            _gutter.Document = document;
        }

        if (_minimap is not null)
        {
            _minimap.Document = document;
        }

        // The matches were found over rows the old model defined; the search runs again against
        // the new one while the bar is open.
        ApplyFindResult(null);
        RequestFind();

        // The blocks are new: no current change until the user picks one.
        SetCurrentChange(-1, scroll: false);
        UpdateOverview();
    }

    private void CancelBuild()
    {
        StopSlowTimer();
        if (_buildCts is { } cts)
        {
            _buildCts = null;
            cts.Cancel();
            cts.Dispose();
        }
    }

    private DiffOptions ComposeOptions()
    {
        return new DiffOptions
        {
            IgnoreWhitespace = IgnoreWhitespace,
            IgnoreCase = IgnoreCase,
            WordDiff = WordDiff,
            MaxWordDiffLineLength = MaxWordDiffLineLength,
            ForceAlignment = ForceAlignment,
        };
    }

    private void StartSlowTimer(int generation)
    {
        StopSlowTimer();
        IsBuildingSlowly = false;
        _slowTimer = TimeProvider.CreateTimer(
            _ => Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation && State == DiffViewState.Building)
                {
                    IsBuildingSlowly = true;
                    UpdateStrip();
                }
            }),
            state: null,
            SlowBuildThreshold,
            Timeout.InfiniteTimeSpan);
    }

    private void StopSlowTimer()
    {
        _slowTimer?.Dispose();
        _slowTimer = null;
        IsBuildingSlowly = false;
    }

    // ── State, banner, strip, headers ──────────────────────────────────────────────────────

    private void SetState(DiffViewState state, string? message)
    {
        SetStateCore(state, message, log: true);
    }

    private void SetStateCore(DiffViewState state, string? message, bool log)
    {
        DiffViewState previous = State;
        bool changed = previous != state || StateMessage != message;
        State = state;
        StateMessage = message;
        if (changed && log)
        {
            DiffViewLog.StateChanged(_buildLogger, previous, state, message);
        }
    }

    private void SetBanner(DiffBannerKind kind, string? message, string? action)
    {
        BannerKind = kind;
        BannerMessage = message;
        BannerActionText = action;
        UpdateBanner();
    }

    private void UpdateBanner()
    {
        if (_bannerAction is not null)
        {
            _bannerAction.Content = BannerActionText;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":banner-none", BannerKind == DiffBannerKind.None);
        PseudoClasses.Set(":banner-error", BannerKind == DiffBannerKind.Error);
        PseudoClasses.Set(":banner-degraded", BannerKind == DiffBannerKind.TooDifferentToAlign);
        PseudoClasses.Set(":banner-identical", BannerKind == DiffBannerKind.Identical);
        PseudoClasses.Set(":empty", State == DiffViewState.Empty);
        PseudoClasses.Set(":building", State == DiffViewState.Building);
        PseudoClasses.Set(":ready", State == DiffViewState.Ready);
        PseudoClasses.Set(":degraded", State == DiffViewState.Degraded);
        PseudoClasses.Set(":failed", State == DiffViewState.Failed);
    }

    private void RefreshStrings()
    {
        SetCurrentValue(LeftPaneNameProperty, DiffViewStrings.Get(DiffViewStrings.LeftPaneName));
        SetCurrentValue(RightPaneNameProperty, DiffViewStrings.Get(DiffViewStrings.RightPaneName));
        SetCurrentValue(StatusStripNameProperty, DiffViewStrings.Get(DiffViewStrings.StatusStripName));
        SetCurrentValue(GutterNameProperty, DiffViewStrings.Get(DiffViewStrings.ConnectorGutterName));
        SetCurrentValue(MinimapNameProperty, DiffViewStrings.Get(DiffViewStrings.MinimapName));
    }

    private void UpdateHeaders()
    {
        UpdateHeader(_leftHeader, DiffSide.Left, LeftSource, _leftInfo);
        UpdateHeader(_rightHeader, DiffSide.Right, RightSource, _rightInfo);
    }

    private void UpdateHeader(DiffPaneHeader? header, DiffSide side, PaneSource? source, TextInfo? info)
    {
        if (header is null)
        {
            return;
        }

        header.IsPaneFocused = FocusedSide == side;
        header.IsDirty = IsDirty(side);
        header.DirtyMarker = header.IsDirty ? DiffViewStrings.Get(DiffViewStrings.HeaderDirty) : null;
        header.Title = source?.Title
                       ?? (source?.Path is { } path ? Path.GetFileName(path) : null)
                       ?? DiffViewStrings.Get(side == DiffSide.Left ? DiffViewStrings.LeftTitle : DiffViewStrings.RightTitle);

        if (source is null || info is null)
        {
            header.Detail = DiffViewStrings.Get(DiffViewStrings.NoContent);
            header.Badge = null;
            header.BadgeKind = StatusKind.None;
            return;
        }

        header.Detail = DiffViewStrings.Format(
            DiffViewStrings.HeaderDetail,
            info.LineCount == 1 ? DiffViewStrings.Get(DiffViewStrings.LineCountOne) : DiffViewStrings.Format(DiffViewStrings.LineCount, info.LineCount.ToString("N0", CultureInfo.CurrentCulture)),
            info.Encoding?.WebName.ToUpperInvariant() ?? DiffViewStrings.Get(DiffViewStrings.EncodingText),
            LineEndingText(info.LineEnding),
            DiffViewStrings.Format(DiffViewStrings.CharCount, info.Length.ToString("N0", CultureInfo.CurrentCulture)));

        if (info.IsBinary)
        {
            header.Badge = DiffViewStrings.Get(DiffViewStrings.BadgeBinary);
            header.BadgeKind = StatusKind.Failure;
        }
        else if (info.Length == 0)
        {
            header.Badge = DiffViewStrings.Get(DiffViewStrings.BadgeEmpty);
            header.BadgeKind = StatusKind.Warning;
        }
        else if (Diagnostics is { Identical: true } && !IsStale)
        {
            header.Badge = DiffViewStrings.Get(DiffViewStrings.BadgeIdentical);
            header.BadgeKind = StatusKind.Success;
        }
        else
        {
            header.Badge = null;
            header.BadgeKind = StatusKind.None;
        }
    }

    private static string LineEndingText(LineEnding ending)
    {
        return DiffViewStrings.Get(ending switch
        {
            LineEnding.Lf => DiffViewStrings.LineEndingLf,
            LineEnding.CrLf => DiffViewStrings.LineEndingCrLf,
            LineEnding.Cr => DiffViewStrings.LineEndingCr,
            LineEnding.Mixed => DiffViewStrings.LineEndingMixed,
            _ => DiffViewStrings.LineEndingNone,
        });
    }

    /// <summary>Which sides hold unsaved edits, named as their headers name them; null for none.</summary>
    private string? DirtySidesText()
    {
        bool left = IsDirty(DiffSide.Left);
        bool right = IsDirty(DiffSide.Right);
        if (!left && !right)
        {
            return null;
        }

        string names = left && right
            ? HeaderTitle(DiffSide.Left, LeftSource) + ", " + HeaderTitle(DiffSide.Right, RightSource)
            : HeaderTitle(left ? DiffSide.Left : DiffSide.Right, left ? LeftSource : RightSource);
        return DiffViewStrings.Format(DiffViewStrings.StatusDirty, names);
    }

    private void UpdateStrip()
    {
        DiffStatusStrip? strip = _statusStrip;
        if (strip is null)
        {
            return;
        }

        strip.State = State;
        strip.StateText = DiffViewStrings.Get(State switch
        {
            DiffViewState.Empty => DiffViewStrings.StateEmpty,
            DiffViewState.Building => DiffViewStrings.StateBuilding,
            DiffViewState.Ready => DiffViewStrings.StateReady,
            DiffViewState.Degraded => DiffViewStrings.StateDegraded,
            _ => DiffViewStrings.StateFailed,
        });
        strip.DirtyText = DirtySidesText();
        strip.IsStale = IsStale;
        strip.StaleText = DiffViewStrings.Get(DiffViewStrings.StatusStale);
        strip.IsBuildingSlowly = IsBuildingSlowly;
        strip.ProgressName = DiffViewStrings.Get(DiffViewStrings.StatusProgressName);
        strip.DismissText = DiffViewStrings.Get(DiffViewStrings.StatusDismiss);

        if (Diagnostics is { } diagnostics)
        {
            strip.CountsText = DiffViewStrings.Format(DiffViewStrings.StatusCounts, diagnostics.Inserted, diagnostics.Deleted, diagnostics.Modified);
            strip.ChangesText = CurrentChangeIndex >= 0
                ? DiffViewStrings.Format(DiffViewStrings.StatusChangeOf, (CurrentChangeIndex + 1).ToString("N0", CultureInfo.CurrentCulture), ChangeCount.ToString("N0", CultureInfo.CurrentCulture))
                : ChangeCount switch
                {
                    0 => DiffViewStrings.Get(DiffViewStrings.StatusNoChanges),
                    1 => DiffViewStrings.Get(DiffViewStrings.StatusChangeOne),
                    _ => DiffViewStrings.Format(DiffViewStrings.StatusChanges, ChangeCount.ToString("N0", CultureInfo.CurrentCulture)),
                };
            strip.BuildTimeText = DiffViewStrings.Format(DiffViewStrings.StatusBuildTime, diagnostics.BuildTime.TotalMilliseconds.ToString("F0", CultureInfo.CurrentCulture));
        }
        else
        {
            strip.CountsText = null;
            strip.ChangesText = null;
            strip.BuildTimeText = null;
        }

        List<string> options = [];
        if (IgnoreWhitespace)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionIgnoreWhitespace));
        }

        if (IgnoreCase)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionIgnoreCase));
        }

        if (WordDiff == WordDiffMode.Off)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionWordDiffOff));
        }
        else if (WordDiff == WordDiffMode.Character)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionWordDiffCharacter));
        }

        if (ForceAlignment)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionForceAlignment));
        }

        strip.OptionsText = options.Count == 0 ? null : string.Join(" · ", options);
        strip.FindText = FindStripText();
        strip.CaretText = FocusedSide is null ? null : DiffViewStrings.Format(DiffViewStrings.StatusCaret, CaretLine, CaretColumn);

        StatusController status = Status;
        strip.TransientText = status.Text;
        strip.TransientKind = status.Kind;
        strip.IsTransientDismissible = status.IsDismissible;
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        UpdateStrip();
    }

    private void OnDismissRequested(object? sender, EventArgs e)
    {
        Status.Dismiss();
    }

    private void OnBannerActionClicked(object? sender, RoutedEventArgs e)
    {
        switch (BannerKind)
        {
            case DiffBannerKind.Error:
                Retry();
                break;
            case DiffBannerKind.TooDifferentToAlign:
                ForceAlign();
                break;
            default:
                break;
        }
    }

    // ── Panes ──────────────────────────────────────────────────────────────────────────────

    private void AttachPane(DiffPanePresenter? pane, DiffSide side)
    {
        if (pane is null)
        {
            return;
        }

        pane.Side = side;
        pane.Document = side == DiffSide.Left ? LeftDocument : RightDocument;
        pane.DiffDocument = Document;
        pane.WordDiffLookup = WordDiffLookup;
        pane.IsReadOnly = side == DiffSide.Left ? LeftReadOnly : RightReadOnly;
        // The other side's flag: a pane offers a copy arrow when the side it would copy to is
        // editable, not when it is itself.
        pane.CanCopyOut = side == DiffSide.Left ? !RightReadOnly : !LeftReadOnly;
        pane.ModifiedLines = side == DiffSide.Left ? _leftModifiedLines : _rightModifiedLines;
        pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
        // The logger first: assigning the file name may install a grammar, which logs.
        pane.Logger = _renderLogger;
        pane.UseSyntaxHighlighting = UseSyntaxHighlighting;
        pane.SyntaxFileName = SyntaxFileNameOf(side == DiffSide.Left ? LeftSource : RightSource);
        ApplyDisplayOptions(pane);
        ApplyPaneFont(pane);
        // The left bar is hidden and the right one reflects both: after priming the extents are equal.
        pane.VerticalScrollBarVisibility = side == DiffSide.Left ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        pane.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        pane.RenderFault += OnPaneRenderFault;
        pane.CopyOutRequested += OnPaneCopyOutRequested;
        pane.CopySelectionRequested += OnPaneCopySelectionRequested;
        pane.ContextMenuRequested += OnPaneContextMenuRequested;
        pane.TemplateApplied += OnPaneTemplateApplied;
        pane.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        pane.TextArea.GotFocus += OnPaneGotFocus;
        pane.TextArea.LostFocus += OnPaneLostFocus;
    }

    /// <summary>Runs <paramref name="action"/> over whichever panes the template has produced.</summary>
    private void ForEachPane(Action<DiffPanePresenter> action)
    {
        if (_leftPane is not null)
        {
            action(_leftPane);
        }

        if (_rightPane is not null)
        {
            action(_rightPane);
        }
    }

    private void ApplyDisplayOptions(DiffPanePresenter pane)
    {
        pane.ShowWhitespace = ShowWhitespace;
        pane.ShowLineEndings = ShowLineEndings;
        pane.TabWidth = TabWidth;
    }

    /// <summary>
    /// The font, when this control names one: an unset size or family leaves the pane's own
    /// theme in charge, so the local value is cleared rather than overwritten with a default.
    /// </summary>
    private void ApplyPaneFont(DiffPanePresenter pane)
    {
        if (double.IsNaN(PaneFontSize))
        {
            pane.ClearValue(FontSizeProperty);
        }
        else
        {
            pane.FontSize = PaneFontSize;
        }

        if (PaneFontFamily is { } family)
        {
            pane.FontFamily = family;
        }
        else
        {
            pane.ClearValue(FontFamilyProperty);
        }
    }

    private void DetachParts()
    {
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is null)
            {
                continue;
            }

            pane.RenderFault -= OnPaneRenderFault;
            pane.TemplateApplied -= OnPaneTemplateApplied;
            pane.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            pane.TextArea.GotFocus -= OnPaneGotFocus;
            pane.TextArea.LostFocus -= OnPaneLostFocus;
        }

        if (_statusStrip is not null)
        {
            _statusStrip.DismissRequested -= OnDismissRequested;
        }

        if (_bannerAction is not null)
        {
            _bannerAction.Click -= OnBannerActionClicked;
        }

        if (_gutter is not null)
        {
            _gutter.BlockClicked -= OnGutterBlockClicked;
            _gutter.ResizeDragged -= OnGutterResizeDragged;
            _gutter.ContextRequested -= OnConnectorContextRequested;
        }

        if (_minimap is not null)
        {
            _minimap.JumpRequested -= OnMinimapJumpRequested;
            _minimap.ContextRequested -= OnMinimapContextRequested;
        }

        if (_findBar is not null)
        {
            _findBar.QueryChanged -= OnFindBarQueryChanged;
            _findBar.OptionsChanged -= OnFindBarOptionsChanged;
            _findBar.NextRequested -= OnFindBarNextRequested;
            _findBar.PreviousRequested -= OnFindBarPreviousRequested;
            _findBar.CloseRequested -= OnFindBarCloseRequested;
        }

        _sync?.Dispose();
        _sync = null;
    }

    private void OnPaneTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        TryWireScrollSync();
    }

    private void TryWireScrollSync()
    {
        if (_sync is not null || _leftPane?.PaneScrollViewer is not { } left || _rightPane?.PaneScrollViewer is not { } right)
        {
            return;
        }

        _sync = new ScrollSync(left, right) { SyncHorizontal = SyncHorizontalScroll };
        left.ScrollChanged += OnPaneScrollChanged;
        right.ScrollChanged += OnPaneScrollChanged;
        UpdateHorizontalScrollBars();
    }

    private void OnPaneScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta != default || e.ViewportDelta != default)
        {
            UpdateHorizontalScrollBars();
        }

        UpdateOverview();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateOverview();
    }

    /// <summary>Feeds the gutter and the minimap the panes' row geometry and scroll position.</summary>
    private void UpdateOverview()
    {
        if (_leftPane is null)
        {
            return;
        }

        double lineHeight = _leftPane.TextArea.TextView.DefaultLineHeight;
        if (lineHeight <= 0)
        {
            return;
        }

        if (_gutter is not null)
        {
            _gutter.RowHeight = lineHeight;
            _gutter.VerticalOffset = _leftPane.VerticalOffset;
            _gutter.ContentOffset = _leftPane.TextArea.TextView.TranslatePoint(new Point(0, 0), _gutter)?.Y ?? 0;
        }

        if (_minimap is not null)
        {
            _minimap.ViewportStartRow = _leftPane.VerticalOffset / lineHeight;
            _minimap.ViewportRowCount = _leftPane.ViewportHeight / lineHeight;
        }
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

    private void OnGutterBlockClicked(object? sender, int blockIndex)
    {
        SetCurrentChange(blockIndex, scroll: true);
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
        if (e.Handled || _gutter is null || Document is not { } model)
        {
            return;
        }

        // The gutter is not focusable, so every request that reaches it carries a pointer.
        if (!e.TryGetPosition(_gutter, out Point point) || _gutter.PolygonAt(point) is not { } polygon)
        {
            return;
        }

        ChangeBlock block = model.Blocks[polygon.BlockIndex];
        int row = Math.Clamp(_gutter.RowAt(point.Y) ?? block.FirstRow, block.FirstRow, block.LastRow);
        e.Handled = OpenMenu(_gutter, RowContext(DiffPaneRegion.ConnectorGutter, model, row, side: null, block), point);
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
        if (e.Handled || _minimap is null || Document is not { } model || model.Rows.Count == 0)
        {
            return;
        }

        if (!e.TryGetPosition(_minimap, out Point point))
        {
            return;
        }

        int row = Math.Clamp(_minimap.RowForClick(point.Y), 0, model.Rows.Count - 1);
        ChangeBlock? block = _leftPane?.Metadata.BlockAtRow(row);
        e.Handled = OpenMenu(_minimap, RowContext(DiffPaneRegion.OverviewMap, model, row, _minimap.LaneAt(point.X), block), point);
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
    private bool OpenMenu(Control owner, DiffPaneContext context, Point pointer)
    {
        LastPaneMenu = DiffPaneMenu.Request(owner, context, pointer, PaneContextMenu, MenuItemsFor, args => PaneContextMenuOpening?.Invoke(this, args));
        return LastPaneMenu is not null;
    }

    private void OnGutterResizeDragged(object? sender, double delta)
    {
        if (_leftPane is null || _rightPane is null)
        {
            return;
        }

        double panes = _leftPane.Bounds.Width + _rightPane.Bounds.Width;
        if (panes <= 0)
        {
            return;
        }

        SplitRatio = (_leftPane.Bounds.Width + delta) / panes;
    }

    /// <summary>
    /// Moves the map between the panes grid's two <c>Auto</c> slots and tells it which of its own
    /// edges now faces the panes. The empty slot takes no width, so the arrangement it is not in
    /// costs nothing.
    /// </summary>
    private void ApplyMinimapPlacement()
    {
        if (_minimap is null)
        {
            return;
        }

        bool onLeft = MinimapPlacement == MinimapPlacement.Left;
        Grid.SetColumn(_minimap, onLeft ? LeftMinimapColumn : RightMinimapColumn);
        _minimap.MirrorEdges = onLeft;

        // The header row has a slot at each end too, and the one over the map has to be exactly
        // as wide as the map is — or the header stops lining up with the pane under it, which is
        // the failure §6 has warned about since the gutter first moved.
        double reserved = ShowMinimap ? DiffMinimap.MapWidth : 0;
        if (_headerLeftSpacer is not null)
        {
            _headerLeftSpacer.Width = onLeft ? reserved : 0;
        }

        if (_headerRightSpacer is not null)
        {
            _headerRightSpacer.Width = onLeft ? 0 : reserved;
        }
    }

    private void OnMinimapJumpRequested(object? sender, int row)
    {
        ScrollToRow(row);
    }

    private void ApplySplit()
    {
        foreach (Grid? grid in new[] { _headersGrid, _panesGrid })
        {
            if (grid is null || grid.ColumnDefinitions.Count <= RightPaneColumn)
            {
                continue;
            }

            grid.ColumnDefinitions[LeftPaneColumn].Width = new GridLength(SplitRatio, GridUnitType.Star);
            grid.ColumnDefinitions[RightPaneColumn].Width = new GridLength(1 - SplitRatio, GridUnitType.Star);
        }
    }

    private void SetCurrentChange(int index, bool scroll)
    {
        int clamped = ChangeCount == 0 ? -1 : Math.Clamp(index, -1, ChangeCount - 1);
        SetAndRaise(CurrentChangeIndexProperty, ref _currentChangeIndex, clamped);
        ChangeBlock? block = clamped < 0 || Document is null ? null : Document.Blocks[clamped];
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is not null)
            {
                pane.CurrentBlock = block;
            }
        }

        if (_gutter is not null)
        {
            _gutter.CurrentChangeIndex = clamped;
        }

        if (_minimap is not null)
        {
            _minimap.CurrentChangeIndex = clamped;
        }

        if (scroll && block is not null)
        {
            ScrollToRows(block.FirstRow, block.RowCount);
        }

        UpdateStrip();
        RaiseNavigationCanExecuteChanged();
    }

    /// <summary>Scrolls both panes so the rows sit at the centre of the viewport; rows are uniform once primed.</summary>
    private void ScrollToRows(int firstRow, int rowCount)
    {
        if (_leftPane?.PaneScrollViewer is not { } viewer)
        {
            return;
        }

        double lineHeight = _leftPane.TextArea.TextView.DefaultLineHeight;
        double viewport = viewer.Viewport.Height;
        double extent = viewer.Extent.Height;
        double top = firstRow * lineHeight;
        double height = rowCount * lineHeight;
        double target = top - Math.Max(0, (viewport - height) / 2);
        target = Math.Clamp(target, 0, Math.Max(0, extent - viewport));
        viewer.Offset = new Vector(viewer.Offset.X, target);
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
        UpdateStrip();
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
                ScrollToRows(lines[match.Line].Row, 1);
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

        if (_leftPane is not null)
        {
            _leftPane.SearchMatches = left;
            _leftPane.CurrentSearchMatch = null;
        }

        if (_rightPane is not null)
        {
            _rightPane.SearchMatches = right;
            _rightPane.CurrentSearchMatch = null;
        }

        if (_minimap is not null)
        {
            _minimap.MatchRows = rows.Count == 0 ? null : rows;
        }

        UpdateFindBar();
        UpdateStrip();
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

    /// <summary>
    /// Both panes show a horizontal bar, or neither: a bar takes height from its viewport, and
    /// the panes must keep equal viewports.
    /// </summary>
    private void UpdateHorizontalScrollBars()
    {
        if (_leftPane is null || _rightPane is null)
        {
            return;
        }

        const double slack = 0.5;
        bool needed = _leftPane.ExtentWidth > _leftPane.ViewportWidth + slack
                      || _rightPane.ExtentWidth > _rightPane.ViewportWidth + slack;
        ScrollBarVisibility visibility = needed ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
        if (_leftPane.HorizontalScrollBarVisibility != visibility)
        {
            _leftPane.HorizontalScrollBarVisibility = visibility;
        }

        if (_rightPane.HorizontalScrollBarVisibility != visibility)
        {
            _rightPane.HorizontalScrollBarVisibility = visibility;
        }
    }

    private void OnPaneRenderFault(object? sender, RenderFaultEventArgs e)
    {
        // The pane has logged it under the Render category; here it becomes state.
        if (State is DiffViewState.Ready or DiffViewState.Degraded)
        {
            SetState(DiffViewState.Degraded, e.Message);
        }

        Status.SetFailure(e.Message);
        UpdateStrip();
        RenderFault?.Invoke(this, e);
    }

    private void OnPaneGotFocus(object? sender, RoutedEventArgs e)
    {
        FocusedSide = ReferenceEquals(sender, _leftPane?.TextArea) ? DiffSide.Left : DiffSide.Right;
        UpdateCaret();
    }

    private void OnPaneLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((FocusedSide == DiffSide.Left && ReferenceEquals(sender, _leftPane?.TextArea))
            || (FocusedSide == DiffSide.Right && ReferenceEquals(sender, _rightPane?.TextArea)))
        {
            FocusedSide = null;
            UpdateCaret();
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        UpdateCaret();
    }

    private void UpdateCaret()
    {
        DiffPanePresenter? pane = FocusedSide is { } side ? Pane(side) : null;
        if (pane is null)
        {
            CaretLine = 0;
            CaretColumn = 0;
        }
        else
        {
            CaretLine = pane.TextArea.Caret.Line;
            CaretColumn = pane.TextArea.Caret.Column;
        }

        // Which pane has focus is a header state as well as a caret: the accent says so where the
        // caret cannot, having been scrolled away.
        if (_leftHeader is not null)
        {
            _leftHeader.IsPaneFocused = FocusedSide == DiffSide.Left;
        }

        if (_rightHeader is not null)
        {
            _rightHeader.IsPaneFocused = FocusedSide == DiffSide.Right;
        }

        UpdateStrip();
    }
}
