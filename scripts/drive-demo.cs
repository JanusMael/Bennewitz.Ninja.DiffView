// scripts/drive-demo.cs — a .NET 10 file-based app.
//
// Drives the running demo for a by-hand pass: starts it, finds its window and the popups it opens,
// clicks and types into it, and captures pixels. AGENTS.md §9 is the prose these verbs replace;
// plan 00023 is why they exist. The X11 and Windows back ends are written; the macOS one is specified
// in that plan and is not.
//
//   scripts/drive-demo.sh launch --edit both            build and start the demo detached, then wait
//                                                       for its window; prints the pid and the log
//   scripts/drive-demo.sh window                        the demo's main window
//   scripts/drive-demo.sh key ctrl+Down                 a key chord into the demo
//   scripts/drive-demo.sh click left 88 16              a click, relative to the demo's window
//   scripts/drive-demo.sh click left 105 829 in popup   ... or to the popup found last
//   scripts/drive-demo.sh drag left 539 364 to 639 364  press, travel and release — the splitter
//   scripts/drive-demo.sh mark                          remember the top-level windows there are now
//   scripts/drive-demo.sh popup                         the window that has appeared since the mark
//   scripts/drive-demo.sh geometry demo                 position, size and map state
//   scripts/drive-demo.sh capture popup menu.png        the window's pixels
//   scripts/drive-demo.sh hover 300 200                 wait for a tooltip there (see below)
//
// A window is `demo`, `popup` or an X window id, decimal or 0x-hex. Verbs chain with `then`, and what
// a chain learns — the mark, the popup, the pid — is kept for the next invocation too, per display,
// under the platform's temp directory:
//
//   scripts/drive-demo.sh mark then click left 88 16 then popup then capture popup view-menu.png
//
// A target is a coordinate, or an `AutomationId` path where the back end can address one:
// `click left --id SideBySide/LeftPane/LineNumbers`. Each step of the path is an id unique within the
// step before it — a view within the window, a part within the view, a margin within the pane — which
// is how DECISIONS.md's *Plan 00023's Windows back end finds DiffView's parts by `AutomationId`* scopes
// a search, and fixtures/automation-ids.txt is the list. Not by automation name: the names are
// translated into eight locales, so a search for *Left pane* finds nothing on a German machine. The X11
// back end cannot address an id at all — there is no accessibility bridge here, and plan 00023 leaves
// AT-SPI out — so it refuses one rather than guessing a position.
//
// What each verb exists to get right, measured on this repository's demo (AGENTS.md §9):
// - An Avalonia popup is its own unnamed, override-redirect top-level window. `popup` finds it by
//   diffing the root's children against the mark; counting unnamed windows picks up stubs.
// - A popup opens on one layout pass and lays out on the next, so a frame taken between them is an
//   empty box. `popup` waits until the window's geometry holds still.
// - `xdotool search` returns unmapped windows as well, which x11grab refuses with BadMatch. Nothing
//   unmapped is ever captured.
// - A session that is not presenting — a closed remote-desktop connection leaves one — hands x11grab
//   a solid black frame and the capture succeeds all the same. `capture` refuses a frame with no pixel
//   brighter than black, and keeps the file.
// - Mutter keeps override windows that are not menus: a screen-sized guard window and a 1×1 off
//   screen. The mark leaves out every window that was there before it, `popup` passes over anything a
//   pixel wide or tall, and a click outside the window it is relative to is refused rather than sent
//   to whatever lies there.
// - xdotool's getwindowgeometry counts a reparenting window manager's frame offset twice — 663,375
//   for a window whose origin is 602,283 — so positions come from xwininfo's absolute upper-left.
// - The F12 log window answers to the demo's window class too, and is wider than the main window, so
//   the main window is the one titled "DiffView Demo", not the widest.
// - A tooltip is its own override-redirect window too, found by the same diff. `hover` parks the
//   pointer inside the demo first, away from anything with a tooltip and away from a screen corner,
//   where a hot corner may be waiting. AGENTS.md once recorded that synthetic motion raised no tooltip
//   here; measured again on 2026-09-28 it raised one on every trial, so a `hover` that finds none is
//   a failure to look into, not an expected result.
// - The agent harness reaps a background child at the turn boundary. `launch` starts the demo in a
//   session of its own, with its output in a log file rather than a pipe that would close under it.
//
// What the Windows back end gets right, measured on this repository's demo on 2026-10-08:
// - `PrintWindow` without `PW_RENDERFULLCONTENT` returns a frame that is **entirely black** for an
//   Avalonia window — mean grey 0, brightest pixel 0 — and reports success. It is the same false green
//   as the X11 black frame above with an unrelated cause, so `capture` passes the flag and checks the
//   pixels regardless.
// - A capture is written as a PNG by hand, because a file-based app carries no image library and this
//   driver takes no dependency to get one. Proven against GDI+, which reads it back as 24-bit PNG.
// - Coordinates are physical pixels, so the process declares itself per-monitor DPI aware before it
//   reads a rectangle. Unaware, Windows answers `GetWindowRect` in virtualised coordinates and every
//   click lands scaled on a display that is not at 100%.
// - Elements are found by `AutomationId`, through the UI Automation reader beside this file: scoped to
//   the demo's process, then down the path's steps. A search of the desktop's whole subtree is never
//   made — XamlQuality's docs/ai-drivable-ui.md measures one harness at 94 s on a quiet desktop and
//   406 s on a busy one for exactly that, and an unscoped search once invoked another application's
//   button.
// - A context menu is a window of its own, as it is on X11, so `mark` and `popup` work the same way:
//   the top-level windows of the demo's process, diffed, then settled.
// - ⛔ `key` needs a `click` before it on a window nobody has touched yet. Holding the foreground is
//   not the same as having something focused inside, and a chord sent into that gap does nothing at
//   all — measured, twice, and it reads as "input does not work here", which is the wrong and
//   expensive conclusion. `click left --id SideBySide/LeftPane then key ctrl+f` opens the find bar;
//   the same chord alone does not.
// - Synthetic motion raises a tooltip here, which is what plan 00023 expected of `SendInput` and the
//   reason `hover` is a verb rather than a museum piece.
// - ⛔ A drag is a press, a TRAVEL and a release. A press, a jump to the far point and a release is
//   two positions rather than a gesture: the splitter and the overview map both move with the
//   pointer and see nothing in between, so the path is walked in steps with the button held. Both
//   back ends do it that way, and both release from a `finally` — a button left down is the
//   person's pointer taken hostage.
//
// xwininfo's per-window fields are read by their labels. Those are fixed strings in the program, and
// measured so: it imports no gettext — only setlocale and nl_langinfo, which serve window names — and
// prints the same bytes under de_DE as under C. The root's child list is read by its two ends instead —
// the id first, the two geometry tokens last — because the name and class in between are free text,
// and a title can contain a geometry.
//
//   --parse <verbs>            print each verb's canonical form, one per line, its tokens separated by
//                              tabs; runs nothing. Feeding the tokens back gives the same lines.
//   --parse-children <file>    print the windows an `xwininfo -root -children -int` listing names.
//   --parse-info <file>        print the window an `xwininfo -int -id` report describes.
//   --parse-chord <chord>      print the keys a chord sends, modifiers first; runs nothing.
//   --parse-element <file>     print the element a drive-demo-uia.ps1 report describes.
//
// The .sh and .ps1 wrappers beside this file run it from the repository root for you.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    Console.Error.WriteLine(Usage.Text);
    return 2;
}

try
{
    switch (args[0])
    {
        case "--parse":
            foreach (Verb verb in VerbParser.Parse(args[1..]))
            {
                Console.WriteLine(string.Join('\t', verb.Canonical()));
            }

            return 0;

        case "--parse-children" when args.Length == 2:
            foreach (TopWindow window in X11.ParseChildren(File.ReadAllText(args[1])))
            {
                Console.WriteLine(window);
            }

            return 0;

        case "--parse-info" when args.Length == 2:
            Console.WriteLine(X11.ParseInfo(File.ReadAllText(args[1])));
            return 0;

        case "--parse-chord" when args.Length == 2:
            foreach (Key key in Chord.Parse(args[1]))
            {
                Console.WriteLine(key);
            }

            return 0;

        case "--parse-element" when args.Length == 2:
            Console.WriteLine(Windows.ParseElement(File.ReadAllText(args[1])));
            return 0;

        default:
            IReadOnlyList<Verb> verbs = VerbParser.Parse(args);
            IBackEnd backEnd = BackEnds.ForThisPlatform();
            DriveState state = DriveState.Load(backEnd.StateFile);
            try
            {
                foreach (Verb verb in verbs)
                {
                    backEnd.Run(verb, state);
                }
            }
            finally
            {
                // What the chain learned before a verb failed — a mark, above all — is kept, so the
                // verb can be retried on its own.
                state.Save();
            }

            return 0;
    }
}
catch (UsageException e)
{
    Console.Error.WriteLine($"drive-demo: {e.Message}");
    Console.Error.WriteLine(Usage.Text);
    return 2;
}
catch (DriveException e)
{
    Console.Error.WriteLine($"drive-demo: {e.Message}");
    return 1;
}

