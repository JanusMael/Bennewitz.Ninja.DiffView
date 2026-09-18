// scripts/catch-crash.cs — a .NET 10 file-based app.
//
// Re-runs the test suite until it crashes, keeping the output of the run that did; or, with
// --check, judges a single captured log.
//
// The defect this exists for is a FALSE GREEN. The test host dies mid-run — SIGABRT, exit 134 —
// and the summary then reads:
//
//     Test run summary: Failed!
//       total: 412
//       failed: 0
//       succeeded: 412
//
// "failed: 0" is true and meaningless: 412 of 676 tests ran. Anyone grepping for `failed:` or for
// `succeeded:` reads that as success, which is how it went unnoticed the first time. So the rule
// here is deliberately not a grep: a run is healthy only if the summary says Passed!, nothing
// failed, the arithmetic closes, and — when --expect is given — the total is the whole suite.
//
//   dotnet run scripts/catch-crash.cs                          20 attempts, default settings
//   dotnet run scripts/catch-crash.cs -- --attempts 5
//   dotnet run scripts/catch-crash.cs -- --expect 676          a short total is a crash too
//   dotnet run scripts/catch-crash.cs -- --check run.log       judge one captured log, run nothing
//
// Anything after `--` that this does not recognise is passed to `dotnet test`.
// The .sh and .ps1 wrappers beside this file run it from the repository root for you.

using System.Globalization;
using System.Text.RegularExpressions;

string? checkPath = null;
int attempts = 20;
int? expect = null;
string prefix = "catch-crash";
List<string> passThrough = [];

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--check" when i + 1 < args.Length:
            checkPath = args[++i];
            break;
        case "--attempts" when i + 1 < args.Length:
            attempts = int.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--expect" when i + 1 < args.Length:
            expect = int.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--prefix" when i + 1 < args.Length:
            prefix = args[++i];
            break;
        default:
            passThrough.Add(args[i]);
            break;
    }
}

if (checkPath is not null)
{
    Verdict only = Judge(File.ReadAllText(checkPath), exitCode: 0, expect);
    Console.WriteLine(only.Healthy ? "healthy" : "NOT HEALTHY: " + only.Reason);
    return only.Healthy ? 0 : 1;
}

for (int attempt = 1; attempt <= attempts; attempt++)
{
    string log = $"{prefix}-{attempt}.log";
    int code = RunSuite(passThrough, log);
    Verdict verdict = Judge(File.ReadAllText(log), code, expect);

    Console.WriteLine($"run {attempt}: exit={code} {(verdict.Healthy ? "healthy" : verdict.Reason)}");

    if (!verdict.Healthy)
    {
        Console.WriteLine($"--- caught it on attempt {attempt}; output kept at {log} ---");
        return 0;
    }

    File.Delete(log);
}

Console.WriteLine($"--- no crash in {attempts} attempts ---");
return 1;

static int RunSuite(List<string> extra, string logPath)
{
    System.Diagnostics.ProcessStartInfo start = new()
    {
        FileName = "dotnet",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    start.ArgumentList.Add("test");
    start.ArgumentList.Add("--solution");
    start.ArgumentList.Add("DiffView.slnx");
    foreach (string a in extra)
    {
        start.ArgumentList.Add(a);
    }

    using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
        ?? throw new InvalidOperationException("Could not start dotnet test.");

    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    File.WriteAllText(logPath, output);
    return process.ExitCode;
}

/// <summary>
/// Whether a captured run is a genuine, complete pass. Every clause here exists because some
/// cheaper check — a grep for "failed:", an exit-code test alone — reads an aborted run as a good
/// one.
/// </summary>
static Verdict Judge(string log, int exitCode, int? expect)
{
    if (exitCode != 0)
    {
        return new(false, $"the run exited {exitCode}");
    }

    Match summary = Regex.Match(log, @"Test run summary:\s*(?<outcome>\w+)!");
    if (!summary.Success)
    {
        return new(false, "no test run summary in the output at all");
    }

    string outcome = summary.Groups["outcome"].Value;
    if (!string.Equals(outcome, "Passed", StringComparison.Ordinal))
    {
        // The whole point. An aborted host says Failed! while reporting failed: 0.
        return new(false, $"the summary says {outcome}!, whatever the counts say");
    }

    int? total = Count(log, "total");
    int? failed = Count(log, "failed");
    int? succeeded = Count(log, "succeeded");
    int? skipped = Count(log, "skipped");

    if (total is null || failed is null || succeeded is null || skipped is null)
    {
        return new(false, "the summary is missing one of total/failed/succeeded/skipped");
    }

    if (failed != 0)
    {
        return new(false, $"{failed} test(s) failed");
    }

    if (succeeded + skipped + failed != total)
    {
        return new(false,
            $"the counts do not close: {succeeded} + {skipped} + {failed} != {total}");
    }

    if (expect is not null && total != expect)
    {
        return new(false, $"only {total} of an expected {expect} tests ran");
    }

    return new(true, $"{total} passed");
}

static int? Count(string log, string label) =>
    Regex.Match(log, $@"^\s*{label}:\s*(\d+)\s*$", RegexOptions.Multiline) is { Success: true } m
        ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
        : null;

internal readonly record struct Verdict(bool Healthy, string Reason);
