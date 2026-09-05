using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Draws the document's own line numbers — no lookup through the model — at each line's text
/// band, so nothing is drawn over padding space. A click puts the caret on that line and focuses
/// the pane. Width follows the digit count of the line count, two digits at least.
/// </summary>
internal sealed class DiffLineNumberMargin : DiffMargin
{
    private const int MinimumDigits = 2;
    private const double HorizontalPadding = 6;

    private readonly List<(int LineNumber, double Y)> _lastRendered = [];
    private int _digits = MinimumDigits;

    public DiffLineNumberMargin(DiffPanePresenter owner)
        : base(owner, nameof(DiffLineNumberMargin), DiffViewStrings.LineNumbersMarginName)
    {
    }

    /// <summary>The numbers of the last frame and the y each was drawn at, in order.</summary>
    public IReadOnlyList<(int LineNumber, double Y)> LastRendered => _lastRendered;

    protected override void OnDocumentChanged(TextDocument? oldDocument, TextDocument? newDocument)
    {
        if (oldDocument is not null)
        {
            oldDocument.LineCountChanged -= OnLineCountChanged;
        }

        base.OnDocumentChanged(oldDocument, newDocument);
        if (newDocument is not null)
        {
            newDocument.LineCountChanged += OnLineCountChanged;
        }

        UpdateDigits();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        FormattedText widest = Format(new string('9', _digits), Owner.Palette[DiffBrush.LineNumberForeground]);
        return new Size(widest.Width + 2 * HorizontalPadding, 0);
    }

    protected override void RenderCore(DrawingContext context, TextView textView)
    {
        _lastRendered.Clear();
        IBrush foreground = Owner.Palette[DiffBrush.LineNumberForeground];
        double right = Bounds.Width - HorizontalPadding;
        foreach (VisualLine line in textView.VisualLines)
        {
            int number = line.FirstDocumentLine.LineNumber;
            FormattedText text = Format(number.ToString(CultureInfo.CurrentCulture), foreground);
            double y = TextTopOf(line, textView);
            context.DrawText(text, new Point(right - text.Width, y));
            _lastRendered.Add((number, y));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || TextView is null || TextArea is null || Document is null)
        {
            return;
        }

        double y = e.GetPosition(TextView).Y + TextView.VerticalOffset;
        DocumentLine? line = TextView.GetDocumentLineByVisualTop(y);
        if (line is null)
        {
            return;
        }

        TextArea.Caret.Offset = line.Offset;
        TextArea.Focus();
        e.Handled = true;
    }

    private void OnLineCountChanged(object? sender, EventArgs e)
    {
        UpdateDigits();
    }

    private void UpdateDigits()
    {
        int digits = Math.Max(MinimumDigits, (Document?.LineCount ?? 1).ToString(CultureInfo.InvariantCulture).Length);
        if (digits != _digits)
        {
            _digits = digits;
            InvalidateMeasure();
        }
    }
}
