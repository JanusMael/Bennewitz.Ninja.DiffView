namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// A pane's context menu is about to open: what was clicked, and the items that will be shown,
/// which the handler may change in place.
/// </summary>
/// <remarks>
/// This is the *amend* shape — our menu, plus the host's. The other shape is the view's
/// <c>PaneContextMenu</c> property, which replaces the menu outright and suppresses this event,
/// because there is then nothing of ours to amend. Either alone would force the wrong shape on
/// half the hosts.
/// </remarks>
public sealed class DiffPaneContextMenuEventArgs : EventArgs
{
    internal DiffPaneContextMenuEventArgs(DiffPaneContext context, IList<DiffMenuItem> items)
    {
        Context = context;
        Items = items;
    }

    /// <summary>What was under the pointer, or under the caret when the keyboard asked.</summary>
    public DiffPaneContext Context { get; }

    /// <summary>
    /// The items about to be shown, in order, ready to be inserted into, removed from or
    /// retitled. Left empty, no menu opens.
    /// </summary>
    public IList<DiffMenuItem> Items { get; }

    /// <summary>Set to suppress the menu entirely for this click.</summary>
    public bool Cancel { get; set; }
}
