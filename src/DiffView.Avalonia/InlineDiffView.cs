using System.Diagnostics;
using System.Globalization;
using System.Text;
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
/// The unified — inline — diff: one <see cref="DiffPanePresenter"/> over a document the control
/// composes from both sides in <c>diff -u</c> order, a header per side above it, a banner for
/// what failed or was skipped, the find bar and a status strip. It is the same model, the same
/// builder, the same renderers, margins, find engine and state machine as
/// <see cref="SideBySideDiffView"/>; what differs is that there is one pane, so there is no
/// padding to render, no scrolling to couple, and the find scope collapses to the lines on
/// screen.
/// </summary>
/// <remarks>
/// The pane's document is <em>derived</em>: unlike a side's document, which is the source text
/// and is never replaced by a build, the unified text is composed from both sides and is
/// rewritten whenever the model changes. Nothing is lost by that — the view is read-only,
/// because half its lines belong to one file and half to the other, and there is no editing
/// gesture that could mean the same thing for both.
/// </remarks>
public class InlineDiffView : TemplatedControl
{
    /// <summary>The template part hosting the pane.</summary>
    public const string PanePart = "PART_Pane";

    /// <summary>The template part hosting the left side's header.</summary>
    public const string LeftHeaderPart = "PART_LeftHeader";

    /// <summary>The template part hosting the right side's header.</summary>
    public const string RightHeaderPart = "PART_RightHeader";

    /// <summary>The template part hosting the status strip.</summary>
    public const string StatusStripPart = "PART_StatusStrip";

    /// <summary>The template part hosting the banner's action button.</summary>
    public const string BannerActionPart = "PART_BannerAction";

    /// <summary>The template part hosting the find bar.</summary>
    public const string FindBarPart = "PART_FindBar";

    /// <summary>Identifies the <see cref="LeftSource"/> property.</summary>
    public static readonly StyledProperty<PaneSource?> LeftSourceProperty =
        AvaloniaProperty.Register<InlineDiffView, PaneSource?>(nameof(LeftSource));

    /// <summary>Identifies the <see cref="RightSource"/> property.</summary>
    public static readonly StyledProperty<PaneSource?> RightSourceProperty =
        AvaloniaProperty.Register<InlineDiffView, PaneSource?>(nameof(RightSource));

    /// <summary>Identifies the <see cref="IgnoreWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> IgnoreWhitespaceProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(IgnoreWhitespace));

