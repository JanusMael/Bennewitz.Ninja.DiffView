// scripts/mutate-behaviour.cs — a .NET 10 file-based app.
//
// Proves a BEHAVIOUR test able to fail, by breaking the thing it guards and checking that it goes
// red — and that it goes red in the right place. `scripts/mutate-gates` does this for the
// static-analysis gates; this is its sibling for tests that drive the control and assert what it did.
//
// Why two harnesses and not one. `mutate-gates` derives its guards from the gate files and judges a
// mutation by which assertion was the FIRST to fail, which is the right question for a gate whose
// every line is an assertion over a derived population. A behaviour test is a scenario: what has to
// hold is that breaking the production code reddens it, that breaking a DIFFERENT part of the
// production code reddens a different stage of it, and that a change which should not matter leaves
// it green. Those are different declarations, so they are compared differently — and bolting a
// second judgement model into a 90 KB harness scoped to the gates would make both harder to read.
//
//   dotnet run scripts/mutate-behaviour.cs -- --list              name them and exit, running nothing
//   dotnet run scripts/mutate-behaviour.cs --                     run them all
//   dotnet run scripts/mutate-behaviour.cs -- --only refold       substring match on the name
//   dotnet run scripts/mutate-behaviour.cs -- --force             run over a dirty tree (see below)
//   dotnet run scripts/mutate-behaviour.cs -- --judge <log> red   judge a captured run, run nothing
//
// ⭐ `Expect` is COMPARED, not printed. A mutation that was meant to stay green and went red is as
// much a failure as one that was meant to go red and survived: the first says the test is asserting
// something it was never meant to, and plan 00031's own history has an example — a scratch mutation
// that "survived" because the generator caught its own exception as a fault boundary and the frame
// still rendered. The test was green and the code was broken.
//
// ⛔ It REFUSES TO START on a dirty tree unless --force, because it reverts with `git checkout` and
// an earlier generation of these harnesses repeatedly destroyed uncommitted work that way. It reads
// `--untracked-files=all`, so a `status.showUntrackedFiles=no` in a user's config cannot hide the
// file a revert is about to take back.
//
// ⛔ What a mutation may touch is DERIVED, not trusted: the whole repository's status is compared
// before and after every edit, and anything written outside the reverted scope is put back and stops
// the run. Without that, an edit landing outside the scope reads as a no-op, survives the revert, and
// contaminates every mutation after it.
//
// ⛔ Reverting a mutation does not UNBUILD it, so the project is rebuilt from a `finally` on every
// exit this program controls. A `dotnet test --no-build` straight after a killed run tests the last
// mutation's assemblies against reverted source.
//
// ⚠ A mutation whose edit matches nothing is a `no-op`, which FAILS the run. It is not a survivor and
// not a pass: the gate was never exercised, the code moved, and the mutation needs re-pointing. When a
// construct is deliberately designed out of existence its mutation is deleted and the plan says why.

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

string repo = Repo.Root();
List<string> only = [];
bool force = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--list":
            foreach (Mutation m in Mutations.All)
            {
                Console.WriteLine($"  {m.Name}");
                Console.WriteLine($"      {m.File}");
                Console.WriteLine($"      expects: {m.Expect} — {m.Because}");
            }

            return 0;

        case "--force":
            force = true;
            break;

        case "--only" when i + 1 < args.Length:
            only.Add(args[++i]);
            break;

        case "--judge" when i + 2 < args.Length:
            Reading read = Judge.Read(File.ReadAllText(args[i + 1]));
            string wanted = args[i + 2];
            Console.WriteLine(read);
            return read.Verdict == wanted ? 0 : 1;

        default:
            Console.Error.WriteLine($"mutate-behaviour: unknown argument `{args[i]}`");
            return 2;
    }
}

List<Mutation> chosen = only.Count == 0
    ? [.. Mutations.All]
    : [.. Mutations.All.Where(m => only.Any(o => m.Name.Contains(o, StringComparison.OrdinalIgnoreCase)))];

if (chosen.Count == 0)
{
    Console.Error.WriteLine("mutate-behaviour: --only matched no mutation. Try --list.");
    return 2;
}

string dirty = Git.Status(repo);
if (dirty.Length > 0 && !force)
{
    Console.Error.WriteLine("mutate-behaviour: refusing to start on a dirty tree. It reverts with");
    Console.Error.WriteLine("`git checkout`, which would take these with it:");
    Console.Error.WriteLine(dirty);
    Console.Error.WriteLine("Commit them, or pass --force if you have decided they are expendable.");
    return 2;
}

Console.WriteLine($"tree clean at {Git.Head(repo)}, {chosen.Count} mutation(s)");

