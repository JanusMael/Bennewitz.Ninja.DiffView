using System.Collections;
using Avalonia.Input;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// What key each <see cref="DiffCommand"/> is on. A host rebinds a command by assigning its
/// gesture, unbinds it with <c>null</c>, and binds one that has no default the same way; the
/// control rebuilds its key bindings on <see cref="Changed"/>.
/// </summary>
/// <remarks>
/// Keyed by command, so one command cannot hold two gestures. The reverse <em>is</em> expressible:
/// two commands may share a gesture, which the control logs rather than refuses — refusing a
/// binding a host asked for, or dropping one silently, would both be worse than saying so.
/// </remarks>
public sealed class DiffKeyMap : IEnumerable<KeyValuePair<DiffCommand, KeyGesture?>>
{
    private readonly Dictionary<DiffCommand, KeyGesture?> _gestures = [];

    /// <summary>An empty map: every command unbound.</summary>
    public DiffKeyMap()
    {
    }

    private DiffKeyMap(IEnumerable<KeyValuePair<DiffCommand, KeyGesture?>> gestures)
    {
        foreach ((DiffCommand command, KeyGesture? gesture) in gestures)
        {
            _gestures[command] = gesture;
        }
    }

    /// <summary>An entry changed; the control rebuilds the bindings it owns.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The side-by-side view's defaults — the bindings that were hard-coded before this map
    /// existed, plus the two block-always copies, which are deliberately unbound.
    /// </summary>
    public static DiffKeyMap Default()
    {
        return new DiffKeyMap
        {
            [DiffCommand.NextChange] = new KeyGesture(Key.F7),
            [DiffCommand.PreviousChange] = new KeyGesture(Key.F7, KeyModifiers.Shift),
            [DiffCommand.SwitchPane] = new KeyGesture(Key.F6),
            [DiffCommand.OpenFind] = new KeyGesture(Key.F, KeyModifiers.Control),
            [DiffCommand.FindNext] = new KeyGesture(Key.F3),
            [DiffCommand.FindPrevious] = new KeyGesture(Key.F3, KeyModifiers.Shift),
            [DiffCommand.CloseFind] = new KeyGesture(Key.Escape),
            // The arrow points the way the text travels, as the gutter's arrows do.
            [DiffCommand.CopyToLeft] = new KeyGesture(Key.Left, KeyModifiers.Alt),
            [DiffCommand.CopyToRight] = new KeyGesture(Key.Right, KeyModifiers.Alt),
            [DiffCommand.CopyBlockToLeft] = null,
            [DiffCommand.CopyBlockToRight] = null,
            // Both are pointer verbs first: a menu entry carries the block that was clicked. A
            // gesture is still expressible — it would mean the current block — so they are here
            // and unbound rather than absent, which is how a host binds one.
            [DiffCommand.GoToChange] = null,
            [DiffCommand.SelectBlock] = null,
            // The three folding modes and the one that opens a run: view options and a pointer
            // verb, so they are here and unbound rather than absent, which is how a host binds one.
            [DiffCommand.ShowAllRows] = null,
            [DiffCommand.ShowDifferencesOnly] = null,
            [DiffCommand.ShowContext] = null,
            [DiffCommand.ExpandFold] = null,
        };
    }

    /// <summary>
    /// The context <see cref="DiffCommand.ShowContext"/> asks for. Three rows either side, which
    /// is what <c>diff -u</c> has shown since 1990 and what a reader's eye is used to.
    /// </summary>
    public const int DefaultContextRows = 3;

    /// <summary>
    /// The unified view's defaults: one pane and one document, so there is no pane to switch to
    /// and no other side to copy to. The same type with a smaller default, rather than a second
    /// implementation to drift from this one.
    /// </summary>
    public static DiffKeyMap UnifiedDefault()
    {
        DiffKeyMap map = Default();
        foreach (DiffCommand command in (DiffCommand[])
                 [
                     DiffCommand.SwitchPane,
                     DiffCommand.CopyToLeft,
                     DiffCommand.CopyToRight,
                     DiffCommand.CopyBlockToLeft,
                     DiffCommand.CopyBlockToRight,
                 ])
        {
            map[command] = null;
        }

        return map;
    }

    /// <summary>The gesture <paramref name="command"/> is on, or <c>null</c> when it is unbound.</summary>
    public KeyGesture? this[DiffCommand command]
    {
        get => _gestures.GetValueOrDefault(command);
        set
        {
            if (_gestures.TryGetValue(command, out KeyGesture? existing) && Equals(existing, value))
            {
                return;
            }

            _gestures[command] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The commands this map has an entry for, bound or not.</summary>
    public IEnumerable<DiffCommand> Commands => _gestures.Keys;

    /// <summary>A copy that raises nothing, for a host that wants to compare or restore.</summary>
    public DiffKeyMap Clone() => new(_gestures);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<DiffCommand, KeyGesture?>> GetEnumerator() => _gestures.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