internal static class Usage
{
    public const string Text = """
        usage: drive-demo <verb> [then <verb>]...
          launch [demo flags...]                build and start the demo detached, wait for its window
          window                                the demo's main window
          key <chord>                           a key chord into the demo, e.g. ctrl+Down
                                                (click something first: a window nobody has touched
                                                 holds the foreground with nothing focused in it)
          click <button> <x> <y> [in <window>]  button: left, middle or right (or 1, 2, 3)
          click <button> --id <path>            by AutomationId, where the back end can
          drag <button> <x> <y> to <x> <y>      press, travel and release; [in <window>] too
          mark                                  remember the top-level windows there are now
          popup                                 the window that appeared since the mark
          geometry <window>                     position, size and map state
          capture <window> <out.png>            the window's pixels
          hover <x> <y> [in <window>]           wait for a tooltip there; it becomes the popup
        <window> is demo, popup, or a window id (decimal or 0x-hex)
        <path> is AutomationIds from the window down, e.g. SideBySide/LeftPane/LineNumbers
        """;
}

internal sealed class UsageException(string message) : Exception(message);

internal sealed class DriveException(string message) : Exception(message);

internal enum MouseButton
{
    Left = 1,
    Middle = 2,
    Right = 3,
}

/// <summary>`demo`, the popup found last, or a window by its X id.</summary>
internal readonly record struct WindowRef(string Kind, long Id)
{
    public static readonly WindowRef Demo = new("demo", 0);
    public static readonly WindowRef Popup = new("popup", 0);

    public string Canonical => Kind == "id" ? Id.ToString(CultureInfo.InvariantCulture) : Kind;
}

internal abstract record Target
{
    public abstract IReadOnlyList<string> Canonical();
}

internal sealed record PointTarget(int X, int Y, WindowRef RelativeTo) : Target
{
    public override IReadOnlyList<string> Canonical() =>
        [X.ToString(CultureInfo.InvariantCulture), Y.ToString(CultureInfo.InvariantCulture), "in", RelativeTo.Canonical];
}

/// <summary>
/// A part by its <c>AutomationId</c>, as a path of ids from the demo's window down: each step is unique
/// within the step before it, which is the scope DECISIONS.md gives every id the library declares.
/// </summary>
internal sealed record IdTarget(string Path) : Target
{
    public override IReadOnlyList<string> Canonical() => ["--id", Path];

    /// <summary>The path's steps. Empty steps are refused at the parser, so none is blank.</summary>
    public IReadOnlyList<string> Steps => Path.Split('/');
}

internal abstract record Verb
{
    public abstract IReadOnlyList<string> Canonical();
}

internal sealed record LaunchVerb(IReadOnlyList<string> DemoArguments) : Verb
{
    public override IReadOnlyList<string> Canonical() => ["launch", .. DemoArguments];
}

internal sealed record WindowVerb : Verb
{
    public override IReadOnlyList<string> Canonical() => ["window"];
}

internal sealed record KeyVerb(string Chord) : Verb
{
    public override IReadOnlyList<string> Canonical() => ["key", Chord];
}

internal sealed record ClickVerb(MouseButton Button, Target Target) : Verb
{
    public override IReadOnlyList<string> Canonical() =>
        ["click", Button.ToString().ToLowerInvariant(), .. Target.Canonical()];
}

internal sealed record MarkVerb : Verb
{
    public override IReadOnlyList<string> Canonical() => ["mark"];
}

internal sealed record PopupVerb : Verb
{
    public override IReadOnlyList<string> Canonical() => ["popup"];
}

internal sealed record GeometryVerb(WindowRef Window) : Verb
{
    public override IReadOnlyList<string> Canonical() => ["geometry", Window.Canonical];
}

internal sealed record CaptureVerb(WindowRef Window, string OutPath) : Verb
{
    public override IReadOnlyList<string> Canonical() => ["capture", Window.Canonical, OutPath];
}

internal sealed record HoverVerb(Target Target) : Verb
{
    public override IReadOnlyList<string> Canonical() => ["hover", .. Target.Canonical()];
}

/// <summary>
/// A press, a path, and a release. Both ends are coordinates in the same window, because what this
/// exists for — the splitter between the panes and the overview map's viewport — are drags to a
/// position rather than onto a part, and a part has no "drop here" of its own.
/// </summary>
internal sealed record DragVerb(MouseButton Button, PointTarget From, int ToX, int ToY) : Verb
{
    public override IReadOnlyList<string> Canonical() =>
    [
        "drag",
        Button.ToString().ToLowerInvariant(),
        From.X.ToString(CultureInfo.InvariantCulture),
        From.Y.ToString(CultureInfo.InvariantCulture),
        "to",
        ToX.ToString(CultureInfo.InvariantCulture),
        ToY.ToString(CultureInfo.InvariantCulture),
        "in",
        From.RelativeTo.Canonical,
    ];
}

internal static class VerbParser
{
    /// <summary>A chain of verbs separated by `then`; `then` is therefore no verb's argument.</summary>
    public static IReadOnlyList<Verb> Parse(IReadOnlyList<string> tokens)
    {
        List<Verb> verbs = [];
        List<string> current = [];
        foreach (string token in tokens)
        {
            if (token == "then")
            {
                verbs.Add(One(current));
                current = [];
            }
            else
            {
                current.Add(token);
            }
        }

        verbs.Add(One(current));
        return verbs;
    }

    private static Verb One(List<string> tokens)
    {
        if (tokens.Count == 0)
        {
            throw new UsageException("an empty verb: `then` with nothing on one side of it");
        }

        string[] rest = [.. tokens.Skip(1)];
        return tokens[0] switch
        {
            "launch" => new LaunchVerb(rest),
            "window" => Bare(rest, "window", new WindowVerb()),
            "key" when rest.Length == 1 && rest[0].Length > 0 => new KeyVerb(rest[0]),
            "key" => throw new UsageException("key takes one chord, e.g. `key ctrl+Down`"),
            "click" when rest.Length >= 1 => new ClickVerb(Button(rest[0]), TargetOf(rest[1..], "click")),
            "click" => throw new UsageException("click takes a button and a target"),
            "mark" => Bare(rest, "mark", new MarkVerb()),
            "popup" => Bare(rest, "popup", new PopupVerb()),
            "geometry" when rest.Length == 1 => new GeometryVerb(WindowOf(rest[0])),
            "geometry" => throw new UsageException("geometry takes one window"),
            "capture" when rest.Length == 2 && rest[1].Length > 0 => new CaptureVerb(WindowOf(rest[0]), rest[1]),
            "capture" => throw new UsageException("capture takes a window and an output path"),
            "hover" => new HoverVerb(TargetOf(rest, "hover")),
            "drag" when rest.Length >= 1 => Drag(Button(rest[0]), rest[1..]),
            "drag" => throw new UsageException("drag takes a button and two points"),
            _ => throw new UsageException($"unknown verb `{tokens[0]}`"),
        };
    }

    private static Verb Bare(string[] rest, string name, Verb verb) =>
        rest.Length == 0 ? verb : throw new UsageException($"{name} takes no arguments");

    private static MouseButton Button(string token) => token.ToLowerInvariant() switch
    {
        "left" or "1" => MouseButton.Left,
        "middle" or "2" => MouseButton.Middle,
        "right" or "3" => MouseButton.Right,
        _ => throw new UsageException($"`{token}` is not a button: left, middle or right (or 1, 2, 3)"),
    };

    private static Target TargetOf(string[] tokens, string verb) => tokens switch
    {
        ["--id", var path] when Steps(path) => new IdTarget(path),
        ["--id", var path] => throw new UsageException(
            $"`{path}` is not an AutomationId path: ids from the window down, e.g. SideBySide/LeftPane"),
        [var x, var y] => new PointTarget(Coordinate(x), Coordinate(y), WindowRef.Demo),
        [var x, var y, "in", var window] => new PointTarget(Coordinate(x), Coordinate(y), WindowOf(window)),
        _ => throw new UsageException($"{verb} takes <x> <y> [in <window>], or --id <path>"),
    };

    /// <summary>Whether a path is steps and not holes: a blank step would scope a search to nothing.</summary>
    private static bool Steps(string path) =>
        path.Length > 0 && path.Split('/').All(step => step.Length > 0);

    /// <summary>
    /// `drag <button> <x> <y> to <x> <y> [in <window>]`. Both ends are read against the same window:
    /// a drag whose ends were relative to different things would be unreadable in a recipe, and
    /// there is nothing a drag means across two windows.
    /// </summary>
    private static Verb Drag(MouseButton button, string[] rest) => rest switch
    {
        [var x1, var y1, "to", var x2, var y2] =>
            new DragVerb(button, new PointTarget(Coordinate(x1), Coordinate(y1), WindowRef.Demo), Coordinate(x2), Coordinate(y2)),
        [var x1, var y1, "to", var x2, var y2, "in", var window] =>
            new DragVerb(button, new PointTarget(Coordinate(x1), Coordinate(y1), WindowOf(window)), Coordinate(x2), Coordinate(y2)),
        _ => throw new UsageException("drag takes <x> <y> to <x> <y> [in <window>]"),
    };

