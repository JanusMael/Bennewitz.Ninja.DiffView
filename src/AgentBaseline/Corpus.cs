using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>How a candidate came into the corpus.</summary>
internal enum Admission
{
    /// <summary>The input named its path.</summary>
    Named,

    /// <summary>A walk found it.</summary>
    Walked,

    /// <summary>A walk found it inside a copy, and the manifest's <c>include</c> re-admitted it.</summary>
    Included,
}

/// <summary>One document under review: parsed, labelled, and for the corpus's one target.</summary>
/// <param name="Label">Unique within its corpus: the manifest's, or defaulted from the path.</param>
/// <param name="Path">Where it was read from.</param>
/// <param name="Admission">How it came in.</param>
/// <param name="Document">The bytes as read and the normalised text, with its outline.</param>
internal sealed record Candidate(string Label, string Path, Admission Admission, IngestedDocument Document);

/// <summary>A candidate the input named or a walk found that could not be read.</summary>
/// <param name="Path">Where it was looked for.</param>
/// <param name="Label">The label the manifest gave it, if any.</param>
/// <param name="Reason">Why it is missing.</param>
internal sealed record MissingCandidate(string Path, string? Label, string Reason);

/// <summary>A file of the target's name that is not a candidate, for what is in it.</summary>
/// <param name="Path">The file.</param>
/// <param name="Label">The label the manifest gave it, if it was named.</param>
/// <param name="Named">Whether the input named it, rather than a walk finding it.</param>
internal sealed record ExcludedFile(string Path, string? Label, bool Named);

/// <summary>
/// A copy a walk found and left out (tier 2): named individually, because naming every one is the
/// whole defence against the risk that this one was the only copy (plan 00034 §7).
/// </summary>
/// <param name="Path">The copy.</param>
/// <param name="Holder">The copy directory it sits in: <c>worktrees</c>, <c>reference</c> or <c>bundles</c>.</param>
/// <param name="Identity">The document it is a copy of, when its path says.</param>
/// <param name="HeldAs">
/// ⭐ The label of the candidate holding that same document, whatever version either is — or
/// <see langword="null"/>, which is the case to read: no candidate holds it, so leaving it out may
/// lose it. Judged by <b>identity</b>, never by bytes (decision 15b): measured, 32 of 66 copies left
/// out under <c>C:\c\cl</c> matched nothing byte for byte and every one was an older version of a
/// document held current.
/// </param>
/// <param name="IsPointer">Whether the copy is a pointer, so that leaving it out loses nothing.</param>
internal sealed record CopySkip(string Path, string Holder, string? Identity, string? HeldAs, bool IsPointer);

/// <summary>Everything the corpus left out, and why. Nothing is skipped without a line here.</summary>
/// <param name="Missing">Candidates that could not be read.</param>
/// <param name="Pruned">Build and tool output, never descended: reported as a count.</param>
/// <param name="Unentered">Directories a walk could not or would not enter for any other reason.</param>
/// <param name="Copies">Copies of our own documents, each named.</param>
/// <param name="Pointers">Pointer files: a count, one per file, never collapsed.</param>
/// <param name="Binary">Files of the target's name that are not text.</param>
/// <param name="UnmatchedIncludes">Includes that re-admitted nothing, so a mistyped one shows.</param>
internal sealed record CorpusReport(
    IReadOnlyList<MissingCandidate> Missing,
    IReadOnlyList<PrunedDirectory> Pruned,
    IReadOnlyList<UnenteredDirectory> Unentered,
    IReadOnlyList<CopySkip> Copies,
    IReadOnlyList<ExcludedFile> Pointers,
    IReadOnlyList<ExcludedFile> Binary,
    IReadOnlyList<string> UnmatchedIncludes)
{
    /// <summary>How many directories of each tier-1 name were pruned, by name.</summary>
    internal IReadOnlyDictionary<string, int> PrunedByName =>
        Pruned.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
}

/// <summary>A candidate in the review queue, with what diffing it against the seed found.</summary>
/// <param name="Candidate">The candidate.</param>
/// <param name="Diff">The diff against the seed's text.</param>
internal sealed record QueuedCandidate(Candidate Candidate, DiffDiagnostics Diff)
{
    /// <summary>How many changes the candidate shows against the seed: the stops F7 makes.</summary>
    internal int Changes => Diff.BlockCount;
}

/// <summary>The seed, and every other candidate in the order they are to be reviewed.</summary>
/// <param name="Seed">The candidate the left starts as — never itself reviewed (decision 9b).</param>
/// <param name="Items">The rest, ordered.</param>
internal sealed record ReviewQueue(Candidate Seed, IReadOnlyList<QueuedCandidate> Items);

