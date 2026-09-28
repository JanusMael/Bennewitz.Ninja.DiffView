using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;
using Serilog;

namespace Bennewitz.Ninja.DiffView.Demo;

/// <summary>
/// Plan 00029: what the demo's user did, written to the demo's own log at <c>Information</c> — the
/// level it keeps — so a bug found by hand can be read back instead of reconstructed.
/// </summary>
/// <remarks>
/// <para>
/// The library logs the two facts nothing public reports — where a click in a pane landed, with the
/// selection it finished, and folds opening — under <see cref="DiffViewLogCategories.Interaction"/>,
/// which <see cref="DemoLogging"/> switches on. This logs everything else a report needs, through the
/// library's public surface: the machine, the window, the demo's own menu, the commands the views ran,
/// the copies their panes asked for, and the edits.
/// </para>
/// <para>
/// Never document text, in any form: names, positions and counts. A key no command is bound to is
/// typing, so a key is logged only as the command it runs; an edit is its lines and its line counts.
/// </para>
/// </remarks>
internal sealed class ActionLog
{
    /// <summary>How long edits, or a resize, must rest before they are written as one line.</summary>
    internal static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);

    private readonly Window _window;
    private readonly SideBySideDiffView _diff;
    private readonly InlineDiffView _unified;
    private readonly HashSet<DiffPanePresenter> _panes = [];
    private readonly HashSet<TextDocument> _documents = [];
    private readonly Dictionary<DiffPanePresenter, PendingEdit> _edits = [];
    private ITimer? _resize;
    private (int Width, int Height, double Scaling)? _logged;

    /// <summary>
    /// Watches the window, its menu and the two views a user can act in. The viewer is not one of them:
    /// it binds no keys, opens no menu, copies nothing and is never edited, and the clicks and selections
    /// in its panes are the library's to log.
    /// </summary>
    public ActionLog(Window window, Menu menu, SideBySideDiffView diff, InlineDiffView unified)
    {
        _window = window;
        _diff = diff;
        _unified = unified;

        window.Opened += (_, _) => LogWindow();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ClientSizeProperty && window.IsVisible)
            {
                ScheduleWindow();
            }
        };
        window.ScalingChanged += (_, _) => ScheduleWindow();

        // Bubbling up from the item. Avalonia's menu handler toggles a check or radio item before it
        // raises the click, so the state read here is the one the click left.
        menu.AddHandler(MenuItem.ClickEvent, OnMenuClick, RoutingStrategies.Bubble, handledEventsToo: true);

        // On the way down, before a view's key bindings handle the chord.
        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        diff.PaneContextMenuOpening += (_, e) => Watch(e.Items, e.Context);
        unified.PaneContextMenuOpening += (_, e) => Watch(e.Items, e.Context);
        diff.HeaderContextMenuOpening += (_, e) => Watch(e.Items, $"{Lower(e.Context.Side)} header", acted: null);

        // The side-by-side view's two panes only. The unified pane offers no copy, and its document is
        // the control's composition of both sides, rewritten whenever the model changes: never an edit.
        diff.TemplateApplied += (_, e) => AttachPanes(e.NameScope, SideBySideDiffView.LeftPanePart, SideBySideDiffView.RightPanePart);
    }

    /// <summary>The side-by-side view's clock: the system's in the demo, a test's hand-advanced one under test.</summary>
    private TimeProvider Clock => _diff.TimeProvider;

    /// <summary>The machine, once, beside the flags line: its host name — never an address — its OS and its runtime.</summary>
    public static void LogMachine()
    {
        Log.Information(
            "Host: {Host:l} · {OS:l} · {Runtime:l}",
            Environment.MachineName, RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription);
    }

    private void ScheduleWindow()
    {
        _resize?.Dispose();
        _resize = Clock.CreateTimer(
            _ => Dispatcher.UIThread.Post(() =>
            {
                _resize?.Dispose();
                _resize = null;
                LogWindow();
            }),
            state: null,
            Quiet,
            Timeout.InfiniteTimeSpan);
    }

    /// <summary>The window's size and scaling, when they differ from the last written: opening lays the window out, which is not a resize.</summary>
    private void LogWindow()
    {
        (int Width, int Height, double Scaling) now = (
            (int)Math.Round(_window.ClientSize.Width),
            (int)Math.Round(_window.ClientSize.Height),
            _window.RenderScaling);
        if (now == _logged)
        {
            return;
        }

        _logged = now;
        Log.Information(
            "Window: {Width}×{Height} at scaling {Scaling:l}",
            now.Width, now.Height, now.Scaling.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not MenuItem item)
        {
            return;
        }

        string path = string.Join(" ▸ ", PathOf(item));
        if (item.ToggleType == MenuItemToggleType.None)
        {
            Log.Information("Menu: {Path:l}", path);
        }
        else
        {
            Log.Information("Menu: {Path:l} → {State:l}", path, item.IsChecked ? "on" : "off");
        }
    }

    /// <summary>The headers from the menu bar down to <paramref name="item"/>, without their access-key markers.</summary>
    private static IEnumerable<string> PathOf(MenuItem item)
    {
        Stack<string> headers = new();
        for (StyledElement? at = item; at is MenuItem menuItem; at = menuItem.Parent)
        {
            headers.Push(Unmarked(menuItem.Header?.ToString()) ?? string.Empty);
        }

        return headers;
    }

    private static string? Unmarked(string? header) => header?.Replace("_", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// A chord, as the command it ran and never as its keys: the name survives a rebind and a German
    /// keyboard, and a key is how typing would leak. Avalonia tries the key bindings of the focused
    /// element and its ancestors before it raises the key event, and a binding marks the key handled
    /// only when its command could run and did — so this handler, first on the key event's route, sees
    /// the key handled exactly when a command ran. One it sees unhandled ran nothing: Escape with no find
    /// bar open, or a chord pressed where no view holds the keyboard, whose bindings were never tried.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled)
        {
            return;
        }

        // The one view on screen is the one whose bindings could have run it.
        if (_diff.IsVisible)
        {
            LogChord(e, _diff.GestureFor);
        }
        else if (_unified.IsVisible)
        {
            LogChord(e, _unified.GestureFor);
        }
    }

    private void LogChord(KeyEventArgs e, Func<DiffCommand, KeyGesture?> gestureFor)
    {
        foreach (DiffCommand command in Enum.GetValues<DiffCommand>())
        {
            if (gestureFor(command) is { } gesture && gesture.Matches(e))
            {
                Log.Information("Command {Name:l} from the keyboard{On:l}", command.ToString(), OnSelection(command, FocusedSelection()));
                return;
            }
        }
    }

    /// <summary>The side and the selection of the pane holding the keyboard, if one does and has a selection.</summary>
    private (DiffSide Side, LineRange Lines)? FocusedSelection()
    {
        foreach (DiffPanePresenter pane in _panes)
        {
            if (pane.IsKeyboardFocusWithin && pane.SelectedLines is { } lines)
            {
                return (pane.Side, lines);
            }
        }

        return null;
    }

    /// <summary>For a copy that acts on a selection when there is one, the selection it acted on.</summary>
    private static string OnSelection(DiffCommand? command, (DiffSide Side, LineRange Lines)? selection) =>
        command is DiffCommand.CopyToLeft or DiffCommand.CopyToRight && selection is { } at
            ? string.Create(CultureInfo.InvariantCulture, $" on {Lower(at.Side)} {Lines(at.Lines)}")
            : string.Empty;

    private static string Lower(DiffSide side) => side == DiffSide.Left ? "left" : "right";

    /// <summary><c>line 5</c> or <c>lines 9–11</c>, 1-based as a reader counts them.</summary>
    private static string Lines(LineRange range) =>
        range.Count <= 1
            ? string.Create(CultureInfo.InvariantCulture, $"line {range.Start + 1}")
            : string.Create(CultureInfo.InvariantCulture, $"lines {range.Start + 1}–{range.Start + range.Count}");

    /// <summary>A menu over a pane, a gutter or the map: named by where it opened, with the selection a copy from it would act on.</summary>
    private static void Watch(IList<DiffMenuItem> items, DiffPaneContext context) =>
        Watch(items, MenuName(context), context.Side is { } side && context.SelectedLines is { } lines ? (side, lines) : null);

    /// <summary>
    /// Wraps each command of a menu about to open so that choosing it is logged — by its verb where it
    /// has one, by its header, quoted, where it has none: a host's own entry, or a save or a revert.
    /// The menu is built afresh each time it opens, so each wrap is its own.
    /// </summary>
    private static void Watch(IList<DiffMenuItem> items, string menu, (DiffSide Side, LineRange Lines)? acted)
    {
        foreach (DiffMenuItem item in items)
        {
            if (item.Command is { } command and not LoggedCommand)
            {
                string name = item.Verb?.ToString() ?? $"\"{Unmarked(item.Header)}\"";
                string on = OnSelection(item.Verb, acted);
                item.Command = new LoggedCommand(command, () => Log.Information("Command {Name:l} from the {Menu:l} menu{On:l}", name, menu, on));
            }

            Watch(item.Items, menu, acted);
        }
    }

    /// <summary>
    /// The surface a menu opened on, and the side it belongs to where it has one: <c>left pane</c>,
    /// <c>right number margin</c>, <c>unified pane</c>, <c>connector</c> — or <c>left overview map</c>,
    /// the map's side being the lane under the pointer.
    /// </summary>
    internal static string MenuName(DiffPaneContext context)
    {
        string surface = context.Region switch
        {
            DiffPaneRegion.LineNumberMargin => "number margin",
            DiffPaneRegion.ChangeMarkerMargin => "change markers",
            DiffPaneRegion.ConnectorGutter => "connector",
            DiffPaneRegion.OverviewMap => "overview map",
            _ => "pane",
        };
        string? side = context.Side is { } known ? Lower(known) : context.IsUnified ? "unified" : null;
        return side is null ? surface : $"{side} {surface}";
    }

    private void AttachPanes(INameScope scope, params string[] parts)
    {
        foreach (string part in parts)
        {
            if (scope.Find<DiffPanePresenter>(part) is not { } pane || !_panes.Add(pane))
            {
                continue;
            }

            pane.CopyOutRequested += (_, block) => Log.Information(
                "Copy requested: {Side:l} block {Block} → {Other:l}", Lower(pane.Side), block + 1, Lower(Other(pane.Side)));
            pane.CopySelectionRequested += (_, _) => Log.Information(
                "Copy requested: {Side:l} {Lines:l} → {Other:l}",
                Lower(pane.Side), pane.SelectedLines is { } lines ? Lines(lines) : "no lines", Lower(Other(pane.Side)));
            pane.DocumentChanged += (_, _) => WatchDocument(pane);
            WatchDocument(pane);
        }
    }

    private static DiffSide Other(DiffSide side) => side == DiffSide.Left ? DiffSide.Right : DiffSide.Left;

    private void WatchDocument(DiffPanePresenter pane)
    {
        if (pane.Document is { } document && _documents.Add(document))
        {
            document.Changed += (_, e) => OnEdited(pane, document, e);
        }
    }

    /// <summary>A change to a side's document is the reader's, whatever made it: a keystroke, a paste, a copy from the other side, a revert.</summary>
    private void OnEdited(DiffPanePresenter pane, TextDocument document, DocumentChangeEventArgs e)
    {
        int first = document.GetLineByOffset(Math.Min(e.Offset, document.TextLength)).LineNumber;
        int last = document.GetLineByOffset(Math.Min(e.Offset + e.InsertionLength, document.TextLength)).LineNumber;
        if (!_edits.TryGetValue(pane, out PendingEdit? pending))
        {
            pending = new PendingEdit(pane, () => Clock);
            _edits[pane] = pending;
        }

        pending.Add(first, last, LineBreaks(e.InsertedText), LineBreaks(e.RemovedText));
    }

    /// <summary>How many line breaks a change carried — counted, never kept: CR LF, LF and a lone CR each one.</summary>
    private static int LineBreaks(ITextSource text)
    {
        int count = 0;
        for (int i = 0; i < text.TextLength; i++)
        {
            char c = text.GetCharAt(i);
            if (c == '\n' || (c == '\r' && (i + 1 == text.TextLength || text.GetCharAt(i + 1) != '\n')))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>A pane's edits since its last line, written once they have rested for <see cref="Quiet"/>.</summary>
    private sealed class PendingEdit(DiffPanePresenter pane, Func<TimeProvider> clock)
    {
        private ITimer? _timer;
        private int _first = int.MaxValue;
        private int _last;
        private int _added;
        private int _removed;

        public void Add(int first, int last, int added, int removed)
        {
            _first = Math.Min(_first, first);
            _last = Math.Max(_last, last);
            _added += added;
            _removed += removed;
            _timer?.Dispose();
            _timer = clock().CreateTimer(_ => Dispatcher.UIThread.Post(Flush), state: null, Quiet, Timeout.InfiniteTimeSpan);
        }

        private void Flush()
        {
            _timer?.Dispose();
            _timer = null;
            if (_first == int.MaxValue)
            {
                return;
            }

            string lines = _first == _last
                ? string.Create(CultureInfo.InvariantCulture, $"line {_first}")
                : string.Create(CultureInfo.InvariantCulture, $"lines {_first}–{_last}");
            Log.Information("{Side:l} pane: edit at {Lines:l}, +{Added} −{Removed} lines", pane.Side.ToString(), lines, _added, _removed);
            _first = int.MaxValue;
            _last = 0;
            _added = 0;
            _removed = 0;
        }
    }

    /// <summary>A menu entry's command, which logs the choice and then does what it always did.</summary>
    private sealed class LoggedCommand(ICommand inner, Action logged) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => inner.CanExecuteChanged += value;
            remove => inner.CanExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter) => inner.CanExecute(parameter);

        public void Execute(object? parameter)
        {
            logged();
            inner.Execute(parameter);
        }
    }
}