    private static int Coordinate(string token) =>
        int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new UsageException($"`{token}` is not a coordinate");

    private static WindowRef WindowOf(string token)
    {
        if (token is "demo")
        {
            return WindowRef.Demo;
        }

        if (token is "popup")
        {
            return WindowRef.Popup;
        }

        bool hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        return long.TryParse(
                hex ? token[2..] : token,
                hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long id) && id > 0
            ? new WindowRef("id", id)
            : throw new UsageException(
                $"`{token}` is not a window: demo, popup, or a window id (an X window or an HWND)");
    }
}

/// <summary>What a chain has learned, kept between invocations: one file per display.</summary>
internal sealed class DriveState
{
    private readonly string _path;

    private DriveState(string path)
    {
        _path = path;
    }

    public HashSet<long>? Mark { get; set; }

    public long? Popup { get; set; }

    public int? Pid { get; set; }

    public static DriveState Load(string path)
    {
        DriveState state = new(path);
        if (!File.Exists(path))
        {
            return state;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts)
            {
                case ["mark", .. var ids]:
                    state.Mark = [.. ids.Select(id => long.Parse(id, CultureInfo.InvariantCulture))];
                    break;
                case ["popup", var id]:
                    state.Popup = long.Parse(id, CultureInfo.InvariantCulture);
                    break;
                case ["pid", var pid]:
                    state.Pid = int.Parse(pid, CultureInfo.InvariantCulture);
                    break;
            }
        }

