using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The one implementation of turning a <see cref="DiffPaneContext"/> and a list of
/// <see cref="DiffMenuItem"/> into an open menu, held by both views. The placement rule and the
/// disabled-item rule live here and nowhere else — a second copy is where they would drift apart,
/// which is what <c>AGENTS.md</c> §7 keeps saying.
/// </summary>
internal static class DiffPaneMenu
{
    /// <summary>
    /// The gap held between a menu row's label and its accelerator. Small and deliberate: enough
    /// that the two never touch on the widest row, not so much that it fights a theme with a
    /// generous margin of its own.
    /// </summary>
    internal static readonly Thickness HeaderGap = new(0, 0, 12, 0);

    /// <summary>
    /// Answers a pane's request: opens <paramref name="replacement"/> if a host set one, and
    /// otherwise builds the control's own items, lets <paramref name="opening"/> amend them, and
    /// opens that. **The replacement suppresses the event** — there is nothing of ours to amend —
    /// and that rule lives here so the two views cannot come to disagree about it.
    /// </summary>
    /// <returns>The menu that was opened, or <c>null</c> when none was — cancelled, or empty.</returns>
    /// <remarks>
    /// <paramref name="owner"/> is whichever control was right-clicked — a pane, and since plan
    /// 00012 phase 2 the connector gutter or the overview map. The menu is placed against it and
    /// opened on it, so a menu about the map does not anchor itself to a pane the pointer was
    /// never over. Only a pane can be asked from the keyboard, which is the one path that needs
    /// a caret.
    /// </remarks>
    public static ContextMenu? Request(
        Control owner,
        DiffPaneContext context,
        Point? pointer,
        ContextMenu? replacement,
        Func<DiffPaneContext, List<DiffMenuItem>> defaults,
        Action<DiffPaneContextMenuEventArgs> opening)
    {
        if (replacement is not null)
        {
            return Show(owner, context, replacement, [], pointer);
        }

        List<DiffMenuItem> items = defaults(context);
        DiffPaneContextMenuEventArgs args = new(context, items);
        opening(args);
        if (args.Cancel)
        {
            return null;
        }

        return Show(owner, context, null, items, pointer);
    }

    /// <summary>
    /// Opens <paramref name="replacement"/> if there is one, and otherwise a menu built from
    /// <paramref name="items"/>; an empty list opens nothing. Either way the context reaches the
    /// menu as its <c>DataContext</c>, which is how a host's own XAML binds to what was clicked —
    /// the replacement shape has no opening event to carry it.
    /// </summary>
    private static ContextMenu? Show(
        Control owner,
        DiffPaneContext context,
        ContextMenu? replacement,
        IList<DiffMenuItem> items,
        Point? pointer)
    {
        ContextMenu? menu = replacement;
        if (menu is null)
        {
            if (items.Count == 0)
            {
                return null;
            }

            menu = new ContextMenu();
            foreach (DiffMenuItem item in items)
            {
                menu.Items.Add(Build(item));
            }
        }

        menu.DataContext = context;
        menu.PlacementTarget = owner;

        if (pointer is { } point)
        {
            // Where the pointer is. `PlacementMode.Pointer` would read the live pointer position,
            // which is the same thing for a click and the wrong thing for a keyboard request.
            menu.Placement = PlacementMode.AnchorAndGravity;
            menu.PlacementAnchor = PopupAnchor.TopLeft;
            menu.PlacementGravity = PopupGravity.BottomRight;
            menu.PlacementRect = new Rect(point, new Size(1, 1));
        }
        else
        {
            // Shift+F10 and the Menu key: a keyboard user asking about "here" means the caret.
            // Only a pane has one — the connector and the map are not focusable, so no keyboard
            // request can reach them — and a control without one is asked about at its origin.
            menu.Placement = PlacementMode.AnchorAndGravity;
            menu.PlacementAnchor = PopupAnchor.TopLeft;
            menu.PlacementGravity = PopupGravity.BottomRight;
            menu.PlacementRect = owner is DiffPanePresenter pane ? CaretRect(pane) : new Rect(default, new Size(1, 1));
        }

        menu.Open(owner);
        return menu;
    }

