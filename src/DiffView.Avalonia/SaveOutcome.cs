namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// What <see cref="SideBySideDiffView.Save"/> did. A save reports rather than throws: every
/// outcome here is something a host may reasonably meet, and none of them is exceptional.
/// </summary>
public enum SaveOutcome
{
    /// <summary>The file was written.</summary>
    Saved,

    /// <summary>There was nothing to write: the side has no unsaved edits.</summary>
    NotDirty,

    /// <summary>The side came from a string rather than a file, so there is nowhere to write.</summary>
    NoPath,

    /// <summary>
    /// The file changed on disk after it was read. Nothing was written, and the edits are still
    /// in the pane — overwriting someone else's change silently is never the right answer.
    /// </summary>
    ChangedOnDisk,

    /// <summary>The write was attempted and failed; the message says why.</summary>
    Failed,
}