        return state;
    }

    public void Save()
    {
        List<string> lines = [];
        if (Mark is not null)
        {
            lines.Add(string.Join(' ', ["mark", .. Mark.Select(id => id.ToString(CultureInfo.InvariantCulture))]));
        }

        if (Popup is long popup)
        {
            lines.Add($"popup {popup.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Pid is int pid)
        {
            lines.Add($"pid {pid.ToString(CultureInfo.InvariantCulture)}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllLines(_path, lines);
    }
}

internal interface IBackEnd
{
    string StateFile { get; }

    void Run(Verb verb, DriveState state);
}

internal static class BackEnds
{
    public static IBackEnd ForThisPlatform()
    {
        if (OperatingSystem.IsLinux())
        {
            return X11.Discover();
        }

        if (OperatingSystem.IsWindows())
        {
            return Windows.Discover();
        }

        throw new DriveException(
            "this platform has no back end: plan 00023 specifies the macOS one and does not write it");
    }
}

/// <summary>What both back ends need and neither owns.</summary>
internal static class Repo
{
    /// <summary>The checkout this is running inside, found by its solution rather than assumed.</summary>
    public static string Root()
    {
        for (DirectoryInfo? dir = new(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DiffView.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DriveException("not inside the DiffView repository: run from it, as the wrappers do");
    }

    /// <summary>Where a launch puts the demo's output and a chain keeps what it has learned.</summary>
    public static string Scratch(string name)
    {
        string path = Path.Combine(Path.GetTempPath(), "drive-demo", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
}

/// <summary>A top-level window as the root's child list gives it: position relative to the root.</summary>
internal readonly record struct TopWindow(long Id, int Width, int Height, int X, int Y)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Id} {Width}x{Height}+{X}+{Y}");
}

/// <summary>A window as xwininfo reports it: absolute position, size, and whether it is viewable.</summary>
internal readonly record struct WindowInfo(long Id, int X, int Y, int Width, int Height, bool Viewable)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Id} {Width}x{Height}+{X}+{Y} {(Viewable ? "viewable" : "not-viewable")}");
}

internal sealed partial class X11 : IBackEnd
{
    /// <summary>The main window's title; the F12 log window shares its class and is wider.</summary>
    private const string DemoTitle = "DiffView Demo";

    private readonly string _display;
    private readonly string? _xauthority;

    private X11(string display, string? xauthority)
    {
        _display = display;
        _xauthority = xauthority;
        StateFile = Path.Combine(Path.GetTempPath(), "drive-demo", $"state-{Sanitise(display)}.txt");
    }

    public string StateFile { get; }

    /// <summary>The display and its cookie, from the environment where it says, discovered where not.</summary>
    public static X11 Discover()
    {
        string? display = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrEmpty(display))
        {
            // Every X server, XWayland included, keeps its socket in the protocol's fixed directory.
            string sockets = Path.Combine(Path.DirectorySeparatorChar + "tmp", ".X11-unix");
            string[] servers = Directory.Exists(sockets) ? Directory.GetFiles(sockets, "X*") : [];
            display = servers.Length == 1
                ? ":" + Path.GetFileName(servers[0])[1..]
                : throw new DriveException(
                    $"DISPLAY is not set, and {servers.Length} X servers are running, not one: set DISPLAY");
        }

        string? xauthority = Environment.GetEnvironmentVariable("XAUTHORITY");
        if (string.IsNullOrEmpty(xauthority) || !File.Exists(xauthority))
        {
            xauthority = FindCookie();
        }

        return new X11(display, xauthority);
    }

    public void Run(Verb verb, DriveState state)
    {
        switch (verb)
        {
            case LaunchVerb launch:
                Launch(launch.DemoArguments, state);
                break;
            case WindowVerb:
                Console.WriteLine($"demo {Demo()}");
                break;
            case KeyVerb key:
                Activate(Demo().Id);
                Tool("xdotool", "key", key.Chord);
                break;
            case ClickVerb click:
                (int x, int y) = Absolute(click.Target, state, "click");
                Tool("xdotool", "mousemove", Invariant(x), Invariant(y), "click", Invariant((int)click.Button));
                break;
            case DragVerb drag:
                Drag(drag, state);
                break;
            case MarkVerb:
                state.Mark = [.. Children().Select(w => w.Id)];
                state.Popup = null;
                Console.WriteLine($"mark {state.Mark.Count} top-level windows");
                break;
            case PopupVerb:
                WindowInfo popup = AppearedSince(state, TimeSpan.FromSeconds(5))
                    ?? throw new DriveException("no new top-level window appeared since the mark");
                state.Popup = popup.Id;
                Console.WriteLine($"popup {popup}");
                break;
            case GeometryVerb geometry:
                Console.WriteLine($"{geometry.Window.Canonical} {Info(Resolve(geometry.Window, state))}");
                break;
            case CaptureVerb capture:
                Capture(Resolve(capture.Window, state), capture.OutPath);
                break;
            case HoverVerb hover:
                Hover(hover.Target, state);
                break;
            default:
                throw new DriveException($"the X11 back end has no implementation of `{verb.Canonical()[0]}`");
        }
    }

    /// <summary>
    /// The root's children from an `xwininfo -root -children -int` listing, topmost first. A child line
    /// is read by its two ends: the id is the first token and the geometry the last two, so the name and
    /// class between them are never read — a title is free text and can contain a geometry. The count
    /// line, `     22 children:`, has the same indent and a leading number and carries no geometry.
    /// </summary>
    public static IReadOnlyList<TopWindow> ParseChildren(string listing)
    {
        List<TopWindow> windows = [];
        foreach (string raw in listing.Split('\n'))
        {
            string[] tokens = raw.TrimEnd('\r').Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3
                || !long.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out long id))
            {
                continue;
            }

            Match geometry = GeometryToken().Match(tokens[^2]);
            if (!geometry.Success || !PositionToken().IsMatch(tokens[^1]))
            {
                continue;
            }

            windows.Add(new TopWindow(
                id,
                Int(geometry.Groups["w"].Value),
                Int(geometry.Groups["h"].Value),
                Int(geometry.Groups["x"].Value),
                Int(geometry.Groups["y"].Value)));
        }

        return windows;
    }

    // `WxH+X+Y`, where xwininfo prints a negative offset after the plus: `1x1+-1+-1`.
    [GeneratedRegex(@"^(?<w>\d+)x(?<h>\d+)\+(?<x>-?\d+)\+(?<y>-?\d+)$")]
    private static partial Regex GeometryToken();

    [GeneratedRegex(@"^\+-?\d+\+-?\d+$")]
    private static partial Regex PositionToken();

    private static int Int(string text) => int.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Sanitise(string display) =>
        new([.. display.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);

    /// <summary>
    /// The X cookie when XAUTHORITY does not name one: GNOME's XWayland writes it into the session's
    /// runtime directory under a random suffix, and a plain X session keeps it in the home directory.
    /// </summary>
    private static string? FindCookie()
    {
        string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrEmpty(runtime) && Directory.Exists(runtime))
        {
            string? mutter = Directory.GetFiles(runtime, ".mutter-Xwaylandauth.*").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
            if (mutter is not null)
            {
                return mutter;
            }
        }

        string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Xauthority");
        return File.Exists(home) ? home : null;
    }

    private IReadOnlyList<TopWindow> Children() => ParseChildren(Tool("xwininfo", "-root", "-children", "-int"));

    private WindowInfo Info(long id) =>
        ParseInfo(Tool("xwininfo", "-int", "-id", id.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// One window's absolute position, size and map state, from an `xwininfo -int -id` report. The labels
    /// are fixed strings in xwininfo — it imports no gettext, and prints the same bytes under de_DE as
    /// under C — so they are safe to read.
    /// </summary>
    public static WindowInfo ParseInfo(string report) => new(
        long.Parse(FirstToken(report, "xwininfo: Window id:"), NumberStyles.None, CultureInfo.InvariantCulture),
        Int(FirstToken(report, "Absolute upper-left X:")),
        Int(FirstToken(report, "Absolute upper-left Y:")),
        Int(FirstToken(report, "Width:")),
        Int(FirstToken(report, "Height:")),
        FirstToken(report, "Map State:") == "IsViewable");

    /// <summary>The first token after a line's label; the id's line goes on to name the window.</summary>
    private static string FirstToken(string report, string label)
    {
        foreach (string line in report.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(label, StringComparison.Ordinal))
            {
                string[] tokens = trimmed[label.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0)
                {
                    return tokens[0];
                }
            }
        }

        throw new DriveException($"xwininfo printed no `{label}` line");
    }

    /// <summary>
    /// The demo's main window: of the viewable windows with its class, the one titled DiffView Demo. The
    /// widest is the wrong rule — the F12 log window shares the class and is wider.
    /// </summary>
    private WindowInfo Demo()
    {
        List<WindowInfo> viewable = [];
        foreach (string line in Tool("xdotool", "search", "--onlyvisible", "--class", "DiffView").Split('\n'))
        {
            if (!long.TryParse(line.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long id))
            {
                continue;
            }

            WindowInfo info = Info(id);
            if (!info.Viewable)
            {
                continue;
            }

            if (Tool("xdotool", "getwindowname", line.Trim()).Trim() == DemoTitle)
            {
                return info;
            }

            viewable.Add(info);
        }

        throw new DriveException(viewable.Count == 0
            ? "no viewable demo window: is the demo running? `launch` starts one"
            : $"{viewable.Count} viewable window(s) with the demo's class, none titled {DemoTitle}");
    }

    private long Resolve(WindowRef window, DriveState state) => window.Kind switch
    {
        "demo" => Demo().Id,
        "popup" => state.Popup ?? throw new DriveException("no popup found yet: `mark`, act, then `popup`"),
        _ => window.Id,
    };

    private (int X, int Y) Absolute(Target target, DriveState state, string verb)
    {
        if (target is not PointTarget point)
        {
            throw new DriveException(
                $"the X11 back end cannot address `{verb}` by AutomationId: it has no accessibility "
                + "bridge (plan 00023 leaves AT-SPI out). Give a coordinate relative to a window instead");
        }

        WindowInfo origin = Info(Resolve(point.RelativeTo, state));
        if (point.X < 0 || point.Y < 0 || point.X >= origin.Width || point.Y >= origin.Height)
        {
            throw new DriveException(string.Create(
                CultureInfo.InvariantCulture,
                $"{point.X},{point.Y} is outside {point.RelativeTo.Canonical}, which is {origin.Width}x{origin.Height}: "
                + $"refused rather than sent to whatever lies there"));
        }

        return (origin.X + point.X, origin.Y + point.Y);
    }

    private void Activate(long id)
    {
        // Best effort: some window managers refuse activation from a client, and a key into an already
        // focused window needs none.
        Run("xdotool", ["windowactivate", id.ToString(CultureInfo.InvariantCulture)]);
        Thread.Sleep(300);
    }

    /// <summary>
    /// The topmost viewable window that was not there at the mark, once its geometry holds still: a
    /// popup opens on one layout pass and lays out on the next, and a frame between them is empty.
    /// </summary>
    private WindowInfo? AppearedSince(DriveState state, TimeSpan patience)
    {
        HashSet<long> mark = state.Mark ?? throw new DriveException("`popup` needs a `mark` before the action that opens it");
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < patience)
        {
            foreach (TopWindow candidate in Children().Where(w => !mark.Contains(w.Id)))
            {
                WindowInfo info = Info(candidate.Id);
                if (info.Viewable && info.Width > 1 && info.Height > 1)
                {
                    return Settled(info);
                }
            }

            Thread.Sleep(100);
        }

        return null;
    }

    private WindowInfo Settled(WindowInfo first)
    {
        WindowInfo previous = first;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Thread.Sleep(250);
            WindowInfo now = Info(first.Id);
            if (now == previous)
            {
                return now;
            }

            previous = now;
        }

        return previous;
    }

    private void Capture(long id, string outPath)
    {
        WindowInfo info = Info(id);
        if (!info.Viewable)
        {
            // x11grab would refuse it with BadMatch, which reads like a geometry or compositor problem.
            throw new DriveException($"window {id} is not viewable, so there is nothing to capture");
        }

        (int exit, string output) = Run("ffmpeg",
        [
            "-hide_banner", "-loglevel", "error", "-f", "x11grab", "-window_id", id.ToString(CultureInfo.InvariantCulture),
            "-video_size", string.Create(CultureInfo.InvariantCulture, $"{info.Width}x{info.Height}"),
            "-i", _display, "-frames:v", "1", "-y", outPath,
        ]);
        if (exit != 0)
        {
            throw new DriveException($"ffmpeg could not capture window {id}:\n{output}");
        }

        if (IsBlack(outPath))
        {
            throw new DriveException(
                $"captured window {id} to {outPath}, and the frame is black: the session is not presenting, "
                + "which is what a closed remote-desktop connection leaves behind (AGENTS.md §9). Judge "
                + "from the demo's log, not from the pixels");
        }

        Console.WriteLine($"capture {info} {outPath}");
    }

    /// <summary>
    /// Whether no pixel of the image is brighter than black. Decoded by ffmpeg to one grey byte a pixel,
    /// through a file rather than a pipe, because the output pipe is read as text.
    /// </summary>
    private bool IsBlack(string image)
    {
        string raw = Path.Combine(Path.GetTempPath(), "drive-demo", $"grey-{Environment.ProcessId}.raw");
        Directory.CreateDirectory(Path.GetDirectoryName(raw)!);
        try
        {
            (int exit, string output) = Run("ffmpeg",
                ["-hide_banner", "-loglevel", "error", "-i", image, "-f", "rawvideo", "-pix_fmt", "gray", "-y", raw]);
            if (exit != 0)
            {
                throw new DriveException($"ffmpeg could not read back {image}:\n{output}");
            }

            // A frame from a session that is not presenting is exactly zero; allow for a codec's rounding.
            return File.ReadAllBytes(raw).All(grey => grey <= 2);
        }
        finally
        {
            File.Delete(raw);
        }
    }

    /// <summary>
    /// Press, travel, release. ⛔ The travel is the point: a press, a jump and a release is two
    /// positions rather than a gesture, and the splitter and the map both move with the pointer, so
    /// they see nothing between them. One `mousemove` per step with the button held is what a hand does.
    /// </summary>
    private void Drag(DragVerb drag, DriveState state)
    {
        (int fromX, int fromY) = Absolute(drag.From, state, "drag");
        WindowInfo over = Info(Resolve(drag.From.RelativeTo, state));
        if (drag.ToX < 0 || drag.ToY < 0 || drag.ToX >= over.Width || drag.ToY >= over.Height)
        {
            throw new DriveException(string.Create(
                CultureInfo.InvariantCulture,
                $"a drag to {drag.ToX},{drag.ToY} ends outside {drag.From.RelativeTo.Canonical}, which is "
                + $"{over.Width}x{over.Height}: refused rather than released over whatever lies there"));
        }

        int toX = over.X + drag.ToX;
        int toY = over.Y + drag.ToY;

        Tool("xdotool", "mousemove", Invariant(fromX), Invariant(fromY));
        Tool("xdotool", "mousedown", Invariant((int)drag.Button));
        try
        {
            const int steps = 16;
            for (int i = 1; i <= steps; i++)
            {
                Tool(
                    "xdotool",
                    "mousemove",
                    Invariant(fromX + ((toX - fromX) * i / steps)),
                    Invariant(fromY + ((toY - fromY) * i / steps)));
                Thread.Sleep(25);
            }
        }
        finally
        {
            // Released whatever happened in between: a button left down is the pointer taken hostage.
            Tool("xdotool", "mouseup", Invariant((int)drag.Button));
        }
    }

    private void Hover(Target target, DriveState state)
    {
        (int x, int y) = Absolute(target, state, "hover");

        // Park the pointer inside the demo first, away from anything with a tooltip: moving from one
        // tooltip's owner to another re-uses the window rather than opening one. Not a screen corner,
        // where a desktop's hot corner may be waiting.
        WindowInfo demo = Demo();
        Tool("xdotool", "mousemove", Invariant(demo.X + demo.Width - 3), Invariant(demo.Y + demo.Height - 3));
        Thread.Sleep(1000);
        state.Mark = [.. Children().Select(w => w.Id)];
        Tool("xdotool", "mousemove", Invariant(x), Invariant(y));

        WindowInfo tooltip = AppearedSince(state, TimeSpan.FromSeconds(3))
            ?? throw new DriveException(
                $"no tooltip appeared at {x},{y} within 3 s: the control there may have none, or the pointer "
                + "may not be where it was meant to be — `capture demo` shows the pointer in the frame");
        state.Popup = tooltip.Id;
        Console.WriteLine($"tooltip {tooltip}");
    }

    private void Launch(IReadOnlyList<string> demoArguments, DriveState state)
    {
        string root = Repo.Root();
        string project = Path.Combine(root, "src", "DiffView.Demo", "DiffView.Demo.csproj");

        // Built in the foreground, so a compile error is reported here rather than buried in the log.
        (int built, string buildOutput) = Run("dotnet", ["build", project, "-c", "Debug", "-nologo", "-v", "q"]);
        if (built != 0)
        {
            throw new DriveException($"the demo did not build:\n{buildOutput}");
        }

        string assembly = Tool("dotnet", "msbuild", project, "-nologo", "-p:Configuration=Debug", "-getProperty:TargetPath").Trim();
        string log = Path.Combine(Path.GetTempPath(), "drive-demo",
            string.Create(CultureInfo.InvariantCulture, $"demo-{DateTime.Now:yyyyMMdd-HHmmss}.log"));
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);

        // setsid puts the demo in a session of its own, so reaping the caller's process group at a turn
        // boundary does not reach it; its output goes to a file, since a pipe would close under it. The
        // child is no group leader, so setsid calls setsid() and execs rather than forking: the pid
        // Process reports is the demo's.
        ProcessStartInfo start = new("setsid") { UseShellExecute = false, WorkingDirectory = root };
        foreach (string argument in (string[])["sh", "-c", "exec \"$0\" \"$@\" > \"$DRIVE_DEMO_LOG\" 2>&1 < /dev/null", "dotnet", assembly])
        {
            start.ArgumentList.Add(argument);
        }

        foreach (string argument in demoArguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DRIVE_DEMO_LOG"] = log;
        Environ(start);
        using Process demo = Process.Start(start) ?? throw new DriveException("could not start setsid");
        state.Pid = demo.Id;
        state.Mark = null;
        state.Popup = null;
        Console.WriteLine($"launched pid={demo.Id.ToString(CultureInfo.InvariantCulture)} log={log}");

        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(60))
        {
            if (demo.HasExited)
            {
                throw new DriveException($"the demo exited with {demo.ExitCode} before showing a window; see {log}");
            }

            try
            {
                Console.WriteLine($"demo {Demo()}");
                return;
            }
            catch (DriveException)
            {
                Thread.Sleep(500);
            }
        }

        throw new DriveException($"the demo started but showed no window in 60 s; see {log}");
    }

    private void Environ(ProcessStartInfo start)
    {
        start.Environment["DISPLAY"] = _display;
        if (_xauthority is not null)
        {
            start.Environment["XAUTHORITY"] = _xauthority;
        }
    }

    /// <summary>A tool that must succeed; its standard output.</summary>
    private string Tool(string tool, params string[] arguments)
    {
        (int exit, string output) = Run(tool, arguments);

        // xdotool search exits 1 when it finds nothing, which is an answer rather than a failure.
        return exit == 0 || (tool == "xdotool" && arguments[0] == "search" && exit == 1)
            ? output
            : throw new DriveException($"`{tool} {string.Join(' ', arguments)}` exited {exit}:\n{output}");
    }

    private (int Exit, string Output) Run(string tool, string[] arguments)
    {
        ProcessStartInfo start = new(tool)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Environ(start);
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new DriveException($"could not start {tool}");
        }
        catch (Win32Exception)
        {
            throw new DriveException(
                $"`{tool}` is not installed. The X11 back end needs xdotool, xwininfo and ffmpeg — on Debian "
                + "or Ubuntu, `apt install xdotool x11-utils ffmpeg`");
        }

        using (process)
        {
            // Both pipes drained concurrently: reading one to its end first deadlocks once the child
            // fills the other's buffer.
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string output = stdout.GetAwaiter().GetResult();
            string errors = stderr.GetAwaiter().GetResult();
            return (process.ExitCode, process.ExitCode == 0 ? output : output + errors);
        }
    }
}