    /// <summary>
    /// An item for one of the control's own verbs, or <c>null</c> where this view has no such
    /// verb — which is the **absent, not disabled** half of the rule. A greyed entry promises a
    /// state in which it would work, and for the unified view's copies there is none.
    /// <paramref name="command"/> is the view's <c>CommandOrNull</c>, so the menu and the key map
    /// cannot come to different answers about what exists.
    /// </summary>
    public static DiffMenuItem? Verb(
        string header,
        DiffCommand verb,
        Func<DiffCommand, ICommand?> command,
        Func<DiffCommand, KeyGesture?> gesture,
        bool enabled)
    {
        if (command(verb) is not { } bound)
        {
            return null;
        }

        return new DiffMenuItem
        {
            Header = header,
            Verb = verb,
            Command = bound,
            Gesture = gesture(verb),
            IsEnabled = enabled,
        };
    }

    /// <summary>
    /// The entries both views carry — navigate, and find — appended in order with the separator
    /// between them. Shared rather than written twice, for §7's usual reason.
    /// </summary>
    public static void AddNavigation(
        List<DiffMenuItem> items,
        Func<DiffCommand, ICommand?> command,
        Func<DiffCommand, KeyGesture?> gesture,
        int changeCount)
    {
        Add(items, Verb(DiffViewStrings.Get(DiffViewStrings.MenuNextChange), DiffCommand.NextChange, command, gesture, changeCount > 0));
        Add(items, Verb(DiffViewStrings.Get(DiffViewStrings.MenuPreviousChange), DiffCommand.PreviousChange, command, gesture, changeCount > 0));
        items.Add(DiffMenuItem.Separator());
        Add(items, Verb(DiffViewStrings.Get(DiffViewStrings.MenuFind), DiffCommand.OpenFind, command, gesture, enabled: true));
    }

    /// <summary>Appends <paramref name="item"/> unless it is <c>null</c>, which means absent.</summary>
    public static void Add(List<DiffMenuItem> items, DiffMenuItem? item)
    {
        if (item is not null)
        {
            items.Add(item);
        }
    }

    /// <summary>The caret's box in the pane's own coordinates; the pane's origin where unknown.</summary>
    private static Rect CaretRect(DiffPanePresenter pane)
    {
        Rect caret = pane.TextArea.Caret.CalculateCaretRectangle();
        if (caret == default)
        {
            return new Rect(default, new Size(1, 1));
        }

        // The caret's rectangle is measured from the top of the *document*, so the scroll offset
        // comes off before it can be translated out of the text view.
        Rect scrolled = caret.Translate(new Vector(-pane.TextArea.TextView.HorizontalOffset, -pane.TextArea.TextView.VerticalOffset));
        Point? origin = pane.TextArea.TextView.TranslatePoint(scrolled.Position, pane);
        return origin is { } point ? new Rect(point, scrolled.Size) : new Rect(default, new Size(1, 1));
    }

    private static object Build(DiffMenuItem item)
    {
        if (item.IsSeparator)
        {
            return new Separator();
        }

        // The header is a TextBlock rather than the bare string so it can carry a small right
        // margin. Without one the label and the accelerator touch on whichever row is widest —
        // that row sets the popup's width, so its own gesture has nowhere to sit — and how tight
        // that looks is otherwise entirely the host theme's `MenuItemInputGestureTextMargin`,
        // which Semi ships at 4 and Fluent at 24. This guarantees the gap without taking the
        // theme's choice away: a host's own value is added to it.
        MenuItem menuItem = new()
        {
            Header = new TextBlock { Text = item.Header, Margin = HeaderGap },
            InputGesture = item.Gesture,
            Icon = item.Icon,
        };

        // A disabled item is given no command at all, rather than a command and an `IsEnabled` the
        // command's own `CanExecute` would then argue with.
        if (item.IsEnabled)
        {
            menuItem.Command = item.Command;
            menuItem.CommandParameter = item.CommandParameter;
        }
        else
        {
            menuItem.IsEnabled = false;
        }

        AutomationProperties.SetName(menuItem, item.AutomationName ?? item.Header ?? string.Empty);

        foreach (DiffMenuItem child in item.Items)
        {
            menuItem.Items.Add(Build(child));
        }

        return menuItem;
    }
}
