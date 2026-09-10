using System.Windows.Input;
using Avalonia.Input;
using Microsoft.Extensions.Logging;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The one implementation of putting a <see cref="DiffKeyMap"/>'s gestures into a control's
/// <see cref="InputElement.KeyBindings"/>, held by both views. Two rules live here and nowhere
/// else — replace only the bindings this created, and log a gesture that lands on two commands —
/// and §7's standing complaint is that a second implementation is where they would drift apart.
/// </summary>
internal sealed class DiffKeyBindings
{
    /// <summary>
    /// What this put in the collection. Only these are replaced: <see cref="InputElement.KeyBindings"/>
    /// is public, so a host may have added its own, and rebuilding it wholesale would delete that
    /// silently — the failure a consumer finds rather than we do.
    /// </summary>
    private readonly List<KeyBinding> _owned = [];

    private readonly InputElement _target;
    private readonly Func<DiffCommand, ICommand?> _resolve;

    /// <summary>
    /// Binds into <paramref name="target"/>'s collection, asking <paramref name="resolve"/> for the
    /// command behind each verb. A resolver answering <c>null</c> says the control has no such
    /// verb — the unified view and the two-sided commands.
    /// </summary>
    public DiffKeyBindings(InputElement target, Func<DiffCommand, ICommand?> resolve)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolve);
        _target = target;
        _resolve = resolve;
    }

    /// <summary>
    /// Replaces the bindings this owns with <paramref name="map"/>'s and leaves every other entry
    /// where it is. A command the resolver has nothing for is skipped and logged: binding its key
    /// to nothing would leave the key dead with no explanation.
    /// </summary>
    public void Rebuild(DiffKeyMap map, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(map);

        foreach (KeyBinding owned in _owned)
        {
            _target.KeyBindings.Remove(owned);
        }

        _owned.Clear();

        Dictionary<KeyGesture, DiffCommand> seen = [];
        foreach (DiffCommand command in map.Commands.Order())
        {
            if (map[command] is not { } gesture)
            {
                continue;
            }

            if (_resolve(command) is not { } bound)
            {
                DiffViewLog.KeyCommandUnsupported(logger, command.ToString(), gesture.ToString());
                continue;
            }

            if (seen.TryGetValue(gesture, out DiffCommand already))
            {
                // Avalonia decides which of the two fires. Refusing the binding, or dropping one
                // without a word, would each be worse than saying so.
                DiffViewLog.KeyGestureConflict(logger, gesture.ToString(), already.ToString(), command.ToString());
            }
            else
            {
                seen[gesture] = command;
            }

            KeyBinding binding = new() { Gesture = gesture, Command = bound };
            _owned.Add(binding);
            _target.KeyBindings.Add(binding);
        }
    }
}
