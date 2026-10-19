using System.Collections.Immutable;
using System.Text;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// One candidate after ingest: the bytes exactly as read, and the normalised text the parse and the
/// diff see — plan 00034 decision 8, <i>"normalise on ingest; keep the raw bytes"</i>.
/// </summary>
/// <remarks>
/// <para>
/// Ingest is, in order: decode, normalise line endings, unwrap a document wholly nested in a list
/// item, parse. It does <b>not</b> touch trailing whitespace — two trailing spaces are a markdown hard
/// line break, so removing them would change what a rule renders as — nor anything else the plan did
/// not ask for.
/// </para>
/// <para>
/// Decoding is <see cref="PaneSource.FromBytes"/>'s, and deliberately not a second implementation:
/// this tool hands its text to the diff control at step 5, and a file this tool decoded one way and
/// the control another would put the outline's line numbers on different lines.
/// </para>
/// <para>
/// ⚠ What ingest <i>records</i> is for the provenance report: what the source really was. A candidate
/// that is binary, or that only decoded as Latin-1, is still ingested — whether it belongs in a corpus
/// is the corpus's judgement (step 4), and refusing here would hide it rather than report it.
/// </para>
/// </remarks>
internal sealed class IngestedDocument
{
    private IngestedDocument(
        ImmutableArray<byte> rawBytes,
        string? path,
        PaneSource source,
        bool hadByteOrderMark,
        LineEnding sourceLineEnding,
        UnwrapResult unwrap)
    {
        RawBytes = rawBytes;
        Path = path;
        Encoding = source.Encoding ?? throw new InvalidOperationException("PaneSource.FromBytes always records an encoding.");
        HadByteOrderMark = hadByteOrderMark;
        Latin1Fallback = source.Latin1Fallback;
        IsBinary = source.IsBinary;
        SourceLineEnding = sourceLineEnding;
        UnwrappedLevels = unwrap.Levels;
        UnwrapRefusal = unwrap.Refusal;
        Text = unwrap.Text;
        Outline = MarkdownOutline.Read(unwrap.Text);
    }

    /// <summary>
    /// The source exactly as read, kept for provenance. A copy: the array a caller passed in remains
    /// the caller's, so changing it afterwards cannot change what this document says the source was.
    /// </summary>
    internal ImmutableArray<byte> RawBytes { get; }

    /// <summary>Where the bytes came from, when they came from a file.</summary>
    internal string? Path { get; }

    /// <summary>The encoding the bytes were decoded with.</summary>
    internal Encoding Encoding { get; }

    /// <summary>
    /// Whether the bytes opened with a byte-order mark. It is never part of <see cref="Text"/>, and
    /// recorded so the report can say the source carried one.
    /// </summary>
    internal bool HadByteOrderMark { get; }

    /// <summary>Whether the bytes were neither marked nor valid UTF-8, so Latin-1 was used.</summary>
    internal bool Latin1Fallback { get; }

    /// <summary>Whether the bytes looked binary — a NUL among the first ones read.</summary>
    internal bool IsBinary { get; }

    /// <summary>The line-ending convention of the source, before normalisation made it LF.</summary>
    internal LineEnding SourceLineEnding { get; }

    /// <summary>How many levels of list nesting ingest removed: 0 for an ordinary document.</summary>
    internal int UnwrappedLevels { get; }

    /// <summary>
    /// Why a nested document was left nested, or <see langword="null"/>. The parse still finds such a
    /// document's headings — it walks descendants — but a take from it carries the indent with it.
    /// </summary>
    internal string? UnwrapRefusal { get; }

    /// <summary>The normalised text: decoded, LF, unwrapped. What the parse and the diff see.</summary>
    internal string Text { get; }

    /// <summary>The heading outline of <see cref="Text"/>.</summary>
    internal MarkdownOutline Outline { get; }

    /// <summary>Ingests <paramref name="bytes"/>.</summary>
    internal static IngestedDocument FromBytes(byte[] bytes, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        ImmutableArray<byte> raw = [.. bytes];
        PaneSource source = PaneSource.FromBytes(bytes, path);

        // A preamble the detected encoding defines and the bytes actually open with. Strict UTF-8 and
        // Latin-1 define none, so an unmarked file can never be reported as marked.
        ReadOnlySpan<byte> preamble = source.Encoding is { } encoding ? encoding.Preamble : default;
        bool marked = preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble);

        return new IngestedDocument(
            raw,
            path,
            source,
            marked,
            TextProbe.Probe(source.Text).LineEnding,
            ListUnwrap.Unwrap(source.Text));
    }

    /// <summary>Reads <paramref name="path"/> and ingests it as <see cref="FromBytes"/> does.</summary>
    internal static IngestedDocument FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromBytes(File.ReadAllBytes(path), path);
    }
}
