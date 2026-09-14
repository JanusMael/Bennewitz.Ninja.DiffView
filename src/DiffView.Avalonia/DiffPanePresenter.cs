using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// One pane of the side-by-side view: an AvaloniaEdit editor over the pane's <em>source</em>
/// document — never a padded copy — that renders the padding a <see cref="SideBySideDocument"/>
/// implies as empty space, fills rows by kind, draws its own text-height selection and caret,
/// and carries line-number and change-marker gutters. Assigning <see cref="DiffDocument"/> swaps
/// the per-line metadata, re-primes the padded heights and redraws; it never replaces the
/// <see cref="TextEditor.Document"/>. Read-only by default; <see cref="TextEditor.IsReadOnly"/>
/// is honoured, not assumed. Every decorator is a fault boundary: a throw is reported once
/// through <see cref="RenderFault"/>, the decorator disables itself, and the text stays visible.
/// </summary>
public class DiffPanePresenter : TextEditor
{
    /// <summary>The key of the control theme the presenter applies to its text area.</summary>
    public const string TextAreaThemeKey = "DiffView.TextAreaTheme";

    /// <summary>Identifies the <see cref="Side"/> property.</summary>
    public static readonly StyledProperty<DiffSide> SideProperty =
        AvaloniaProperty.Register<DiffPanePresenter, DiffSide>(nameof(Side));

    /// <summary>Identifies the <see cref="DiffDocument"/> property.</summary>
    public static readonly StyledProperty<SideBySideDocument?> DiffDocumentProperty =
        AvaloniaProperty.Register<DiffPanePresenter, SideBySideDocument?>(nameof(DiffDocument));

    /// <summary>Identifies the <see cref="InlineDocument"/> property.</summary>
    public static readonly StyledProperty<InlineDocument?> InlineDocumentProperty =
        AvaloniaProperty.Register<DiffPanePresenter, InlineDocument?>(nameof(InlineDocument));