/// <summary>
/// Every candidate for one baseline, parsed at once — plan 00034 decision 2, <i>a corpus of parsed
/// candidates, not a path queue</i> — with a report of everything it left out.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Three exclusions, and each answers a different question.</b> Build and tool output is pruned
/// during the walk and counted (decision 15, tier 1). A copy of one of our own documents is walked,
/// named and re-admittable (tier 2). A pointer file — <c>@AGENTS.md</c> and nothing else — is
/// excluded <b>by content</b> and counted one per file (decision 15b): measured under
/// <c>C:\c\cl</c> it is 90 of 236 candidates, and ⛔ a content-hash rule would collapse 314 of them
/// into one entry and take 314 repositories' presence with it.
/// </para>
/// <para>
/// ⭐ A path the operator <b>named</b> is always a candidate, wherever it sits: pruning governs the
/// traversal of a directory, never a path written down, so a named file under <c>bin/</c> is admitted
/// and the walk that meets the same file again leaves it alone.
/// </para>
/// <para>
/// A corpus is for <b>one target</b> (decision 12), and a mixed set is refused before a byte is read.
/// A candidate that is not there is <b>reported, never skipped</b>: measured, januswax2 has a
/// <c>.claude</c> directory and no <c>CLAUDE.md</c>, so missing is ordinary.
/// </para>
/// </remarks>
internal sealed class Corpus
{
    private Corpus(
        CandidateTarget target,
        IReadOnlyList<Candidate> candidates,
        CorpusReport report,
        string? requestedSeed,
        CorpusOrder order)
    {
        Target = target;
        Candidates = candidates;
        Report = report;
        RequestedSeed = requestedSeed;
        Order = order;
    }

    /// <summary>
    /// How paths compare: ignoring case where the file system does. A path named twice in two
    /// spellings is one file there, and two on Linux.
    /// </summary>
    internal static StringComparison PathComparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer { get; } =
        PathComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>What every candidate here is a candidate for.</summary>
    internal CandidateTarget Target { get; }

    /// <summary>The candidates: named ones in the input's order, then the walks' finds in theirs.</summary>
    internal IReadOnlyList<Candidate> Candidates { get; }

    /// <summary>Everything left out, and why.</summary>
    internal CorpusReport Report { get; }

    /// <summary>The seed's label when the input named one.</summary>
    internal string? RequestedSeed { get; }

    /// <summary>How <see cref="Queue"/> orders the candidates.</summary>
    internal CorpusOrder Order { get; }

    /// <summary>Gathers the corpus <paramref name="request"/> describes.</summary>
    /// <exception cref="CorpusRefusedException">The request cannot be honoured at all.</exception>
    internal static Corpus Gather(CorpusRequest request, ICorpusFileSystem? files = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        files ??= LocalFileSystem.Instance;

        CandidateTarget target = OneTarget(request);
        HashSet<string> explicitLabels = ExplicitLabels(request);

        List<(string Path, string? Label, Admission Admission, IngestedDocument Document)> admitted = [];
        List<MissingCandidate> missing = [];
        List<ExcludedFile> pointers = [];
        List<ExcludedFile> binary = [];
        List<PrunedDirectory> pruned = [];
        List<UnenteredDirectory> unentered = [];
        List<(FoundFile Found, bool IsPointer)> copies = [];
        HashSet<string> seen = new(PathComparer);
        HashSet<string> includesUsed = new(PathComparer);

        foreach (CorpusEntry entry in request.Candidates)
        {
            if (!seen.Add(entry.Path))
            {
                throw new CorpusRefusedException($"\"{entry.Path}\" is named twice");
            }

            if (files.DirectoryExists(entry.Path))
            {
                throw new CorpusRefusedException(
                    $"\"{entry.Path}\" is a directory; a candidate is a file, and a manifest's \"walk\" is how a directory is read");
            }

            if (Read(files, entry.Path, entry.Label, missing) is not { } document)
            {
                continue;
            }

            Judge(document, entry.Path, entry.Label, Admission.Named, named: true);
        }

        foreach (string root in request.Walks)
        {
            if (!files.DirectoryExists(root))
            {
                if (files.FileExists(root))
                {
                    throw new CorpusRefusedException(
                        $"\"{root}\" is a file; a walk reads a directory, and a file is named as a candidate");
                }

                missing.Add(new MissingCandidate(root, null, "the directory to walk does not exist"));
                continue;
            }

            WalkResult walk = CorpusWalk.Walk(files, root, target.FileName);
            pruned.AddRange(walk.Pruned);
            unentered.AddRange(walk.Unentered);

            foreach (FoundFile found in walk.Files)
            {
                // Named already, or met by an earlier walk: a named path keeps its name and its label.
                if (!seen.Add(found.Path))
                {
                    continue;
                }

                string? include = found.Copy is null
                    ? null
                    : request.Includes.FirstOrDefault(i => IsAtOrUnder(found.Path, i));

                if (Read(files, found.Path, null, missing) is not { } document)
                {
                    continue;
                }

                if (found.Copy is not null && include is null)
                {
                    copies.Add((found, !document.IsBinary && IsPointer(document)));
                    continue;
                }

                if (include is not null)
                {
                    includesUsed.Add(include);
                }

                Judge(document, found.Path, null, include is null ? Admission.Walked : Admission.Included, named: false);
            }
        }

        List<Candidate> candidates = AssignLabels(admitted, explicitLabels);

        List<CopySkip> skipped =
        [
            .. copies.Select(c => new CopySkip(
                c.Found.Path,
                c.Found.Copy!.Holder,
                c.Found.Copy.Identity,
                c.Found.Copy.Identity is { } identity ? HolderOf(candidates, identity)?.Label : null,
                c.IsPointer)),
        ];

        CorpusReport report = new(
            missing,
            pruned,
            unentered,
            skipped,
            pointers,
            binary,
            [.. request.Includes.Where(i => !includesUsed.Contains(i))]);

        return new Corpus(target, candidates, report, request.Seed, request.Order);

        void Judge(IngestedDocument document, string path, string? label, Admission admission, bool named)
        {
            if (document.IsBinary)
            {
                binary.Add(new ExcludedFile(path, label, named));
            }
            else if (IsPointer(document))
            {
                pointers.Add(new ExcludedFile(path, label, named));
            }
            else
            {
                admitted.Add((path, label, admission, document));
            }
        }
    }

