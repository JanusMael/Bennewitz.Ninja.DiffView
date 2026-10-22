namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// Entry point for the baseline consolidation tool: many machines' <c>CLAUDE.md</c> and
/// <c>AGENTS.md</c> reviewed into one new baseline.
/// </summary>
/// <remarks>
/// Plan 00034 step 1 put the project in the solution and deliberately nothing else in it. The parse
/// arrived at step 2, ingest normalisation at step 3, the corpus at step 4, the review at steps 5 and 6,
/// and at step 7 <see cref="ReviewWindow"/>, the loop as the operator drives it. What is not built is
/// the start-up between them: nothing yet reads a corpus and a seed from the command line and opens the
/// window on them, so the window is reached by the tests alone.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// What the executable says while it has nothing to do. Exposed so a test can assert it without
    /// starting a process.
    /// </summary>
    internal const string NothingToRunYet =
        "agent-baseline: nothing to run yet — the review window is built, and nothing yet reads a corpus "
        + "and a seed to open it on.";

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
