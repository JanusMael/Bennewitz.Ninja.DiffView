using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    /// <summary>How long a build runs before the strip shows progress.</summary>
    public static readonly TimeSpan SlowBuildThreshold = TimeSpan.FromMilliseconds(100);

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

    /// <summary>Identifies the <see cref="LeftPaneName"/> property.</summary>
    public static readonly StyledProperty<string> LeftPaneNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(LeftPaneName), string.Empty);

    /// <summary>Identifies the <see cref="RightPaneName"/> property.</summary>
    public static readonly StyledProperty<string> RightPaneNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(RightPaneName), string.Empty);

    /// <summary>Identifies the <see cref="StatusStripName"/> property.</summary>
    public static readonly StyledProperty<string> StatusStripNameProperty =
        AvaloniaProperty.Register<SideBySideDiffView, string>(nameof(StatusStripName), string.Empty);

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
    private int _generation;
    private CancellationTokenSource? _buildCts;
    private ITimer? _slowTimer;

    private DiffPanePresenter? _leftPane;
    private DiffPanePresenter? _rightPane;
    private DiffPaneHeader? _leftHeader;
    private DiffPaneHeader? _rightHeader;
    private DiffStatusStrip? _statusStrip;
    private Button? _bannerAction;
    private ScrollSync? _sync;

    /// <summary>Creates the control with its compiled theme merged into its own resources.</summary>
    public SideBySideDiffView()
    {
        Resources.MergedDictionaries.Add(new SideBySideDiffViewTheme());
        _retry = new DelegateCommand(Retry, () => State == DiffViewState.Failed);
        _force = new DelegateCommand(ForceAlign, () => BannerKind == DiffBannerKind.TooDifferentToAlign);
        Builder = static (left, right, options, token) => DiffDocumentBuilder.Build(left, right, options, token);
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

    /// <summary>The build routine; tests replace it to make a build slow or throw.</summary>
    internal Func<PaneSource, PaneSource, DiffOptions, CancellationToken, DiffBuildResult> Builder { get; set; }

    /// <summary>The in-flight build, completing when its outcome has been applied or discarded; <c>null</c> when idle.</summary>
    internal Task? CurrentBuild { get; private set; }

    /// <summary>The word-level lookup of the current model, bound to the options its build ran under; <c>null</c> without a model.</summary>
    public WordDiffLookup? WordDiffLookup { get; private set; }

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

        TryWireScrollSync();
        UpdateHeaders();
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
        else if (change.Property == LeftReadOnlyProperty && _leftPane is not null)
        {
            _leftPane.IsReadOnly = LeftReadOnly;
        }
        else if (change.Property == RightReadOnlyProperty && _rightPane is not null)
        {
            _rightPane.IsReadOnly = RightReadOnly;
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
            if (_leftPane is not null)
            {
                _leftPane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
            }

            if (_rightPane is not null)
            {
                _rightPane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
            }
        }
        else if (change.Property == StateProperty || change.Property == BannerKindProperty)
        {
            UpdatePseudoClasses();
            _retry.RaiseCanExecuteChanged();
            _force.RaiseCanExecuteChanged();
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

        DiffPanePresenter? pane = Pane(side);
        if (pane is not null)
        {
            pane.Document = document;
        }

        // The old model described the old text; nothing of it applies to the new document.
        RequestBuild(keepModel: false);
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

        string? message = result.Warnings.Count == 0 ? null : string.Join(" ", result.Warnings.Select(w => w.Message));
        SetState(result.Warnings.Count == 0 ? DiffViewState.Ready : DiffViewState.Degraded, message);

        if (result.Warnings.Count > 0)
        {
            Status.SetWarning(message!);
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
            strip.ChangesText = ChangeCount switch
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
        pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
        pane.Logger = _renderLogger;
        // The left bar is hidden and the right one reflects both: after priming the extents are equal.
        pane.VerticalScrollBarVisibility = side == DiffSide.Left ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        pane.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        pane.RenderFault += OnPaneRenderFault;
        pane.TemplateApplied += OnPaneTemplateApplied;
        pane.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        pane.TextArea.GotFocus += OnPaneGotFocus;
        pane.TextArea.LostFocus += OnPaneLostFocus;
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

        UpdateStrip();
    }
}
