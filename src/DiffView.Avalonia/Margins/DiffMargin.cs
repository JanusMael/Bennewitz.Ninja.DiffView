using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A gutter margin of the presenter: paints the gutter background, then — while the text view's
/// visual lines are valid — its own content through a rendering boundary that reports one fault
/// and disables the content rather than throwing on every frame. Not focusable; carries an
/// automation name so a screen reader can name the gutter.
/// </summary>
internal abstract class DiffMargin : AbstractMargin
{
    protected DiffMargin(DiffPanePresenter owner, string decoratorName, string automationNameKey)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
        DecoratorName = decoratorName;
        Focusable = false;
        AutomationProperties.SetName(this, DiffViewStrings.Get(automationNameKey));
    }

    /// <summary>The presenter this margin belongs to.</summary>
    public DiffPanePresenter Owner { get; }

    /// <summary>The name a fault is reported under.</summary>
    public string DecoratorName { get; }

    /// <summary>Whether a fault has disabled the margin's content.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Re-enables the content after a fault.</summary>
    public void Reset()
    {
        IsDisabled = false;
        InvalidateVisual();
    }

    public sealed override void Render(DrawingContext context)
    {
        context.FillRectangle(Owner.Palette[DiffBrush.GutterBackground], new Rect(Bounds.Size));
        if (IsDisabled || TextView is not { VisualLinesValid: true } textView)
        {
            return;
        }

        try
        {
            RenderCore(context, textView);
        }
        catch (Exception ex)
        {
            IsDisabled = true;
            Owner.ReportFault(DecoratorName, null, ex);
        }
    }

    /// <summary>The tooltip for <paramref name="lineNumber"/>, or <c>null</c> for none; shown under the pointer.</summary>
    public abstract string? TooltipFor(int lineNumber);

    /// <summary>Draws the content; the visual lines are valid when this runs.</summary>
    protected abstract void RenderCore(DrawingContext context, TextView textView);

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (TextView is not { } view || Document is null)
        {
            ToolTip.SetTip(this, null);
            return;
        }

        DocumentLine? line = view.GetDocumentLineByVisualTop(e.GetPosition(this).Y + view.VerticalOffset);
        ToolTip.SetTip(this, line is null ? null : TooltipFor(line.LineNumber));
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetTip(this, null);
    }

    /// <summary>Text in the margin's inherited font, in <paramref name="brush"/>.</summary>
    protected FormattedText Format(string text, IBrush brush)
    {
        Typeface typeface = new(GetValue(TextBlock.FontFamilyProperty), GetValue(TextBlock.FontStyleProperty), GetValue(TextBlock.FontWeightProperty));
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, GetValue(TextBlock.FontSizeProperty), brush);
    }

    /// <summary>The top of the line's text band in margin coordinates: past the padding above it, and scrolled.</summary>
    protected static double TextTopOf(VisualLine line, TextView textView)
    {
        return line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;
    }
}