    /// <summary>Identifies the <see cref="IgnoreCase"/> property.</summary>
    public static readonly StyledProperty<bool> IgnoreCaseProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(IgnoreCase));

    /// <summary>Identifies the <see cref="WordDiff"/> property.</summary>
    public static readonly StyledProperty<WordDiffMode> WordDiffProperty =
        AvaloniaProperty.Register<InlineDiffView, WordDiffMode>(nameof(WordDiff), WordDiffMode.Word);

    /// <summary>Identifies the <see cref="MaxWordDiffLineLength"/> property.</summary>
    public static readonly StyledProperty<int> MaxWordDiffLineLengthProperty =
        AvaloniaProperty.Register<InlineDiffView, int>(nameof(MaxWordDiffLineLength), DiffOptions.Default.MaxWordDiffLineLength);

    /// <summary>Identifies the <see cref="ForceAlignment"/> property.</summary>
    public static readonly StyledProperty<bool> ForceAlignmentProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(ForceAlignment));

    /// <summary>Identifies the <see cref="IsCaretBlinkEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsCaretBlinkEnabledProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(IsCaretBlinkEnabled), defaultValue: true);

    /// <summary>Identifies the <see cref="UseSyntaxHighlighting"/> property.</summary>
    public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(UseSyntaxHighlighting), defaultValue: true);

    /// <summary>Identifies the <see cref="ShowWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> ShowWhitespaceProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(ShowWhitespace));

    /// <summary>Identifies the <see cref="ShowLineEndings"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLineEndingsProperty =
        AvaloniaProperty.Register<InlineDiffView, bool>(nameof(ShowLineEndings));

    /// <summary>Identifies the <see cref="TabWidth"/> property. Coerced to at least 1.</summary>
    public static readonly StyledProperty<int> TabWidthProperty =
        AvaloniaProperty.Register<InlineDiffView, int>(
            nameof(TabWidth),
            defaultValue: 4,
            coerce: static (_, value) => Math.Max(1, value));

    /// <summary>Identifies the <see cref="PaneFontSize"/> property. <see cref="double.NaN"/> leaves the pane's own.</summary>
    public static readonly StyledProperty<double> PaneFontSizeProperty =
        AvaloniaProperty.Register<InlineDiffView, double>(nameof(PaneFontSize), defaultValue: double.NaN);

    /// <summary>Identifies the <see cref="PaneFontFamily"/> property. <c>null</c> leaves the pane's own.</summary>
    public static readonly StyledProperty<FontFamily?> PaneFontFamilyProperty =
        AvaloniaProperty.Register<InlineDiffView, FontFamily?>(nameof(PaneFontFamily));

    /// <summary>Identifies the <see cref="PaneName"/> property.</summary>
    public static readonly StyledProperty<string> PaneNameProperty =
        AvaloniaProperty.Register<InlineDiffView, string>(nameof(PaneName), string.Empty);

    /// <summary>Identifies the <see cref="StatusStripName"/> property.</summary>
    public static readonly StyledProperty<string> StatusStripNameProperty =
        AvaloniaProperty.Register<InlineDiffView, string>(nameof(StatusStripName), string.Empty);

    /// <summary>Identifies the <see cref="CurrentChangeIndex"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, int> CurrentChangeIndexProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, int>(nameof(CurrentChangeIndex), o => o.CurrentChangeIndex, (o, v) => o.CurrentChangeIndex = v, unsetValue: -1);

    /// <summary>Identifies the <see cref="IsFindBarOpen"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, bool> IsFindBarOpenProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, bool>(nameof(IsFindBarOpen), o => o.IsFindBarOpen, (o, v) => o.IsFindBarOpen = v);

    /// <summary>Identifies the <see cref="FindQuery"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, string> FindQueryProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, string>(nameof(FindQuery), o => o.FindQuery, (o, v) => o.FindQuery = v, unsetValue: "");

    /// <summary>Identifies the <see cref="FindOptions"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, FindOptions> FindOptionsProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, FindOptions>(nameof(FindOptions), o => o.FindOptions, (o, v) => o.FindOptions = v);

    /// <summary>Identifies the <see cref="FindResult"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, FindResult?> FindResultProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, FindResult?>(nameof(FindResult), o => o.FindResult);

    /// <summary>Identifies the <see cref="CurrentFindMatchIndex"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, int> CurrentFindMatchIndexProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, int>(nameof(CurrentFindMatchIndex), o => o.CurrentFindMatchIndex, (o, v) => o.CurrentFindMatchIndex = v, unsetValue: -1);

    /// <summary>Identifies the <see cref="State"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, DiffViewState> StateProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, DiffViewState>(nameof(State), o => o.State);

    /// <summary>Identifies the <see cref="StateMessage"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, string?> StateMessageProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, string?>(nameof(StateMessage), o => o.StateMessage);

    /// <summary>Identifies the <see cref="Document"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, SideBySideDocument?> DocumentProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, SideBySideDocument?>(nameof(Document), o => o.Document);

    /// <summary>Identifies the <see cref="Inline"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, InlineDocument?> InlineProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, InlineDocument?>(nameof(Inline), o => o.Inline);

    /// <summary>Identifies the <see cref="Diagnostics"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, DiffDiagnostics?> DiagnosticsProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, DiffDiagnostics?>(nameof(Diagnostics), o => o.Diagnostics);

    /// <summary>Identifies the <see cref="Warnings"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, IReadOnlyList<DiffWarning>> WarningsProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, IReadOnlyList<DiffWarning>>(nameof(Warnings), o => o.Warnings);

    /// <summary>Identifies the <see cref="ChangeCount"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, int> ChangeCountProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, int>(nameof(ChangeCount), o => o.ChangeCount);

    /// <summary>Identifies the <see cref="BannerKind"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, DiffBannerKind> BannerKindProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, DiffBannerKind>(nameof(BannerKind), o => o.BannerKind);

    /// <summary>Identifies the <see cref="BannerMessage"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, string?> BannerMessageProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, string?>(nameof(BannerMessage), o => o.BannerMessage);

    /// <summary>Identifies the <see cref="BannerActionText"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, string?> BannerActionTextProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, string?>(nameof(BannerActionText), o => o.BannerActionText);

    /// <summary>Identifies the <see cref="IsStale"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, bool> IsStaleProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, bool>(nameof(IsStale), o => o.IsStale);

    /// <summary>Identifies the <see cref="IsBuildingSlowly"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, bool> IsBuildingSlowlyProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, bool>(nameof(IsBuildingSlowly), o => o.IsBuildingSlowly);

    /// <summary>Identifies the <see cref="IsPaneFocused"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, bool> IsPaneFocusedProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, bool>(nameof(IsPaneFocused), o => o.IsPaneFocused);

    /// <summary>Identifies the <see cref="CaretLine"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, int> CaretLineProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, int>(nameof(CaretLine), o => o.CaretLine);

    /// <summary>Identifies the <see cref="CaretColumn"/> property.</summary>
    public static readonly DirectProperty<InlineDiffView, int> CaretColumnProperty =
        AvaloniaProperty.RegisterDirect<InlineDiffView, int>(nameof(CaretColumn), o => o.CaretColumn);

    private readonly DelegateCommand _retry;
    private readonly DelegateCommand _force;
    private readonly DelegateCommand _nextChange;
    private readonly DelegateCommand _previousChange;
    private readonly DelegateCommand _firstChange;
    private readonly DelegateCommand _lastChange;
    private readonly DelegateCommand _openFind;
    private readonly DelegateCommand _closeFind;
    private readonly DelegateCommand _findNext;
    private readonly DelegateCommand _findPrevious;
    private readonly TextDocument _paneDocument = new();
    private bool _isFindBarOpen;
    private string _findQuery = string.Empty;
    private FindOptions _findOptions = FindOptions.Default;
    private FindResult? _findResult;
    private int _currentFindMatchIndex = -1;
    private int _currentChangeIndex = -1;
    private DiffViewState _state = DiffViewState.Empty;
    private string? _stateMessage;
    private SideBySideDocument? _document;
    private InlineDocument? _inline;
    private DiffDiagnostics? _diagnostics;
    private IReadOnlyList<DiffWarning> _warnings = [];
    private int _changeCount;
    private DiffBannerKind _bannerKind;
    private string? _bannerMessage;
    private string? _bannerActionText;
    private bool _isStale;
    private bool _isBuildingSlowly;
    private bool _isPaneFocused;
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

    private int _findGeneration;
    private CancellationTokenSource? _findCts;
    private ITimer? _findTimer;
    private bool _syncingFindBar;

    private DiffPanePresenter? _pane;
    private DiffPaneHeader? _leftHeader;
    private DiffPaneHeader? _rightHeader;
    private DiffStatusStrip? _statusStrip;
    private DiffFindBar? _findBar;
    private Button? _bannerAction;

    private readonly DiffKeyBindings _bindings;

    private DiffKeyMap _keyMap = new();

    /// <summary>Creates the control with its compiled theme merged into its own resources.</summary>
    public InlineDiffView()
    {
        Resources.MergedDictionaries.Add(new InlineDiffViewTheme());
        _retry = new DelegateCommand(Retry, () => State == DiffViewState.Failed);
        _force = new DelegateCommand(ForceAlign, () => BannerKind == DiffBannerKind.TooDifferentToAlign);
        _nextChange = new DelegateCommand(NextChange, () => ChangeCount > 0);
        _previousChange = new DelegateCommand(PreviousChange, () => ChangeCount > 0);
        _firstChange = new DelegateCommand(FirstChange, () => ChangeCount > 0);
        _lastChange = new DelegateCommand(LastChange, () => ChangeCount > 0);
        _openFind = new DelegateCommand(OpenFind);
        _closeFind = new DelegateCommand(CloseFind, () => IsFindBarOpen);
        _findNext = new DelegateCommand(FindNext, () => IsFindBarOpen);
        _findPrevious = new DelegateCommand(FindPrevious, () => IsFindBarOpen);
        Builder = static (left, right, options, token) => DiffDocumentBuilder.Build(left, right, options, token);
        Searcher = static (document, left, right, query, options, token) => DiffSearch.Find(document, left, right, query, options, token);

        // The same map and the same binder as the side-by-side view, with a smaller default:
        // there is one pane to switch to and no other side to copy to. Escape and F3 execute only
        // while the find bar is open, and a binding that does not execute leaves the key unhandled.
        _bindings = new DiffKeyBindings(this, CommandOrNull);
        KeyMap = DiffKeyMap.UnifiedDefault();

        RefreshStrings();
        UpdatePseudoClasses();
        SetStateCore(DiffViewState.Empty, DiffViewStrings.Get(DiffViewStrings.StateEmptyMessage), log: false);
    }

    /// <summary>A build produced a document and the pane shows it.</summary>
    public event EventHandler<DiffBuildCompletedEventArgs>? BuildCompleted;

    /// <summary>A build failed; the control is <see cref="DiffViewState.Failed"/>.</summary>
    public event EventHandler<DiffBuildFailedEventArgs>? BuildFailed;

    /// <summary>The pane's decorator threw and disabled itself; the control is <see cref="DiffViewState.Degraded"/>.</summary>
    public event EventHandler<RenderFaultEventArgs>? RenderFault;

    /// <summary>A search finished and its matches are on screen; a bad pattern arrives here too, as <see cref="Core.FindResult.Error"/>.</summary>
    public event EventHandler<DiffFindCompletedEventArgs>? FindCompleted;

    /// <summary>The left side's input; assigning it rebuilds.</summary>
    public PaneSource? LeftSource
    {
        get => GetValue(LeftSourceProperty);
        set => SetValue(LeftSourceProperty, value);
    }

    /// <summary>The right side's input; assigning it rebuilds.</summary>
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

    /// <summary>Whether the caret blinks while the pane has focus.</summary>
    public bool IsCaretBlinkEnabled
    {
        get => GetValue(IsCaretBlinkEnabledProperty);
        set => SetValue(IsCaretBlinkEnabledProperty, value);
    }

    /// <summary>
    /// Whether the pane colours its text from a TextMate grammar. The grammar is chosen from the
    /// right side's file name, falling back to the left's: one pane shows both files, and the
    /// side that is being compared <em>to</em> is the one whose name the view carries.
    /// </summary>
    public bool UseSyntaxHighlighting
    {
        get => GetValue(UseSyntaxHighlightingProperty);
        set => SetValue(UseSyntaxHighlightingProperty, value);
    }

    /// <summary>Whether spaces and tabs are drawn as glyphs.</summary>
    public bool ShowWhitespace
    {
        get => GetValue(ShowWhitespaceProperty);
        set => SetValue(ShowWhitespaceProperty, value);
    }

    /// <summary>Whether a line terminator is drawn at the end of its line.</summary>
    public bool ShowLineEndings
    {
        get => GetValue(ShowLineEndingsProperty);
        set => SetValue(ShowLineEndingsProperty, value);
    }

    /// <summary>Columns a tab advances to, 4 by default and never below 1.</summary>
    public int TabWidth
    {
        get => GetValue(TabWidthProperty);
        set => SetValue(TabWidthProperty, value);
    }

    /// <summary>The pane's font size, or <see cref="double.NaN"/> to leave the pane theme's own.</summary>
    public double PaneFontSize
    {
        get => GetValue(PaneFontSizeProperty);
        set => SetValue(PaneFontSizeProperty, value);
    }

    /// <summary>The pane's font family, or <c>null</c> to leave the pane theme's own.</summary>
    public FontFamily? PaneFontFamily
    {
        get => GetValue(PaneFontFamilyProperty);
        set => SetValue(PaneFontFamilyProperty, value);
    }

    /// <summary>The pane's automation name.</summary>
    public string PaneName
    {
        get => GetValue(PaneNameProperty);
        set => SetValue(PaneNameProperty, value);
    }

    /// <summary>The status strip's automation name.</summary>
    public string StatusStripName
    {
        get => GetValue(StatusStripNameProperty);
        set => SetValue(StatusStripNameProperty, value);
    }

    /// <summary>The current change block, or -1 for none.</summary>
    public int CurrentChangeIndex
    {
        get => _currentChangeIndex;
        set => SetCurrentChange(value, scroll: true);
    }

    /// <summary>Whether the find bar is open.</summary>
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

    /// <summary>The query; assigning it schedules a search after the debounce.</summary>
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

    /// <summary>
    /// How the search runs. The scope is always <see cref="FindScope.Both"/> here: the pane shows
    /// lines of both sides, so a scope of one would hide matches that are on screen.
    /// </summary>
    public FindOptions FindOptions
    {
        get => _findOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            FindOptions options = value with { Scope = FindScope.Both };
            if (SetAndRaise(FindOptionsProperty, ref _findOptions, options))
            {
                UpdateFindBar();
                RequestFind();
            }
        }
    }

    /// <summary>
    /// The last search's outcome, with every match addressed by the line of the unified document
    /// it is on. A match found on a line the unified view does not show — the right line of a
    /// context row, whose text the left line already carries — is not in it.
    /// </summary>
    public FindResult? FindResult
    {
        get => _findResult;
        private set => SetAndRaise(FindResultProperty, ref _findResult, value);
    }

    /// <summary>The current match, or -1 for none.</summary>
    public int CurrentFindMatchIndex
    {
        get => _currentFindMatchIndex;
        set => SetCurrentFindMatch(value, scroll: true);
    }

    /// <summary>The control's one state.</summary>
    public DiffViewState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <summary>What the state is about, when there is something to say.</summary>
    public string? StateMessage
    {
        get => _stateMessage;
        private set => SetAndRaise(StateMessageProperty, ref _stateMessage, value);
    }

    /// <summary>The model the pane shows, or <c>null</c> before the first build lands.</summary>
    public SideBySideDocument? Document
    {
        get => _document;
        private set => SetAndRaise(DocumentProperty, ref _document, value);
    }

    /// <summary>The unified reading of <see cref="Document"/>, which is what the pane's lines are.</summary>
    public InlineDocument? Inline
    {
        get => _inline;
        private set => SetAndRaise(InlineProperty, ref _inline, value);
    }

    /// <summary>What the last build measured.</summary>
    public DiffDiagnostics? Diagnostics
    {
        get => _diagnostics;
        private set => SetAndRaise(DiagnosticsProperty, ref _diagnostics, value);
    }

    /// <summary>The last build's warnings.</summary>
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

    /// <summary>What the banner is showing.</summary>
    public DiffBannerKind BannerKind
    {
        get => _bannerKind;
        private set => SetAndRaise(BannerKindProperty, ref _bannerKind, value);
    }

    /// <summary>The banner's message.</summary>
    public string? BannerMessage
    {
        get => _bannerMessage;
        private set => SetAndRaise(BannerMessageProperty, ref _bannerMessage, value);
    }

    /// <summary>The banner's action, or <c>null</c> when it has none.</summary>
    public string? BannerActionText
    {
        get => _bannerActionText;
        private set => SetAndRaise(BannerActionTextProperty, ref _bannerActionText, value);
    }

    /// <summary>Whether what is on screen is a previous build's.</summary>
    public bool IsStale
    {
        get => _isStale;
        private set => SetAndRaise(IsStaleProperty, ref _isStale, value);
    }

    /// <summary>Whether the build has passed <see cref="SideBySideDiffView.SlowBuildThreshold"/>.</summary>
    public bool IsBuildingSlowly
    {
        get => _isBuildingSlowly;
        private set => SetAndRaise(IsBuildingSlowlyProperty, ref _isBuildingSlowly, value);
    }

    /// <summary>Whether the pane has keyboard focus.</summary>
    public bool IsPaneFocused
    {
        get => _isPaneFocused;
        private set => SetAndRaise(IsPaneFocusedProperty, ref _isPaneFocused, value);
    }

    /// <summary>
    /// The line the caret is on, as its own side numbers it — not the unified document's own
    /// count, which names no line of either file. 0 while the pane has no focus.
    /// </summary>
    public int CaretLine
    {
        get => _caretLine;
        private set => SetAndRaise(CaretLineProperty, ref _caretLine, value);
    }

    /// <summary>The caret's column. 0 while the pane has no focus.</summary>
    public int CaretColumn
    {
        get => _caretColumn;
        private set => SetAndRaise(CaretColumnProperty, ref _caretColumn, value);
    }

    /// <summary>The unified document the pane shows: composed from both sides, and rewritten by every build.</summary>
    public TextDocument PaneDocument => _paneDocument;

    /// <summary>The left side's document; it is not displayed, and it is what the search and the word diff read.</summary>
    public TextDocument LeftDocument { get; private set; } = new();

    /// <summary>The right side's document; it is not displayed, and it is what the search and the word diff read.</summary>
    public TextDocument RightDocument { get; private set; } = new();

    /// <summary>The word-level lookup of the current model, bound to the options its build ran under.</summary>
    public WordDiffLookup? WordDiffLookup { get; private set; }

    /// <summary>The loggers the control writes to, by <see cref="DiffViewLogCategories"/>.</summary>
    public ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set
        {
            _loggerFactory = value;
            _buildLogger = value?.CreateLogger(DiffViewLogCategories.Build);
            _renderLogger = value?.CreateLogger(DiffViewLogCategories.Render);
            _findLogger = value?.CreateLogger(DiffViewLogCategories.Find);
            if (_pane is not null)
            {
                _pane.Logger = _renderLogger;
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

    /// <summary>Opens the find bar; Ctrl+F by default.</summary>
    public ICommand OpenFindCommand => _openFind;

    /// <summary>Closes the find bar; Escape by default.</summary>
    public ICommand CloseFindCommand => _closeFind;

    /// <summary>Moves to the next match; F3 or Enter by default.</summary>
    public ICommand FindNextCommand => _findNext;

    /// <summary>Moves to the previous match; Shift+F3 or Shift+Enter by default.</summary>
    public ICommand FindPreviousCommand => _findPrevious;

    /// <summary>
    /// What key each command is on. Assigning a map, or changing one in place, rebuilds the
    /// bindings this control owns and leaves any a host added alone.
    /// </summary>
    /// <remarks>
    /// The same type the side-by-side view holds, defaulting to <see cref="DiffKeyMap.UnifiedDefault"/>.
    /// A gesture on a command this view has no meaning for — <see cref="DiffCommand.SwitchPane"/>
    /// and the four copies — is skipped and logged rather than bound to nothing.
    /// </remarks>
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

    /// <summary>The command object behind <paramref name="command"/>, for a host that wants to invoke it.</summary>
    /// <exception cref="NotSupportedException">
    /// <paramref name="command"/> is one the unified view does not have: there is a single pane to
    /// switch between and a single composed document to copy between.
    /// </exception>
    public ICommand CommandFor(DiffCommand command)
    {
        return CommandOrNull(command)
            ?? throw new NotSupportedException($"The unified view has no {command} command: it has one pane and one document.");
    }

    /// <summary>The command behind <paramref name="command"/>, or <c>null</c> where this view has none.</summary>
    private ICommand? CommandOrNull(DiffCommand command)
    {
        return command switch
        {
            DiffCommand.NextChange => _nextChange,
            DiffCommand.PreviousChange => _previousChange,
            DiffCommand.OpenFind => _openFind,
            DiffCommand.FindNext => _findNext,
            DiffCommand.FindPrevious => _findPrevious,
            DiffCommand.CloseFind => _closeFind,
            DiffCommand.SwitchPane => null,
            DiffCommand.CopyToLeft => null,
            DiffCommand.CopyToRight => null,
            DiffCommand.CopyBlockToLeft => null,
            DiffCommand.CopyBlockToRight => null,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
        };
    }

    private void OnKeyMapChanged(object? sender, EventArgs e) => RebuildKeyBindings();

    private void RebuildKeyBindings() => _bindings.Rebuild(_keyMap, _renderLogger);

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

    internal DiffPanePresenter? Pane => _pane;

    internal DiffPaneHeader? LeftHeader => _leftHeader;

    internal DiffPaneHeader? RightHeader => _rightHeader;

    internal DiffStatusStrip? StatusStrip => _statusStrip;

    internal DiffFindBar? FindBar => _findBar;

    internal Button? BannerAction => _bannerAction;

    /// <summary>Runs the build again with the current sources and options; the pane's faults are cleared.</summary>
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

    /// <summary>Scrolls the pane so <paramref name="line"/> of the unified document sits at the centre of the viewport.</summary>
    /// <param name="line">The 0-based unified line.</param>
    public void ScrollToLine(int line)
    {
        ScrollToLines(line, 1);
    }

    /// <summary>
    /// Opens the find bar, pre-fills the query from the pane's selection when it is a single
    /// line, and puts the caret in the query box.
    /// </summary>
    public void OpenFind()
    {
        if (!_isFindBarOpen)
        {
            SetAndRaise(IsFindBarOpenProperty, ref _isFindBarOpen, true);
            RaiseFindCanExecuteChanged();
        }

        if (SelectionOfPane() is { } selection)
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

    /// <summary>Closes the find bar, drops the highlights and returns focus to the pane.</summary>
    public void CloseFind()
    {
        if (!_isFindBarOpen)
        {
            return;
        }

        CancelFind();
        SetAndRaise(IsFindBarOpenProperty, ref _isFindBarOpen, false);
        ApplyFindResult(null);
        UpdateFindBar();
        UpdateStrip();
        _pane?.TextArea.Focus();
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

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _pane = e.NameScope.Find<DiffPanePresenter>(PanePart);
        _leftHeader = e.NameScope.Find<DiffPaneHeader>(LeftHeaderPart);
        _rightHeader = e.NameScope.Find<DiffPaneHeader>(RightHeaderPart);
        _statusStrip = e.NameScope.Find<DiffStatusStrip>(StatusStripPart);
        _bannerAction = e.NameScope.Find<Button>(BannerActionPart);
        _findBar = e.NameScope.Find<DiffFindBar>(FindBarPart);

        AttachPane(_pane);
        if (_statusStrip is not null)
        {
            _statusStrip.DismissRequested += OnDismissRequested;
        }

        if (_bannerAction is not null)
        {
            _bannerAction.Click += OnBannerActionClicked;
        }

        if (_findBar is not null)
        {
            // One pane: a scope of one side would hide matches that are on screen.
            _findBar.ShowScope = false;
            _findBar.QueryChanged += OnFindBarQueryChanged;
            _findBar.OptionsChanged += OnFindBarOptionsChanged;
            _findBar.NextRequested += OnFindBarNextRequested;
            _findBar.PreviousRequested += OnFindBarPreviousRequested;
            _findBar.CloseRequested += OnFindBarCloseRequested;
        }

        UpdateHeaders();
        UpdateFindBar();
        UpdateStrip();
        UpdateBanner();
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
        else if (change.Property == IgnoreWhitespaceProperty
                 || change.Property == IgnoreCaseProperty
                 || change.Property == WordDiffProperty
                 || change.Property == MaxWordDiffLineLengthProperty
                 || change.Property == ForceAlignmentProperty)
        {
            RequestBuild(keepModel: true);
        }
        else if (change.Property == IsCaretBlinkEnabledProperty && _pane is not null)
        {
            _pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
        }
        else if (change.Property == UseSyntaxHighlightingProperty && _pane is not null)
        {
            _pane.UseSyntaxHighlighting = UseSyntaxHighlighting;
        }
        else if (change.Property == ShowWhitespaceProperty
                 || change.Property == ShowLineEndingsProperty
                 || change.Property == TabWidthProperty)
        {
            if (_pane is not null)
            {
                ApplyDisplayOptions(_pane);
            }
        }
        else if (change.Property == PaneFontSizeProperty || change.Property == PaneFontFamilyProperty)
        {
            if (_pane is not null)
            {
                ApplyPaneFont(_pane);
            }
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
        if (side == DiffSide.Left)
        {
            _leftInfo = info;
            LeftDocument = document;
        }
        else
        {
            _rightInfo = info;
            RightDocument = document;
        }

        // The grammar follows the file, not the build.
        if (_pane is not null)
        {
            _pane.SyntaxFileName = SyntaxFileName();
        }

        RequestBuild(keepModel: false);
    }

    /// <summary>What the grammar is chosen from: the right side's file, falling back to the left's.</summary>
    private string? SyntaxFileName()
    {
        return RightSource?.Path ?? RightSource?.Title ?? LeftSource?.Path ?? LeftSource?.Title;
    }

    private void RequestBuild(bool keepModel)
    {
        PaneSource? left = LeftSource;
        PaneSource? right = RightSource;
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

        _pane?.ResetFaults();
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
    /// The fault the pane is still carrying: one its decorators raised since they were last
    /// re-enabled, or the one that turned its syntax highlighting off, which outlives a rebuild
    /// because a rebuild is not what would fix it.
    /// </summary>
    private RenderFaultEventArgs? PaneFault()
    {
        return _pane?.Faults.FirstOrDefault() ?? _pane?.SyntaxFault;
    }

    private void ApplyModel(SideBySideDocument? document, IReadOnlyList<DiffWarning> warnings, DiffDiagnostics? diagnostics, DiffOptions? options)
    {
        Document = document;
        Inline = document is null ? null : InlineDocument.Build(document);
        Warnings = warnings;
        Diagnostics = diagnostics;
        ChangeCount = document?.Blocks.Count ?? 0;
        // One lookup per result, bound to the options the build ran under, over the two source
        // documents — which the unified document is composed from, line for line.
        WordDiffLookup = document is null || options is null
            ? null
            : new WordDiffLookup(new WordDiffCache(options), LeftDocument, RightDocument, document);

        ComposeUnifiedText();
        if (_pane is not null)
        {
            _pane.InlineDocument = Inline;
            _pane.WordDiffLookup = WordDiffLookup;
        }

        // The matches were found over lines the old model laid out; the search runs again against
        // the new one while the bar is open.
        ApplyFindResult(null);
        RequestFind();

        // The blocks are new: no current change until the user picks one.
        SetCurrentChange(-1, scroll: false);
    }

    /// <summary>
    /// Rewrites the pane's document from the unified table: each line's text comes from the side
    /// it names, joined with a single newline. The document instance is kept, so the caret and
    /// the scroll offset survive a rebuild that leaves the text where it was; the undo stack is
    /// dropped, because a composed document has no edit history worth keeping.
    /// </summary>
    private void ComposeUnifiedText()
    {
        StringBuilder text = new();
        foreach (InlineLine line in Inline?.Lines ?? [])
        {
            TextDocument source = line.Side == DiffSide.Left ? LeftDocument : RightDocument;
            if (line.SourceLine >= 0 && line.SourceLine < source.LineCount)
            {
                DocumentLine sourceLine = source.GetLineByNumber(line.SourceLine + 1);
                text.Append(source.GetText(sourceLine.Offset, sourceLine.Length));
            }

            text.Append('\n');
        }

        if (text.Length > 0)
        {
            // No terminator after the last line: one would add an empty line the model has not
            // got, and the editor's line count must equal the unified table's.
            text.Length--;
        }

        _paneDocument.Text = text.ToString();
        _paneDocument.UndoStack.ClearAll();
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
            SideBySideDiffView.SlowBuildThreshold,
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
        SetCurrentValue(PaneNameProperty, DiffViewStrings.Get(DiffViewStrings.UnifiedPaneName));
        SetCurrentValue(StatusStripNameProperty, DiffViewStrings.Get(DiffViewStrings.StatusStripName));
    }

    private void UpdateHeaders()
    {
        UpdateHeader(_leftHeader, DiffSide.Left, LeftSource, _leftInfo);
        UpdateHeader(_rightHeader, DiffSide.Right, RightSource, _rightInfo);
    }

    /// <summary>
    /// A header per side, above the one pane: which file each side is, and what probing it found.
    /// Neither carries the focus accent — there is one pane, so which one has focus says nothing.
    /// </summary>
    private void UpdateHeader(DiffPaneHeader? header, DiffSide side, PaneSource? source, TextInfo? info)
    {
        if (header is null)
        {
            return;
        }

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
        strip.CaretText = IsPaneFocused ? DiffViewStrings.Format(DiffViewStrings.StatusCaret, CaretLine, CaretColumn) : null;

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

    // ── The pane ───────────────────────────────────────────────────────────────────────────

    private void AttachPane(DiffPanePresenter? pane)
    {
        if (pane is null)
        {
            return;
        }

        // Unified first: it is what the pane's metadata, its log lines and its gutters follow.
        pane.IsUnified = true;
        pane.Document = _paneDocument;
        pane.InlineDocument = Inline;
        pane.WordDiffLookup = WordDiffLookup;
        // Half the lines are one file's and half the other's: there is no edit that could mean
        // the same thing for both, so the unified view is read-only, full stop.
        pane.IsReadOnly = true;
        pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
        // The logger first: assigning the file name may install a grammar, which logs.
        pane.Logger = _renderLogger;
        pane.UseSyntaxHighlighting = UseSyntaxHighlighting;
        pane.SyntaxFileName = SyntaxFileName();
        ApplyDisplayOptions(pane);
        ApplyPaneFont(pane);
        pane.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        pane.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        pane.RenderFault += OnPaneRenderFault;
        pane.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        pane.TextArea.GotFocus += OnPaneGotFocus;
        pane.TextArea.LostFocus += OnPaneLostFocus;
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
        if (_pane is not null)
        {
            _pane.RenderFault -= OnPaneRenderFault;
            _pane.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            _pane.TextArea.GotFocus -= OnPaneGotFocus;
            _pane.TextArea.LostFocus -= OnPaneLostFocus;
        }

        if (_statusStrip is not null)
        {
            _statusStrip.DismissRequested -= OnDismissRequested;
        }

        if (_bannerAction is not null)
        {
            _bannerAction.Click -= OnBannerActionClicked;
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

    private void SetCurrentChange(int index, bool scroll)
    {
        int clamped = ChangeCount == 0 ? -1 : Math.Clamp(index, -1, ChangeCount - 1);
        SetAndRaise(CurrentChangeIndexProperty, ref _currentChangeIndex, clamped);
        ChangeBlock? block = clamped < 0 || Document is null ? null : Document.Blocks[clamped];
        if (_pane is not null)
        {
            _pane.CurrentBlock = block;
        }

        if (scroll && block is not null && Inline is { } inline)
        {
            LineRange lines = inline.LinesOfBlock(block.Index);
            ScrollToLines(lines.Start, lines.Count);
        }

        UpdateStrip();
        RaiseNavigationCanExecuteChanged();
    }

    /// <summary>Scrolls the pane so the lines sit at the centre of the viewport; rows are uniform, there being no padding.</summary>
    private void ScrollToLines(int firstLine, int count)
    {
        if (_pane?.PaneScrollViewer is not { } viewer)
        {
            return;
        }

        double lineHeight = _pane.TextArea.TextView.DefaultLineHeight;
        double viewport = viewer.Viewport.Height;
        double extent = viewer.Extent.Height;
        double top = firstLine * lineHeight;
        double height = count * lineHeight;
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
    }

    // ── Find ───────────────────────────────────────────────────────────────────────────────

    /// <summary>The pane's selection when it is one line of text; <c>null</c> otherwise.</summary>
    private string? SelectionOfPane()
    {
        if (_pane is null)
        {
            return null;
        }

        string text = _pane.SelectedText;
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
        if (_pane is not null)
        {
            _pane.CurrentSearchMatch = current;
        }

        if (scroll && current is { } chosen)
        {
            RevealMatch(chosen);
        }

        UpdateFindBar();
        UpdateStrip();
    }

    /// <summary>Selects the match in the pane, focuses it, and centres its line.</summary>
    private void RevealMatch(FindMatch match)
    {
        if (_pane is null)
        {
            return;
        }

        if (match.Line >= 0 && match.Line < _paneDocument.LineCount)
        {
            DocumentLine line = _paneDocument.GetLineByNumber(match.Line + 1);
            int start = Math.Min(line.Offset + match.Column, line.EndOffset);
            _pane.Select(start, Math.Min(match.Length, line.EndOffset - start));
        }

        _pane.TextArea.Focus();

        // Last, so the centring wins over any scroll the caret brought about.
        ScrollToLines(match.Line, 1);
    }

    /// <summary>Schedules a search: the one in flight is cancelled and the query rests for the debounce.</summary>
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
            SideBySideDiffView.FindDebounce,
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
        // the line table, never a TextDocument. The two sides are searched, not the composed
        // document, so the engine is the one the side-by-side view uses, unchanged.
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

        FindCompleted?.Invoke(this, new DiffFindCompletedEventArgs(FindResult ?? found));
    }

    /// <summary>Puts a result — or no result — on the pane, the bar and the strip.</summary>
    private void ApplyFindResult(FindResult? result)
    {
        FindResult = ToUnified(result);
        // A fresh result has no current match: typing must not pull focus out of the query box.
        SetAndRaise(CurrentFindMatchIndexProperty, ref _currentFindMatchIndex, -1);

        if (_pane is not null)
        {
            _pane.SearchMatches = FindResult?.Matches ?? [];
            _pane.CurrentSearchMatch = null;
        }

        UpdateFindBar();
        UpdateStrip();
        RaiseFindCanExecuteChanged();
    }

    /// <summary>
    /// The search's matches, addressed by unified line and in the order they appear on screen. A
    /// match on a line the unified view does not show is dropped: the right line of a context
    /// row is not on screen, its left twin carrying the row's text.
    /// </summary>
    private FindResult? ToUnified(FindResult? result)
    {
        if (result is null || Inline is not { } inline)
        {
            return result;
        }

        List<FindMatch> matches = [];
        int left = 0;
        int right = 0;
        foreach (FindMatch match in result.Matches)
        {
            if (inline.LineOf(match.Side, match.Line) is not { } line)
            {
                continue;
            }

            matches.Add(match with { Line = line });
            if (match.Side == DiffSide.Left)
            {
                left++;
            }
            else
            {
                right++;
            }
        }

        // The engine walks the model's rows, left before right within a row; the unified view
        // prints a block's left lines before its right ones, so the matches are re-ordered to the
        // order they are read in — which is what the renderer's per-line lookup expects.
        matches.Sort(static (a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : a.Column.CompareTo(b.Column));
        return new FindResult(matches, left, right, result.Truncated, result.Error);
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
            bar.Scope = FindScope.Both;
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

    /// <summary>The strip's find lane: the count, only while the bar is open. There is no scope to name.</summary>
    private string? FindStripText()
    {
        if (!IsFindBarOpen)
        {
            return null;
        }

        if (FindResult is not { Error: null } result || string.IsNullOrEmpty(FindQuery))
        {
            return null;
        }

        string total = result.Matches.Count.ToString("N0", CultureInfo.CurrentCulture);
        string count = result.Matches.Count == 0
            ? DiffViewStrings.Get(DiffViewStrings.FindNoMatches)
            : CurrentFindMatchIndex >= 0
                ? DiffViewStrings.Format(DiffViewStrings.StatusFindMatchOf, (CurrentFindMatchIndex + 1).ToString("N0", CultureInfo.CurrentCulture), total)
                : DiffViewStrings.Format(DiffViewStrings.StatusFindMatches, total);
        return DiffViewStrings.Format(DiffViewStrings.StatusFindNoScope, count);
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
        IsPaneFocused = true;
        UpdateCaret();
    }

    private void OnPaneLostFocus(object? sender, RoutedEventArgs e)
    {
        IsPaneFocused = false;
        UpdateCaret();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        UpdateCaret();
    }

    /// <summary>
    /// The caret, reported as its own side numbers it: the unified document's own line count
    /// belongs to neither file, so the line the strip shows is the source line the caret is on.
    /// </summary>
    private void UpdateCaret()
    {
        if (_pane is null || !IsPaneFocused)
        {
            CaretLine = 0;
            CaretColumn = 0;
        }
        else
        {
            int line = _pane.TextArea.Caret.Line;
            CaretLine = Inline is { } inline && line >= 1 && line <= inline.Lines.Count
                ? inline.Lines[line - 1].SourceLine + 1
                : line;
            CaretColumn = _pane.TextArea.Caret.Column;
        }

        UpdateStrip();
    }
}
