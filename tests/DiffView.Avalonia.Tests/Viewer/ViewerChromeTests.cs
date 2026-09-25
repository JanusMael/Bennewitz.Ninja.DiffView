using Avalonia;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests.Viewer;

/// <summary>
/// Plan 00021: the chrome the viewer keeps, each piece switchable. Every §6 dual-path contract the
/// editor's tests hold through <see cref="CompositeHost"/> is held here again, because none of that
/// coverage reaches a second control: set after load, and set before the template applies.
/// </summary>
/// <remarks>
/// As in <see cref="ChromeToggleTests"/>, a hidden part is asserted through <c>IsVisible</c> and the
/// survivors' arithmetic, never its own bounds — Avalonia does not re-arrange an invisible control,
/// so a hidden part goes on reporting the size it had.
/// </remarks>
public sealed class ViewerChromeTests
{
    private const int Precision = 3;

    [AvaloniaFact]
    public async Task The_headers_hide_and_the_panes_take_the_room()
    {
        using ViewerHost host = await LoadedAsync();
        double baseline = host.Left.Bounds.Height;
        Assert.True(host.View.HeadersGrid!.IsVisible);

        host.View.ShowHeaders = false;
        ViewerHost.Layout();
        Assert.False(host.View.HeadersGrid!.IsVisible);
        Assert.True(host.Left.Bounds.Height > baseline);

        host.View.ShowHeaders = true;
        ViewerHost.Layout();
        Assert.True(host.View.HeadersGrid!.IsVisible);
        Assert.Equal(baseline, host.Left.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task The_status_strip_hides_and_the_panes_take_the_room()
    {
        using ViewerHost host = await LoadedAsync();
        double baseline = host.Left.Bounds.Height;
        Assert.True(host.View.StatusStrip!.IsVisible);

        host.View.ShowStatusStrip = false;
        ViewerHost.Layout();
        Assert.False(host.View.StatusStrip!.IsVisible);
        Assert.True(host.Left.Bounds.Height > baseline);

        host.View.ShowStatusStrip = true;
        ViewerHost.Layout();
        Assert.True(host.View.StatusStrip!.IsVisible);
        Assert.Equal(baseline, host.Left.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task The_banner_hides_and_comes_back_and_the_panes_take_the_room()
    {
        // Identical sources, so the banner has something to say and hiding it hides something.
        using ViewerHost host = await LoadedAsync(identical: true);
        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.True(host.View.Banner!.IsVisible);
        double baseline = host.Left.Bounds.Height;

        host.View.ShowBanner = false;
        ViewerHost.Layout();
        Assert.False(host.View.Banner!.IsVisible);
        Assert.True(host.Left.Bounds.Height > baseline);

        // The half a local IsVisible write cannot do: it would outrank the style for the control's life.
        host.View.ShowBanner = true;
        ViewerHost.Layout();
        Assert.True(host.View.Banner!.IsVisible);
        Assert.Equal(baseline, host.Left.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task A_permitted_banner_with_nothing_to_say_stays_hidden()
    {
        using ViewerHost host = await LoadedAsync(identical: true);
        host.View.ShowBanner = false;
        ViewerHost.Layout();
        host.View.ShowBanner = true;
        ViewerHost.Layout();

        // Now give the build nothing to say. The toggle permits; it must not command.
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        Assert.Equal(DiffBannerKind.None, host.View.BannerKind);
        Assert.False(host.View.Banner!.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_map_hides_and_the_panes_take_its_width()
    {
        using ViewerHost host = await LoadedAsync();
        DiffMinimap map = host.View.Minimap!;
        Assert.True(map.IsVisible);
        Assert.Equal(DiffMinimap.MapWidth, map.Bounds.Width);
        double panes = host.Left.Bounds.Width + host.Right.Bounds.Width;

        host.View.ShowMinimap = false;
        ViewerHost.Layout();
        Assert.False(map.IsVisible);
        Assert.Equal(panes + DiffMinimap.MapWidth, host.Left.Bounds.Width + host.Right.Bounds.Width, 1);
    }

    [AvaloniaTheory]
    [InlineData(MinimapPlacement.Right)]
    [InlineData(MinimapPlacement.Left)]
    public async Task The_map_docks_either_side_and_each_header_stays_over_its_pane(MinimapPlacement placement)
    {
        using ViewerHost host = await LoadedAsync();
        host.View.MinimapPlacement = placement;
        ViewerHost.Layout();

        DiffMinimap map = host.View.Minimap!;
        if (placement == MinimapPlacement.Left)
        {
            Assert.True(XOf(host, map) + map.Bounds.Width <= XOf(host, host.Left) + 1, "the map should sit outside the left pane");
            Assert.True(map.MirrorEdges);
        }
        else
        {
            Assert.True(XOf(host, map) >= XOf(host, host.Right) + host.Right.Bounds.Width - 1, "the map should sit outside the right pane");
            Assert.False(map.MirrorEdges);
        }

        AssertHeadersOverPanes(host);
    }

    [AvaloniaFact]
    public async Task The_split_ratio_moves_the_panes_and_the_headers_with_them()
    {
        using ViewerHost host = await LoadedAsync();
        double before = host.Left.Bounds.Width;

        host.View.SplitRatio = 0.7;
        ViewerHost.Layout();

        Assert.True(host.Left.Bounds.Width > before + 50, $"the left pane should widen from {before}, got {host.Left.Bounds.Width}");
        Assert.Equal(0.7, host.Left.Bounds.Width / (host.Left.Bounds.Width + host.Right.Bounds.Width), 2);
        AssertHeadersOverPanes(host);
    }

    // ── Set before the template applies — the path a host setting it in XAML takes ─────────

    [AvaloniaFact]
    public void The_headers_flag_set_before_the_template_applies_still_takes()
    {
        using ViewerHost host = new();
        host.View.ShowHeaders = false;
        host.Show();

        Assert.False(host.View.HeadersGrid!.IsVisible);
    }

    [AvaloniaFact]
    public void The_status_strip_flag_set_before_the_template_applies_still_takes()
    {
        using ViewerHost host = new();
        host.View.ShowStatusStrip = false;
        host.Show();

        Assert.False(host.View.StatusStrip!.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_banner_flag_set_before_the_template_applies_still_takes()
    {
        using ViewerHost host = new();
        host.View.ShowBanner = false;
        host.Show();

        (string text, _) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));

        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.False(host.View.Banner!.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_map_flag_set_before_the_template_applies_still_takes()
    {
        using ViewerHost host = new();
        host.View.ShowMinimap = false;
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        Assert.False(host.View.Minimap!.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_placement_set_before_the_template_applies_still_takes()
    {
        using ViewerHost host = new();
        host.View.MinimapPlacement = MinimapPlacement.Left;
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        DiffMinimap map = host.View.Minimap!;
        Assert.True(map.MirrorEdges);
        Assert.True(XOf(host, map) + map.Bounds.Width <= XOf(host, host.Left) + 1);
        AssertHeadersOverPanes(host);
    }

    [AvaloniaFact]
    public async Task The_split_ratio_set_before_the_template_applies_still_takes()
    {
        using ViewerHost host = new();
        host.View.SplitRatio = 0.7;
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(left, right);

        Assert.Equal(0.7, host.Left.Bounds.Width / (host.Left.Bounds.Width + host.Right.Bounds.Width), 2);
        AssertHeadersOverPanes(host);
    }

    private static async Task<ViewerHost> LoadedAsync(bool identical = false)
    {
        ViewerHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(identical ? left : right));
        return host;
    }

    /// <summary>Each header is exactly as wide as its pane and starts where it starts — the half a width cannot show.</summary>
    private static void AssertHeadersOverPanes(ViewerHost host)
    {
        Assert.Equal(host.Left.Bounds.Width, host.View.LeftHeader!.Bounds.Width, 1);
        Assert.Equal(host.Right.Bounds.Width, host.View.RightHeader!.Bounds.Width, 1);
        Assert.Equal(XOf(host, host.Left), XOf(host, host.View.LeftHeader!), 1);
        Assert.Equal(XOf(host, host.Right), XOf(host, host.View.RightHeader!), 1);
    }

    private static double XOf(ViewerHost host, Visual control)
    {
        return control.TranslatePoint(new Point(0, 0), host.Window)?.X
               ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
    }
}
