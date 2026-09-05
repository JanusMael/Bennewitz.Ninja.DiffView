using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A background renderer that is a rendering boundary: <see cref="Draw"/> returns early while
/// the text view's visual lines are invalid (the <c>VisualLinesValid</c> guard SourceGit's fork
/// adds to <c>BackgroundGeometryBuilder</c>), and any exception from <see cref="DrawCore"/> is
/// reported once to the owner as a <see cref="RenderFaultEventArgs"/> after which the renderer
/// disables itself rather than throwing on every frame. The text layer is untouched either way.
/// </summary>
internal abstract class GuardedBackgroundRenderer : IBackgroundRenderer
{
    protected GuardedBackgroundRenderer(DiffPanePresenter owner, KnownLayer layer, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrEmpty(name);
        Owner = owner;
        Layer = layer;
        Name = name;
    }

    /// <summary>The presenter this renderer draws for.</summary>
    public DiffPanePresenter Owner { get; }

    /// <inheritdoc/>
    public KnownLayer Layer { get; }

    /// <summary>The name a fault is reported under.</summary>
    public string Name { get; }

    /// <summary>Whether a fault has disabled the renderer.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Re-enables the renderer after a fault.</summary>
    public void Reset()
    {
        IsDisabled = false;
    }

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (IsDisabled || !textView.VisualLinesValid)
        {
            return;
        }

        try
        {
            DrawCore(textView, drawingContext);
        }
        catch (Exception ex)
        {
            IsDisabled = true;
            Owner.ReportFault(Name, null, ex);
        }
    }

    /// <summary>Draws; the visual lines are valid when this runs.</summary>
    protected abstract void DrawCore(TextView textView, DrawingContext drawingContext);
}
