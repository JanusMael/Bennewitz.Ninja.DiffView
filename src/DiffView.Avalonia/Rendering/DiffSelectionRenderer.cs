using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Draws the selection from the text area's segments over text bands only, on
/// <see cref="KnownLayer.Selection"/>. The presenter makes AvaloniaEdit's own
/// <c>SelectionBrush</c> transparent and its <c>SelectionBorder</c> null, so the selection never
/// paints padding space.
/// </summary>
internal sealed class DiffSelectionRenderer : GuardedBackgroundRenderer
{
    public DiffSelectionRenderer(DiffPanePresenter owner)
        : base(owner, KnownLayer.Selection, nameof(DiffSelectionRenderer))
    {
    }

    protected override void DrawCore(TextView textView, DrawingContext drawingContext)
    {
        Selection selection = Owner.TextArea.Selection;
        if (selection.IsEmpty)
        {
            return;
        }

        IBrush brush = Owner.Palette[DiffBrush.Selection];
        foreach (SelectionSegment segment in selection.Segments)
        {
            foreach (Rect rect in TextBands.ForSegment(textView, segment))
            {
                drawingContext.FillRectangle(brush, rect);
            }
        }
    }
}
