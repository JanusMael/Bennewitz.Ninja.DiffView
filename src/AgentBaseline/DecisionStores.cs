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

/// <summary>The word for each decision, in one place: a refusal, the window and the report all say it.</summary>
internal static class DecisionKinds
{
    /// <summary><paramref name="kind"/> as it is said: taken, left or dropped.</summary>
    internal static string Spoken(this DecisionKind kind) => kind switch
    {
        DecisionKind.Taken => "taken",
        DecisionKind.Rejected => "left",
        _ => "dropped",
    };
}

/// <summary>One decision about one version of one section.</summary>
/// <param name="Kind">Taken, rejected or dropped.</param>
/// <param name="Section">
/// Which rule, and which version of it, the decision was about. For a take it is the version taken, so
/// its hash is decision 9c's <i>hash of the source section at take time</i>, taken as decision 6 takes
/// every hash: over normalised text.
/// </param>
/// <param name="Source">
/// The candidate whose version was taken or left, by its label; <see langword="null"/> for a drop,
/// which removes the accumulator's own section and comes from no candidate.
/// </param>
/// <param name="Text">
/// For a take, the content taken — decision 9c, because the edit that may follow it happens in a pane
/// and is gone when the session ends, so the text it was made over cannot be recovered afterwards.
/// <see langword="null"/> for a leave or a drop, which keep identities alone.
/// </param>
internal sealed record SectionDecision(DecisionKind Kind, SectionIdentity Section, string? Source, string? Text)
{
    /// <summary>
    /// Whether the take was edited by hand before its baseline was saved — decision 9c's <i>edited</i>
    /// flag. Only a saved take is ever edited: <see cref="DurableStore.Reconcile"/> reads the accumulator's
    /// section against <see cref="Text"/> when a save writes the store, so a decision this run has not
    /// saved says <see langword="false"/> whatever the left now holds.
    /// </summary>
    internal bool Edited { get; init; }

    /// <summary>
    /// What the edit made of the take: the accumulator's section at the take's heading path when the take
    /// was saved — decision 9c's <i>resulting text</i>, the one of its three points a later run can read
    /// nowhere else. <see langword="null"/> for a take not edited, and for one the accumulator no longer
    /// held a section for: an inserted take undone, or the section deleted or its heading changed by hand.
    /// </summary>
    internal string? Result { get; init; }
}

/// <summary>What a later run is told of one candidate's version of a section — plan 00034 step 8.</summary>
internal enum StandingKind
{
    /// <summary>Nothing is decided about this version, which a reworded version of a decided one is too.</summary>
    Undecided,

    /// <summary>This version was taken.</summary>
    Taken,

    /// <summary>This version was left.</summary>
    Rejected,

    /// <summary>This version was dropped from the baseline.</summary>
    Dropped,

    /// <summary>
    /// Decision 9c: this candidate's section was taken and then edited, and the candidate still carries
    /// the version taken — the edit was made against what is still there, so it stands.
    /// </summary>
    EditStands,

    /// <summary>
    /// Decision 9c: this candidate's section was taken and then edited, and the candidate has rewritten it
    /// since — the edit was made against a version that is gone, so it is reviewed again.
    /// </summary>
    ReReview,
}

/// <summary>What the durable store says of one candidate's version of a section.</summary>
/// <param name="Kind">What it says.</param>
/// <param name="Decision">The decision that says it; <see langword="null"/> for a version nothing decided.</param>
internal sealed record SectionStanding(StandingKind Kind, SectionDecision? Decision)
{
    /// <summary>
    /// Whether a review offers this version for a decision: one nothing is decided about, or one whose
    /// source moved under an edit. A version decided before is not offered again — what decision 10's
    /// durable store is for.
    /// </summary>
    internal bool IsPresented => Kind is StandingKind.Undecided or StandingKind.ReReview;

