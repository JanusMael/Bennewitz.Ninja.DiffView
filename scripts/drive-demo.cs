// scripts/drive-demo.cs — a .NET 10 file-based app.
//
// Drives the running demo for a by-hand pass: starts it, finds its window and the popups it opens,
// clicks and types into it, and captures pixels. AGENTS.md §9 is the prose these verbs replace;
// plan 00023 is why they exist. Only the X11 back end is written. The Windows back end is plan 00023
// phase 4, and the macOS one is specified there and not written.
//
//   scripts/drive-demo.sh launch --edit both            build and start the demo detached, then wait
//                                                       for its window; prints the pid and the log
//   scripts/drive-demo.sh window                        the demo's main window
//   scripts/drive-demo.sh key ctrl+Down                 a key chord into the demo
//   scripts/drive-demo.sh click left 88 16              a click, relative to the demo's window
//   scripts/drive-demo.sh click left 105 829 in popup   ... or to the popup found last
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
// A target is a coordinate, or an automation name where the back end can address one:
// `click left --name "Show whitespace"`. The X11 back end cannot — there is no accessibility bridge
// here, and plan 00023 leaves AT-SPI out — so it refuses a name rather than guessing a position.
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
//
// The .sh and .ps1 wrappers beside this file run it from the repository root for you.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
          click <button> <x> <y> [in <window>]  button: left, middle or right (or 1, 2, 3)
          click <button> --name <text>          by automation name, where the back end can
          mark                                  remember the top-level windows there are now
          popup                                 the window that appeared since the mark
          geometry <window>                     position, size and map state
          capture <window> <out.png>            the window's pixels
          hover <x> <y> [in <window>]           wait for a tooltip there; it becomes the popup
        <window> is demo, popup, or an X window id (decimal or 0x-hex)
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

internal sealed record NameTarget(string Name) : Target
{
    public override IReadOnlyList<string> Canonical() => ["--name", Name];
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
        ["--name", var name] when name.Length > 0 => new NameTarget(name),
        [var x, var y] => new PointTarget(Coordinate(x), Coordinate(y), WindowRef.Demo),
        [var x, var y, "in", var window] => new PointTarget(Coordinate(x), Coordinate(y), WindowOf(window)),
        _ => throw new UsageException($"{verb} takes <x> <y> [in <window>], or --name <text>"),
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
            : throw new UsageException($"`{token}` is not a window: demo, popup, or an X window id");
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

        throw new DriveException(OperatingSystem.IsWindows()
            ? "the Windows back end is plan 00023 phase 4 and is not written yet"
            : "this platform has no back end: plan 00023 specifies the macOS one and does not write it");
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
                $"the X11 back end cannot address `{verb}` by automation name: it has no accessibility "
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
        string root = RepoRoot();
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

    private static string RepoRoot()
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
