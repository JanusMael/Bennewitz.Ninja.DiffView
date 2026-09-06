using System.Globalization;
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

    private readonly PaddingElementGenerator _generator;
    private readonly PaddingHeightPrimer _primer = new();
    private readonly DiffLineBackgroundRenderer _backgroundRenderer;
    private readonly DiffSelectionRenderer _selectionRenderer;
    private readonly DiffCaretRenderer _caretRenderer;
    private readonly DiffLineNumberMargin _lineNumberMargin;
    private readonly ChangeMarkerMargin _changeMarkerMargin;
    private readonly List<RenderFaultEventArgs> _faults = [];
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
        _selectionRenderer = new DiffSelectionRenderer(this);
        _caretRenderer = new DiffCaretRenderer(this);
        TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);
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

    /// <summary>Receives faults at <c>Error</c> with the exception attached; never document text.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>Whether a decorator has faulted since the last model was assigned.</summary>
    public bool IsDegraded => _faults.Count > 0;

    /// <summary>The faults since the last model was assigned, in order.</summary>
    public IReadOnlyList<RenderFaultEventArgs> Faults => _faults;

    /// <summary>The model's build stamp the pane currently renders; 0 without a model.</summary>
    public int MetadataVersion => Metadata.Version;

    /// <summary>Lines the last height priming built.</summary>
    public int PrimedLineCount { get; private set; }

    internal PaneMetadata Metadata { get; private set; } = PaneMetadata.Empty;

    internal DiffBrushes Palette { get; }

    internal PaddingElementGenerator PaddingGenerator => _generator;

    internal DiffLineBackgroundRenderer BackgroundRenderer => _backgroundRenderer;

    internal DiffCaretRenderer CaretRenderer => _caretRenderer;

    internal DiffLineNumberMargin LineNumberMargin => _lineNumberMargin;

    internal ChangeMarkerMargin ChangeMarkerMargin => _changeMarkerMargin;

    /// <summary>Replaces the padding source; a test seam for provoking a generator fault.</summary>
    internal Func<int, PaddingSpec>? PaddingSourceForTesting { get; set; }

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
    internal void ReportFault(string source, int? lineNumber, Exception exception)
    {
        RenderFaultEventArgs fault = new(source, lineNumber, exception);
        _faults.Add(fault);
        Logger?.LogError(
            exception,
            "{Decorator} on the {Side} pane failed at line {Line} and is disabled until the next model",
            source,
            Side,
            lineNumber?.ToString(CultureInfo.InvariantCulture) ?? "-");
        // A fault is usually caught inside a render pass, where a listener may not invalidate a
        // visual; the event is raised once the pass is over.
        Dispatcher.UIThread.Post(() => RenderFault?.Invoke(this, fault));
    }

    /// <summary>Forgets the faults and re-enables every decorator.</summary>
    internal void ResetFaults()
    {
        _faults.Clear();
        _caretNormalisationDisabled = false;
        _generator.Reset();
        _backgroundRenderer.Reset();
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
    }

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourcesChanged += OnResourcesChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        RefreshPalette();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        ResourcesChanged -= OnResourcesChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _caretRenderer.Stop();
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
