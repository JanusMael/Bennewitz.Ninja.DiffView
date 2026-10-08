using System.Diagnostics;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>scripts/mutate-behaviour</c> proves a behaviour test able to fail: it breaks what the test
/// guards and checks that it goes red, and makes a change that should not matter and checks that it
/// stays green. Running a mutation needs a clean tree, several minutes and a rebuild, so what is
/// tested here is the half that decides the verdict — how it reads a run — which is where a harness
/// is wrong in silence.
/// </summary>
/// <remarks>
/// <para>
/// Driven through its real command line, as <see cref="CatchCrashTests"/> and
/// <see cref="DriveDemoTests"/> drive theirs: what runs here is what a developer runs.
/// </para>
/// <para>
/// ⛔ The reason this is worth a test at all is <c>aborted.log</c>. A test host that dies mid-run
/// prints <c>Failed!</c> with <c>failed: 0</c> and a total short of the suite, so every reading built
/// on counts calls it healthy — the defect <c>scripts/catch-crash</c> exists for (<c>AGENTS.md</c>
/// §5). A harness that called that green would report a mutation as surviving, and a surviving
/// mutation reads as "the test does not guard this", which is the opposite of what happened.
/// </para>
/// </remarks>
public sealed class MutateBehaviourTests
{
    private static string Fixture(string name) =>
        RepoPaths.Source(Path.Combine("fixtures", "mutations", name));

    [Fact]
    public void A_run_that_went_red_is_read_as_red_and_says_where_its_trace_stopped()
    {
        (int exitCode, string output) = Run(["--judge", Fixture("killed.log"), "red"]);

        Assert.True(exitCode == 0, output);
        Assert.Contains("red", output, StringComparison.Ordinal);

        // Both frames, in order: the assertion helper and the test line it was reached from. Which
        // stage of a staged test failed is the whole discrimination a mutation is judged on.
        Assert.Contains("98", output, StringComparison.Ordinal);
        Assert.Contains("346", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_stayed_green_is_read_as_green()
    {
        (int exitCode, string output) = Run(["--judge", Fixture("survived.log"), "green"]);

        Assert.True(exitCode == 0, output);
        Assert.Contains("green", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reading_that_disagrees_with_the_declaration_fails()
    {
        // The whole value of the harness: `Expect` is compared, never printed. A mutation declared
        // green that goes red is as much a failure as one declared red that survives.
        (int exitCode, string output) = Run(["--judge", Fixture("survived.log"), "red"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("green", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_aborted_host_is_not_read_as_green()
    {
        // `Failed!` with `failed: 0` and a short total. No arithmetic over the counts can see this —
        // 4 + 0 + 0 closes perfectly — so the outcome line is what the reading rests on.
        (int exitCode, string output) = Run(["--judge", Fixture("aborted.log"), "green"]);

        Assert.True(exitCode == 1, "an aborted host was read as green: " + output);
        Assert.Contains("red", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edit_that_changed_only_the_file_it_declared_has_strayed_nowhere()
    {
        (int exitCode, string output) = Run(
        [
            "--strayed",
            Fixture("status-before.txt"),
            Fixture("status-after-clean.txt"),
            "src/DiffView.Avalonia/DiffBuildController.cs",
        ]);

        // The untracked draft is in both readings, so it is not a stray — only what APPEARED is.
        Assert.True(exitCode == 0, output);
        Assert.Contains("nothing strayed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_edit_that_wrote_outside_the_file_it_declared_is_caught_and_named()
    {
        (int exitCode, string output) = Run(
        [
            "--strayed",
            Fixture("status-before.txt"),
            Fixture("status-after-stray.txt"),
            "src/DiffView.Avalonia/DiffBuildController.cs",
        ]);

        // This is the guard the harness exists to add over the one-off it came from. Without it a
        // write outside the declared file reads as a no-op, survives the revert that cannot reach
        // it, and contaminates every mutation after it.
        Assert.Equal(1, exitCode);
        Assert.Contains("SideBySideDiffView.cs", output, StringComparison.Ordinal);
        Assert.Contains("scratch-output.txt", output, StringComparison.Ordinal);
        Assert.DoesNotContain("DiffBuildController.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_narrowed_list_is_narrowed()
    {
        // `--list` used to print from inside the argument loop and return, so `--only` had not been
        // read yet and a narrowed list silently showed everything. A filter that does nothing is
        // worse than one that errors, because its output looks like an answer.
        (int exitCode, string output) = Run(["--only", "refold", "--list"]);

        Assert.True(exitCode == 0, output);
        Assert.Equal(1, output.Split('\n').Count(l => l.Contains("expects:", StringComparison.Ordinal)));
        Assert.Contains("refold", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_mutation_declares_the_file_it_may_touch_and_why_it_expects_what_it_does()
    {
        (int exitCode, string output) = Run(["--list"]);

        Assert.True(exitCode == 0, output);

        // A mutation with no stated reason is one nobody can judge a year from now, and the file it
        // names is what stops a stray edit being counted as a result.
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int named = lines.Count(l => l.Contains("expects:", StringComparison.Ordinal));
        Assert.True(named >= 3, "the harness names fewer mutations than it carries: " + output);
        Assert.All(
            lines.Where(l => l.Contains("expects:", StringComparison.Ordinal)),
            line => Assert.Contains(" — ", line, StringComparison.Ordinal));
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
        start.ArgumentList.Add(Path.Combine("scripts", "mutate-behaviour.cs"));
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
