namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The verbs the diff controls bind keys to. Closed on purpose: it names <em>this</em> control's
/// commands, and a host's own commands stay the host's — it adds its own
/// <see cref="global::Avalonia.Input.KeyBinding"/> beside these, which
/// <see cref="DiffKeyMap"/> does not touch.
/// </summary>
public enum DiffCommand
{
    /// <summary>Move to the next change block.</summary>
    NextChange,

    /// <summary>Move to the previous change block.</summary>
    PreviousChange,

    /// <summary>Move focus between the panes; the unified view has one pane and no default for this.</summary>
    SwitchPane,

    /// <summary>Open the find bar, pre-filled from the selection.</summary>
    OpenFind,

    /// <summary>Move to the next find match.</summary>
    FindNext,

    /// <summary>Move to the previous find match.</summary>
    FindPrevious,

    /// <summary>Close the find bar and hand focus back.</summary>
    CloseFind,

    /// <summary>
    /// Copy leftwards: the selected lines when there is a selection, and the current block
    /// otherwise. That is the rule the gutter applies when a selection arrow takes a block
    /// arrow's cell, and the rule cut, copy and delete follow everywhere.
    /// </summary>
    CopyToLeft,

    /// <summary>Copy rightwards, by the same rule as <see cref="CopyToLeft"/>.</summary>
    CopyToRight,

    /// <summary>
    /// Copy the current <em>block</em> leftwards whatever is selected. Unbound by default: it
    /// exists so a host that wants the pre-plan-00009 behaviour can bind a gesture to it rather
    /// than lose the verb.
    /// </summary>
    CopyBlockToLeft,

    /// <summary>Copy the current block rightwards whatever is selected.</summary>
    CopyBlockToRight,
}