    /// <summary>Identifies the <see cref="IsUnified"/> property.</summary>
    public static readonly StyledProperty<bool> IsUnifiedProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(IsUnified));

    /// <summary>Identifies the <see cref="CanCopyOut"/> property.</summary>
    public static readonly StyledProperty<bool> CanCopyOutProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(CanCopyOut));

    /// <summary>Identifies the <see cref="IsCaretBlinkEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsCaretBlinkEnabledProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(IsCaretBlinkEnabled), defaultValue: true);

    /// <summary>Identifies the <see cref="UseSyntaxHighlighting"/> property.</summary>
    public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(UseSyntaxHighlighting), defaultValue: true);

    /// <summary>Identifies the <see cref="SyntaxFileName"/> property.</summary>
    public static readonly StyledProperty<string?> SyntaxFileNameProperty =
        AvaloniaProperty.Register<DiffPanePresenter, string?>(nameof(SyntaxFileName));

    /// <summary>Identifies the <see cref="ShowWhitespace"/> property.</summary>
    public static readonly StyledProperty<bool> ShowWhitespaceProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(ShowWhitespace));

    /// <summary>Identifies the <see cref="ShowLineEndings"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLineEndingsProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(ShowLineEndings));

    /// <summary>Identifies the <see cref="TabWidth"/> property. Coerced to at least 1.</summary>
    public static readonly StyledProperty<int> TabWidthProperty =
        AvaloniaProperty.Register<DiffPanePresenter, int>(
            nameof(TabWidth),
            defaultValue: 4,
            coerce: static (_, value) => Math.Max(1, value));

    private readonly PaddingElementGenerator _generator;
    private readonly FoldPlaceholderGenerator _foldGenerator;
    private readonly PaddingHeightPrimer _primer = new();
    private readonly DiffLineBackgroundRenderer _backgroundRenderer;
    private readonly SearchMatchRenderer _searchRenderer;
    private readonly DiffSelectionRenderer _selectionRenderer;
    private readonly DiffCaretRenderer _caretRenderer;
    private readonly DiffLineNumberMargin _lineNumberMargin;
    private readonly ChangeMarkerMargin _changeMarkerMargin;
    private IReadOnlySet<int> _modifiedLines = new HashSet<int>();
    private readonly List<CollapsedLineSection> _collapsed = [];
    private readonly List<RenderFaultEventArgs> _faults = [];
    private SyntaxHighlighting? _syntax;
    private bool _syntaxDisabled;
    private WordDiffLookup? _wordDiffLookup;
    private ChangeBlock? _currentBlock;
    private IReadOnlyList<FindMatch> _searchMatches = [];
    private FindMatch? _currentSearchMatch;
    private bool _primePending;
    private bool _caretNormalisationDisabled;

    /// <summary>Creates a presenter with an empty document and no model.</summary>
    public DiffPanePresenter()
    {
        // The control themes travel with the control, so the structure never depends on a host
        // include; the DiffView.* tokens they bind to come from the host's theme include, with
        // hard fallbacks in the palette. The compiled dictionary class keeps the merge trim-safe:
        // a runtime ResourceInclude loads by reflection and the trimmer refuses it (IL2026).
        Resources.MergedDictionaries.Add(new DiffPanePresenterTheme());
        if (!this.TryFindResource(TextAreaThemeKey, out object? theme) || theme is not ControlTheme textAreaTheme)
        {
            throw new InvalidOperationException($"{nameof(DiffPanePresenterTheme)} does not define {TextAreaThemeKey}.");
        }

        TextArea.Theme = textAreaTheme;

        // TextEditor applies IsReadOnly to the text area only when the property changes, so the
        // default is a local value set here rather than an overridden default value.
        IsReadOnly = true;

        // A fresh options object: the styled property's default instance is shared. Scrolling
        // below the document would break the equal-extent invariant; hyperlinks would recolour
        // and restructure runs.
        Options = new TextEditorOptions
        {
            AllowScrollBelowDocument = false,
            EnableVirtualSpace = false,
            EnableHyperlinks = false,
            EnableEmailHyperlinks = false,
            EnableTextDragDrop = false,
            HighlightCurrentLine = false,
        };

        // AvaloniaEdit's selection and caret layers span padded lines; ours draw text bands only.
        TextArea.SelectionBrush = Brushes.Transparent;
        TextArea.SelectionBorder = null;
        TextArea.Caret.CaretBrush = Brushes.Transparent;

        Palette = new DiffBrushes();
        _generator = new PaddingElementGenerator(PaddingForLine, (line, ex) => ReportFault(nameof(PaddingElementGenerator), line, ex));
        TextArea.TextView.ElementGenerators.Add(_generator);
        _foldGenerator = new FoldPlaceholderGenerator(
            (line, ex) => ReportFault(nameof(FoldPlaceholderGenerator), line, ex),
            line => FoldExpandRequested?.Invoke(this, line));
        TextArea.TextView.ElementGenerators.Add(_foldGenerator);

        _backgroundRenderer = new DiffLineBackgroundRenderer(this);
        _searchRenderer = new SearchMatchRenderer(this);
        _selectionRenderer = new DiffSelectionRenderer(this);
        _caretRenderer = new DiffCaretRenderer(this);
        TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);
        // The search and selection renderers share KnownLayer.Selection and are drawn in list
        // order: matches first, so a selected match still reads as selected.
        TextArea.TextView.BackgroundRenderers.Add(_searchRenderer);
        TextArea.TextView.BackgroundRenderers.Add(_selectionRenderer);
        TextArea.TextView.BackgroundRenderers.Add(_caretRenderer);

        _lineNumberMargin = new DiffLineNumberMargin(this);
        _changeMarkerMargin = new ChangeMarkerMargin(this);
        TextArea.LeftMargins.Add(_lineNumberMargin);
        TextArea.LeftMargins.Add(_changeMarkerMargin);

        TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        TextArea.GotFocus += OnTextAreaFocusChanged;
        TextArea.LostFocus += OnTextAreaFocusChanged;
        // The selection arrow appears and vanishes with the selection rather than at the next
        // unrelated redraw, which would look like a race and would not be one.
        TextArea.SelectionChanged += OnSelectionChanged;
        DocumentChanged += OnDocumentSwapped;
        LayoutUpdated += OnLayoutUpdated;
        // Both the right-click and Shift+F10 arrive here, which is why the menu hangs off this
        // rather than off a ContextMenu assigned in a template: the keyboard's request carries no
        // position, and a templated menu would answer it with one built for the wrong place.
        ContextRequested += OnContextRequested;
    }

    /// <summary>A decorator threw and disabled itself; the text is still rendered.</summary>
    public event EventHandler<RenderFaultEventArgs>? RenderFault;

    /// <summary>Which side of the model this pane presents.</summary>
    public DiffSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>
    /// The model this pane takes its per-line kinds and padding from. Assigning a new model swaps
    /// the metadata, re-primes and redraws; the <see cref="TextEditor.Document"/> is untouched.
    /// </summary>
    public SideBySideDocument? DiffDocument
    {
        get => GetValue(DiffDocumentProperty);
        set => SetValue(DiffDocumentProperty, value);
    }

    /// <summary>
    /// The unified line table this pane presents, when <see cref="IsUnified"/>: the document is
    /// the unified text and this says what each of its lines is. Assigning one has the same
    /// effect as assigning <see cref="DiffDocument"/> — the metadata is swapped, the decorators
    /// are re-enabled and the pane redraws.
    /// </summary>
    public InlineDocument? InlineDocument
    {
        get => GetValue(InlineDocumentProperty);
        set => SetValue(InlineDocumentProperty, value);
    }

    /// <summary>
    /// Whether this pane shows the unified reading of both sides rather than one side of the
    /// model. Set by <see cref="InlineDiffView"/> and left alone by a side-by-side host, where
    /// <see cref="Side"/> is what the pane presents. A unified pane takes its metadata from
    /// <see cref="InlineDocument"/>, has no padding to prime, and names itself "unified" in the
    /// log, where a side would be a lie.
    /// </summary>
    public bool IsUnified
    {
        get => GetValue(IsUnifiedProperty);
        set => SetValue(IsUnifiedProperty, value);
    }

    /// <summary>
    /// Whether this pane's change blocks can be copied to the other side — which is to say
    /// whether the <em>other</em> side is editable, not this one. Set, the line-number margin
    /// offers a copy arrow on each block's anchor row, pointing the way the text would travel.
    /// </summary>
    public bool CanCopyOut
    {
        get => GetValue(CanCopyOutProperty);
        set => SetValue(CanCopyOutProperty, value);
    }

    /// <summary>
    /// A copy arrow in this pane's gutter was clicked: the block to copy out of it. The composite
    /// performs the copy, because a pane knows nothing about the other side.
    /// </summary>
    public event EventHandler<int>? CopyOutRequested;

    /// <summary>Raises <see cref="CopyOutRequested"/> for <paramref name="blockIndex"/>.</summary>
    internal void RequestCopyOut(int blockIndex) => CopyOutRequested?.Invoke(this, blockIndex);

    /// <summary>
    /// The selection arrow in this pane's gutter was clicked: the selection is to be copied out
    /// of it. No payload, because the selection is the pane's own and the composite reads it back
    /// through <see cref="SelectedLines"/> — passing a range would let the two disagree.
    /// </summary>
    public event EventHandler? CopySelectionRequested;

    /// <summary>Raises <see cref="CopySelectionRequested"/>.</summary>
    internal void RequestCopySelection() => CopySelectionRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A context menu was asked for over this pane, by pointer or by keyboard, with what was under
    /// it. The composite answers, because the items name verbs a pane knows nothing about — the
    /// other side, the find bar, the save.
    /// </summary>
    internal event EventHandler<PaneContextRequest>? ContextMenuRequested;

    /// <summary>
    /// What a context-menu request carries: the context, where to open — <c>null</c> from the
    /// keyboard, which the menu answers at the caret — and whether anything was opened.
    /// </summary>
    internal sealed class PaneContextRequest(DiffPaneContext context, Point? pointer)
    {
        public DiffPaneContext Context { get; } = context;

        public Point? Pointer { get; } = pointer;

        /// <summary>
        /// Set when a menu actually opened. The pane marks the routed event handled only then, so
        /// a request it answers with nothing still reaches a <c>ContextMenu</c> a host put on an
        /// ancestor — the view itself, say — rather than being eaten in silence.
        /// </summary>
        public bool Opened { get; set; }
    }

    /// <summary>
    /// Describes the line at <paramref name="lineNumber"/> and this pane's selection, for a host
    /// that wants to know what was clicked. Public because <c>PaneMetadata</c> is not: without it
    /// a consumer cannot write a menu of their own at all.
    /// </summary>
    public DiffPaneContext ContextAt(int lineNumber, DiffPaneRegion region = DiffPaneRegion.Text)
    {
        bool known = Metadata.Knows(lineNumber);
        InlineLine? unified = IsUnified ? Metadata.UnifiedLineAt(lineNumber) : null;

        // The unified view's line belongs to one of the two files and the pane belongs to
        // neither; the side-by-side view's line belongs to the pane's own side.
        DiffSide? sourceSide = IsUnified ? unified?.Side : Side;
        int? sourceLine = IsUnified ? unified?.SourceLine + 1 : known ? lineNumber : null;

        return new DiffPaneContext(
            region,
            IsUnified ? null : Side,
            lineNumber,
            sourceSide,
            sourceLine,
            known ? Metadata.RowOf(lineNumber) : null,
            known ? Metadata.BlockAt(lineNumber) : null,
            known ? Metadata.KindOf(lineNumber) : DiffLineKind.Unchanged,
            SelectedLines,
            IsUnified,
            IsReadOnly);
    }

    /// <summary>
    /// A context menu was asked for. Hooked rather than overridden: <c>Control</c> exposes
    /// <c>ContextRequested</c> as an event with no virtual to override.
    /// </summary>
    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled || Document is null)
        {
            return;
        }

        // Which surface was clicked. The event's source is the margin itself, so this is a type
        // test rather than pointer arithmetic against the margins' widths: the margins own those
        // widths, and a constant here would be the misaligned-header bug of plan 00008 waiting to
        // happen a second time. A margin the library did not draw — AvaloniaEdit's own, or a
        // host's — is left alone rather than answered with the text's menu, because its verbs are
        // not ours to guess.
        // Matched by identity against this pane's own two margins rather than by type: it says
        // "our line-number gutter" instead of "any margin of that class", which is the question
        // actually being asked, and it does not depend on the pane hosting exactly one of each.
        DiffPaneRegion? resolved = e.Source switch
        {
            _ when ReferenceEquals(e.Source, _lineNumberMargin) => DiffPaneRegion.LineNumberMargin,
            _ when ReferenceEquals(e.Source, _changeMarkerMargin) => DiffPaneRegion.ChangeMarkerMargin,
            AbstractMargin => null,
            _ => DiffPaneRegion.Text,
        };
        if (resolved is not { } region)
        {
            return;
        }

        Point? pointer = e.TryGetPosition(this, out Point point) ? point : null;

        // A margin is as tall as the text beside it and only its width differs, so the line under
        // the pointer is the same question in all three regions: LineAt reads the y and ignores
        // the x entirely.
        int line = LineAt(pointer) ?? TextArea.Caret.Line;
        if (line < 1)
        {
            return;
        }

        // The caret is deliberately not moved. A right-click that moved it would discard the
        // selection the menu is about to offer to copy, which is the whole point of the menu.
        PaneContextRequest request = new(ContextAt(line, region), pointer);
        ContextMenuRequested?.Invoke(this, request);
        e.Handled = request.Opened;
    }

    /// <summary>The line under <paramref name="pointer"/>, or <c>null</c> for none and for no pointer.</summary>
    private int? LineAt(Point? pointer)
    {
        if (pointer is not { } point)
        {
            return null;
        }

        TextView view = TextArea.TextView;
        Point inView = this.TranslatePoint(point, view) ?? point;
        return view.GetDocumentLineByVisualTop(inView.Y + view.VerticalOffset)?.LineNumber;
    }

    /// <summary>
    /// The whole lines this pane's selection covers, as the model counts them — 0-based, and
    /// <c>null</c> when there is no selection. A selection that starts or ends mid-line takes the
    /// whole of both lines, because every copy in the library is line-based; one whose end sits on
    /// the very start of a line stops at the line above, which is what a drag onto the next line's
    /// first column means in every editor.
    /// </summary>
    public LineRange? SelectedLines
    {
        get
        {
            Selection selection = TextArea.Selection;
            if (selection.IsEmpty || Document is not { } document || selection.SurroundingSegment is not { } segment)
            {
                return null;
            }

            DocumentLine first = document.GetLineByOffset(segment.Offset);
            DocumentLine last = document.GetLineByOffset(segment.EndOffset);
            if (last.LineNumber > first.LineNumber && segment.EndOffset == last.Offset)
            {
                last = last.PreviousLine!;
            }

            return new LineRange(first.LineNumber - 1, last.LineNumber - first.LineNumber + 1);
        }
    }

    /// <summary>Whether the caret blinks while the pane has focus. Off, it stays visible.</summary>
    public bool IsCaretBlinkEnabled
    {
        get => GetValue(IsCaretBlinkEnabledProperty);
        set => SetValue(IsCaretBlinkEnabledProperty, value);
    }

    /// <summary>
    /// Whether the pane colours its text from a TextMate grammar chosen by
    /// <see cref="SyntaxFileName"/>. On by default; off leaves plain text and removes an
    /// installed grammar. The diff highlighting is a separate layer either way.
    /// </summary>
    public bool UseSyntaxHighlighting
    {
        get => GetValue(UseSyntaxHighlightingProperty);
        set => SetValue(UseSyntaxHighlightingProperty, value);
    }

    /// <summary>
    /// The file name — or path — the grammar is chosen from, by extension. <c>null</c>, an
    /// extensionless name or an extension no grammar claims leaves the pane plain text, which is
    /// a result and not a failure. The composite assigns the side's <see cref="PaneSource.Path"/>,
    /// falling back to its <see cref="PaneSource.Title"/>.
    /// </summary>
    public string? SyntaxFileName
    {
        get => GetValue(SyntaxFileNameProperty);
        set => SetValue(SyntaxFileNameProperty, value);
    }

    /// <summary>
    /// The language currently colouring the pane — <c>csharp</c>, <c>json</c> — or <c>null</c>
    /// while it is plain text.
    /// </summary>
    public string? SyntaxLanguageId => _syntax?.Installed?.LanguageId;

    /// <summary>Whether spaces and tabs are drawn as glyphs. Off by default.</summary>
    public bool ShowWhitespace
    {
        get => GetValue(ShowWhitespaceProperty);
        set => SetValue(ShowWhitespaceProperty, value);
    }

    /// <summary>Whether a line terminator is drawn at the end of its line. Off by default.</summary>
    public bool ShowLineEndings
    {
        get => GetValue(ShowLineEndingsProperty);
        set => SetValue(ShowLineEndingsProperty, value);
    }

    /// <summary>
    /// Columns a tab advances to, 4 by default and never below 1. A width change moves text
    /// sideways only: rows keep their heights, so the panes stay aligned.
    /// </summary>
    public int TabWidth
    {
        get => GetValue(TabWidthProperty);
        set => SetValue(TabWidthProperty, value);
    }

    /// <summary>Receives faults at <c>Error</c> with the exception attached; never document text.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>The current change block, which the background renderer outlines; <c>null</c> for none.</summary>
    public ChangeBlock? CurrentBlock
    {
        get => _currentBlock;
        set
        {
            if (Equals(_currentBlock, value))
            {
                return;
            }

            _currentBlock = value;
            TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
    }

    /// <summary>
    /// The word-level pieces the background renderer draws over modified rows, or <c>null</c>
    /// for none. The composite assigns one per build; a host of two bare presenters builds one
    /// over their documents itself.
    /// </summary>
    public WordDiffLookup? WordDiffLookup
    {
        get => _wordDiffLookup;
        set
        {
            if (ReferenceEquals(_wordDiffLookup, value))
            {
                return;
            }

            _wordDiffLookup = value;
            TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
    }

    /// <summary>
    /// This pane's find matches, ordered by line then column, which the search renderer
    /// highlights. The composite assigns the side's share of a <see cref="FindResult"/>; a host
    /// of two bare presenters runs <see cref="DiffSearch"/> itself.
    /// </summary>
    public IReadOnlyList<FindMatch> SearchMatches
    {
        get => _searchMatches;
        set
        {
            IReadOnlyList<FindMatch> matches = value ?? [];
            if (ReferenceEquals(_searchMatches, matches))
            {
                return;
            }

            _searchMatches = matches;
            TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }
    }

    /// <summary>The match drawn in the current-match brush, or <c>null</c> when the current match is on the other side.</summary>
    public FindMatch? CurrentSearchMatch
    {
        get => _currentSearchMatch;
        set
        {
            if (_currentSearchMatch == value)
            {
                return;
            }

            _currentSearchMatch = value;
            TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }
    }

    /// <summary>Whether a decorator has faulted since the last model was assigned.</summary>
    public bool IsDegraded => _faults.Count > 0;

    /// <summary>The faults since the last model was assigned, in order.</summary>
    public IReadOnlyList<RenderFaultEventArgs> Faults => _faults;

    /// <summary>The model's build stamp the pane currently renders; 0 without a model.</summary>
    public int MetadataVersion => Metadata.Version;

    /// <summary>Lines the last height priming built.</summary>
    public int PrimedLineCount { get; private set; }

    /// <summary>
    /// The fault that turned the syntax highlighting off, or <c>null</c>. Unlike
    /// <see cref="Faults"/> it survives <see cref="ResetFaults"/>: a rebuild is not what would fix
    /// a grammar that will not install, so the pane stays plain text — and the control stays
    /// degraded — until the file or the toggle changes.
    /// </summary>
    internal RenderFaultEventArgs? SyntaxFault { get; private set; }

    internal PaneMetadata Metadata { get; private set; } = PaneMetadata.Empty;

    /// <summary>The side a log line names, or <c>null</c> for the unified pane, which is neither.</summary>
    internal DiffSide? LogSide => IsUnified ? null : Side;

    internal DiffBrushes Palette { get; }

    internal PaddingElementGenerator PaddingGenerator => _generator;

    internal DiffLineBackgroundRenderer BackgroundRenderer => _backgroundRenderer;

    internal SearchMatchRenderer SearchRenderer => _searchRenderer;

    internal DiffCaretRenderer CaretRenderer => _caretRenderer;

    /// <summary>
    /// The 1-based lines the user has edited since this pane's source was assigned. The change
    /// marker margin draws them alongside the diff's own marks; empty until something is typed.
    /// </summary>
    internal IReadOnlySet<int> ModifiedLines
    {
        get => _modifiedLines;
        set
        {
            _modifiedLines = value;
            _changeMarkerMargin.InvalidateVisual();
        }
    }

    internal DiffLineNumberMargin LineNumberMargin => _lineNumberMargin;

    internal ChangeMarkerMargin ChangeMarkerMargin => _changeMarkerMargin;

    /// <summary>Replaces the padding source; a test seam for provoking a generator fault.</summary>
    internal Func<int, PaddingSpec>? PaddingSourceForTesting { get; set; }

    /// <summary>Replaces the grammar install; a test seam for provoking a syntax fault.</summary>
    internal Action<SyntaxGrammar>? SyntaxInstallerForTesting { get; set; }

    /// <summary>Primes now when the pane is laid out, otherwise on its next layout.</summary>
    internal void RequestPrime()
    {
        if (CanPrimeNow)
        {
            PrimeNow();
        }
        else
        {
            _primePending = true;
        }
    }

    /// <summary>Records a fault, logs it and raises <see cref="RenderFault"/>.</summary>
    /// <param name="source">The decorator that failed, by type name.</param>
    /// <param name="lineNumber">The 1-based line being processed, when it was one.</param>
    /// <param name="exception">What was thrown.</param>
    /// <param name="subject">What was being processed when it was not a line — a grammar's language.</param>
    internal RenderFaultEventArgs ReportFault(string source, int? lineNumber, Exception exception, string? subject = null)
    {
        RenderFaultEventArgs fault = new(source, lineNumber, exception, subject);
        _faults.Add(fault);
        DiffViewLog.RenderFault(Logger, LogSide, fault);
        // A fault is usually caught inside a render pass, where a listener may not invalidate a
        // visual; the event is raised once the pass is over.
        Dispatcher.UIThread.Post(() => RenderFault?.Invoke(this, fault));
        return fault;
    }

    /// <summary>Forgets the faults and re-enables every decorator.</summary>
    internal void ResetFaults()
    {
        _faults.Clear();
        _caretNormalisationDisabled = false;
        _generator.Reset();
        _backgroundRenderer.Reset();
        _searchRenderer.Reset();
        _selectionRenderer.Reset();
        _caretRenderer.Reset();
        _lineNumberMargin.Reset();
        _changeMarkerMargin.Reset();
    }

    /// <summary>
    /// The scroll viewer of the template, once applied; the composite couples two of these.
    /// AvaloniaEdit's own <c>ScrollViewer</c> property is internal.
    /// </summary>
    public ScrollViewer? PaneScrollViewer { get; private set; }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        PaneScrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        // TextEditor installs its search panel every time its template is applied; its key
        // bindings would collide with the composite's find bar, so it goes straight back out.
        SearchPanel?.Uninstall();
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Loaded is dispatched after the first layout pass — after that pass's LayoutUpdated —
        // so a prime requested before the control was in the tree runs here, with the styled
        // font on the text view and its measure valid. Without this, only the padded lines
        // inside the first viewport would ever reach the height tree.
        if (_primePending && CanPrimeNow)
        {
            PrimeNow();
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty
            || change.Property == DiffDocumentProperty
            || change.Property == InlineDocumentProperty
            || change.Property == IsUnifiedProperty)
        {
            ApplyMetadata();
        }
        else if (change.Property == WordWrapProperty && change.GetNewValue<bool>())
        {
            // Wrapping would break row alignment; the option is forced off.
            Logger?.LogWarning("WordWrap was set on the {Side} pane and is forced off: wrapped lines cannot stay row-aligned", Side);
            SetCurrentValue(WordWrapProperty, false);
        }
        else if (change.Property == IsCaretBlinkEnabledProperty)
        {
            _caretRenderer.OnFocusChanged();
        }
        else if (change.Property == CanCopyOutProperty)
        {
            // The arrow takes an anchor row's number cell, so the numbers change with it. Measure
            // as well as visual: the cell is expected to be wide enough already, and a frame that
            // proved otherwise should widen rather than clip.
            _lineNumberMargin.InvalidateMeasure();
            _lineNumberMargin.InvalidateVisual();
        }
        else if (change.Property == ShowWhitespaceProperty
                 || change.Property == ShowLineEndingsProperty
                 || change.Property == TabWidthProperty
                 || change.Property == OptionsProperty)
        {
            ApplyDisplayOptions();
        }
        else if (change.Property == UseSyntaxHighlightingProperty || change.Property == SyntaxFileNameProperty)
        {
            // The install's own inputs changed, so a grammar that would not install is tried
            // again — but only here: a rebuild over the same file would repeat the same failure.
            _syntaxDisabled = false;
            SyntaxFault = null;
            UpdateSyntax();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourcesChanged += OnResourcesChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        RefreshPalette();
        // The variant is only known once the pane is in a tree, and it chooses the syntax theme.
        UpdateSyntax();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        ResourcesChanged -= OnResourcesChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _caretRenderer.Stop();
        // TextMateSharp tokenizes on a thread of its own, which the installation owns: a pane
        // that leaves the tree gives it up, and takes it again when it comes back.
        RemoveSyntax();
        base.OnDetachedFromLogicalTree(e);
    }

    // Loaded means attached and through a layout pass, so the styled font has reached the text
    // view and its measured line height is the one the padding will be built against.
    private bool CanPrimeNow =>
        Document is not null
        && IsLoaded
        && TextArea.TextView.IsMeasureValid;

    private void ApplyMetadata()
    {
        Metadata = IsUnified ? PaneMetadata.Unified(InlineDocument) : PaneMetadata.For(DiffDocument, Side);
        ResetFaults();
        // Built lines carry the old padding: drop them so the generator runs again, then prime.
        TextArea.TextView.Redraw();
        RequestPrime();
        _lineNumberMargin.OnMetadataChanged();
        _changeMarkerMargin.InvalidateVisual();
    }

    private void OnDocumentSwapped(object? sender, EventArgs e)
    {
        // A new document has a new height tree; the old primed set and the old collapsed
        // sections mean nothing to it.
        _primer.Forget();
        _collapsed.Clear();
        RequestPrime();
    }

    /// <summary>
    /// Collapses exactly these 1-based inclusive line ranges, replacing whatever was collapsed
    /// before. Returns how many ranges were taken.
    /// </summary>
    /// <remarks>
    /// Collapsing writes the height tree, which answers <c>DocumentHeight</c> at once — the
    /// visual lines and the published scroll extent follow only from a redraw and a measure pass,
    /// the same two steps <see cref="PaddingHeightPrimer"/> takes for the same reason.
    /// </remarks>
    internal int SetCollapsedLines(IReadOnlyList<(int First, int Last)> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        foreach (CollapsedLineSection section in _collapsed)
        {
            section.Uncollapse();
        }

        _collapsed.Clear();

        TextView textView = TextArea.TextView;
        List<(int First, int Last)> taken = [];
        if (Document is { } document)
        {
            foreach ((int first, int last) in ranges)
            {
                if (first < 1 || last < first || last > document.LineCount)
                {
                    continue;
                }

                _collapsed.Add(textView.CollapseLines(document.GetLineByNumber(first), document.GetLineByNumber(last)));
                taken.Add((first, last));
            }
        }

        // The generator spans exactly what was collapsed. Without it the text view walks from a
        // visual line to the next document line, finds it collapsed, and throws.
        _foldGenerator.Reset();
        _foldGenerator.SetRanges(taken);

        textView.Redraw();
        textView.InvalidateMeasure();
        return _collapsed.Count;
    }

    /// <summary>How many line ranges are collapsed in this pane.</summary>
    internal int CollapsedSectionCount => _collapsed.Count;

    /// <summary>
    /// A fold's placeholder was clicked; the argument is the run's first collapsed line. The pane
    /// does not act on it — which rows a fold covers is the composite's to decide, because a run
    /// is a row range and this pane knows only one side of it.
    /// </summary>
    internal event EventHandler<int>? FoldExpandRequested;

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Document is null)
        {
            return;
        }

        if (_primePending)
        {
            if (CanPrimeNow)
            {
                PrimeNow();
            }
        }
        else if (_primer.LineHeightChanged(TextArea.TextView) && CanPrimeNow)
        {
            // A font change rebased the plain lines only; padded lines keep stale heights.
            PrimeNow();
        }
    }

    private void PrimeNow()
    {
        _primePending = false;
        try
        {
            PrimedLineCount = _primer.Prime(TextArea.TextView, Metadata.PaddedLineNumbers(Document.LineCount));
        }
        catch (Exception ex)
        {
            _primer.Forget();
            ReportFault(nameof(PaddingHeightPrimer), null, ex);
        }
    }

    private PaddingSpec PaddingForLine(int lineNumber)
    {
        if (PaddingSourceForTesting is { } source)
        {
            return source(lineNumber);
        }

        return Metadata.PaddingFor(lineNumber, Document?.LineCount ?? 0);
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (!_caretNormalisationDisabled && Document is not null)
        {
            try
            {
                NormaliseCaretColumn();
            }
            catch (Exception ex)
            {
                _caretNormalisationDisabled = true;
                ReportFault("CaretNormalisation", null, ex);
            }
        }

        _caretRenderer.OnCaretMoved();
    }

    /// <summary>
    /// <c>Home</c> pressed twice — AvalonEdit's toggle to "column 0, before the indentation" —
    /// puts the caret on the padding element's column: same offset, same x, and the next
    /// <c>Right</c> is a no-op. The first text column of a padded line is column 1 at the same
    /// offset, so the caret is moved there. Setting the position raises the event again with
    /// column 1, which is a no-op here.
    /// </summary>
    private void NormaliseCaretColumn()
    {
        Caret caret = TextArea.Caret;
        if (PaddingForLine(caret.Line).IsEmpty)
        {
            return;
        }

        TextViewPosition position = caret.Position;
        if (position.VisualColumn == 0)
        {
            caret.Position = new TextViewPosition(position.Location, 1);
        }
    }

    private void OnTextAreaFocusChanged(object? sender, RoutedEventArgs e)
    {
        _caretRenderer.OnFocusChanged();
    }

    /// <summary>
    /// The selection moved: only the number margin's selection arrow depends on it, so this is a
    /// margin invalidation and not a redraw of the pane. The margin already repaints on every
    /// scroll, which is far more often than a selection changes.
    /// </summary>
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _lineNumberMargin.OnSelectionChanged();
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        RefreshPalette();
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshPalette();
        UpdateSyntax();
    }

    /// <summary>
    /// Brings the syntax colouring in line with <see cref="UseSyntaxHighlighting"/>,
    /// <see cref="SyntaxFileName"/> and the current variant: installs the grammar the file name
    /// implies, re-themes one already installed, or removes it. A file no grammar claims is plain
    /// text and <em>not</em> a fault; anything thrown along the way is, and leaves the colouring
    /// off until the file name or the toggle changes.
    /// </summary>
    private void UpdateSyntax()
    {
        if (_syntaxDisabled)
        {
            return;
        }

        string? fileName = SyntaxFileName;
        if (!UseSyntaxHighlighting || string.IsNullOrEmpty(fileName))
        {
            RemoveSyntax();
            return;
        }

        ThemeVariant variant = ActualThemeVariant;
        SyntaxHighlighting syntax = _syntax ??= new SyntaxHighlighting(this, OnSyntaxException);
        SyntaxGrammar? grammar = null;
        try
        {
            grammar = syntax.GrammarFor(fileName, variant);
            if (grammar is not { } found)
            {
                DiffViewLog.SyntaxUnavailable(Logger, LogSide, System.IO.Path.GetExtension(fileName));
                RemoveSyntax();
                return;
            }

            if (found == syntax.Installed)
            {
                syntax.SetTheme(variant);
                return;
            }

            if (SyntaxInstallerForTesting is { } installer)
            {
                installer(found);
            }
            else
            {
                syntax.Apply(found, variant);
            }

            DiffViewLog.SyntaxInstalled(Logger, LogSide, found.LanguageId);
            TextArea.TextView.Redraw();
        }
        catch (Exception ex)
        {
            DisableSyntax(grammar?.LanguageId ?? System.IO.Path.GetExtension(fileName), ex);
        }
    }

    /// <summary>
    /// Pushes the display options onto the editor's <see cref="TextEditorOptions"/>, which is
    /// also where they land again if a host replaces the whole options object. None of them
    /// changes a row's height, so nothing here re-primes.
    /// </summary>
    private void ApplyDisplayOptions()
    {
        TextEditorOptions options = Options;
        options.ShowSpaces = ShowWhitespace;
        options.ShowTabs = ShowWhitespace;
        options.ShowEndOfLine = ShowLineEndings;
        options.IndentationSize = TabWidth;
    }

    private void RemoveSyntax()
    {
        if (_syntax?.Remove() == true)
        {
            TextArea.TextView.Redraw();
        }
    }

    /// <summary>
    /// The installation threw after it was installed. TextMateSharp tokenizes on its own thread,
    /// so the fault becomes state on the UI thread, and only the first one does: the rest of the
    /// stream is the same failure.
    /// </summary>
    private void OnSyntaxException(Exception exception)
    {
        Dispatcher.UIThread.Post(() => DisableSyntax(_syntax?.Installed?.LanguageId ?? SyntaxFileName ?? "?", exception));
    }

    private void DisableSyntax(string subject, Exception exception)
    {
        if (_syntaxDisabled)
        {
            return;
        }

        _syntaxDisabled = true;
        RemoveSyntax();
        SyntaxFault = ReportFault(nameof(SyntaxHighlighting), null, exception, subject);
    }

    private void RefreshPalette()
    {
        if (!Palette.Resolve(this))
        {
            return;
        }

        TextView textView = TextArea.TextView;
        textView.InvalidateLayer(KnownLayer.Background);
        textView.InvalidateLayer(KnownLayer.Selection);
        textView.InvalidateLayer(KnownLayer.Caret);
        _lineNumberMargin.InvalidateVisual();
        _changeMarkerMargin.InvalidateVisual();
    }
}
