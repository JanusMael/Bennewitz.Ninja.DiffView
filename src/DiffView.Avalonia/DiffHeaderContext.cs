using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// What a header's context menu is about: a side and its file, with no line at all.
/// </summary>
/// <remarks>
/// <para>
/// A <em>sibling</em> of <see cref="DiffPaneContext"/> rather than another region of it. Plan
/// 00010 decided that giving another surface a menu later must not change
/// <see cref="DiffPaneContext"/>, and for the margins, the connector and the map that holds
/// exactly: each is line-, row- or block-shaped, and the type already carries all three.
/// </para>
/// <para>
/// It does not hold here. <see cref="DiffPaneContext.LineNumber"/> is a non-nullable <c>int</c>
/// and a header has no line to put in it. A synthetic one — <c>0</c>, or line 1 — is a lie a
/// consumer reads as truth, and the whole point of a context object is that a host can act on it
/// without guessing; relaxing the field to <c>int?</c> charges the five regions that <em>do</em>
/// have a line for the one that does not. The header's subject genuinely differs in kind: its
/// verbs are the file's — save, revert — not the line's.
/// </para>
/// <para>
/// Both types go through the one menu implementation, so what differs is the context and never
/// the menu's behaviour. Like <see cref="DiffPaneContext"/> this is a snapshot taken as the menu
/// opens, not a live view.
/// </para>
/// </remarks>
/// <param name="Side">
/// The side this header shows. Non-nullable, unlike <see cref="DiffPaneContext.Side"/>: a header
/// always belongs to one file. The unified view raises no header menu at all — it is read-only,
/// so it has none of the file verbs a header offers — which is why there is no case here of a
/// header without a side.
/// </param>
/// <param name="Title">The title on screen: the source's title, its file name, or the side's name.</param>
/// <param name="Detail">
/// The detail line — line count, encoding, line endings, size — or <c>null</c> where the header
/// shows none.
/// </param>
/// <param name="IsDirty">Whether this side holds edits that are not on disk.</param>
/// <param name="IsReadOnly">Whether this side refuses typing.</param>
public sealed record DiffHeaderContext(
    DiffSide Side,
    string Title,
    string? Detail,
    bool IsDirty,
    bool IsReadOnly);
