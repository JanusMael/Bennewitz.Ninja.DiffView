using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.Editing;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// A pane's text area: AvaloniaEdit's, built over a <see cref="DiffTextView"/>. It exists because a text
/// area takes a view of its own only through AvaloniaEdit's protected constructor, which only a subclass
/// can call; it changes nothing else.
/// </summary>
/// <remarks>
/// Its style key stays AvaloniaEdit's, so every style and theme written for a <see cref="TextArea"/> —
/// <see cref="DiffPanePresenter.TextAreaThemeKey"/> among them — still applies.
/// </remarks>
internal sealed class DiffTextArea : TextArea
{
    public DiffTextArea()
        : base(new DiffTextView())
    {
        ((DiffTextView)TextView).Caret = Caret;

        // TextArea's public constructor adds these two and the protected one does not: each offers a key
        // to the stacked input handlers before the text area handles it, as AvaloniaEdit's own handlers do.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
    }

    protected override Type StyleKeyOverride => typeof(TextArea);

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        foreach (TextAreaStackedInputHandler handler in StackedInputHandlers)
        {
            if (e.Handled)
            {
                break;
            }

            handler.OnPreviewKeyDown(e);
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        foreach (TextAreaStackedInputHandler handler in StackedInputHandlers)
        {
            if (e.Handled)
            {
                break;
            }

            handler.OnPreviewKeyUp(e);
        }
    }
}
