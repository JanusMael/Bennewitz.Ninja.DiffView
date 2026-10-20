namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// Entry point for the baseline consolidation tool: many machines' <c>CLAUDE.md</c> and
/// <c>AGENTS.md</c> reviewed into one new baseline.
/// </summary>
/// <remarks>
/// Plan 00034 step 1 puts the project in the solution and deliberately nothing else in it. The
/// parse arrives at step 2, ingest normalisation at step 3, the corpus at step 4, and at step 5
/// <see cref="ReviewSession"/>, which drives the diff control a window will host.
/// <para>
/// ⛔ Markup stays out until that window arrives, and not merely because it is unneeded:
/// <c>AccessibilityCoverageTests</c> asserts that the library's and the demo's subjects together
/// account for every element its scan of <c>src</c> finds, so a third markup-bearing project under
/// <c>src</c> makes that equality false the moment one exists. Step 11 is where this project's own
/// subject and measured floor are added, and <c>ProjectShapeTests</c> holds the tripwire that says
/// so when the first <c>.axaml</c> lands here.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// What the executable says while it has nothing to do. Exposed so a test can assert it without
    /// starting a process.
    /// </summary>
    internal const string NothingToRunYet =
        "agent-baseline: nothing to run yet — the corpus, the verbs and the window arrive in "
        + "plan 00034's steps 2 to 7.";

    /// <summary>
    /// Reports that the tool is not wired up and exits non-zero, because a tool that can do nothing
    /// and reports success is the shape of defect this repository keeps finding.
    /// </summary>
    private static int Main()
    {
        Console.WriteLine(NothingToRunYet);
        return 2;
    }
}
