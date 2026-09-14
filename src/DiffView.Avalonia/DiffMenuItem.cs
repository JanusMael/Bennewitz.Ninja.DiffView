using System.Windows.Input;
using Avalonia.Input;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// One entry in a pane's context menu, before it becomes an Avalonia <c>MenuItem</c>. A plain
/// mutable object rather than a control, so that a host amending the list through
/// <see cref="DiffPaneContextMenuEventArgs"/> retitles or disables an item without needing to know
/// how it will be templated.
/// </summary>
public sealed class DiffMenuItem
{
    /// <summary>A separator, which carries nothing else.</summary>
    public static DiffMenuItem Separator() => new() { IsSeparator = true };

    /// <summary>The text shown. Null on a separator.</summary>
    public string? Header { get; set; }

    /// <summary>What the item does.</summary>
    public ICommand? Command { get; set; }

    /// <summary>The argument passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get; set; }

    /// <summary>
    /// The accelerator shown beside the header. Written from <c>GestureFor</c> for the control's
    /// own items, never typed in — a literal would start lying the first time a host rebinds.
    /// </summary>
    public KeyGesture? Gesture { get; set; }

    /// <summary>
    /// The icon, in a column that is laid out whether or not anything fills it. Null throughout
    /// v1: the column is reserved now because adding it later would move every label.
    /// </summary>
    public object? Icon { get; set; }

    /// <summary>
    /// Whether the item can be invoked. **Disabled, not removed** — the menu keeps one shape so
    /// that a host's "insert after this item" survives a change of state.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether this is a separator rather than an entry.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>What a screen reader announces; falls back to <see cref="Header"/>.</summary>
    public string? AutomationName { get; set; }

    /// <summary>
    /// The verb this item is, for a host that wants to find one without matching on its text.
    /// Null on a separator and on an item a host added, which carries its own command.
    /// </summary>
    public DiffCommand? Verb { get; set; }

    /// <summary>Child entries; empty for a leaf.</summary>
    public IList<DiffMenuItem> Items { get; } = [];
}
