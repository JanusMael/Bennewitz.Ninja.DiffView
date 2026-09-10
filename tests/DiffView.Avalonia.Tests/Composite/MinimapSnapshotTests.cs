using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00007 §Phase 3, the rendered evidence: a pair whose one block belongs to the left side
/// only, so the left lane is inked over those buckets and the right lane is notched — the thing
/// the single-lane map could not say — in both variants and both palettes.
/// </summary>
/// <remarks>
/// A bucket is one pixel row, so per <c>AGENTS.md</c> §5 the PNG is not the guard: the assertions
/// beside the capture are, and the frame is what a reviewer looks at.
/// </remarks>
public sealed class MinimapSnapshotTests
{
    private static readonly Uri BaseUri = new("avares://DiffView.Avalonia.Tests/");

    [AvaloniaTheory]
    [InlineData("Light", "Default")]
    [InlineData("Dark", "Default")]
    [InlineData("Light", "ColorBlind")]
    [InlineData("Dark", "ColorBlind")]
    public async Task A_one_sided_block_inks_one_lane_and_notches_the_other(string variant, string palette)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        ResourceInclude? colourBlind = palette == "ColorBlind"
            ? new ResourceInclude(BaseUri) { Source = DiffViewResources.ColorBlindTokensUri }
            : null;
        if (colourBlind is not null)
        {
            app.Resources.MergedDictionaries.Add(colourBlind);
        }

        MemoryStream png = new();
        try
        {
            using CompositeHost host = new(width: 900, height: 600);
            host.Show();

            // One block, left-only, and long enough to cover many buckets on its own.
            await host.LoadAsync(Lines("keep", 40) + Lines("gone", 60) + Lines("tail", 40), Lines("keep", 40) + Lines("tail", 40));
            CompositeHost.Layout();

            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);

            DiffMinimap map = host.View.Minimap!;
            Point origin = map.TranslatePoint(new Point(0, 0), host.Window)!.Value;
            ChangeBlock block = Assert.Single(host.View.Document!.Blocks);
            Assert.Equal(DiffLineKind.Deleted, block.Kind);

            Color deleted = PresenterHost.Token("DiffView.MarkerDeletedBrush");
            int top = map.BucketOfRow(block.FirstRow) + 2;
            int bottom = map.BucketOfRow(block.LastRow) - 2;
            Assert.True(bottom > top, $"the block should cover several buckets; {top}..{bottom}");

            // The lanes, read where the block is: the left carries the deleted colour and the
            // right carries none of it. The x offsets come from LaneAt, so a lane that moved would
            // be measured where it moved to — and then the other assertion would fail.
            // Rows are sparser than pixels on a pair this size, so a lane is thin lines rather
            // than a band: the expectation is the buckets the map *reports* as this side's, which
            // MinimapLaneTests has already tied to the model. What is asserted here is that the
            // drawing matches the report — the half a report cannot vouch for.
            int expected = 0;
            for (int bucket = top; bucket < bottom; bucket++)
            {
                if (map.KindOfBucket(bucket, DiffSide.Left) == DiffLineKind.Deleted)
                {
                    expected++;
                }
            }

            Assert.True(expected > 20, $"the block should report many inked buckets; got {expected}");
            int inLeft = Ink(frame, origin, LaneCentre(map, DiffSide.Left), top, bottom, deleted);
            int inRight = Ink(frame, origin, LaneCentre(map, DiffSide.Right), top, bottom, deleted);
            Assert.Equal(expected, inLeft);
            Assert.Equal(0, inRight);

            // And the two lanes really are two: the gap between them carries no ink at all.
            Assert.Equal(0, Ink(frame, origin, LaneCentre(map, DiffSide.Left) + 5, top, bottom, deleted));
        }
        finally
        {
            if (colourBlind is not null)
            {
                app.Resources.MergedDictionaries.Remove(colourBlind);
            }

            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(variant, palette);
    }

    /// <summary>The x the map itself says is inside <paramref name="side"/>'s lane.</summary>
    private static double LaneCentre(DiffMinimap map, DiffSide side)
    {
        for (double x = 0; x < map.Bounds.Width; x += 1)
        {
            if (map.LaneAt(x) == side && map.LaneAt(x + 3) == side)
            {
                return x + 3;
            }
        }

        throw new InvalidOperationException($"no lane found for {side}");
    }

    private static int Ink(WriteableBitmap frame, Point origin, double x, int top, int bottom, Color colour)
    {
        int column = (int)Math.Round(origin.X + x);
        int rows = 0;
        for (int bucket = top; bucket < bottom; bucket++)
        {
            PixelRect probe = new(column, (int)Math.Round(origin.Y) + bucket, 1, 1);
            if (PixelProbe.Count(frame, probe, c => PresenterHost.Near(c, colour, tolerance: 20)) > 0)
            {
                rows++;
            }
        }

        return rows;
    }

    private static string Lines(string text, int count)
    {
        return string.Concat(Enumerable.Range(0, count).Select(i => $"{text}{i}\n"));
    }
}