    /// <summary>The standing as the tool says it.</summary>
    internal string Spoken
    {
        get
        {
            string from = $"\"{Decision?.Source}\"";
            return Kind switch
            {
                StandingKind.Undecided => "not decided yet",
                StandingKind.EditStands => $"taken from {from} and then edited: {from} is unchanged, your edit stands",
                StandingKind.ReReview => $"taken from {from} and then edited: {from} has rewritten this, re-review it",
                StandingKind.Taken when Decision!.Edited => $"taken from {from} and then edited",
                StandingKind.Taken => $"already taken, from {from}",
                StandingKind.Rejected => $"already left, from {from}",
                _ => "already dropped from the baseline",
            };
        }
    }
}

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
/// decided twice — left, then taken after all — is answered by the later of the two.
/// </para>
/// <para>
/// ⭐ <b>It outlives the run</b> — step 8. A save writes it beside the baseline (<see cref="DecisionFile"/>),
/// and a later run begins from what that file holds: those are the first <see cref="Inherited"/> of its
/// decisions, which the later run reads and never changes, and what it decides goes after them. So the
/// file a save writes holds every run's decisions, and the report beside it this run's alone.
/// </para>
/// </remarks>
internal sealed class DurableStore
{
    private readonly List<SectionDecision> decisions = [];

    /// <summary>An empty store, for a first run.</summary>
    internal DurableStore()
    {
    }