    /// <summary>
    /// The review queue from <paramref name="seed"/>: every other candidate, each diffed against the
    /// seed's text through the public <see cref="DiffDocumentBuilder.Build"/> — plan 00034 §5: the
    /// control computes one pair, so these N−1 diffs are the app's own.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>Most changes first.</b> Decision 11 makes any take stale every candidate reviewed before
    /// it that carries its own version of the section, so the candidates likeliest to give the most
    /// are reviewed while the fewest candidates lie behind them. Ties keep the corpus's own order.
    /// </remarks>
    /// <param name="seed">
    /// The seed's label, or <see langword="null"/> for the one the input named. ⛔ There is no third
    /// way: decision 9b says the seed is stated or chosen, <i>never implied by ordering</i>.
    /// </param>
    /// <param name="cancellationToken">Cancels between diffs.</param>
    internal ReviewQueue Queue(string? seed = null, CancellationToken cancellationToken = default)
    {
        string label = seed ?? RequestedSeed ?? throw new CorpusRefusedException(
            "no seed is named: the manifest's \"seed\" names one, or one is chosen at start-up — it is never implied by the order");

        Candidate chosen = Candidates.FirstOrDefault(c => string.Equals(c.Label, label, StringComparison.Ordinal))
            ?? throw new CorpusRefusedException(WhyNotACandidate(label));

        PaneSource left = new(chosen.Document.Text);
        List<QueuedCandidate> queued = [];
        foreach (Candidate candidate in Candidates)
        {
            if (ReferenceEquals(candidate, chosen))
            {
                continue;
            }

            DiffBuildResult diff = DiffDocumentBuilder.Build(left, new PaneSource(candidate.Document.Text), cancellationToken);
            queued.Add(new QueuedCandidate(candidate, diff.Diagnostics));
        }

        IReadOnlyList<QueuedCandidate> ordered = Order == CorpusOrder.Divergence
            ? [.. queued.OrderByDescending(q => q.Changes)]
            : queued;

        return new ReviewQueue(chosen, ordered);
    }

    /// <summary>
    /// Whether a document is a pointer: every line that is not blank is an import — <c>@</c> and a
    /// path — and there is at least one. ⭐ Judged by content, never by path or size (decision 15b):
    /// the 11-byte <c>@AGENTS.md</c> is the measured case, and a pointer has no sections, so it can be
    /// neither taken from nor merged.
    /// </summary>
    internal static bool IsPointer(IngestedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        bool imports = false;
        foreach (string line in document.Outline.Lines)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.Length < 2 || trimmed[0] != '@' || trimmed.Any(char.IsWhiteSpace))
            {
                return false;
            }

