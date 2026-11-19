using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Tests.Presenter;

/// <summary>
/// Counts a text view's scroll-offset changes, step by step, and stops a series of them that runs
/// away, so that a regression fails its test instead of overflowing the stack and taking the test
/// host down with every test after it.
/// </summary>
/// <remarks>
/// ⛔ <b>It listens through AvaloniaEdit's weak event manager, and that is what lets it stop anything.</b>
/// The manager subscribes to a view once, when the first weak listener arrives — the selection layer,
/// in <c>TextArea</c>'s constructor — and that is before the text area attaches its selection handler,
/// whose own handler of the same event re-extends the selection and brings the caret into view again.
/// So this runs ahead of that handler at every level of the recursion, and a throw from here unwinds
/// it. Subscribed to the event directly it would run after that handler, which never returns.
/// </remarks>
internal sealed class ScrollTripwire : IDisposable
{
    /// <summary>Changes in one step past which the step is running away rather than scrolling.</summary>
    public const int Limit = 64;

    /// <summary>
    /// Changes one step may make: a bring-into-view, and one before it — a wheel's own scroll, or a bring-into-view
    /// that moved the caret to another line.
    /// </summary>
    public const int Settled = 2;

    private readonly TextView _view;
    private int _count;

    public ScrollTripwire(TextView view)
    {
        _view = view;
        TextViewWeakEventManager.ScrollOffsetChanged.AddHandler(view, OnScrollOffsetChanged);
    }

    /// <summary>Asserts the step since the last call scrolled the view no more than a settled step does.</summary>
    public void AssertSettled(string step)
    {
        int count = _count;
        _count = 0;
        Assert.True(
            count <= Settled,
            $"{step} changed the scroll offset {count} times; a step that brings the caret into view moves it at most {Settled}.");
    }

    public void Dispose()
    {
        TextViewWeakEventManager.ScrollOffsetChanged.RemoveHandler(_view, OnScrollOffsetChanged);
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        _count++;
        if (_count > Limit)
        {
            throw new InvalidOperationException(
                $"The scroll offset changed {_count} times in one step: bringing the caret into view is "
                + "scrolling the pane back and forth, and would until the stack overflowed.");
        }
    }
}