Dictionary<string, Reading> results = [];
bool started = false;
try
{
    foreach (Mutation mutation in chosen)
    {
        string path = Path.Combine(repo, mutation.File.Replace('/', Path.DirectorySeparatorChar));
        string before = File.ReadAllText(path);
        int matches = Count(before, mutation.Old);
        if (matches != 1)
        {
            Console.WriteLine($"  no-op  {mutation.Name} — matched {matches.ToString(CultureInfo.InvariantCulture)} times, not once");
            results[mutation.Name] = Reading.NoOp(matches);
            continue;
        }

        string statusBefore = Git.Status(repo);
        File.WriteAllText(path, before.Replace(mutation.Old, mutation.New, StringComparison.Ordinal));
        started = true;

        try
        {
            // Compared before the run, not after it: an edit that lands somewhere unexpected must stop
            // the run before it has spent four minutes producing a verdict about the wrong tree.
            string stray = Git.Strayed(repo, statusBefore, mutation.File);
            if (stray.Length > 0)
            {
                Console.Error.WriteLine($"mutate-behaviour: `{mutation.Name}` wrote outside {mutation.File}:");
                Console.Error.WriteLine(stray);
                return 2;
            }

            if (!Dotnet.Build(repo, out string buildLog))
            {
                Console.WriteLine($"  build  {mutation.Name} — the mutation does not compile");
                results[mutation.Name] = Reading.Unbuildable(buildLog);
                continue;
            }

            Reading reading = Judge.Read(Dotnet.Test(repo, mutation.Class));
            results[mutation.Name] = reading;
            Console.WriteLine($"  {(reading.Verdict == mutation.Expect ? "ok" : "WRONG")}     {mutation.Name} — expected {mutation.Expect}, got {reading.Verdict}");
        }
        finally
        {
            Git.Restore(repo, mutation.File);
            if (File.ReadAllText(path) != before)
            {
                Console.Error.WriteLine($"mutate-behaviour: the revert of {mutation.File} did not take. Check it by hand.");
            }
        }
    }
}
finally
{
    if (started)
    {
        // A revert leaves the mutation's assemblies in bin/. Anything run after this program without
        // a rebuild would test them.
        Console.WriteLine("rebuilding, because a revert does not unbuild a mutation");
        Dotnet.Build(repo, out _);
    }
}

Console.WriteLine();
bool ok = true;
foreach (Mutation mutation in chosen)
{
    if (!results.TryGetValue(mutation.Name, out Reading r))
    {
        continue;
    }

    bool agree = r.Verdict == mutation.Expect;
    ok &= agree;
    Console.WriteLine($"{(agree ? "  ok  " : " WRONG")}  {mutation.Name}");
    Console.WriteLine($"          expected {mutation.Expect}, got {r.Verdict} — {mutation.Because}");
    if (r.Message.Length > 0)
    {
        Console.WriteLine($"          {r.Message}");
    }

    foreach (int line in r.Lines)
    {
        Console.WriteLine($"          {mutation.Class.Split('.')[^1]}.cs:{line.ToString(CultureInfo.InvariantCulture)}");
    }
}

Console.WriteLine();
Console.WriteLine(ok
    ? "every mutation matched its declaration"
    : "A MUTATION DID NOT MATCH ITS DECLARATION — the test does not guard what it claims");
return ok ? 0 : 1;

static int Count(string body, string needle)
{
    int n = 0;
    for (int at = body.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = body.IndexOf(needle, at + 1, StringComparison.Ordinal))
    {
        n++;
    }

    return n;
}

/// <summary>What a run of the filtered class said: the verdict, and where its trace stopped.</summary>
internal readonly record struct Reading(string Verdict, string Message, IReadOnlyList<int> Lines)
{
    public static Reading NoOp(int matches) =>
        new("no-op", $"matched {matches.ToString(CultureInfo.InvariantCulture)} times, not once", []);

    public static Reading Unbuildable(string log) =>
        new("build", log.Length > 400 ? log[^400..] : log, []);

    public override string ToString() =>
        Lines.Count == 0
            ? $"{Verdict} {Message}".TrimEnd()
            : $"{Verdict} {Message} at {string.Join(',', Lines)}".Trim();
}

/// <summary>
/// Reads a `dotnet test` log. ⛔ The outcome line is load-bearing and no arithmetic replaces it: an
/// aborted host prints `Failed!` with `failed: 0`, which every count-based reading calls healthy —
/// `AGENTS.md` §5 and `scripts/catch-crash`. So anything that is not an explicit `Passed!` is red.
/// </summary>
internal static partial class Judge
{
    public static Reading Read(string log)
    {
        bool passed = log.Contains("Test run summary: Passed!", StringComparison.Ordinal);
        string message = string.Empty;
        foreach (string raw in log.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("Assert.", StringComparison.Ordinal))
            {
                message = line;
                break;
            }
        }

