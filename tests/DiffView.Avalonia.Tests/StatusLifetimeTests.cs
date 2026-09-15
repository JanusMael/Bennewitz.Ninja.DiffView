using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00020 §Phase 1: the view owns the controller it builds, so leaving the visual tree
/// releases it and coming back rebuilds it. Both views have the same lazy field, the same
/// omission and the same call to the lazy getter at the end of <c>UpdateStrip</c>, so every
/// case is pinned on both.
/// </summary>
public sealed class StatusLifetimeTests
{
    [AvaloniaFact]
    public void A_detached_view_leaves_no_armed_timer()
    {
        using CompositeHost host = new();
        host.Show();

        host.View.Status.SetSuccess("done");
        Assert.Equal(1, host.Time.ActiveTimers);

        host.Window.Content = null;
        CompositeHost.Layout();

        Assert.Equal(0, host.Time.ActiveTimers);
        Assert.Null(host.View.StatusOrNull);
    }

    [AvaloniaFact]
    public void A_detached_unified_view_leaves_no_armed_timer()
    {
        using InlineHost host = new();
        host.Show();

        host.View.Status.SetSuccess("done");
        Assert.Equal(1, host.Time.ActiveTimers);

        host.Window.Content = null;
        InlineHost.Layout();

        Assert.Equal(0, host.Time.ActiveTimers);
        Assert.Null(host.View.StatusOrNull);
    }

    [AvaloniaFact]
    public void A_strip_refresh_on_a_detached_view_does_not_rebuild_the_controller()
    {
        using CompositeHost host = new();
        host.Show();
        host.View.Status.SetSuccess("done");

        host.Window.Content = null;
        CompositeHost.Layout();
        Assert.Null(host.View.StatusOrNull);

        // A refresh that never mentions Status. While UpdateStrip ended in a call to the lazy
        // getter, this rebuilt the controller and re-subscribed Changed on a view that will
        // never detach again — measured at two armed timers before the field read landed.
        host.View.OpenFind();
        CompositeHost.Layout();

        Assert.Null(host.View.StatusOrNull);
    }

    [AvaloniaFact]
    public void A_strip_refresh_on_a_detached_unified_view_does_not_rebuild_the_controller()
    {
        using InlineHost host = new();
        host.Show();
        host.View.Status.SetSuccess("done");

        host.Window.Content = null;
        InlineHost.Layout();
        Assert.Null(host.View.StatusOrNull);

        host.View.OpenFind();
        InlineHost.Layout();

        Assert.Null(host.View.StatusOrNull);
    }

    [AvaloniaFact]
    public void A_re_attached_view_still_takes_a_message()
    {
        using CompositeHost host = new();
        host.Show();
        host.View.Status.SetSuccess("first");

        host.Window.Content = null;
        CompositeHost.Layout();
        host.Window.Content = host.View;
        CompositeHost.Layout();

        // Dispose latches _disposed and Set throws on it, so without the null beside it this
        // line is an ObjectDisposedException and every tab switch breaks the view it returns to.
        host.View.Status.SetSuccess("second");
        CompositeHost.Layout();

        Assert.Equal("second", host.View.Status.Text);
        Assert.Equal("second", host.View.StatusStrip?.TransientText);
    }

    [AvaloniaFact]
    public void A_re_attached_unified_view_still_takes_a_message()
    {
        using InlineHost host = new();
        host.Show();
        host.View.Status.SetSuccess("first");

        host.Window.Content = null;
        InlineHost.Layout();
        host.Window.Content = host.View;
        InlineHost.Layout();

        host.View.Status.SetSuccess("second");
        InlineHost.Layout();

        Assert.Equal("second", host.View.Status.Text);
        Assert.Equal("second", host.View.StatusStrip?.TransientText);
    }

    [AvaloniaFact]
    public void Detaching_with_a_message_on_screen_does_not_throw()
    {
        using CompositeHost host = new();
        host.Show();
        host.View.Status.SetSuccess("on screen");
        CompositeHost.Layout();
        Assert.Equal("on screen", host.View.StatusStrip?.TransientText);

        // Dispose calls Apply, which raises Changed into UpdateStrip while the view is
        // detaching. A throw here fails the test on its own; the assertion is that the
        // teardown path ran rather than being skipped.
        host.Window.Content = null;
        CompositeHost.Layout();

        Assert.Null(host.View.StatusStrip?.TransientText);
    }

    [AvaloniaFact]
    public void Detaching_a_unified_view_with_a_message_on_screen_does_not_throw()
    {
        using InlineHost host = new();
        host.Show();
        host.View.Status.SetSuccess("on screen");
        InlineHost.Layout();
        Assert.Equal("on screen", host.View.StatusStrip?.TransientText);

        host.Window.Content = null;
        InlineHost.Layout();

        Assert.Null(host.View.StatusStrip?.TransientText);
    }

    [AvaloniaFact]
    public async Task A_build_that_completes_after_detach_arms_one_timer_and_it_drains()
    {
        using CompositeHost host = new();
        host.Show();
        (string left, string right) = CompositeHost.SmallFixture();

        host.Window.Content = null;
        CompositeHost.Layout();
        Assert.Equal(0, host.Time.ActiveTimers);

        // Set arms its timer *after* it raises Changed, so nothing driven by Changed can cancel
        // what the call is about to create. The residue is one timer of at most ten seconds, and
        // this pins that bound rather than pretending it is zero: it turns red the day a message
        // posted after a detach stops draining.
        await host.LoadAsync(new PaneSource(left), new PaneSource(right));
        CompositeHost.Layout();
        Assert.Equal(1, host.Time.ActiveTimers);

        host.Time.Advance(StatusController.DefaultWarningAutoClearDelay + TimeSpan.FromMilliseconds(1));
        CompositeHost.Layout();

        Assert.Equal(0, host.Time.ActiveTimers);
    }

    [AvaloniaFact]
    public async Task A_unified_build_that_completes_after_detach_arms_one_timer_and_it_drains()
    {
        using InlineHost host = new();
        host.Show();
        (string left, string right) = InlineHost.SmallFixture();

        host.Window.Content = null;
        InlineHost.Layout();
        Assert.Equal(0, host.Time.ActiveTimers);

        await host.LoadAsync(new PaneSource(left), new PaneSource(right));
        InlineHost.Layout();
        Assert.Equal(1, host.Time.ActiveTimers);

        host.Time.Advance(StatusController.DefaultWarningAutoClearDelay + TimeSpan.FromMilliseconds(1));
        InlineHost.Layout();

        Assert.Equal(0, host.Time.ActiveTimers);
    }
}
