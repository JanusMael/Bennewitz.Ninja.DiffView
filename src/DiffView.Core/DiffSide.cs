namespace Bennewitz.Ninja.DiffView.Core;

/// <summary>
/// Names one side of a comparison. Used wherever the API refers to a side; nothing is
/// called "old" or "new", because a side that becomes editable is neither.
/// </summary>
public enum DiffSide
{
    /// <summary>The left pane.</summary>
    Left = 0,

    /// <summary>The right pane.</summary>
    Right = 1,
}