            imports = true;
        }

        return imports;
    }

    /// <summary>
    /// A label for each candidate: the manifest's, or the shortest run of trailing directory names
    /// that no other candidate shares and no manifest label is — <c>ClaudeForge</c>, then
    /// <c>old/ClaudeForge</c> beside it. Decision 7: a path list is accepted with labels defaulted from
    /// the path.
    /// </summary>
    private static List<Candidate> AssignLabels(
        List<(string Path, string? Label, Admission Admission, IngestedDocument Document)> admitted,
        HashSet<string> explicitLabels)
    {
        string[][] directories =
            [.. admitted.Select(a => CorpusWalk.Segments(System.IO.Path.GetDirectoryName(a.Path) ?? string.Empty))];

        List<Candidate> labelled = new(admitted.Count);
        for (int i = 0; i < admitted.Count; i++)
        {
            (string path, string? label, Admission admission, IngestedDocument document) = admitted[i];
            labelled.Add(new Candidate(label ?? DefaultLabel(i), path, admission, document));
        }

        return labelled;

        string DefaultLabel(int index)
        {
            string[] mine = directories[index];
            for (int length = 1; length <= mine.Length; length++)
            {
                string candidate = Suffix(mine, length);
                bool shared = false;
                for (int other = 0; other < admitted.Count && !shared; other++)
                {
                    shared = other != index
                        && admitted[other].Label is null
                        && string.Equals(Suffix(directories[other], length), candidate, StringComparison.Ordinal);
                }

                if (!shared && !explicitLabels.Contains(candidate))
                {
                    return candidate;
                }
            }

            // Two candidates in one directory, under two names: only the file name tells them apart.
            return string.Join('/', [.. mine, System.IO.Path.GetFileName(admitted[index].Path)]);
        }
    }

    private static string Suffix(string[] segments, int length) =>
        string.Join('/', segments[Math.Max(0, segments.Length - length)..]);

    /// <summary>
    /// The candidate holding the document <paramref name="identity"/> names: one whose path ends in
    /// those segments, whatever version of the document either is.
    /// </summary>
    private static Candidate? HolderOf(IReadOnlyList<Candidate> candidates, string identity)
    {
        string[] wanted = CorpusWalk.Segments(identity);
        return candidates.FirstOrDefault(c =>
        {
            string[] path = CorpusWalk.Segments(c.Path);
            return path.Length >= wanted.Length
                && wanted.Select((segment, i) => string.Equals(segment, path[path.Length - wanted.Length + i], PathComparison)).All(match => match);
        });
    }

    private static bool IsAtOrUnder(string path, string include)
    {
        string root = System.IO.Path.TrimEndingDirectorySeparator(include);
        return string.Equals(path, root, PathComparison)
            || path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>
    /// The one target the request's candidates and walks are for. ⛔ Refused before anything is read
    /// when there is more than one — decision 12 says a mixed set is refused, not merged.
    /// </summary>
    private static CandidateTarget OneTarget(CorpusRequest request)
    {
        List<(CandidateTarget Target, string Where)> declared =
            [.. request.Candidates.Select(c => (c.Target, c.Path))];
        if (request.WalkTarget is { } walkTarget)
        {
            declared.AddRange(request.Walks.Select(w => (walkTarget, w)));
        }

        List<IGrouping<CandidateTarget, (CandidateTarget Target, string Where)>> targets =
            [.. declared.GroupBy(d => d.Target)];

        return targets.Count switch
        {
            0 => throw new CorpusRefusedException("the input names no candidate and no directory to walk"),
            1 => targets[0].Key,
            _ => throw new CorpusRefusedException(
                $"the input is for {targets.Count} targets and a corpus is for one — "
                + string.Join("; ", targets.Select(t => $"{t.Key}: {string.Join(", ", t.Select(d => d.Where))}"))),
        };
    }

    private static HashSet<string> ExplicitLabels(CorpusRequest request)
    {
        HashSet<string> labels = new(StringComparer.Ordinal);
        foreach (CorpusEntry entry in request.Candidates)
        {
            if (entry.Label is not { } label)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new CorpusRefusedException($"\"{entry.Path}\" has an empty label");
            }

            if (!labels.Add(label))
            {
                throw new CorpusRefusedException($"two candidates are labelled \"{label}\", and a label names one");
            }
        }

        return labels;
    }

    private static IngestedDocument? Read(ICorpusFileSystem files, string path, string? label, List<MissingCandidate> missing)
    {
        if (!files.FileExists(path))
        {
            missing.Add(new MissingCandidate(path, label, "does not exist"));
            return null;
        }

        try
        {
            return IngestedDocument.FromBytes(files.ReadAllBytes(path), path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            missing.Add(new MissingCandidate(path, label, $"could not be read: {ex.Message}"));
            return null;
        }
    }

    private string WhyNotACandidate(string label)
    {
        if (Report.Missing.FirstOrDefault(m => m.Label == label) is { } absent)
        {
            return $"the seed \"{label}\" is missing — {absent.Path} {absent.Reason}";
        }

        if (Report.Pointers.FirstOrDefault(p => p.Label == label) is { } pointer)
        {
            return $"the seed \"{label}\" is a pointer with nothing in it to start from — {pointer.Path}";
        }

        return Report.Binary.FirstOrDefault(b => b.Label == label) is { } binary
            ? $"the seed \"{label}\" is not text — {binary.Path}"
            : $"no candidate is labelled \"{label}\"";
    }
}
