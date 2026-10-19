using System.Text.Json;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// What a candidate is a candidate <i>for</i> — plan 00034 decision 12. A <c>CLAUDE.md</c> baseline
/// and an <c>AGENTS.md</c> baseline are different documents, and so are a root file and a project
/// file, so a corpus holds candidates for one target and a mixed set is refused rather than merged.
/// </summary>
/// <remarks>
/// ⚠ The file name is half of a target and the scope is the other half, because decision 7 records
/// that nothing in a path tells a root file from a project one: the file name can be read off a path,
/// the scope only declared. A path list therefore carries targets with no scope, and a corpus that
/// spans scopes needs a manifest to say so.
/// </remarks>
/// <param name="FileName">
/// The file a candidate is, and the name a walk looks for. Compared ignoring case, as the file
/// systems of two of the three platforms do.
/// </param>
/// <param name="Scope">A declared scope — <c>global</c>, <c>root</c>, <c>project</c> — or none.</param>
internal sealed record CandidateTarget(string FileName, string? Scope)
{
    /// <inheritdoc/>
    public bool Equals(CandidateTarget? other) =>
        other is not null
        && string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Scope, other.Scope, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(FileName),
            Scope is null ? 0 : StringComparer.Ordinal.GetHashCode(Scope));

    /// <inheritdoc/>
    public override string ToString() => Scope is null ? FileName : $"{FileName} ({Scope})";
}

/// <summary>
/// One candidate the operator named: a file, with the label and the target the input gave it, if
/// any. A named path is always a candidate — decision 15 — whatever directory it sits in.
/// </summary>
/// <param name="Path">The full path.</param>
/// <param name="Label">The label the manifest gave it, or <see langword="null"/> to default one.</param>
/// <param name="Target">The target it declares, or the one its file name gives it.</param>
internal sealed record CorpusEntry(string Path, string? Label, CandidateTarget Target);

/// <summary>How the review queue is ordered.</summary>
internal enum CorpusOrder
{
    /// <summary>Most changes against the seed first — the default.</summary>
    Divergence,

    /// <summary>As the input listed the candidates, then as the walks found them.</summary>
    Listed,
}

/// <summary>
/// The corpus refused its input. A refusal is for input the corpus cannot honour at all — a mixed
/// target, a label used twice, a key the manifest does not have — and never for a candidate that
/// merely is not there, which is reported instead (step 4: <i>"a missing candidate is reported
/// rather than skipped"</i>).
/// </summary>
internal sealed class CorpusRefusedException(string message) : Exception(message);

/// <summary>
/// What to gather a corpus from: plan 00034 decision 7's two inputs, a bare path list and a
/// manifest, resolved to full paths.
/// </summary>
/// <remarks>
/// <para>
/// A <b>path list</b> is the input the request asked for, so it is accepted as it stands: one path
/// or <c>file://</c> URI per line, each a candidate, labels defaulted from the path, targets read
/// off the file names. It cannot walk a directory, because a walk has to know which file it is
/// looking for and a path list names no target.
/// </para>
/// <para>
/// A <b>manifest</b> is the richer, optional form: labels, targets, a scope, a seed, an order, the
/// directories to walk, and the <c>include</c> list that re-admits a copy a walk would otherwise
/// leave out (decision 15). It is JSON, read by hand rather than bound to a type, so that ⛔ a key it
/// does not know is refused instead of ignored — <c>"inlcude"</c> doing nothing is exactly the
/// silent failure this tool exists to avoid.
/// </para>
/// <para>
/// ⛔ There is no transport (decision 7, and §4 rules it out). A URI with any scheme but
/// <c>file</c> is refused rather than reported missing: it names something this tool cannot do at
/// all, not a machine that happens to be off.
/// </para>
/// </remarks>
internal sealed class CorpusRequest
{
    private static readonly HashSet<string> ManifestKeys =
        ["target", "scope", "seed", "order", "candidates", "walk", "include"];

    private static readonly HashSet<string> CandidateKeys = ["path", "label", "target", "scope"];

    private CorpusRequest(
        IReadOnlyList<CorpusEntry> candidates,
        CandidateTarget? walkTarget,
        IReadOnlyList<string> walks,
        IReadOnlyList<string> includes,
        string? seed,
        CorpusOrder order)
    {
        Candidates = candidates;
        WalkTarget = walkTarget;
        Walks = walks;
        Includes = includes;
        Seed = seed;
        Order = order;
    }

    /// <summary>The candidates the input named, in its order.</summary>
    internal IReadOnlyList<CorpusEntry> Candidates { get; }

    /// <summary>The target every file a walk finds is a candidate for; set whenever there is a walk.</summary>
    internal CandidateTarget? WalkTarget { get; }

    /// <summary>The directories to walk, in order.</summary>
    internal IReadOnlyList<string> Walks { get; }

    /// <summary>Paths at or under which a copy a walk finds is admitted after all.</summary>
    internal IReadOnlyList<string> Includes { get; }

    /// <summary>
    /// The seed, by label or by path, when the input names one. ⛔ Never defaulted: decision 9b says
    /// the seed is stated or chosen at start-up, <i>never implied by ordering</i>.
    /// </summary>
    internal string? Seed { get; }

    /// <summary>How the queue is ordered.</summary>
    internal CorpusOrder Order { get; }

