namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// A header's context menu is about to open: which side and its file, and the items that will be
/// shown, which the handler may change in place.
/// </summary>
/// <remarks>
/// The header's own pair of the two shapes <see cref="DiffPaneContextMenuEventArgs"/> describes —
/// this is the <em>amend</em> one, and the view's <c>HeaderContextMenu</c> property is the
/// replacement, which suppresses this event because there is then nothing of ours to amend. A
/// separate event rather than a shared one because the contexts differ in kind: a handler that
/// had to test which it was handed would be doing the work this type does.
/// </remarks>
public sealed class DiffHeaderContextMenuEventArgs : EventArgs
{
    internal DiffHeaderContextMenuEventArgs(DiffHeaderContext context, IList<DiffMenuItem> items)
    {
        Context = context;
        Items = items;
    }

    /// <summary>The header that was clicked: its side, its file, and their state.</summary>
    public DiffHeaderContext Context { get; }

    /// <summary>
    /// The items about to be shown, in order, ready to be inserted into, removed from or
    /// retitled. Left empty, no menu opens.
    /// </summary>
    public IList<DiffMenuItem> Items { get; }

    /// <summary>Set to suppress the menu entirely for this click.</summary>
    public bool Cancel { get; set; }
}