/// <summary>
/// An element as the UI Automation reader beside this file reports it: absolute, physical pixels, and
/// the top-level window it was found in — which is the demo's for a part and a menu's own for an entry.
/// </summary>
internal readonly record struct ElementInfo(
    string Path, string ControlType, int X, int Y, int Width, int Height, long Window)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Path} {ControlType} {Width}x{Height}+{X}+{Y} in {Window}");
}

/// <summary>One key of a chord: what to send, and whether Windows calls it an extended key.</summary>
internal readonly record struct Key(string Name, ushort Code, bool Extended)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Name} 0x{Code:X2}{(Extended ? " extended" : string.Empty)}");
}

/// <summary>
/// A chord in xdotool's spelling, which is the one the X11 back end takes and the one every recipe in
/// AGENTS.md §9 is written in, read into virtual-key codes. Both back ends therefore answer to
/// `ctrl+Down`, and a by-hand pass written on one platform runs on the other.
/// </summary>
internal static class Chord
{
    /// <summary>
    /// Modifiers first, then the one key they hold. The codes are WinUser.h's, read from the Windows
    /// SDK rather than recalled; the extended flag is what tells the arrows and the navigation block
    /// apart from the numeric keypad, which share their virtual-key codes.
    /// </summary>
    public static IReadOnlyList<Key> Parse(string chord)
    {
        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new UsageException($"`{chord}` is not a chord");
        }

        List<Key> keys = [];
        for (int i = 0; i < parts.Length - 1; i++)
        {
            keys.Add(Modifier(parts[i], chord));
        }

        keys.Add(Named(parts[^1], chord));
        return keys;
    }

    private static Key Modifier(string part, string chord) => part.ToLowerInvariant() switch
    {
        "ctrl" or "control" => new Key("VK_CONTROL", 0x11, false),
        "shift" => new Key("VK_SHIFT", 0x10, false),
        "alt" => new Key("VK_MENU", 0x12, false),
        "super" or "meta" or "win" => new Key("VK_LWIN", 0x5B, true),
        _ => throw new UsageException(
            $"`{part}` in `{chord}` is not a modifier: ctrl, shift, alt or super"),
    };

    private static Key Named(string part, string chord)
    {
        // The arrows and the navigation block are extended; their codes are shared with the keypad,
        // and without the flag a `Down` can arrive as the keypad's 2.
        switch (part)
        {
            case "Down": return new Key("VK_DOWN", 0x28, true);
            case "Up": return new Key("VK_UP", 0x26, true);
            case "Left": return new Key("VK_LEFT", 0x25, true);
            case "Right": return new Key("VK_RIGHT", 0x27, true);
            case "Home": return new Key("VK_HOME", 0x24, true);
            case "End": return new Key("VK_END", 0x23, true);
            case "Prior" or "Page_Up": return new Key("VK_PRIOR", 0x21, true);
            case "Next" or "Page_Down": return new Key("VK_NEXT", 0x22, true);
            case "Insert": return new Key("VK_INSERT", 0x2D, true);
            case "Delete": return new Key("VK_DELETE", 0x2E, true);
            case "Return" or "Enter": return new Key("VK_RETURN", 0x0D, false);
            case "Escape" or "Esc": return new Key("VK_ESCAPE", 0x1B, false);
            case "Tab": return new Key("VK_TAB", 0x09, false);
            case "space": return new Key("VK_SPACE", 0x20, false);
            case "BackSpace": return new Key("VK_BACK", 0x08, false);
        }

        if (part.Length > 1 && part[0] == 'F' && int.TryParse(part[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int f) && f is >= 1 and <= 24)
        {
            return new Key($"VK_F{f.ToString(CultureInfo.InvariantCulture)}", (ushort)(0x70 + f - 1), false);
        }

        if (part.Length == 1 && char.IsAsciiLetterOrDigit(part[0]))
        {
            // A letter's virtual-key code is its capital; the keyboard layout decides what it types,
            // which is the right behaviour for an accelerator and the wrong one for text.
            return new Key($"VK_{char.ToUpperInvariant(part[0])}", char.ToUpperInvariant(part[0]), false);
        }

        throw new UsageException(
            $"`{part}` in `{chord}` is not a key this driver can send: a letter, a digit, F1 to F24, or "
            + "Down, Up, Left, Right, Home, End, Prior, Next, Insert, Delete, Return, Escape, Tab, space "
            + "or BackSpace");
    }
}

/// <summary>
/// The Windows back end: UI Automation to find a part by its <c>AutomationId</c>, <c>SendInput</c> for
/// input, and <c>PrintWindow</c> for pixels — plan 00023 phase 4, written and measured on a Windows
/// machine because UI Automation only runs on one.
/// </summary>
internal sealed class Windows : IBackEnd
{
    /// <summary>The main window's title; the F12 log window is a second window titled differently.</summary>
    private const string DemoTitle = "DiffView Demo";

    /// <summary>The process a launch in this chain, or an earlier one, started. Null until there is one.</summary>
    private int? _launched;