    /// <summary>A store that begins with <paramref name="earlier"/>: an earlier run's decisions, read back.</summary>
    internal DurableStore(IEnumerable<SectionDecision> earlier)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        decisions.AddRange(earlier);
        Inherited = decisions.Count;
    }

    /// <summary>How many of <see cref="Decisions"/>, at their head, an earlier run made.</summary>
    internal int Inherited { get; }

    /// <summary>Every decision, in the order made: an earlier run's first.</summary>
    internal IReadOnlyList<SectionDecision> Decisions => decisions;

    /// <summary>The decisions this run made, in the order made.</summary>
    internal IReadOnlyList<SectionDecision> Made => decisions[Inherited..];

    /// <summary>The latest decision about <paramref name="section"/>, or <see langword="null"/> for none.</summary>
    internal SectionDecision? LatestFor(SectionIdentity section) =>
        decisions.LastOrDefault(d => d.Section == section);

    /// <summary>Records <paramref name="decision"/> after every one made before it.</summary>
    internal void Record(SectionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decisions.Add(decision);
    }

    /// <summary>
    /// What this store says of <paramref name="section"/>, the version of a section that the candidate
    /// labelled <paramref name="source"/> carries: whether a review offers it, and why not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Keyed by identity, so a reworded version is a new one.</b> A version decided before is answered
    /// by the latest decision about it, whichever candidate it was seen in; a version nothing decided — a
    /// rejected rule reworded since, or one only deferred, which is never here — is offered again.
    /// </para>
    /// <para>
    /// ⭐ <b>Decision 9c's three points.</b> Where what the baseline holds at this heading path follows from
    /// a take of this very candidate's section that was then edited, the edit was made against the version
    /// taken, and only the candidate can say whether that version still stands: the same hash — over
    /// normalised text, so a source that changed only its line endings or its list indent is unchanged —
    /// and the edit stands; another, and it is reviewed again. A later decision about this exact version
    /// answers it instead.
    /// </para>
    /// <para>
    /// ⚠ A source is known by its label, so a candidate labelled otherwise in a later run is another source:
    /// its version is answered by identity alone, and a rewrite under an edit is offered as undecided
    /// rather than as one to review again.
    /// </para>
    /// </remarks>
    internal SectionStanding StandingOf(MarkdownSection section, string source)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(source);

        SectionIdentity version = SectionIdentity.Of(section);
        int latest = decisions.FindLastIndex(d => d.Section == version);
        int governing = GoverningAt(section.HeadingPath);
        if (governing >= 0
            && governing >= latest
            && decisions[governing] is { Kind: DecisionKind.Taken, Edited: true } edit
            && string.Equals(edit.Source, source, StringComparison.Ordinal))
        {
            return new SectionStanding(edit.Section.Hash == version.Hash ? StandingKind.EditStands : StandingKind.ReReview, edit);
        }

        if (latest < 0)
        {
            return new SectionStanding(StandingKind.Undecided, null);
        }

        SectionDecision decided = decisions[latest];
        return new SectionStanding(
            decided.Kind switch
            {
                DecisionKind.Taken => StandingKind.Taken,
                DecisionKind.Rejected => StandingKind.Rejected,
                _ => StandingKind.Dropped,
            },
            decided);
    }

    /// <summary>
    /// The decisions a save keeps, each take this run made read against <paramref name="accumulator"/> —
    /// the text the save writes — for decision 9c: whether the left still carries what the take took. An
    /// earlier run's decisions are kept as they were read, and this store is not changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A take is read only while the accumulator's section at its heading path follows from it: while it
    /// is the last take or drop there. One a later take replaced, or a drop removed, is history and is kept
    /// as taken — the left holds the later decision's text there, not an edit of this one.
    /// </para>
    /// <para>
    /// The left's section at the take's path is compared with the text taken. The same, and the take is
    /// kept as it was; different, and it is edited, with that section as its result; none, and it is edited
    /// with no result, the baseline holding no rule under that path any more.
    /// </para>
    /// </remarks>
    /// <exception cref="SaveRefusedException">
    /// The accumulator holds more than one section at a take's heading path, so what the take became cannot
    /// be told — refused as a take or a drop refuses it, before anything is written.
    /// </exception>
    internal IReadOnlyList<SectionDecision> Reconcile(string accumulator)
    {
        ArgumentNullException.ThrowIfNull(accumulator);

        MarkdownOutline left = MarkdownOutline.Read(accumulator);
        List<SectionDecision> kept = [.. decisions];
        for (int i = Inherited; i < kept.Count; i++)
        {
            SectionDecision take = kept[i];
            if (take.Kind != DecisionKind.Taken || GoverningAt(take.Section.HeadingPath) != i)
            {
                continue;
            }

            IReadOnlyList<int> held = left.IndicesOf(take.Section.HeadingPath);
            if (held.Count > 1)
            {
                throw new SaveRefusedException(
                    $"the accumulator holds {held.Count} sections at {MarkdownOutline.Spoken(take.Section.HeadingPath)}, which was taken from "
                    + $"\"{take.Source}\", so a save cannot tell what that take became — make their headings differ by hand first");
            }

            string? result = held.Count == 1 ? left.Sections[held[0]].Content : null;
            if (!string.Equals(result, take.Text, StringComparison.Ordinal))
            {
                kept[i] = take with { Edited = true, Result = result };
            }
        }

        return kept;
    }

    /// <summary>
    /// Where in <see cref="Decisions"/> the last take or drop at <paramref name="headingPath"/> is — what the
    /// accumulator's section there follows from — or -1 for none. A leave writes nothing to the left, so it
    /// governs nothing.
    /// </summary>
    private int GoverningAt(string headingPath) =>
        decisions.FindLastIndex(d =>
            d.Kind != DecisionKind.Rejected && string.Equals(d.Section.HeadingPath, headingPath, StringComparison.Ordinal));
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
/// <para>
/// <c>defer</c> lives here and nowhere else, because rejections persist and <i>not now</i> must not:
/// see <see cref="DurableStore"/>. A durable decision about a deferred version settles it, so a section
/// is never at once put off and decided.
/// </para>
/// <para>
/// ⛔ <b>Nothing of it is written.</b> A save writes the durable store and this one not at all, so a later
/// run begins at the first candidate with nothing put off, and a section deferred is offered again: what
/// it means for <c>defer</c> to be session state.
/// </para>
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
