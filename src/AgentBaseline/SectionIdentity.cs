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
/// ⚠ The heading path includes every ancestor, a document's own title among them, so two files
/// titled differently share no path at all. That is decision 6 as step 2 built it, and step 6 keeps
/// it; whether a sole top-level title belongs in identity is open, and this type is the one place it
/// would change.
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