    public string StateFile { get; } = Repo.Scratch("state-windows.txt");

    public static Windows Discover()
    {
        // Every rectangle below is physical pixels. A process that has not said this is DPI aware is
        // answered in virtualised coordinates on any display past 100%, and every click lands scaled.
        // XamlQuality's docs/ai-drivable-ui.md, rule 11: a figure halved or doubled is a DPI story.
        Native.SetProcessDpiAwarenessContext(Native.PerMonitorAwareV2);
        return new Windows();
    }

    public void Run(Verb verb, DriveState state)
    {
        // A launch records which process it started, and every verb after it prefers that one's
        // window. Two demos on one desktop otherwise answer to the same title, and parallel work
        // collides — the TailBlazer port's rule, learned from its own parallel runs.
        _launched = state.Pid;

        switch (verb)
        {
            case LaunchVerb launch:
                Launch(launch.DemoArguments, state);
                break;
            case WindowVerb:
                Console.WriteLine($"demo {Demo()}");
                break;
            case KeyVerb key:
                // Parsed before the window is touched, so a chord nobody can send fails without
                // having taken anyone's foreground away first.
                IReadOnlyList<Key> chord = Chord.Parse(key.Chord);
                Focused(Demo().Id);
                Send(chord);
                break;
            case ClickVerb click:
                // Where it will land is worked out before anything is raised, and only the window the
                // click is actually for is raised. Raising the demo before clicking an entry of a menu
                // the demo opened is what light-dismisses that menu — the click then lands on the pane
                // underneath, which is a plausible-looking wrong result rather than a failure.
                (int x, int y, long onto) = Absolute(click.Target, state, "click");
                if (onto == Demo().Id)
                {
                    Activate(onto);
                }

                Click(click.Button, x, y, onto);
                break;
            case DragVerb drag:
                (int fromX, int fromY, long surface) = Absolute(drag.From, state, "drag");
                WindowInfo over = Info(surface);
                if (drag.ToX < 0 || drag.ToY < 0 || drag.ToX >= over.Width || drag.ToY >= over.Height)
                {
                    throw new DriveException(string.Create(
                        CultureInfo.InvariantCulture,
                        $"a drag to {drag.ToX},{drag.ToY} ends outside {drag.From.RelativeTo.Canonical}, which is "
                        + $"{over.Width}x{over.Height}: refused rather than released over whatever lies there"));
                }

                if (surface == Demo().Id)
                {
                    Activate(surface);
                }

                Drag(drag.Button, fromX, fromY, over.X + drag.ToX, over.Y + drag.ToY, surface);
                break;
            case MarkVerb:
                state.Mark = [.. Tops()];
                state.Popup = null;
                Console.WriteLine($"mark {state.Mark.Count} top-level windows");
                break;
            case PopupVerb:
                WindowInfo popup = AppearedSince(state, TimeSpan.FromSeconds(5))
                    ?? throw new DriveException("no new top-level window appeared since the mark");
                state.Popup = popup.Id;
                Console.WriteLine($"popup {popup}");
                break;
            case GeometryVerb geometry:
                Console.WriteLine($"{geometry.Window.Canonical} {Info(Resolve(geometry.Window, state))}");
                break;
            case CaptureVerb capture:
                Capture(Resolve(capture.Window, state), capture.OutPath);
                break;
            case HoverVerb hover:
                Hover(hover.Target, state);
                break;
            default:
                throw new DriveException($"the Windows back end has no implementation of `{verb.Canonical()[0]}`");
        }
    }

    /// <summary>
    /// An element the UI Automation reader found, by a path of ids from the demo's window down. Read
    /// from a report of tab-separated fields rather than parsed out of prose, so no locale reaches it.
    /// </summary>
    public static ElementInfo ParseElement(string report)
    {
        string[] fields = report.Trim().Split('\t');
        if (fields.Length != 7)
        {
            throw new DriveException(
                $"the UI Automation reader answered {fields.Length} fields, not 7: {report.Trim()}");
        }

        return new ElementInfo(
            fields[0],
            fields[1],
            Int(fields[2], report),
            Int(fields[3], report),
            Int(fields[4], report),
            Int(fields[5], report),
            Int(fields[6], report));
    }

    private static int Int(string field, string report) =>
        int.TryParse(field, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new DriveException($"the UI Automation reader answered `{field}`, not a number: {report.Trim()}");

    /// <summary>
    /// The demo's main window, as its client area in screen coordinates — not its frame, so a
    /// coordinate means the same thing here as it does under X11, where the window manager's frame is
    /// a window of its own and `demo` is the client.
    /// </summary>
    private WindowInfo Demo()
    {
        // Titled, because the demo's F12 log window shares this process and would otherwise answer
        // too; and of the launched process where there was a launch, because the title is all that
        // tells two demos apart.
        List<long> titled =
        [
            .. Tops(onlyDemoProcess: false).Where(id => Native.Title((nint)id) == DemoTitle),
        ];

        if (_launched is { } process && titled.Any(id => Native.ProcessOf((nint)id) == process))
        {
            titled = [.. titled.Where(id => Native.ProcessOf((nint)id) == process)];
        }

        List<WindowInfo> candidates = [.. titled.Select(Info).Where(info => info.Viewable)];

        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new DriveException(
                $"no viewable window titled \"{DemoTitle}\": is the demo running? `launch` starts it"),
            _ => throw new DriveException(
                $"{candidates.Count} viewable windows are titled \"{DemoTitle}\": close the ones you are not driving"),
        };
    }

