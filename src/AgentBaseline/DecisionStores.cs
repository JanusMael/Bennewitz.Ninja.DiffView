namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>A decision the durable store keeps — plan 00034 decision 10's three.</summary>
internal enum DecisionKind
{
    /// <summary>A candidate's version of a section was taken into the accumulator.</summary>
    Taken,

    /// <summary>A candidate's version of a section was left out: <c>leave</c>.</summary>
    Rejected,

    /// <summary>The accumulator's own section was removed from the baseline.</summary>
    Dropped,
}

/// <summary>One decision about one version of one section.</summary>
/// <param name="Kind">Taken, rejected or dropped.</param>
/// <param name="Section">Which rule, and which version of it, the decision was about.</param>
/// <param name="Source">
/// The candidate whose version was taken or left, by its label; <see langword="null"/> for a drop,
/// which removes the accumulator's own section and comes from no candidate.
/// </param>
/// <param name="Text">
/// For a take, the content taken — decision 9c, because the edit that may follow it happens in a pane
/// and is gone when the session ends, so the text it was made over cannot be recovered afterwards.
/// <see langword="null"/> for a leave or a drop, which keep identities alone.
/// </param>
internal sealed record SectionDecision(DecisionKind Kind, SectionIdentity Section, string? Source, string? Text);

/// <summary>
/// The durable store: taken, rejected and dropped, by identity — plan 00034 decision 10. What a later
/// run must know was decided, so a rejected rule is not offered again next month.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Nothing deferred is ever here.</b> <c>defer</c> means <i>not now</i>, and spelled as a
/// rejection it would poison the next run: the rule would never be offered again although nobody
/// decided against it. That is the distinction decision 10 exists for, and the session store holds
/// what is deferred instead.
/// </para>
/// <para>
/// It keeps every decision in the order made: the decision log is the product (§1), and a section
/// decided twice — left, then taken after all — is answered by the later of the two. Held in memory
/// until step 8 gives it a file; step 8 is also where it is read back across a restart.
/// </para>
/// </remarks>
internal sealed class DurableStore
{
    private readonly List<SectionDecision> decisions = [];

    /// <summary>Every decision, in the order made.</summary>
    internal IReadOnlyList<SectionDecision> Decisions => decisions;

    /// <summary>The latest decision about <paramref name="section"/>, or <see langword="null"/> for none.</summary>
    internal SectionDecision? LatestFor(SectionIdentity section) =>
        decisions.LastOrDefault(d => d.Section == section);

    /// <summary>Records <paramref name="decision"/> after every one made before it.</summary>
    internal void Record(SectionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decisions.Add(decision);
    }
}

/// <summary>A section put off for now, and the candidate it was seen in.</summary>
/// <param name="Section">Which rule, and which version of it.</param>
/// <param name="Source">The candidate's label.</param>
internal sealed record DeferredSection(SectionIdentity Section, string Source);

/// <summary>
/// The session store: where the review is and what it has put off — plan 00034 decision 10. Staleness
/// joins it at step 9.
/// </summary>
/// <remarks>
/// <c>defer</c> lives here and nowhere else, because rejections persist and <i>not now</i> must not:
/// see <see cref="DurableStore"/>. A durable decision about a deferred version settles it, so a section
/// is never at once put off and decided.
/// </remarks>
internal sealed class SessionStore
{
    private readonly List<DeferredSection> deferred = [];

    /// <summary>Where in the queue the right pane is.</summary>
    internal int Position { get; set; }

    /// <summary>What is put off, in the order it was.</summary>
    internal IReadOnlyList<DeferredSection> Deferred => deferred;

    /// <summary>Whether <paramref name="section"/> is put off.</summary>
    internal bool IsDeferred(SectionIdentity section) => deferred.Any(d => d.Section == section);

    /// <summary>Puts <paramref name="section"/> off; once is enough, so a second deferral changes nothing.</summary>
    internal void Defer(DeferredSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!IsDeferred(section.Section))
        {
            deferred.Add(section);
        }
    }

    /// <summary>Takes <paramref name="section"/> off the deferred list: it has been decided.</summary>
    internal void Settle(SectionIdentity section) => deferred.RemoveAll(d => d.Section == section);
}
