namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// Entry point for the baseline consolidation tool: many machines' <c>CLAUDE.md</c> and
/// <c>AGENTS.md</c> reviewed into one new baseline.
/// </summary>
/// <remarks>
/// Plan 00034 step 1 put the project in the solution and deliberately nothing else in it. The parse
/// arrived at step 2, ingest normalisation at step 3, the corpus at step 4, the review at steps 5 and 6,
/// the window at step 7 and the decision file at step 8. <see cref="Startup"/> is what joins them: the
/// command line names a corpus, a seed and the decision file an earlier save wrote, and the window opens
/// on them.
/// </remarks>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args) =>
        Run(args, Environment.CurrentDirectory, Console.Out, Console.Error, App.Run);

    /// <summary>
    /// Reads the command line and opens the review it names, saying first everything the corpus left out.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <param name="workingDirectory">What a relative path on it is taken against.</param>
    /// <param name="output">Where the start-up says what it is about to review.</param>
    /// <param name="errors">Where a refusal is said.</param>
    /// <param name="review">Runs the review; <see cref="App.Run"/> outside the tests.</param>
    /// <returns>
    /// The review's own exit code, or 2 when the start-up refused — a tool that cannot start and reports
    /// success is the shape of defect this repository keeps finding.
    /// </returns>
    internal static int Run(
        IReadOnlyList<string> args,
        string workingDirectory,
        TextWriter output,
        TextWriter errors,
        Func<StartupPlan, int> review)
    {
        StartupRequest request;
        try
        {
            request = Startup.Parse(args, workingDirectory);
        }
        catch (StartupRefusedException refused)
        {
            errors.WriteLine($"agent-baseline: {refused.Message}");
            errors.WriteLine(Startup.Usage);
            return 2;
        }

        StartupPlan plan;
        try
        {
            plan = Startup.Prepare(request);
        }
        catch (Exception ex) when (ex is StartupRefusedException or CorpusRefusedException)
        {
            errors.WriteLine($"agent-baseline: {ex.Message}");
            return 2;
        }

        foreach (string line in Startup.Describe(plan))
        {
            output.WriteLine(line);
        }

        return review(plan);
    }
}
