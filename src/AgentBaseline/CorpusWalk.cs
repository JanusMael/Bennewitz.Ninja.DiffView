namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>A directory the walk pruned without entering it: build or tool output (tier 1).</summary>
/// <param name="Path">The directory.</param>
/// <param name="Name">Its name — the member of <see cref="CorpusWalk.BuildOutput"/> it matched.</param>
internal sealed record PrunedDirectory(string Path, string Name);

/// <summary>A directory the walk could not or would not enter, for a reason other than tier 1.</summary>
/// <param name="Path">The directory.</param>
/// <param name="Reason">Why: a link, or the error that reading it raised.</param>
internal sealed record UnenteredDirectory(string Path, string Reason);

/// <summary>
/// Where a file a walk found sits, when it sits inside a copy of one of our own documents (tier 2).
/// </summary>
/// <param name="Holder">The copy directory's name — <c>worktrees</c>, <c>reference</c> or <c>bundles</c>.</param>
/// <param name="Identity">
/// The document this is a copy of, as the trailing segments of a path joined with <c>/</c> —
/// <c>ClaudeForge/AGENTS.md</c> — or <see langword="null"/> when the path does not say. Plan 00034
/// decision 15b: <i>a path identifies the document, a hash identifies which version of it</i>.
/// </param>
internal sealed record CopyPlace(string Holder, string? Identity);

/// <summary>A file of the target's name that a walk found.</summary>
/// <param name="Path">The file.</param>
/// <param name="Copy">Where it sits when that is inside a copy, or <see langword="null"/>.</param>
internal sealed record FoundFile(string Path, CopyPlace? Copy);

/// <summary>What one walk found and what it declined to enter.</summary>
internal sealed record WalkResult(
    IReadOnlyList<FoundFile> Files,
    IReadOnlyList<PrunedDirectory> Pruned,
    IReadOnlyList<UnenteredDirectory> Unentered);

