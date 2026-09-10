using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;

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
    /// Answers a pane's request: opens <paramref name="replacement"/> if a host set one, and
    /// otherwise builds the control's own items, lets <paramref name="opening"/> amend them, and
    /// opens that. **The replacement suppresses the event** — there is nothing of ours to amend —
    /// and that rule lives here so the two views cannot come to disagree about it.
    /// </summary>
    /// <returns>The menu that was opened, or <c>null</c> when none was — cancelled, or empty.</returns>
    public static ContextMenu? Request(
        DiffPanePresenter pane,
        DiffPaneContext context,
        Point? pointer,
        ContextMenu? replacement,
        Func<DiffPaneContext, List<DiffMenuItem>> defaults,
        Action<DiffPaneContextMenuEventArgs> opening)
    {
        if (replacement is not null)
        {
            return Show(pane, context, replacement, [], pointer);
        }

        List<DiffMenuItem> items = defaults(context);
        DiffPaneContextMenuEventArgs args = new(context, items);
        opening(args);
        if (args.Cancel)
        {
            return null;
        }

        return Show(pane, context, null, items, pointer);
    }

    /// <summary>
    /// Opens <paramref name="replacement"/> if there is one, and otherwise a menu built from
    /// <paramref name="items"/>; an empty list opens nothing. Either way the context reaches the
    /// menu as its <c>DataContext</c>, which is how a host's own XAML binds to what was clicked —
    /// the replacement shape has no opening event to carry it.
    /// </summary>
    private static ContextMenu? Show(
        DiffPanePresenter pane,
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
        menu.PlacementTarget = pane;

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
            menu.Placement = PlacementMode.AnchorAndGravity;
            menu.PlacementAnchor = PopupAnchor.TopLeft;
            menu.PlacementGravity = PopupGravity.BottomRight;
            menu.PlacementRect = CaretRect(pane);
        }

        menu.Open(pane);
        return menu;
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

        MenuItem menuItem = new()
        {
            Header = item.Header,
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
