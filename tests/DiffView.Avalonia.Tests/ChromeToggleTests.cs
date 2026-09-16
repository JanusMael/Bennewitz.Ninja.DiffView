using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00022 §Phase 1: three pieces of chrome a host can switch off, on both views.
/// </summary>
/// <remarks>
/// Every case asserts <c>IsVisible</c> on the part and the arithmetic on the <em>survivors</em>,
/// never the hidden part's own bounds. Avalonia does not re-arrange an invisible control, so a
/// hidden part goes on reporting the height it had — measured at 22, 23 and 29 for the headers,
/// the strip and the banner — and an assertion on that passes for a broken implementation too.
/// </remarks>
public sealed class ChromeToggleTests
{
    private const int Precision = 3;

    // ── The side-by-side view ──────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task The_headers_hide_and_the_panes_take_the_room()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        double baseline = host.Left.Bounds.Height;
        Assert.True(host.View.HeadersGrid!.IsVisible);

        host.View.ShowHeaders = false;
        CompositeHost.Layout();
        Assert.False(host.View.HeadersGrid!.IsVisible);
        Assert.True(host.Left.Bounds.Height > baseline);

        host.View.ShowHeaders = true;
        CompositeHost.Layout();
        Assert.True(host.View.HeadersGrid!.IsVisible);
        Assert.Equal(baseline, host.Left.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task The_status_strip_hides_and_the_panes_take_the_room()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        double baseline = host.Left.Bounds.Height;
        Assert.True(host.View.StatusStrip!.IsVisible);

        host.View.ShowStatusStrip = false;
        CompositeHost.Layout();
        Assert.False(host.View.StatusStrip!.IsVisible);
        Assert.True(host.Left.Bounds.Height > baseline);

        host.View.ShowStatusStrip = true;
        CompositeHost.Layout();
        Assert.True(host.View.StatusStrip!.IsVisible);
        Assert.Equal(baseline, host.Left.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task The_banner_hides_and_the_panes_take_the_room()
    {
        using CompositeHost host = new();
        host.Show();
        // Identical sources, so the banner has something to say. Toggling it in the default
        // state would assert nothing: with no banner there is nothing to suppress.
        (string text, _) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));

        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.True(host.View.Banner!.IsVisible);
        double baseline = host.Left.Bounds.Height;

        host.View.ShowBanner = false;
        CompositeHost.Layout();
        Assert.False(host.View.Banner!.IsVisible);
        Assert.True(host.Left.Bounds.Height > baseline);

        // The half the rejected mechanism cannot do: a local IsVisible write would outrank the
        // style for the life of the control and the banner would never come back under its rule.
        host.View.ShowBanner = true;
        CompositeHost.Layout();
        Assert.True(host.View.Banner!.IsVisible);
        Assert.Equal(baseline, host.Left.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task A_permitted_banner_with_nothing_to_say_stays_hidden()
    {
        using CompositeHost host = new();
        host.Show();
        (string text, _) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));

        host.View.ShowBanner = false;
        CompositeHost.Layout();
        host.View.ShowBanner = true;
        CompositeHost.Layout();

        // Now give the build nothing to say. The toggle permits; it must not command.
        (string left, string right) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));
        CompositeHost.Layout();

        Assert.Equal(DiffBannerKind.None, host.View.BannerKind);
        Assert.False(host.View.Banner!.IsVisible);
    }

    [AvaloniaFact]
    public void The_headers_flag_set_before_the_template_applies_still_takes()
    {
        using CompositeHost host = new();
        host.View.ShowHeaders = false;
        host.Show();

        Assert.False(host.View.HeadersGrid!.IsVisible);
    }

    [AvaloniaFact]
    public void The_status_strip_flag_set_before_the_template_applies_still_takes()
    {
        using CompositeHost host = new();
        host.View.ShowStatusStrip = false;
        host.Show();

        Assert.False(host.View.StatusStrip!.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_banner_flag_set_before_the_template_applies_still_takes()
    {
        using CompositeHost host = new();
        host.View.ShowBanner = false;
        host.Show();

        (string text, _) = CompositeHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));
        CompositeHost.Layout();

        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.False(host.View.Banner!.IsVisible);
    }

    [Fact]
    public void The_demos_three_chrome_entries_carry_automation_names()
    {
        // AccessibilityCoverageTests scans the .axaml files, but its element set is a hardcoded
        // literal that does not include MenuItem — so three unnamed entries would pass it.
        string xaml = File.ReadAllText(RepoPaths.Source(Path.Combine("src", "DiffView.Demo", "MainWindow.axaml")));

        foreach (string name in new[] { "ShowHeaders", "ShowStatusStrip", "ShowBanner" })
        {
            int at = xaml.IndexOf("x:Name=\"" + name + "\"", StringComparison.Ordinal);
            Assert.True(at >= 0, name + " has no entry in the demo's View menu");

            int end = xaml.IndexOf("/>", at, StringComparison.Ordinal);
            Assert.True(end > at, name + "'s entry is not a self-closing element");
            Assert.Contains("AutomationProperties.Name=\"", xaml[at..end], StringComparison.Ordinal);
        }
    }

    // ── The unified view ───────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task The_unified_headers_hide_and_the_pane_takes_the_room()
    {
        using InlineHost host = new();
        host.Show();
        (string left, string right) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        double baseline = host.Pane.Bounds.Height;
        Assert.True(host.View.HeadersGrid!.IsVisible);

        host.View.ShowHeaders = false;
        InlineHost.Layout();
        Assert.False(host.View.HeadersGrid!.IsVisible);
        Assert.True(host.Pane.Bounds.Height > baseline);

        host.View.ShowHeaders = true;
        InlineHost.Layout();
        Assert.True(host.View.HeadersGrid!.IsVisible);
        Assert.Equal(baseline, host.Pane.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task The_unified_status_strip_hides_and_the_pane_takes_the_room()
    {
        using InlineHost host = new();
        host.Show();
        (string left, string right) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));

        double baseline = host.Pane.Bounds.Height;
        Assert.True(host.View.StatusStrip!.IsVisible);

        host.View.ShowStatusStrip = false;
        InlineHost.Layout();
        Assert.False(host.View.StatusStrip!.IsVisible);
        Assert.True(host.Pane.Bounds.Height > baseline);

        host.View.ShowStatusStrip = true;
        InlineHost.Layout();
        Assert.True(host.View.StatusStrip!.IsVisible);
        Assert.Equal(baseline, host.Pane.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task The_unified_banner_hides_and_the_pane_takes_the_room()
    {
        using InlineHost host = new();
        host.Show();
        (string text, _) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));

        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.True(host.View.Banner!.IsVisible);
        double baseline = host.Pane.Bounds.Height;

        host.View.ShowBanner = false;
        InlineHost.Layout();
        Assert.False(host.View.Banner!.IsVisible);
        Assert.True(host.Pane.Bounds.Height > baseline);

        host.View.ShowBanner = true;
        InlineHost.Layout();
        Assert.True(host.View.Banner!.IsVisible);
        Assert.Equal(baseline, host.Pane.Bounds.Height, Precision);
    }

    [AvaloniaFact]
    public async Task A_permitted_unified_banner_with_nothing_to_say_stays_hidden()
    {
        using InlineHost host = new();
        host.Show();
        (string text, _) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));

        host.View.ShowBanner = false;
        InlineHost.Layout();
        host.View.ShowBanner = true;
        InlineHost.Layout();

        (string left, string right) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));
        InlineHost.Layout();

        Assert.Equal(DiffBannerKind.None, host.View.BannerKind);
        Assert.False(host.View.Banner!.IsVisible);
    }

    [AvaloniaFact]
    public void The_unified_headers_flag_set_before_the_template_applies_still_takes()
    {
        using InlineHost host = new();
        host.View.ShowHeaders = false;
        host.Show();

        Assert.False(host.View.HeadersGrid!.IsVisible);
    }

    [AvaloniaFact]
    public void The_unified_status_strip_flag_set_before_the_template_applies_still_takes()
    {
        using InlineHost host = new();
        host.View.ShowStatusStrip = false;
        host.Show();

        Assert.False(host.View.StatusStrip!.IsVisible);
    }

    [AvaloniaFact]
    public async Task The_unified_banner_flag_set_before_the_template_applies_still_takes()
    {
        using InlineHost host = new();
        host.View.ShowBanner = false;
        host.Show();

        (string text, _) = InlineHost.SmallFixture();
        await host.LoadAsync(new PaneSource(text), new PaneSource(text));
        InlineHost.Layout();

        Assert.Equal(DiffBannerKind.Identical, host.View.BannerKind);
        Assert.False(host.View.Banner!.IsVisible);
    }
}