    /// <summary>A window's client area in screen coordinates, and whether anything of it is on screen.</summary>
    private static WindowInfo Info(long id)
    {
        nint window = (nint)id;
        if (!Native.IsWindow(window))
        {
            throw new DriveException($"there is no window {id.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!Native.GetClientRect(window, out Native.Rect client))
        {
            throw new DriveException($"could not read window {id.ToString(CultureInfo.InvariantCulture)}");
        }

        Native.Point origin = default;
        Native.ClientToScreen(window, ref origin);
        bool viewable = Native.IsWindowVisible(window) && !Native.IsIconic(window);
        return new WindowInfo(id, origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top, viewable);
    }

    /// <summary>
    /// The top-level windows of the demo's process. A context menu is a window of its own — the
    /// hosting guide says so and `OverlayPopups` is left unset — so it is found from the desktop root
    /// AND in the process, never under the main window and never across the whole desktop.
    /// </summary>
    private IReadOnlyList<long> Tops(bool onlyDemoProcess = true)
    {
        uint wanted = onlyDemoProcess ? DemoProcess() : 0;
        List<long> found = [];
        Native.EnumWindows(
            (window, _) =>
            {
                if (!onlyDemoProcess || Native.ProcessOf(window) == wanted)
                {
                    found.Add(window);
                }

                return true;
            },
            0);
        return found;
    }

    private uint DemoProcess() => Native.ProcessOf((nint)Demo().Id);

    private long Resolve(WindowRef window, DriveState state) => window.Kind switch
    {
        "demo" => Demo().Id,
        "popup" => state.Popup ?? throw new DriveException("no popup found yet: `mark`, act, then `popup`"),
        _ => window.Id,
    };

    /// <summary>Where a target is on the screen, and the top-level window that pixel should belong to.</summary>
    private (int X, int Y, long Window) Absolute(Target target, DriveState state, string verb)
    {
        if (target is IdTarget id)
        {
            ElementInfo element = Element(id);
            Console.WriteLine($"element {element}");
            return (element.X + (element.Width / 2), element.Y + (element.Height / 2), element.Window);
        }

        PointTarget point = (PointTarget)target;
        WindowInfo origin = Info(Resolve(point.RelativeTo, state));
        if (point.X < 0 || point.Y < 0 || point.X >= origin.Width || point.Y >= origin.Height)
        {
            throw new DriveException(string.Create(
                CultureInfo.InvariantCulture,
                $"{point.X},{point.Y} is outside {point.RelativeTo.Canonical}, which is {origin.Width}x{origin.Height}: "
                + $"refused rather than sent to whatever lies there"));
        }

        return (origin.X + point.X, origin.Y + point.Y, origin.Id);
    }

    /// <summary>
    /// The part a path of <c>AutomationId</c>s names, through the reader beside this file. A separate
    /// process because UI Automation's managed client lives in the Windows Desktop framework, which a
    /// portable file-based app cannot reference without becoming a Windows-only one.
    /// </summary>
    private ElementInfo Element(IdTarget target)
    {
        string reader = Path.Combine(Repo.Root(), "scripts", "drive-demo-uia.ps1");
        (int exit, string output) = Run("pwsh", [
            "-NoProfile",
            "-NonInteractive",
            "-File", reader,
            "-DemoProcessId", DemoProcess().ToString(CultureInfo.InvariantCulture),
            "-Path", target.Path,
        ]);

        return exit == 0
            ? ParseElement(output)
            : throw new DriveException($"the UI Automation reader could not find `{target.Path}`:\n{output.Trim()}");
    }

    /// <summary>
    /// Asks for the keyboard, which is all a key needs. Best effort, as it is under X11: Windows
    /// refuses a foreground change asked for by a process that does not already hold the foreground.
    /// </summary>
    /// <remarks>
    /// ⛔ It does not force the window up the z-order, and must not. A driver that raises the demo over
    /// whatever the person is doing takes their clicks instead — which is the same defect as the one
    /// below, pointed the other way. The TailBlazer port, which has ~31 of these harnesses on this
    /// estate, sends no synthetic input on a shared desktop at all for exactly this reason: it drives
    /// through automation patterns, gives a control an honest peer where Avalonia leaves one silent,
    /// and moves anything that truly needs a keystroke or a drag into a headless fixture. Here the
    /// input verbs stay, because a by-hand pass is attended by someone looking at the window — and
    /// when the window is not actually clickable, <see cref="Beneath"/> says so rather than guessing.
    /// </remarks>
    private static bool Activate(long id)
    {
        nint window = (nint)id;
        if (Native.IsIconic(window))
        {
            Native.ShowWindow(window, Native.ShowRestore);
        }

        Native.SetForegroundWindow(window);

        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(2))
        {
            if (Native.GetForegroundWindow() == window)
            {
                // It has the keyboard, but its first paint after being raised may not have landed.
                Thread.Sleep(150);
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
    }

    /// <summary>
    /// Refuses to type when the demo does not hold the keyboard. An injected keystroke goes to
    /// whatever window has focus, so a chord sent at a demo that is not focused is typed into the
    /// person's own application — silently, and `ctrl+f` or `ctrl+s` there does something real. This
    /// is <see cref="Beneath"/>'s rule for the keyboard, and it is the more dangerous of the two.
    /// </summary>
    private static void Focused(long demo)
    {
        if (Activate(demo))
        {
            return;
        }

        nint holding = Native.GetForegroundWindow();
        string what = holding == 0
            ? "nothing"
            : $"\"{Native.Title(holding)}\" (window {((long)holding).ToString(CultureInfo.InvariantCulture)})";
        throw new DriveException(
            $"the demo could not take the keyboard — {what} is holding it, and Windows refuses a "
            + $"foreground change asked for by a process that does not already hold one. The chord is "
            + $"NOT sent, because it would have been typed into whatever does hold the keyboard. Click "
            + $"the demo once and run this again, or run the pass on a desktop nobody else is using");
    }

    private static void Send(IReadOnlyList<Key> chord)
    {
        List<Native.Input> inputs = [];
        foreach (Key key in chord)
        {
            inputs.Add(Native.Press(key, down: true));
        }

        // Released in the opposite order, so a modifier is still held while its key comes up.
        for (int i = chord.Count - 1; i >= 0; i--)
        {
            inputs.Add(Native.Press(chord[i], down: false));
        }

        Native.Inject([.. inputs], $"the chord {string.Join('+', chord.Select(k => k.Name))}");
    }

    private static void Click(MouseButton button, int x, int y, long expected)
    {
        (uint down, uint up) = button switch
        {
            MouseButton.Left => (0x0002u, 0x0004u),
            MouseButton.Middle => (0x0020u, 0x0040u),
            _ => (0x0008u, 0x0010u),
        };

        // The pointer is moved with SetCursorPos rather than a normalised absolute SendInput, which is
        // expressed in 65535ths of the virtual desktop and rounds a pixel out on a multi-monitor desk.
        if (!Native.SetCursorPos(x, y))
        {
            throw new DriveException(string.Create(CultureInfo.InvariantCulture, $"could not move the pointer to {x},{y}"));
        }

        Thread.Sleep(50);
        Beneath(x, y, expected, "click");
        Native.Inject([Native.Button(down), Native.Button(up)], "a click");
    }

    /// <summary>
    /// Press, travel, release. ⛔ The travel is the part that matters and the part a first attempt
    /// leaves out: a press followed by a jump to the far point and a release is two positions, not a
    /// gesture, and a control that tracks the pointer sees nothing between them. Avalonia's splitter
    /// and the overview map both move with the pointer, so the path is walked in steps with the
    /// button held, as a hand would.
    /// </summary>
    private static void Drag(MouseButton button, int fromX, int fromY, int toX, int toY, long expected)
    {
        (uint down, uint up) = button switch
        {
            MouseButton.Left => (0x0002u, 0x0004u),
            MouseButton.Middle => (0x0020u, 0x0040u),
            _ => (0x0008u, 0x0010u),
        };

        Native.SetCursorPos(fromX, fromY);
        Thread.Sleep(80);
        Beneath(fromX, fromY, expected, "drag");
        Native.Inject([Native.Button(down)], "a drag's press");

        try
        {
            const int steps = 16;
            for (int i = 1; i <= steps; i++)
            {
                Native.SetCursorPos(
                    fromX + ((toX - fromX) * i / steps),
                    fromY + ((toY - fromY) * i / steps));

                // Slower than a hand, deliberately: a control that coalesces moves still sees every
                // one of these, and a run nobody is watching has no reason to hurry.
                Thread.Sleep(25);
            }
        }
        finally
        {
            // Released whatever happened in between: a button left down is the pointer taken
            // hostage, and the person gets their desktop back in that state.
            Native.Inject([Native.Button(up)], "a drag's release");
        }
    }

    /// <summary>
    /// Refuses a click that would land on some other window. An injected click goes to whatever is
    /// topmost at that pixel, so a window of the person's own lying over the demo takes it — silently,
    /// and in whatever application that is. This is the X11 back end's "refused rather than sent to
    /// whatever lies there" guard against a second way of arriving at the same place.
    /// </summary>
    private static void Beneath(int x, int y, long expected, string verb)
    {
        nint under = Native.WindowFromPoint(new Native.Point { X = x, Y = y });
        nint top = under == 0 ? 0 : Native.GetAncestor(under, Native.GaRoot);
        if (top == (nint)expected)
        {
            return;
        }

        string what = top == 0
            ? "nothing"
            : $"\"{Native.Title(top)}\" (window {((long)top).ToString(CultureInfo.InvariantCulture)})";
        throw new DriveException(string.Create(
            CultureInfo.InvariantCulture,
            $"{verb} at {x},{y} would land on {what}, not on window {expected}: another window is over the "
            + $"demo there. Raise the demo, move what is on top of it, or run the pass on a desktop nobody "
            + $"else is using"));
    }

    /// <summary>
    /// The window that was not there at the mark, once its geometry holds still. A popup opens on one
    /// layout pass and lays out on the next here as it does under X11, so a frame taken between them
    /// is an empty box; and a deadline is polled rather than slept through.
    /// </summary>
    private WindowInfo? AppearedSince(DriveState state, TimeSpan patience)
    {
        HashSet<long> mark = state.Mark ?? throw new DriveException("`popup` needs a `mark` before the action that opens it");
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < patience)
        {
            foreach (long candidate in Tops().Where(id => !mark.Contains(id)))
            {
                WindowInfo info = Info(candidate);
                if (info.Viewable && info.Width > 1 && info.Height > 1)
                {
                    return Settled(info);
                }
            }

            Thread.Sleep(100);
        }

        return null;
    }

    private static WindowInfo Settled(WindowInfo first)
    {
        WindowInfo previous = first;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Thread.Sleep(250);
            WindowInfo now = Info(first.Id);
            if (now == previous)
            {
                return now;
            }

            previous = now;
        }

        return previous;
    }

    private static void Capture(long id, string outPath)
    {
        WindowInfo info = Info(id);
        if (!info.Viewable)
        {
            throw new DriveException($"window {id.ToString(CultureInfo.InvariantCulture)} is not viewable, so there is nothing to capture");
        }

        byte[] pixels = Native.Draw((nint)id, info.Width, info.Height);
        if (pixels.Chunk(4).All(pixel => pixel[0] <= 2 && pixel[1] <= 2 && pixel[2] <= 2))
        {
            // Measured on 2026-10-08: PrintWindow without PW_RENDERFULLCONTENT returns exactly this for
            // an Avalonia window, and reports success. The flag is passed, so a black frame now means
            // something else — and it is still not evidence about the application.
            throw new DriveException(
                $"captured window {id.ToString(CultureInfo.InvariantCulture)} and every pixel is black: the window drew nothing. "
                + "Judge from the demo's log, not from the pixels");
        }

        Png.Write(outPath, info.Width, info.Height, pixels);
        Console.WriteLine($"capture {info} {outPath}");
    }

    private void Hover(Target target, DriveState state)
    {
        Activate(Demo().Id);
        (int x, int y, long onto) = Absolute(target, state, "hover");

        // Parked inside the demo first and away from anything with a tooltip, for the reason the X11
        // back end parks: moving from one tooltip's owner to another re-uses the window rather than
        // opening one. Not a screen corner, where a hot corner may be waiting.
        WindowInfo demo = Demo();
        Native.SetCursorPos(demo.X + demo.Width - 3, demo.Y + demo.Height - 3);
        Thread.Sleep(1000);
        state.Mark = [.. Tops()];
        Beneath(x, y, onto, "hover");
        Native.SetCursorPos(x, y);

        WindowInfo tooltip = AppearedSince(state, TimeSpan.FromSeconds(3))
            ?? throw new DriveException(
                $"no tooltip appeared at {x},{y} within 3 s: the control there may have none, or the pointer "
                + "may not be where it was meant to be — `capture demo` shows the pointer in the frame");
        state.Popup = tooltip.Id;
        Console.WriteLine($"tooltip {tooltip}");
    }

    private void Launch(IReadOnlyList<string> demoArguments, DriveState state)
    {
        string root = Repo.Root();
        string project = Path.Combine(root, "src", "DiffView.Demo", "DiffView.Demo.csproj");

        // Built in the foreground, so a compile error is reported here rather than buried in a log.
        (int built, string buildOutput) = Run("dotnet", ["build", project, "-c", "Debug", "-nologo", "-v", "q"]);
        if (built != 0)
        {
            throw new DriveException($"the demo did not build:\n{buildOutput}");
        }

        string assembly = Tool("dotnet", "msbuild", project, "-nologo", "-p:Configuration=Debug", "-getProperty:TargetPath").Trim();

        // TargetPath is the managed assembly; what the shell can start is the apphost beside it.
        string host = Path.ChangeExtension(assembly, ".exe");
        if (!File.Exists(host))
        {
            throw new DriveException($"the demo built but there is no {host} to start");
        }

        // Started through the shell, which is what detaches it: a child whose output this process
        // redirects would be reaped with it at a turn boundary, and a redirected pipe nobody drains
        // fills and stops the demo. The demo writes its own log, which is the record either way.
        ProcessStartInfo start = new(host) { UseShellExecute = true, WorkingDirectory = root };
        foreach (string argument in demoArguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process demo = Process.Start(start) ?? throw new DriveException($"could not start {host}");
        state.Pid = demo.Id;
        state.Mark = null;
        state.Popup = null;
        string logs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiffView", "logs");
        Console.WriteLine($"launched pid={demo.Id.ToString(CultureInfo.InvariantCulture)} logs={logs}");

        Stopwatch clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(60))
        {
            if (demo.HasExited)
            {
                throw new DriveException($"the demo exited with {demo.ExitCode} before showing a window; see {logs}");
            }

            try
            {
                Console.WriteLine($"demo {Demo()}");
                return;
            }
            catch (DriveException)
            {
                Thread.Sleep(500);
            }
        }

        throw new DriveException($"the demo started but showed no window in 60 s; see {logs}");
    }

    /// <summary>A tool that must succeed; its standard output.</summary>
    private static string Tool(string tool, params string[] arguments)
    {
        (int exit, string output) = Run(tool, arguments);
        return exit == 0
            ? output
            : throw new DriveException($"`{tool} {string.Join(' ', arguments)}` exited {exit}:\n{output}");
    }

    private static (int Exit, string Output) Run(string tool, string[] arguments)
    {
        ProcessStartInfo start = new(tool)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new DriveException($"could not start {tool}");
        }
        catch (Win32Exception)
        {
            throw new DriveException(
                $"`{tool}` is not installed. The Windows back end needs PowerShell 7 for its UI Automation "
                + "reader — `winget install Microsoft.PowerShell`");
        }

        using (process)
        {
            // Both pipes drained concurrently: reading one to its end first deadlocks once the child
            // fills the other's buffer.
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string output = stdout.GetAwaiter().GetResult();
            string errors = stderr.GetAwaiter().GetResult();
            return (process.ExitCode, process.ExitCode == 0 ? output : output + errors);
        }
    }
}