/// <summary>
/// The traversal behind a manifest's <c>walk</c>: plan 00034 decision 15's two tiers, pruned
/// <b>during</b> the descent.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Prune during the descent; never filter after it.</b> Decision 15's first wording skipped
/// six names after the walk had found them, which is O(the disk) rather than O(the corpus): a
/// script written from it walked every <c>node_modules</c> under <c>C:\c</c> and burned
/// <b>1,174 s of CPU without printing a line</b>, and the first repair reintroduced the cost by
/// keeping the names walkable so the files inside them could be judged. So a directory in
/// <see cref="BuildOutput"/> is never listed at all — and that is asserted by instrumenting
/// <see cref="ICorpusFileSystem.List"/>, because a walk that visited everything first produces the
/// same candidates.
/// </para>
/// <para>
/// <b>Tier 1</b>, <see cref="BuildOutput"/>, is a <i>class</i> — build and tool output — and its
/// members are this estate's measured five (<c>.git</c>, <c>node_modules</c>, <c>bin</c>,
/// <c>obj</c>, <c>artifacts</c>) and the rest of their kind from the global prune list. Nothing in it
/// is ours, so it is reported as a count and has no manifest override. ⚠ §7: a class with an open
/// membership will be wrong again — <c>artifacts</c> was missing from the first list and was 448 of
/// 684 files on the first corpus measured.
/// </para>
/// <para>
/// <b>Tier 2</b>, <see cref="CopyHolders"/>, holds copies of our own documents. It is walked, and
/// every file of the target's name in it is returned with its <see cref="CopyPlace"/>, for the corpus
/// to name individually. Measured on this estate, the three hold copies three ways: a worktree is
/// <c>&lt;repository&gt;/.claude/worktrees/&lt;name&gt;/…</c>, a copy of the repository holding it;
/// <c>reference/&lt;name&gt;/…</c> and <c>bundles/&lt;name&gt;/…</c> are copies of the repository
/// called <c>&lt;name&gt;</c>. So a copy's identity is read per kind — ⛔ a copy under
/// <c>Beta/reference/Alpha/</c> is Alpha's document, not Beta's.
/// </para>
/// <para>
/// ⭐ The walk root itself is never tiered: decision 15 says pruning governs the traversal, never a
/// path the operator wrote down, so only directories met <i>below</i> a root are judged.
/// </para>
/// </remarks>
internal static class CorpusWalk
{
    /// <summary>Tier 1: build and tool output, never descended.</summary>
    internal static readonly IReadOnlySet<string> BuildOutput = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "artifacts",
        "dist", "build", "target", "out", "packages",
        ".venv", "venv", "__pycache__", "TestResults", ".vs", ".idea", ".next", ".nuxt",
    };

    /// <summary>Tier 2: directories holding copies of our own documents, walked and named.</summary>
    internal static readonly IReadOnlySet<string> CopyHolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "worktrees", "reference", "bundles",
    };

    /// <summary>
    /// The directory Claude Code keeps a repository's worktrees under, inside the repository itself.
    /// </summary>
    private const string ClaudeDirectory = ".claude";

    /// <summary>Walks <paramref name="root"/> for files named <paramref name="fileName"/>.</summary>
    internal static WalkResult Walk(ICorpusFileSystem files, string root, string fileName)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        List<FoundFile> found = [];
        List<PrunedDirectory> pruned = [];
        List<UnenteredDirectory> unentered = [];

        // Depth first, children in ordinal order, so a walk reads the same on every platform.
        Stack<string> pending = new([root]);
        while (pending.TryPop(out string? directory))
        {
            DirectoryListing listing;
            try
            {
                listing = files.List(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                unentered.Add(new UnenteredDirectory(directory, ex.Message));
                continue;
            }

            foreach (string file in listing.Files)
            {
                if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(new FoundFile(file, CopyPlaceOf(root, file)));
                }
            }

            foreach (string link in listing.Links)
            {
                unentered.Add(new UnenteredDirectory(link, "a link, which the walk does not follow"));
            }

            for (int i = listing.Directories.Count - 1; i >= 0; i--)
            {
                string subdirectory = listing.Directories[i];
                string name = Path.GetFileName(subdirectory);
                if (BuildOutput.Contains(name))
                {
                    pruned.Add(new PrunedDirectory(subdirectory, name));
                }
                else
                {
                    pending.Push(subdirectory);
                }
            }
        }

        // Pushed in reverse so they pop in order; reported in order too.
        pruned.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return new WalkResult(found, pruned, unentered);
    }

    /// <summary>
    /// Where <paramref name="file"/> sits relative to the copies below <paramref name="root"/>: the
    /// innermost copy directory wins, so a <c>reference</c> checkout inside a worktree is a copy of
    /// what the reference holds.
    /// </summary>
    internal static CopyPlace? CopyPlaceOf(string root, string file)
    {
        string[] below = Segments(Path.GetRelativePath(root, file));

        // The last segment is the file itself, never a directory to judge.
        int holder = -1;
        for (int i = below.Length - 2; i >= 0; i--)
        {
            if (CopyHolders.Contains(below[i]))
            {
                holder = i;
                break;
            }
        }

        if (holder < 0)
        {
            return null;
        }

        string holderName = below[holder];

        // The copy's own root is the directory just inside the holder; a file sitting directly in
        // the holder has none, and its path says nothing about what it is a copy of.
        if (holder + 1 >= below.Length - 1)
        {
            return new CopyPlace(holderName, null);
        }

        string[] within = below[(holder + 2)..];
        string? owner = string.Equals(holderName, "worktrees", StringComparison.OrdinalIgnoreCase)
            ? RepositoryHolding(root, below[..holder])
            : below[holder + 1];

        return new CopyPlace(holderName, owner is null ? null : string.Join('/', [owner, .. within]));
    }

    /// <summary>
    /// The name of the repository a <c>worktrees</c> directory belongs to: the directory holding it,
    /// or the one above that when it is Claude Code's own <c>.claude</c>.
    /// </summary>
    private static string? RepositoryHolding(string root, string[] aboveHolder)
    {
        string directory = Path.Combine([root, .. aboveHolder]);
        if (string.Equals(Path.GetFileName(directory), ClaudeDirectory, StringComparison.OrdinalIgnoreCase))
        {
            directory = Path.GetDirectoryName(directory) ?? string.Empty;
        }

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        return name.Length > 0 ? name : null;
    }

    /// <summary>A path's segments, whichever separator it uses, without empty ones.</summary>
    internal static string[] Segments(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
}
