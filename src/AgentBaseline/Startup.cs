using System.Text;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// The start-up will not do what the command line asks, and says why — before any window opens, as a
/// corpus refusal is said.
/// </summary>
internal sealed class StartupRefusedException(string message) : Exception(message);

/// <summary>What the command line names, each path made full against the directory it was given in.</summary>
/// <param name="CorpusPath">The manifest or the path list.</param>
/// <param name="IsManifest">Whether <paramref name="CorpusPath"/> is a manifest rather than a path list.</param>
/// <param name="Seed">The seed's label, when the command line names one.</param>
/// <param name="DecisionsPath">The decision file an earlier save wrote, when the command line names one.</param>
internal sealed record StartupRequest(string CorpusPath, bool IsManifest, string? Seed, string? DecisionsPath);

/// <summary>What the review window opens on.</summary>
/// <param name="Corpus">The corpus, with its report of everything it left out.</param>
/// <param name="Queue">The seed, and every other candidate in review order.</param>
/// <param name="Earlier">The decisions earlier runs made, as the decision file holds them — none on a first run.</param>
/// <param name="DecisionsPath">The decision file they were read from, or <see langword="null"/> when none was named.</param>
internal sealed record StartupPlan(Corpus Corpus, ReviewQueue Queue, IReadOnlyList<SectionDecision> Earlier, string? DecisionsPath);

/// <summary>
/// The start-up between the command line and the review window: told a corpus, a seed and the decision
/// file an earlier save wrote, it opens the window on them — the piece no step of plan 00034 names, built
/// ahead of step 9 on Brian's word of 2026-10-29.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Nothing is guessed.</b> A corpus is named as a manifest or as a path list, never told apart by
/// its extension or its content. The seed is the manifest's or <c>--seed</c>'s, and two different answers
/// are refused rather than one chosen — decision 9b: the seed is stated or chosen at start-up, <i>never
/// implied by ordering</i>. A decision file is read only when one is named, and a named one that is not
/// there is refused: a first run names none, and a mistyped path must not open a review that has
/// forgotten every decision made before it.
/// </para>
/// <para>
/// ⭐ <b>Everything the corpus left out is said before the window opens</b> — <see cref="Describe"/>.
/// Step 4 built a report of it, and a report nobody is shown is a candidate skipped in silence, which is
/// what step 4 exists to refuse.
/// </para>
/// </remarks>
internal static class Startup
{
    /// <summary>How the command line is written, said after a command line the start-up cannot read.</summary>
    internal const string Usage =
        "usage: agent-baseline (--manifest <file> | --list <file>) [--seed <label>] [--decisions <file>]";

    private static readonly string[] Options = ["--manifest", "--list", "--seed", "--decisions"];

    /// <summary>Reads the command line, taking a relative path against <paramref name="workingDirectory"/>.</summary>
    /// <exception cref="StartupRefusedException">
    /// An option it does not know, one with no value or given twice, or no corpus or two.
    /// </exception>
    internal static StartupRequest Parse(IReadOnlyList<string> args, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);

        Dictionary<string, string> given = new(StringComparer.Ordinal);
        for (int i = 0; i < args.Count; i++)
        {
            string option = args[i];
            if (!Options.Contains(option))
            {
                throw new StartupRefusedException(
                    $"\"{option}\" is not an option here; the options are {string.Join(", ", Options)}");
            }

            if (i + 1 == args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || Options.Contains(args[i + 1]))
            {
                throw new StartupRefusedException($"{option} needs a value");
            }

            if (!given.TryAdd(option, args[++i]))
            {
                throw new StartupRefusedException($"{option} is given twice");
            }
        }

        bool manifest = given.TryGetValue("--manifest", out string? manifestPath);
        bool list = given.TryGetValue("--list", out string? listPath);
        if (manifest == list)
        {
            throw new StartupRefusedException(manifest
                ? "--manifest and --list are both given, and a corpus is read from one of them"
                : "no corpus is named: --manifest names a manifest, and --list a path list");
        }