    /// <summary>
    /// Builds a request directly; the inputs below are the two ways an operator writes one.
    /// </summary>
    internal static CorpusRequest Create(
        IReadOnlyList<CorpusEntry> candidates,
        CandidateTarget? walkTarget = null,
        IReadOnlyList<string>? walks = null,
        IReadOnlyList<string>? includes = null,
        string? seed = null,
        CorpusOrder order = CorpusOrder.Divergence)
    {
        walks ??= [];
        if (walks.Count > 0 && walkTarget is null)
        {
            throw new CorpusRefusedException(
                "a walk needs a target, to know which file it is looking for — a manifest's \"target\" says so");
        }

        return new CorpusRequest(candidates, walkTarget, walks, includes ?? [], seed, order);
    }

    /// <summary>
    /// Reads a path list: one path or <c>file://</c> URI per line; blank lines and lines opening
    /// with <c>#</c> are ignored, and a relative path is taken against <paramref name="baseDirectory"/>.
    /// </summary>
    internal static CorpusRequest FromPathList(IEnumerable<string> lines, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<CorpusEntry> candidates = [];
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string path = Resolve(line, baseDirectory);
            candidates.Add(new CorpusEntry(path, null, new CandidateTarget(System.IO.Path.GetFileName(path), null)));
        }

        return Create(candidates);
    }

    /// <summary>
    /// Reads a manifest. A relative path in it is taken against <paramref name="manifestDirectory"/>,
    /// the directory the manifest sits in, so a manifest means the same thing wherever it is run from.
    /// </summary>
    internal static CorpusRequest FromManifest(string json, string manifestDirectory)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new CorpusRefusedException($"the manifest is not JSON: {ex.Message}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new CorpusRefusedException("the manifest is not a JSON object");
            }

            RefuseUnknownKeys(root, ManifestKeys, "the manifest");

            string? targetName = OptionalString(root, "target", "the manifest");
            string? scope = OptionalString(root, "scope", "the manifest");
            string? seed = OptionalString(root, "seed", "the manifest");
            CorpusOrder order = OptionalString(root, "order", "the manifest") switch
            {
                null or "divergence" => CorpusOrder.Divergence,
                "listed" => CorpusOrder.Listed,
                string other => throw new CorpusRefusedException(
                    $"the manifest's \"order\" is \"{other}\"; it is \"divergence\" or \"listed\""),
            };

            List<CorpusEntry> candidates = [];
            foreach (JsonElement item in OptionalArray(root, "candidates"))
            {
                candidates.Add(ReadCandidate(item, targetName, scope, manifestDirectory));
            }

            List<string> walks = [.. OptionalArray(root, "walk").Select(w => Resolve(PathString(w, "\"walk\""), manifestDirectory))];
            List<string> includes = [.. OptionalArray(root, "include").Select(i => Resolve(PathString(i, "\"include\""), manifestDirectory))];

            return Create(
                candidates,
                targetName is null ? null : new CandidateTarget(targetName, scope),
                walks,
                includes,
                seed,
                order);
        }
    }

    private static CorpusEntry ReadCandidate(JsonElement item, string? targetName, string? scope, string manifestDirectory)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            string path = Resolve(item.GetString()!, manifestDirectory);
            return new CorpusEntry(path, null, new CandidateTarget(targetName ?? System.IO.Path.GetFileName(path), scope));
        }

        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new CorpusRefusedException("a candidate is a path, or an object with a \"path\"");
        }

        RefuseUnknownKeys(item, CandidateKeys, "a candidate");

        string resolved = Resolve(
            OptionalString(item, "path", "a candidate") ?? throw new CorpusRefusedException("a candidate has no \"path\""),
            manifestDirectory);

        return new CorpusEntry(
            resolved,
            OptionalString(item, "label", "a candidate"),
            new CandidateTarget(
                OptionalString(item, "target", "a candidate") ?? targetName ?? System.IO.Path.GetFileName(resolved),
                OptionalString(item, "scope", "a candidate") ?? scope));
    }

    /// <summary>
    /// A path or a <c>file://</c> URI, as a full path. Only an explicit <c>://</c> makes a URI, so a
    /// Windows drive letter is never read as a scheme.
    /// </summary>
    internal static string Resolve(string location, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        if (location.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(location, UriKind.Absolute, out Uri? uri) || !uri.IsFile)
            {
                throw new CorpusRefusedException(
                    $"\"{location}\" is not a path or a file:// URI — this tool has no transport, so a "
                    + "candidate on another machine is fetched to a path first");
            }

            return System.IO.Path.GetFullPath(uri.LocalPath);
        }

        return System.IO.Path.GetFullPath(location, baseDirectory);
    }

    private static void RefuseUnknownKeys(JsonElement element, HashSet<string> known, string what)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name))
            {
                throw new CorpusRefusedException(
                    $"{what} has a key \"{property.Name}\" it does not know; it knows "
                    + string.Join(", ", known.Order(StringComparer.Ordinal).Select(k => $"\"{k}\"")));
            }
        }
    }

    private static string? OptionalString(JsonElement element, string key, string what)
    {
        if (!element.TryGetProperty(key, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new CorpusRefusedException($"{what}'s \"{key}\" is not a string");
    }

    private static IEnumerable<JsonElement> OptionalArray(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : throw new CorpusRefusedException($"the manifest's \"{key}\" is not a list");
    }

    private static string PathString(JsonElement element, string what) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString()!
            : throw new CorpusRefusedException($"an entry of {what} is not a path");
}
