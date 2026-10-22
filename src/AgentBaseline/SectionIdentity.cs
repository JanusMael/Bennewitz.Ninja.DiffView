using System.Security.Cryptography;
using System.Text;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// What a decision is about: the rule, by its heading path, and the version of it, by a hash of its
/// normalised text — plan 00034 decision 6.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The hash is over normalised text, never over bytes.</b> Measured in plan 00034 §2: the same
/// rule hashed raw and line-ending-normalised differs, and a file wholly nested in a list item puts
/// two spaces in front of every line — so a raw hash cannot match across the machines this tool
/// exists to merge. A candidate's sections come from ingest's normalised text (decision 8), and the
/// accumulator was seeded with that text, so both sides of every comparison are LF and unwrapped.
/// </para>
/// <para>
/// ⚠ <b>The hash leaves out a section's trailing blank lines</b>, which the plan does not say and
/// which is a reading made rather than assumed: those lines say where the section sits — between two
/// others, at the end of a file, before or after its last line ending — and the same rule moved from
/// the middle of a file to its end is the same version of it. Trailing spaces on a line are kept,
/// two of them being a markdown hard line break.
/// </para>
/// <para>
/// ⭐ <b>A document's title is in no heading path</b>, so one rule is one identity in two files titled
/// differently — <c>## 1. Hard invariants</c> in each project's <c>AGENTS.md</c> — and the titles
/// themselves share the empty path, as versions of one section. Decision 6 as step 2 built it took
/// every ancestor; Brian settled open item 22 on 2026-10-22 before step 8 writes a key to disk, after
/// which a key's shape is a migration. <see cref="MarkdownOutline"/> is where the path is built, and the
/// remarks there say what makes a heading the title.
/// </para>
/// </remarks>
/// <param name="HeadingPath">The rule: <see cref="MarkdownSection.HeadingPath"/>.</param>
/// <param name="Hash">The version: SHA-256 of <see cref="MarkdownSection.Content"/> as UTF-8, in hex.</param>
internal sealed record SectionIdentity(string HeadingPath, string Hash)
{
    /// <summary>The identity of <paramref name="section"/>.</summary>
    internal static SectionIdentity Of(MarkdownSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return new SectionIdentity(section.HeadingPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(section.Content))));
    }
}
