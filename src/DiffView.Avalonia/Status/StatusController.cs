using Avalonia.Threading;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Owns the status strip's transient message and its lifecycle. Lifted from ClaudeForge's
/// <c>StatusController</c> (MIT) and moved onto <see cref="TimeProvider"/>: a
/// <see cref="StatusKind.Success"/> message clears itself after
/// <see cref="SuccessAutoClearDelay"/>, a <see cref="StatusKind.Warning"/> after
/// <see cref="WarningAutoClearDelay"/>, a <see cref="StatusKind.Failure"/> sticks until
/// <see cref="Dismiss"/> or the next message, <see cref="StatusKind.Active"/> and
/// <see cref="StatusKind.State"/> stick until replaced. The typed <c>Set…</c> helpers are the
/// only way to emit, so the severity is explicit at every call site; a new message cancels the
/// pending clear of the previous one. Timer callbacks are marshalled to the UI thread.
/// </summary>
public sealed class StatusController : IDisposable
{
    /// <summary>How long a success message stays by default.</summary>
    public static readonly TimeSpan DefaultSuccessAutoClearDelay = TimeSpan.FromSeconds(6);

    /// <summary>How long a warning stays by default: longer, because "nothing to do" deserves a second look.</summary>
    public static readonly TimeSpan DefaultWarningAutoClearDelay = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _timeProvider;
    private readonly Action<Action> _dispatch;
    private ITimer? _pendingClear;
    private object? _pendingClearToken;
    private bool _disposed;

    /// <param name="timeProvider">The clock the auto-clear delays run on.</param>
    /// <param name="dispatch">
    /// Marshals a timer callback to the UI thread. The default runs inline when already on
    /// Avalonia's UI thread and posts to it otherwise.
    /// </param>
    public StatusController(TimeProvider timeProvider, Action<Action>? dispatch = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _dispatch = dispatch ?? DispatchToUiThread;
    }

    /// <summary>The message or its kind changed.</summary>
    public event EventHandler? Changed;

    /// <summary>How long a success message stays.</summary>
    public TimeSpan SuccessAutoClearDelay { get; set; } = DefaultSuccessAutoClearDelay;

    /// <summary>How long a warning stays.</summary>
    public TimeSpan WarningAutoClearDelay { get; set; } = DefaultWarningAutoClearDelay;

    /// <summary>The visible text, or <c>null</c> when nothing is shown.</summary>
    public string? Text { get; private set; }

    /// <summary>The severity of <see cref="Text"/>; <see cref="StatusKind.None"/> when nothing is shown.</summary>
    public StatusKind Kind { get; private set; }

    /// <summary>Whether there is a message to show.</summary>
    public bool HasText => !string.IsNullOrEmpty(Text);

    /// <summary>Whether the message shows a dismiss control: only a failure does.</summary>
    public bool IsDismissible => Kind == StatusKind.Failure;

    /// <summary>An operation in flight; sticks until the next message.</summary>
    public void SetActive(string text) => Set(text, StatusKind.Active);

    /// <summary>A confirmation; clears itself after <see cref="SuccessAutoClearDelay"/>.</summary>
    public void SetSuccess(string text) => Set(text, StatusKind.Success);

    /// <summary>A notice; clears itself after <see cref="WarningAutoClearDelay"/>.</summary>
    public void SetWarning(string text) => Set(text, StatusKind.Warning);

    /// <summary>A failure; sticks until dismissed or replaced.</summary>
    public void SetFailure(string text) => Set(text, StatusKind.Failure);

    /// <summary>Quiet identity text; sticks until replaced.</summary>
    public void SetState(string text) => Set(text, StatusKind.State);

    /// <summary>Clears the message and cancels any pending clear.</summary>
    public void Dismiss()
    {
        CancelPendingClear();
        Apply(null, StatusKind.None);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingClear();
        Apply(null, StatusKind.None);
    }

    private static void DispatchToUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            Dispatcher.UIThread.Post(action);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The dispatcher has shut down under a pending clear; there is no UI left to update.
            _ = ex;
        }
    }

    private void Set(string text, StatusKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelPendingClear();
        Apply(text.Length == 0 ? null : text, text.Length == 0 ? StatusKind.None : kind);

        TimeSpan? clearAfter = Kind switch
        {
            StatusKind.Success => SuccessAutoClearDelay,
            StatusKind.Warning => WarningAutoClearDelay,
            _ => null,
        };
        if (clearAfter is { } delay)
        {
            // The token identifies the message this clear belongs to. It is created before the
            // timer because the callback needs something to compare against, and the timer
            // cannot be its own state.
            object token = new();
            _pendingClearToken = token;
            _pendingClear = _timeProvider.CreateTimer(OnClearDue, token, delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnClearDue(object? state)
    {
        _dispatch(() =>
        {
            // The clear belongs to the message that scheduled it. A timer that came due before
            // this dispatch ran has already been replaced by a later message, and clearing then
            // would take that message with it — so the token, not merely "is one pending", is
            // what decides.
            if (_disposed || !ReferenceEquals(_pendingClearToken, state))
            {
                return;
            }

            CancelPendingClear();
            Apply(null, StatusKind.None);
        });
    }

    private void CancelPendingClear()
    {
        _pendingClear?.Dispose();
        _pendingClear = null;
        _pendingClearToken = null;
    }

    private void Apply(string? text, StatusKind kind)
    {
        if (Text == text && Kind == kind)
        {
            return;
        }

        Text = text;
        Kind = kind;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
