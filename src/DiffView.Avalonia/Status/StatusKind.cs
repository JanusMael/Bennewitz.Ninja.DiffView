namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The severity of a transient status message, which decides its colours, its glyph and its
/// lifecycle. Lifted from ClaudeForge's <c>StatusKind</c> (MIT).
/// </summary>
public enum StatusKind
{
    /// <summary>Nothing to show.</summary>
    None = 0,

    /// <summary>An operation in flight ("Building…"); sticks until the operation reports its outcome.</summary>
    Active = 1,

    /// <summary>A confirmation; clears itself after <see cref="StatusController.SuccessAutoClearDelay"/>.</summary>
    Success = 2,

    /// <summary>A notice worth a longer look; clears itself after <see cref="StatusController.WarningAutoClearDelay"/>.</summary>
    Warning = 3,

    /// <summary>A failure; sticks until the user dismisses it or the next message replaces it.</summary>
    Failure = 4,

    /// <summary>Quiet identity text with no pill; sticks until replaced.</summary>
    State = 5,
}
