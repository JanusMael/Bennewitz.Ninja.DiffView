namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The verbs the diff controls bind keys to. Closed on purpose: it names <em>this</em> control's
/// commands, and a host's own commands stay the host's — it adds its own
/// <see cref="Avalonia.Input.KeyBinding"/> beside these, which
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

    /// <summary>
    /// Make a change block the current one. Unbound by default, and the verb the connector's own
    /// left-click already is: a menu entry carries the block under the pointer, and a gesture —
    /// if a host binds one — means the block the caret is in, which is the only reading a
    /// keyboard has.
    /// </summary>
    GoToChange,

    /// <summary>
    /// Select a change block's lines. Unbound by default, by the same rule: a menu entry carries
    /// the block under the pointer, a gesture the current one. From the connector both panes
    /// select, because the block spans both files and picking a side there would be arbitrary.
    /// </summary>
    SelectBlock,

    /// <summary>
    /// Show every row, folding nothing — <c>UnchangedContextRows</c> set to <c>null</c>. Beyond
    /// Compare's <i>Show All</i>. Unbound by default: it is a view option, and this library binds
    /// no gesture a host has not asked for.
    /// </summary>
    ShowAllRows,

    /// <summary>
    /// Fold every unchanged run, keeping no context — <c>UnchangedContextRows</c> set to
    /// <c>0</c>. Beyond Compare's <i>Show Differences</i>. Unbound by default.
    /// </summary>
    ShowDifferencesOnly,

    /// <summary>
    /// Fold the unchanged runs but keep a few rows around every change —
    /// <c>UnchangedContextRows</c> set to <see cref="DiffKeyMap.DefaultContextRows"/>. Beyond
    /// Compare's <i>Show Context</i>. Unbound by default.
    /// </summary>
    ShowContext,

    /// <summary>
    /// Give back the run the caret is inside, leaving the rest folded. The pointer verb is a
    /// click on the placeholder itself; a gesture means the run at the caret, which is the only
    /// reading a keyboard has. Unbound by default.
    /// </summary>
    ExpandFold,
}
