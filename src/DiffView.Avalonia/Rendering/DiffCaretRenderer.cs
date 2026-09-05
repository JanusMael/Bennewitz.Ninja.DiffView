using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Draws the caret one text line tall over its line's text band, on
/// <see cref="KnownLayer.Caret"/>, while the text area has keyboard focus; blinks at
/// AvaloniaEdit's cadence unless the presenter turns blinking off. The presenter makes
/// AvaloniaEdit's own <c>CaretBrush</c> transparent, so the caret is never padded-line tall.
/// </summary>
internal sealed class DiffCaretRenderer : GuardedBackgroundRenderer
{
    /// <summary>Caret width in pixels.</summary>
    public const double Width = 2;

    /// <summary>The blink interval AvaloniaEdit's own caret layer uses.</summary>
    public static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _timer;
    private bool _phaseVisible = true;

    public DiffCaretRenderer(DiffPanePresenter owner)
        : base(owner, KnownLayer.Caret, nameof(DiffCaretRenderer))
    {
        _timer = new DispatcherTimer { Interval = BlinkInterval };
        _timer.Tick += OnTick;
    }

    /// <summary>Whether the caret is drawn right now: focused, and in the visible phase of the blink.</summary>
    public bool IsCaretShown => Owner.TextArea.IsFocused && (_phaseVisible || !Owner.IsCaretBlinkEnabled);

    /// <summary>Focus arrived or left, or blinking was toggled: restart or stop the blink.</summary>
    public void OnFocusChanged()
    {
        _phaseVisible = true;
        if (Owner.TextArea.IsFocused && Owner.IsCaretBlinkEnabled)
        {
            _timer.Stop();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        Invalidate();
    }

    /// <summary>The caret moved: show it and restart the blink, as editors do.</summary>
    public void OnCaretMoved()
    {
        _phaseVisible = true;
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            _timer.Start();
        }

        Invalidate();
    }

    /// <summary>Stops the blink timer; the presenter left the tree.</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    protected override void DrawCore(TextView textView, DrawingContext drawingContext)
    {
        if (!IsCaretShown)
        {
            return;
        }

        if (TextBands.ForCaret(textView, Owner.TextArea.Caret, Width) is { } rect)
        {
            drawingContext.FillRectangle(Owner.Palette[DiffBrush.Caret], rect);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _phaseVisible = !_phaseVisible;
        Invalidate();
    }

    private void Invalidate()
    {
        Owner.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);
    }
}
