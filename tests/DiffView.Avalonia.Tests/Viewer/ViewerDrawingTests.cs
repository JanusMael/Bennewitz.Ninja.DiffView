using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// Plan 00021: the viewer draws the diff the editor draws. Same sources, same size, same
/// decorators — a visual difference for the same input is a defect, not a variant.
/// </summary>
/// <remarks>
/// Compared as each decorator's drawn state and as the two frames against each other, never
/// against a stored picture. A stored baseline passes while a fraction of it has moved
/// (<c>AGENTS.md</c> §5); two frames one rasterizer drew in one process are compared exactly, and
/// that holds on every platform because both sides of the comparison are that platform's own.
/// </remarks>
public sealed class ViewerDrawingTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaFact]
    public async Task The_viewer_draws_exactly_what_the_editor_draws()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost editor = new(width: 900, height: 600);
        using ViewerHost viewer = new(width: 900, height: 600);
        editor.Show();
        viewer.Show();
        await editor.LoadAsync(left, right);
        await viewer.LoadAsync(left, right);

        // The same change current in both, so the outline and the map's marker are compared too.
        editor.View.CurrentChangeIndex = 1;
        viewer.View.CurrentChangeIndex = 1;

        using WriteableBitmap editorFrame = editor.Capture();
        using WriteableBitmap viewerFrame = viewer.Capture();

        // Each decorator's own record of what it drew, so a failure names the decorator.
        Assert.NotEmpty(viewer.View.Gutter!.LastPolygons);
        Assert.Equal(editor.View.Gutter!.LastPolygons, viewer.View.Gutter.LastPolygons);
        foreach (DiffSide side in new[] { DiffSide.Left, DiffSide.Right })
        {
            DiffPanePresenter drawn = editor.View.Pane(side)!;
            DiffPanePresenter shown = viewer.View.Pane(side)!;
            Assert.Equal(drawn.LineNumberMargin.LastColumnRight, shown.LineNumberMargin.LastColumnRight, Tolerance);
            Assert.NotEmpty(shown.ChangeMarkerMargin.LastChips);
            Assert.Equal(drawn.ChangeMarkerMargin.LastChips, shown.ChangeMarkerMargin.LastChips);
            Assert.NotNull(shown.BackgroundRenderer.LastCurrentBlockBorder);
            Assert.Equal(drawn.BackgroundRenderer.LastCurrentBlockBorder, shown.BackgroundRenderer.LastCurrentBlockBorder);
        }

        DiffMinimap editorMap = editor.View.Minimap!;
        DiffMinimap viewerMap = viewer.View.Minimap!;
        Assert.Equal(editorMap.ViewportBounds, viewerMap.ViewportBounds);
        Assert.Equal(editorMap.BucketCount, viewerMap.BucketCount);
        foreach (DiffSide side in new[] { DiffSide.Left, DiffSide.Right })
        {
            Assert.Equal(
                Enumerable.Range(0, editorMap.BucketCount).Select(b => editorMap.KindOfBucket(b, side)),
                Enumerable.Range(0, viewerMap.BucketCount).Select(b => viewerMap.KindOfBucket(b, side)));
        }

        // And the whole of both frames, pixel for pixel.
        Assert.Equal(editorFrame.PixelSize, viewerFrame.PixelSize);
        (int count, PixelPoint? first) = PixelProbe.Differing(editorFrame, viewerFrame, new PixelRect(editorFrame.PixelSize));
        Assert.True(count == 0, $"{count} pixels differ between the editor's frame and the viewer's, the first at {first}.");
    }
}
