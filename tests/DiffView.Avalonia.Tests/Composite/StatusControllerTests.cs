namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// The transient-message lifecycle lifted from ClaudeForge, on a hand-advanced clock: success
/// clears after its delay, failure sticks until dismissed, a new message cancels the pending
/// clear, active and state stick until replaced.
/// </summary>
public sealed class StatusControllerTests
{
    [Fact]
    public void A_success_message_clears_after_its_delay()
    {
        FakeTimeProvider time = new();
        using StatusController status = new(time, action => action());
        int changes = 0;
        status.Changed += (_, _) => changes++;

        status.SetSuccess("done");
        Assert.Equal("done", status.Text);
        Assert.Equal(StatusKind.Success, status.Kind);
        Assert.False(status.IsDismissible);
        Assert.Equal(1, changes);

        time.Advance(StatusController.DefaultSuccessAutoClearDelay - TimeSpan.FromMilliseconds(1));
        Assert.Equal("done", status.Text);

        time.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Null(status.Text);
        Assert.Equal(StatusKind.None, status.Kind);
        Assert.Equal(2, changes);
        Assert.Equal(0, time.ActiveTimers);
    }

    [Fact]
    public void A_failure_sticks_until_dismissed()
    {
        FakeTimeProvider time = new();
        using StatusController status = new(time, action => action());

        status.SetFailure("broken");
        Assert.True(status.IsDismissible);
        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal("broken", status.Text);
        Assert.Equal(StatusKind.Failure, status.Kind);

        status.Dismiss();
        Assert.Null(status.Text);
        Assert.Equal(StatusKind.None, status.Kind);
        Assert.False(status.HasText);
    }

    [Fact]
    public void A_new_message_cancels_the_pending_clear()
    {
        FakeTimeProvider time = new();
        using StatusController status = new(time, action => action());

        status.SetSuccess("first");
        time.Advance(TimeSpan.FromSeconds(5));
        status.SetWarning("second");
        Assert.Equal(1, time.ActiveTimers);

        // The first message's clear at six seconds must not take the second one with it.
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal("second", status.Text);
        Assert.Equal(StatusKind.Warning, status.Kind);

        // The warning clears on its own, longer, delay counted from its own arrival.
        time.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal("second", status.Text);
        time.Advance(TimeSpan.FromSeconds(1.5));
        Assert.Null(status.Text);
    }

    [Fact]
    public void Active_and_state_messages_stick_until_replaced()
    {
        FakeTimeProvider time = new();
        using StatusController status = new(time, action => action());

        status.SetActive("working");
        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(StatusKind.Active, status.Kind);
        Assert.Equal(0, time.ActiveTimers);

        status.SetState("Ready");
        time.Advance(TimeSpan.FromHours(1));
        Assert.Equal("Ready", status.Text);
        Assert.Equal(StatusKind.State, status.Kind);

        status.SetSuccess("saved");
        Assert.Equal(StatusKind.Success, status.Kind);
        Assert.Equal(1, time.ActiveTimers);
    }

    [Fact]
    public void Disposing_clears_and_stops_the_clock_work()
    {
        FakeTimeProvider time = new();
        StatusController status = new(time, action => action());
        status.SetSuccess("done");
        Assert.Equal(1, time.ActiveTimers);

        status.Dispose();
        Assert.Null(status.Text);
        Assert.Equal(0, time.ActiveTimers);
        Assert.Throws<ObjectDisposedException>(() => status.SetSuccess("again"));
    }
}
