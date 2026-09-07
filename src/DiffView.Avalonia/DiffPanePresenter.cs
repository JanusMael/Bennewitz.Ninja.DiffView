using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

namespace Bennewitz.Ninja.DiffView.Avalonia;

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

    /// <summary>Identifies the <see cref="IsCaretBlinkEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsCaretBlinkEnabledProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(IsCaretBlinkEnabled), defaultValue: true);

    /// <summary>Identifies the <see cref="UseSyntaxHighlighting"/> property.</summary>
    public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
        AvaloniaProperty.Register<DiffPanePresenter, bool>(nameof(UseSyntaxHighlighting), defaultValue: true);

    /// <summary>Identifies the <see cref="SyntaxFileName"/> property.</summary>
    public static readonly StyledProperty<string?> SyntaxFileNameProperty =
        AvaloniaProperty.Register<DiffPanePresenter, string?>(nameof(SyntaxFileName));

    private readonly PaddingElementGenerator _generator;
    private readonly PaddingHeightPrimer _primer = new();
    private readonly DiffLineBackgroundRenderer _backgroundRenderer;
    private readonly SearchMatchRenderer _searchRenderer;
    private readonly DiffSelectionRenderer _selectionRenderer;
    private readonly DiffCaretRenderer _caretRenderer;
    private readonly DiffLineNumberMargin _lineNumberMargin;
    private readonly ChangeMarkerMargin _changeMarkerMargin;
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
        DocumentChanged += OnDocumentSwapped;
        LayoutUpdated += OnLayoutUpdated;
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

    internal DiffBrushes Palette { get; }

    internal PaddingElementGenerator PaddingGenerator => _generator;

    internal DiffLineBackgroundRenderer BackgroundRenderer => _backgroundRenderer;

    internal SearchMatchRenderer SearchRenderer => _searchRenderer;

    internal DiffCaretRenderer CaretRenderer => _caretRenderer;

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
        DiffViewLog.RenderFault(Logger, Side, fault);
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
        if (change.Property == SideProperty || change.Property == DiffDocumentProperty)
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
        Metadata = PaneMetadata.For(DiffDocument, Side);
        ResetFaults();
        // Built lines carry the old padding: drop them so the generator runs again, then prime.
        TextArea.TextView.Redraw();
        RequestPrime();
        _changeMarkerMargin.InvalidateVisual();
    }

    private void OnDocumentSwapped(object? sender, EventArgs e)
    {
        // A new document has a new height tree; the old primed set means nothing to it.
        _primer.Forget();
        RequestPrime();
    }

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
                DiffViewLog.SyntaxUnavailable(Logger, Side, System.IO.Path.GetExtension(fileName));
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

            DiffViewLog.SyntaxInstalled(Logger, Side, found.LanguageId);
            TextArea.TextView.Redraw();
        }
        catch (Exception ex)
        {
            DisableSyntax(grammar?.LanguageId ?? System.IO.Path.GetExtension(fileName), ex);
        }
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