/// <summary>
/// Enough of a PNG writer for a window capture: 8-bit truecolour, one unfiltered scanline per row.
/// Hand-written because a file-based app carries no image library, and plan 00023's driver takes no
/// dependency to get one. Proven against GDI+, which reads what it writes back as a 24-bit PNG.
/// </summary>
internal static class Png
{
    /// <summary>Writes <paramref name="bgra"/>, top-down and four bytes a pixel with blue first.</summary>
    public static void Write(string path, int width, int height, byte[] bgra)
    {
        byte[] raw = new byte[height * ((width * 3) + 1)];
        int at = 0;
        for (int y = 0; y < height; y++)
        {
            raw[at++] = 0;                                  // filter: none
            int row = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                raw[at++] = bgra[row + (x * 4) + 2];
                raw[at++] = bgra[row + (x * 4) + 1];
                raw[at++] = bgra[row + (x * 4)];
            }
        }

        using MemoryStream deflated = new();
        using (ZLibStream zlib = new(deflated, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using FileStream file = File.Create(path);
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        byte[] header = new byte[13];
        Big(header, 0, width);
        Big(header, 4, height);
        header[8] = 8;      // bits per channel
        header[9] = 2;      // colour type: truecolour
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", deflated.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void Chunk(Stream file, string type, byte[] data)
    {
        byte[] length = new byte[4];
        Big(length, 0, data.Length);
        file.Write(length);

        byte[] typed = [(byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3]];
        file.Write(typed);
        file.Write(data);

        byte[] crc = new byte[4];
        Big(crc, 0, (int)Crc32([.. typed, .. data]));
        file.Write(crc);
    }

    private static void Big(byte[] into, int at, int value)
    {
        into[at] = (byte)(value >> 24);
        into[at + 1] = (byte)(value >> 16);
        into[at + 2] = (byte)(value >> 8);
        into[at + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}

/// <summary>
/// The Win32 calls the Windows back end makes. Every constant here was read from the Windows SDK's own
/// headers on this machine — <c>WinUser.h</c> for the virtual keys, the input flags and
/// <c>PW_RENDERFULLCONTENT</c> — rather than recalled.
/// </summary>
internal static class Native
{
    public const uint PwClientOnly = 0x00000001;
    public const uint PwRenderFullContent = 0x00000002;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtended = 0x0001;
    private const uint KeyEventUp = 0x0002;

    /// <summary>GA_ROOT: the top-level window a child belongs to.</summary>
    public const uint GaRoot = 2;

    /// <summary>SW_RESTORE.</summary>
    public const int ShowRestore = 9;


    /// <summary>PER_MONITOR_AWARE_V2.</summary>
    public static readonly nint PerMonitorAwareV2 = -4;

    public delegate bool EnumWindowsProc(nint window, nint parameter);

    public static Input Press(Key key, bool down) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                Vk = key.Code,
                Flags = (key.Extended ? KeyEventExtended : 0) | (down ? 0 : KeyEventUp),
            },
        },
    };

    public static Input Button(uint flags) => new()
    {
        Type = InputMouse,
        Union = new InputUnion { Mouse = new MouseInput { Flags = flags } },
    };

    public static void Inject(Input[] inputs, string what)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            // UIPI blocks input into a window of a more privileged process, and says so only this way.
            throw new DriveException(
                $"Windows accepted {sent.ToString(CultureInfo.InvariantCulture)} of "
                + $"{inputs.Length.ToString(CultureInfo.InvariantCulture)} inputs for {what} "
                + $"(error {Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)}): if the demo "
                + "is running elevated and this is not, Windows refuses input into it");
        }
    }

    /// <summary>A window's title, which is how the demo is told from its own log window.</summary>
    public static string Title(nint window)
    {
        int length = GetWindowTextLength(window);
        if (length <= 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder text = new(length + 1);
        return GetWindowText(window, text, text.Capacity) > 0 ? text.ToString() : string.Empty;
    }

    public static uint ProcessOf(nint window)
    {
        GetWindowThreadProcessId(window, out uint process);
        return process;
    }

    /// <summary>
    /// A window's client area as pixels, four bytes each, blue first and top-down. The window draws
    /// itself, so this works with it behind others or partly off screen; without
    /// <see cref="PwRenderFullContent"/> an Avalonia window draws nothing at all and still reports
    /// success, which is measured in this file's header.
    /// </summary>
    public static byte[] Draw(nint window, int width, int height)
    {
        nint screen = GetDC(0);
        nint memory = CreateCompatibleDC(screen);
        BitmapInfo info = new()
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,          // top-down, so row 0 is the top row
                Planes = 1,
                BitCount = 32,
                Compression = 0,           // BI_RGB
            },
        };

        nint bitmap = CreateDIBSection(memory, ref info, 0, out nint bits, 0, 0);
        if (bitmap == 0)
        {
            DeleteDC(memory);
            ReleaseDC(0, screen);
            throw new DriveException("could not make a bitmap to draw the window into");
        }

        nint previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(window, memory, PwClientOnly | PwRenderFullContent))
            {
                throw new DriveException(
                    $"the window would not draw itself (error {Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)})");
            }

            byte[] pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nuint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint Extra;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colour0;
        public uint Colour1;
        public uint Colour2;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDpiAwarenessContext(nint context);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(nint window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(nint window, ref Point point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(nint window, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint window, out uint process);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();


    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    public static extern nint WindowFromPoint(Point point);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(nint window, nint deviceContext, uint flags);

    [DllImport("user32.dll")]
    public static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll")]
    public static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern nint CreateDIBSection(
        nint deviceContext, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);

    [DllImport("gdi32.dll")]
    public static extern nint SelectObject(nint deviceContext, nint o);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(nint o);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(nint deviceContext);
}