        return new StartupRequest(
            Path.GetFullPath(manifest ? manifestPath! : listPath!, workingDirectory),
            manifest,
            given.GetValueOrDefault("--seed"),
            given.TryGetValue("--decisions", out string? decisions) ? Path.GetFullPath(decisions, workingDirectory) : null);
    }

    /// <summary>
    /// Reads what <paramref name="request"/> names — the decision file first, so a mistyped one is refused
    /// before any corpus is walked — and builds the queue from the seed.
    /// </summary>
    /// <exception cref="StartupRefusedException">
    /// A file cannot be read or is no decision file, or the seed is named twice over or not at all.
    /// </exception>
    /// <exception cref="CorpusRefusedException">
    /// The corpus refused its input, the seed is no candidate, or the seed is all there is to review.
    /// </exception>
    internal static StartupPlan Prepare(StartupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<SectionDecision> earlier = request.DecisionsPath is { } decisions ? ReadDecisions(decisions) : [];

        string text = ReadText(request.CorpusPath, request.IsManifest ? "the manifest" : "the path list");
        string directory = Path.GetDirectoryName(request.CorpusPath)!;
        Corpus corpus = Corpus.Gather(request.IsManifest
            ? CorpusRequest.FromManifest(text, directory)
            : CorpusRequest.FromPathList(text.Split('\n'), directory));

        if (request.Seed is { } named
            && corpus.RequestedSeed is { } stated
            && !string.Equals(named, stated, StringComparison.Ordinal))
        {
            throw new StartupRefusedException(
                $"the manifest names the seed \"{stated}\" and --seed names \"{named}\", and the seed is one candidate");
        }

        if ((request.Seed ?? corpus.RequestedSeed) is not { } seed)
        {
            throw new StartupRefusedException(corpus.Candidates.Count == 0
                ? "no seed is named, and the corpus holds no candidate to name"
                : $"no seed is named: --seed names one of {string.Join(", ", corpus.Candidates.Select(c => $"\"{c.Label}\""))}"
                  + " — it is never implied by the order");
        }

        ReviewQueue queue = corpus.Queue(seed, cancellationToken);
        if (ReviewSession.WhyNothingToReview(queue) is { } why)
        {
            throw new CorpusRefusedException(why);
        }

        return new StartupPlan(corpus, queue, earlier, request.DecisionsPath);
    }

    /// <summary>
    /// Starts the review <paramref name="plan"/> describes in <paramref name="window"/> — its queue, and
    /// every decision the decision file held — and hands the window back.
    /// </summary>
    internal static ReviewWindow Open(StartupPlan plan, ReviewWindow window)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(window);

        window.Review(plan.Queue, plan.Earlier);
        return window;
    }

    /// <summary>
    /// What the start-up says before the window opens: what is reviewed against which seed, which earlier
    /// decisions came with it, and every line of the corpus's report of what it left out.
    /// </summary>
    internal static IReadOnlyList<string> Describe(StartupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        ReviewQueue queue = plan.Queue;
        List<string> lines =
        [
            $"Reviewing {plan.Corpus.Target}: {Counted(queue.Items.Count, "candidate")} against the seed "
                + $"\"{queue.Seed.Label}\", {(plan.Corpus.Order == CorpusOrder.Divergence ? "most changes first" : "in the order listed")}.",
            $"The seed is {queue.Seed.Path}.",
            plan.DecisionsPath is { } decisions
                ? $"{Counted(plan.Earlier.Count, "earlier decision")}, from {decisions}."
                : "No decision file is named, so no earlier decision is known.",
        ];
        lines.AddRange(LeftOut(plan.Corpus.Report));
        return lines;
    }

    /// <summary>
    /// <paramref name="report"/> as lines, one for each file or directory it names and one for each count
    /// it keeps: nothing in it goes unsaid, and an empty report says nothing at all.
    /// </summary>
    internal static IReadOnlyList<string> LeftOut(CorpusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        List<string> lines = [];
        foreach (MissingCandidate missing in report.Missing)
        {
            lines.Add($"Missing: {missing.Path}{(missing.Label is { } label ? $" (\"{label}\")" : "")} — {Clause(missing.Reason)}.");
        }

        foreach (ExcludedFile binary in report.Binary)
        {
            lines.Add($"Not text, so no candidate: {binary.Path}.");
        }

        if (report.Pointers.Count > 0)
        {
            lines.Add($"Pointers, each nothing but an import, so no candidate: {report.Pointers.Count}.");
        }

        if (report.Pruned.Count > 0)
        {
            IEnumerable<string> byName = report.PrunedByName
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => $"{p.Key} {p.Value}");
            lines.Add($"Build and tool output, never entered: {Counted(report.Pruned.Count, "directory", "directories")}, {string.Join(", ", byName)}.");
        }

        foreach (UnenteredDirectory unentered in report.Unentered)
        {
            lines.Add($"Not entered: {unentered.Path} — {Clause(unentered.Reason)}.");
        }

        foreach (CopySkip copy in report.Copies)
        {
            string what = copy.IsPointer ? "A pointer" : $"A copy{(copy.Identity is { } identity ? $" of {identity}" : "")}";
            string held = copy.IsPointer ? "" : copy.HeldAs is { } holder ? $", held as \"{holder}\"" : ", ⚠ held by no candidate";
            lines.Add($"{what} in {copy.Holder}: {copy.Path}{held}.");
        }

        foreach (string include in report.UnmatchedIncludes)
        {
            lines.Add($"An include that re-admitted nothing: {include}.");
        }

        return lines;
    }

    private static string Counted(int count, string one, string? many = null) =>
        $"{count} {(count == 1 ? one : many ?? one + "s")}";

    /// <summary>
    /// A reason as a clause of a line that ends in its own full stop: an exception's message brings one,
    /// and two would read as a typing slip.
    /// </summary>
    private static string Clause(string reason) => reason.TrimEnd('.');

    private static IReadOnlyList<SectionDecision> ReadDecisions(string path)
    {
        try
        {
            return DecisionFile.Read(path);
        }
        catch (DecisionFileRefusedException refused)
        {
            throw new StartupRefusedException($"{refused.Message}, in {path}");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new StartupRefusedException($"the decision file {path} is not there — a first run names none");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StartupRefusedException($"the decision file {path} cannot be read: {ex.Message}");
        }
    }

    private static string ReadText(string path, string what)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new StartupRefusedException($"{what} {path} is not there");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StartupRefusedException($"{what} {path} cannot be read: {ex.Message}");
        }
    }
}
