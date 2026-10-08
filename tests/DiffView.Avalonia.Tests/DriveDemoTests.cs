using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>scripts/drive-demo</c> drives the running demo for a by-hand pass (plan 00023). Its verbs need a
/// display and a window, which a test run has neither of. What can be tested without them is how it
/// reads — its own command line, and the two reports <c>xwininfo</c> hands it — and that is where a
/// driver goes wrong in silence: a window misread is a click sent somewhere else.
/// </summary>
/// <remarks>
/// <para>
/// Driven through its real command line, as <see cref="CatchCrashTests"/> drives <c>catch-crash</c>:
/// what runs here is what a developer runs.
/// </para>
/// <para>
/// The fixtures under <c>fixtures/x11</c> were captured from the real tools on 2026-09-28 with the demo
/// running, under <c>LC_ALL=de_DE.UTF-8</c>. For the root listing, the demo's unmapped log window — a
/// direct child of the root, which the window manager has not reparented — was given a hostile title
/// first: a geometry, a position, a colon and quotes, set as <c>UTF8_STRING</c> the way a toolkit
/// sets one.
/// </para>
/// </remarks>
public sealed class DriveDemoTests
{
    private static string Fixture(string name) =>
        RepoPaths.Source(Path.Combine("fixtures", "x11", name));

    private static string WindowsFixture(string name) =>
        RepoPaths.Source(Path.Combine("fixtures", "windows", name));

    [Fact]
    public void Every_verb_prints_in_canonical_form_and_reads_back_to_the_same_lines()
    {
        string[] chain =
        [
            "launch", "--edit", "both",
            "then", "window",
            "then", "key", "ctrl+Down",
            "then", "click", "left", "88", "16",
            "then", "click", "3", "105", "829", "in", "popup",
            "then", "click", "Right", "--id", "SideBySide/LeftPane/LineNumbers",
            "then", "mark",
            "then", "popup",
            "then", "geometry", "0x1200017",
            "then", "capture", "popup", "menu.png",
            "then", "hover", "300", "200",
        ];

        (int exitCode, string output) = Run(["--parse", .. chain]);
        Assert.True(exitCode == 0, output);
        string[] lines = Lines(output);
        Assert.Equal(
            [
                "launch\t--edit\tboth",
                "window",
                "key\tctrl+Down",
                "click\tleft\t88\t16\tin\tdemo",
                "click\tright\t105\t829\tin\tpopup",
                "click\tright\t--id\tSideBySide/LeftPane/LineNumbers",
                "mark",
                "popup",
                "geometry\t18874391",
                "capture\tpopup\tmenu.png",
                "hover\t300\t200\tin\tdemo",
            ],
            lines);

        // Fed back, the canonical tokens give the same lines: nothing the canonical form prints is read
        // differently the second time, an id path with its separators included.
        string[] again = [.. lines.SelectMany((line, i) => i == 0 ? line.Split('\t') : ["then", .. line.Split('\t')])];
        (int againExit, string againOutput) = Run(["--parse", .. again]);
        Assert.True(againExit == 0, againOutput);
        Assert.Equal(lines, Lines(againOutput));
    }