        List<int> lines = [];
        foreach (Match m in TraceLine().Matches(log))
        {
            int at = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!lines.Contains(at))
            {
                lines.Add(at);
            }
        }

        lines.Sort();
        return new Reading(passed ? "green" : "red", message, lines);
    }

    /// <summary>A stack frame in a test file: the line its trace stopped on.</summary>
    [GeneratedRegex(@"Tests[\\/][^\s:]*\.cs:(\d+)")]
    private static partial Regex TraceLine();
}

internal static class Git
{
    /// <summary>
    /// ⛔ `--untracked-files=all`, not the default: `status.showUntrackedFiles=no` in a user's config
    /// would otherwise hide exactly the file a `git checkout` is about to delete.
    /// </summary>
    public static string Status(string repo) =>
        Shell.Run(repo, "git", ["status", "--porcelain", "--untracked-files=all"]).Output.Trim();

    public static string Head(string repo) =>
        Shell.Run(repo, "git", ["rev-parse", "--short", "HEAD"]).Output.Trim();

    public static void Restore(string repo, string file) =>
        Shell.Run(repo, "git", ["checkout", "--", file]);

    /// <summary>Whatever the edit changed besides the file it declared.</summary>
    public static string Strayed(string repo, string before, string declared)
    {
        HashSet<string> was = [.. before.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())];
        List<string> now = [];
        foreach (string line in Status(repo).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string entry = line.Trim();
            if (!was.Contains(entry) && !entry.EndsWith(declared, StringComparison.Ordinal))
            {
                now.Add(entry);
            }
        }

        return string.Join(Environment.NewLine, now);
    }
}

internal static class Dotnet
{
    private const string Project = "tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj";

    public static bool Build(string repo, out string log)
    {
        Shell.Result result = Shell.Run(repo, "dotnet", ["build", Project, "-c", "Debug", "--nologo", "-v", "q"]);
        log = result.Output;
        return result.Exit == 0;
    }

    public static string Test(string repo, string filterClass) =>
        Shell.Run(repo, "dotnet", ["test", "--project", Project, "--no-build", "-c", "Debug", "--filter-class", filterClass]).Output;
}

internal static class Shell
{
    public readonly record struct Result(int Exit, string Output);

    public static Result Run(string repo, string tool, string[] arguments)
    {
        ProcessStartInfo start = new(tool)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repo,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"could not start {tool}");

        // Both pipes drained concurrently: reading one to its end first deadlocks once the child
        // fills the other's buffer.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new Result(process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}

internal static class Repo
{
    public static string Root()
    {
        for (DirectoryInfo? dir = new(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DiffView.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("not inside the DiffView repository: run from it, as the wrappers do");
    }
}

/// <param name="Name">What is broken, in the words of the contract it breaks.</param>
/// <param name="File">The one file the edit may touch; anything else stops the run.</param>
/// <param name="Old">A literal that must appear exactly once.</param>
/// <param name="New">What replaces it.</param>
/// <param name="Class">The test class to run.</param>
/// <param name="Expect">`red` or `green`, compared rather than printed.</param>
/// <param name="Because">Why that is the right expectation — the part a reader needs in a year.</param>
internal sealed record Mutation(
    string Name, string File, string Old, string New, string Class, string Expect, string Because);

internal static class Mutations
{
    private const string EditUnderFold = "Bennewitz.Ninja.DiffView.Tests.Composite.EditUnderFoldTests";

    public static readonly IReadOnlyList<Mutation> All =
    [
        new(
            "the re-diff stops refolding",
            "src/DiffView.Avalonia/DiffBuildController.cs",
            "        // And the runs are new, so they are computed again for this model rather than carried.\n        RefreshFolds();",
            "        // MUTATION: the re-diff no longer refolds.\n        ////RefreshFolds();",
            EditUnderFold,
            "red",
            "the folds after a revert can only come back from the re-diff's RefreshFolds, so the "
            + "post-build state reddens while the pre-build one stays green — that discrimination is "
            + "what proves the test's wait did not swallow the assertion"),

        new(
            "the revert leaves the text alone",
            "src/DiffView.Avalonia/SideBySideDiffView.cs",
            "            document.Text = source.Text;",
            "            ////document.Text = source.Text;   // MUTATION",
            EditUnderFold,
            "red",
            "with the text never restored the fold's lines are still the edited ones, so the middle "
            + "state — nothing collapsed the moment the revert returns — is what fails"),

        new(
            "a runner that loses the race",
            "tests/DiffView.Avalonia.Tests/Composite/CompositeHost.cs",
            "        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, token, options);",
            "        Thread.Sleep(50);   // MUTATION\n        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, token, options);",
            EditUnderFold,
            "green",
            "⭐ the whole point. A build that takes 50 ms is what a slower CI runner is, and it is what "
            + "reddened this test intermittently on macOS before the repair: the same sleep fails the "
            + "OLD test with CI's numbers character for character, 17.554062499999997 against "
            + "351.08124999999995. Staying green here is the proof that the test now waits rather than "
            + "races, and a red means the wait has come undone"),
    ];
}
