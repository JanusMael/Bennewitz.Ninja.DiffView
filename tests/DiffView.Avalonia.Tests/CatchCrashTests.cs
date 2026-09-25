using System.Diagnostics;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>scripts/catch-crash</c> exists for a <em>false green</em>: the test host aborts mid-run and
/// the summary then reads <c>Failed!</c> with <c>failed: 0</c> and a total well short of the suite.
/// Every cheap check reads that as success — a grep for <c>failed:</c>, a grep for
/// <c>succeeded:</c>, even an eye skimming for red.
/// </summary>
/// <remarks>
/// <para>
/// So the test that matters is the <em>negative</em> one: fed an aborted run, the tool must refuse
/// it. A test that only fed it a healthy log would pass against a tool that always said "healthy",
/// which is the tool this defect would produce if nobody checked.
/// </para>
/// <para>
/// These drive the script through its real command line rather than calling a shared type, for the
/// same reason <c>LocaleReviewTests</c> reads the committed markdown rather than re-rendering it: a
/// gate that re-derives its subject from the code that produced it tests very little. What runs
/// here is what a developer runs.
/// </para>
/// </remarks>
public sealed class CatchCrashTests
{
    private static string Fixture(string name) =>
        RepoPaths.Source(Path.Combine("fixtures", "test-runs", name));

    [Fact]
    public void An_aborted_run_reporting_zero_failures_is_not_healthy()
    {
        (int exitCode, string output) = Check(Fixture("aborted.log"));

        Assert.False(
            exitCode == 0,
            "catch-crash accepted an aborted run. Its whole purpose is that `failed: 0` on a run "
            + "that died after 412 of 676 tests is a false green:\n" + output);
        Assert.Contains("NOT HEALTHY", output, StringComparison.Ordinal);
        Assert.Contains("Failed!", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_complete_passing_run_is_healthy()
    {
        (int exitCode, string output) = Check(Fixture("healthy.log"));

        Assert.True(exitCode == 0, "catch-crash rejected a genuine complete pass:\n" + output);
        Assert.Contains("healthy", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_short_run_is_not_healthy_even_when_the_summary_says_it_passed()
    {
        // The second line of defence: a summary that claims Passed! but ran fewer tests than the
        // suite holds. --expect is what turns a plausible-looking log into a caught one.
        (int exitCode, string output) = Check(Fixture("healthy.log"), "--expect", "99999");

        Assert.False(exitCode == 0, "a total short of --expect was accepted:\n" + output);
        Assert.Contains("99999", output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output) Check(string logPath, params string[] extra)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("run");
        start.ArgumentList.Add(Path.Combine("scripts", "catch-crash.cs"));
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--check");
        start.ArgumentList.Add(logPath);
        foreach (string argument in extra)
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