    [Theory]
    [InlineData("click left 5", "click takes <x> <y> [in <window>], or --id <path>")]
    [InlineData("mark then", "an empty verb")]
    [InlineData("frobnicate", "unknown verb `frobnicate`")]
    [InlineData("click sideways 1 2", "`sideways` is not a button")]
    [InlineData("geometry nowhere", "`nowhere` is not a window")]
    [InlineData("key", "key takes one chord")]
    [InlineData("click left --id SideBySide//LeftPane", "is not an AutomationId path")]
    [InlineData("click left --id /LeftPane", "is not an AutomationId path")]
    public void A_malformed_chain_is_refused_as_usage_and_says_what_is_wrong(string chain, string expected)
    {
        (int exitCode, string output) = Run(["--parse", .. chain.Split(' ')]);

        Assert.Equal(2, exitCode);
        Assert.Contains(expected, output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_roots_children_are_read_by_the_two_ends_of_each_line_whatever_a_title_holds()
    {
        string fixture = Fixture("root-children.txt");
        (int exitCode, string output) = Run(["--parse-children", fixture]);
        Assert.True(exitCode == 0, output);
        string[] windows = Lines(output);

        // Every child, and nothing else: the listing says how many it holds.
        int declared = int.Parse(
            Regex.Match(File.ReadAllText(fixture), @"(\d+) children:").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(declared, windows.Length);

        // The hostile title carries 999x99+7+7 and +7+7; the window is what the END of its line says.
        Assert.Contains("16777235 1728x873+10+10", windows);
        Assert.DoesNotContain(windows, w => w.Contains("999x99", StringComparison.Ordinal));

        // In the listing's own order — topmost first: the tooltip open at the capture leads, and
        // mutter's guard window, at the bottom of the stack, ends it.
        Assert.Equal("16777307 153x35+617+543", windows[0]);
        Assert.Equal("4194314 2304x1296+0+0", windows[^1]);

        // xwininfo writes a negative offset after the plus.
        Assert.Contains("4194310 1x1+-100+-100", windows);
    }

    [Fact]
    public void A_window_report_is_read_by_its_labels_under_a_German_locale()
    {
        // Each report also carries a relative position and a -geometry hint that disagree with the
        // absolute one, so reading the wrong line shows.
        Assert.Equal(
            "16777239 1100x720+602+283 viewable",
            Single(Run(["--parse-info", Fixture("window-viewable.txt")])));
        Assert.Equal(
            "16777235 1728x873+10+10 not-viewable",
            Single(Run(["--parse-info", Fixture("window-unmapped.txt")])));
    }

    [Theory]
    [InlineData("ctrl+Down", "VK_CONTROL 0x11|VK_DOWN 0x28 extended")]
    [InlineData("shift+F7", "VK_SHIFT 0x10|VK_F7 0x76")]
    [InlineData("ctrl+shift+f", "VK_CONTROL 0x11|VK_SHIFT 0x10|VK_F 0x46")]
    [InlineData("alt+v", "VK_MENU 0x12|VK_V 0x56")]
    [InlineData("F12", "VK_F12 0x7B")]
    [InlineData("Return", "VK_RETURN 0x0D")]
    [InlineData("Page_Down", "VK_NEXT 0x22 extended")]
    [InlineData("super+Left", "VK_LWIN 0x5B extended|VK_LEFT 0x25 extended")]
    public void A_chord_is_read_as_the_modifiers_it_holds_and_the_one_key_they_hold(string chord, string expected)
    {
        // The arrows and the navigation block must come out extended: they share their virtual-key
        // codes with the numeric keypad, and without the flag `Down` can arrive as the keypad's 2.
        (int exitCode, string output) = Run(["--parse-chord", chord]);

        Assert.True(exitCode == 0, output);
        Assert.Equal(expected.Split('|'), Lines(output));
    }

    [Theory]
    [InlineData("hyper+a", "`hyper` in `hyper+a` is not a modifier")]
    [InlineData("ctrl+Enormous", "`Enormous` in `ctrl+Enormous` is not a key this driver can send")]
    [InlineData("F25", "`F25` in `F25` is not a key this driver can send")]
    public void A_chord_nobody_can_send_is_refused_and_says_which_part_of_it_is_wrong(string chord, string expected)
    {
        (int exitCode, string output) = Run(["--parse-chord", chord]);

        Assert.Equal(2, exitCode);
        Assert.Contains(expected, output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_elements_report_is_read_by_its_fields_wherever_on_the_desktop_it_is()
    {
        // Tab-separated fields and not prose, so no locale reaches the reading; and a window left of
        // the primary screen has negative coordinates, which a reader that forgot the sign drops.
        Assert.Equal(
            "SideBySide/LeftPane ControlType.Edit 531x618+268+346 in 70387178",
            Single(Run(["--parse-element", WindowsFixture("element-left-pane.txt")])));
        Assert.Equal(
            "Unified/Pane ControlType.Edit 1024x768+-1612+-284 in 133182",
            Single(Run(["--parse-element", WindowsFixture("element-on-a-second-screen.txt")])));
    }

    [Fact]
    public void Nothing_in_scripts_assumes_a_uid_a_display_or_a_repository_path()
    {
        // The scratch scripts the driver replaced each wrote all three in: the cookie under
        // /run/user/1001, the display as :0.0, and the checkout's own path (plan 00023).
        Regex[] assumptions =
        [
            new(@"/run/user/\d"),
            new(@"/home/\w"),
            new(@"(?i)\b[a-z]:\\+users\\+"),
            new(@"Xwaylandauth\.[A-Za-z0-9]"),
            new(@"DISPLAY=:\d"),
            new(@"""\s*:\d+(\.\d+)?\s*"""),
            new(@"\s-i\s+:\d"),
        ];

        string[] hits =
        [
            .. Directory.EnumerateFiles(RepoPaths.Source("scripts"))
                .SelectMany(path => File.ReadAllLines(path).Select((line, i) => (path, line, number: i + 1)))
                .Where(entry => assumptions.Any(a => a.IsMatch(entry.line)))
                .Select(entry => $"{Path.GetFileName(entry.path)}:{entry.number}: {entry.line.Trim()}"),
        ];

        Assert.True(
            hits.Length == 0,
            "A script assumes a uid, a display or a repository path — discover it instead, as "
            + "scripts/drive-demo.cs does:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    private static string[] Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();

    private static string Single((int ExitCode, string Output) run)
    {
        Assert.True(run.ExitCode == 0, run.Output);
        return Assert.Single(Lines(run.Output));
    }

    private static (int ExitCode, string Output) Run(string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("run");
        start.ArgumentList.Add(Path.Combine("scripts", "drive-demo.cs"));
        start.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start dotnet run.");

        // Both pipes drained concurrently: stdout to EOF first deadlocks once the child fills the
        // stderr pipe buffer, and the test then hangs rather than failing.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
