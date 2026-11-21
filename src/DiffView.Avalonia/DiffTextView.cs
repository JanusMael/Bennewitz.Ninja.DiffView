using Avalonia;
using Avalonia.Controls.Primitives;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// A pane's text view: AvaloniaEdit's, differing only in what it brings into view. AvaloniaEdit's caret
/// brings itself into view as the whole visual line it sits on, which on a padded line includes the
/// padding — rows standing in for the other side's lines, as many as that side's block has — and
/// <see cref="TextView.MakeVisible"/> never settles on a rectangle taller than the viewport: shown from its
/// top, its bottom overflows, and shown from its bottom, its top does. While a mouse selection is in
/// progress, AvaloniaEdit re-extends the selection on every scroll and brings the caret into view again,
/// so the two offsets alternated until the stack overflowed.
/// </summary>
/// <remarks>
/// Reached through <see cref="DiffTextArea"/>, because a text area takes a view of its own only through
/// AvaloniaEdit's protected constructor. Its style key stays AvaloniaEdit's, so a style written for a
/// <see cref="TextView"/> still reaches it.
/// </remarks>
internal sealed class DiffTextView : TextView
{
    /// <summary>How near two edges must be to be the same edge, in device-independent pixels.</summary>
    private const double Tolerance = 1e-6;

    /// <summary>
    /// The caret of the text area this view belongs to, set by that text area once it has made it:
    /// AvaloniaEdit creates the caret after the view.
    /// </summary>
    internal Caret? Caret { get; set; }

    protected override Type StyleKeyOverride => typeof(TextView);

    /// <summary>
    /// Scrolls the view so that <paramref name="rectangle"/> is visible — the caret's rectangle on a padded
    /// line narrowed to the line's own row, and the viewport left where it is by any rectangle that already
    /// covers it from top to bottom.
    /// </summary>
    public override void MakeVisible(Rect rectangle)
    {
        base.MakeVisible(Settled(OwnRowOfCaret(rectangle)));
    }

    /// <summary>
    /// The caret brings itself into view as its line's whole visual line inflated by a border. On a padded
    /// line that rectangle becomes the line's own row, inflated by the same border: the caret the pane
    /// draws is a row tall, and the row is what the reader is looking for. Any other rectangle is returned
    /// as it is.
    /// </summary>
    private Rect OwnRowOfCaret(Rect rectangle)
    {
        // The caret builds its line before it asks, so a caret whose line is not built is not asking.
        if (Caret is not { } caret || Document is null || GetVisualLine(caret.Line) is not { } line)
        {
            return rectangle;
        }

        PaddingSpec padding = DiffLineBackgroundRenderer.PaddingOf(line);
        if (padding.IsEmpty)
        {
            return rectangle;
        }

        Rect whole = caret.CalculateCaretRectangle();
        double border = whole.Y - rectangle.Y;
        if (border < 0 || !IsClose(rectangle, whole.Inflate(border)))
        {
            return rectangle;
        }

        double lineHeight = DefaultLineHeight;
        return new Rect(whole.X, line.VisualTop + padding.Above * lineHeight, whole.Width, lineHeight).Inflate(border);
    }

    /// <summary>
    /// The viewport's own top and height in place of <paramref name="rectangle"/>'s when the rectangle
    /// already covers the viewport from top to bottom. Such a rectangle is as much in view as it can be, and
    /// AvaloniaEdit's rule would move to one of its edges, from which the next request would move back to
    /// the other. A pane shorter than one row brings even a row's rectangle in this way.
    /// </summary>
    private Rect Settled(Rect rectangle)
    {
        double top = ScrollOffset.Y;
        double height = ((IScrollable)this).Viewport.Height;
        return rectangle.Y <= top + Tolerance && rectangle.Bottom >= top + height - Tolerance
            ? new Rect(rectangle.X, top, rectangle.Width, height)
            : rectangle;
    }

    private static bool IsClose(Rect a, Rect b) =>
        Math.Abs(a.X - b.X) < Tolerance
        && Math.Abs(a.Y - b.Y) < Tolerance
        && Math.Abs(a.Width - b.Width) < Tolerance
        && Math.Abs(a.Height - b.Height) < Tolerance;
}
