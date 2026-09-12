using System.Globalization;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// How the library resolves its user-visible text: an optional host resolver and an optional
/// culture, as one value.
/// </summary>
/// <remarks>
/// <para>
/// They are one record rather than two settable properties because they have to agree. A resolver
/// answering for one language while the bundled translations answer for another is a mixed-language
/// interface — a host covering most of the catalogue for its own Japanese chrome would otherwise
/// see the rest resolve in whatever language the machine happened to be set to — and two properties
/// cannot be changed, or restored, in one step.
/// </para>
/// <para>
/// Assign through <see cref="DiffViewStrings.Localization"/>, or scope a change with
/// <see cref="DiffViewStrings.Override"/>.
/// </para>
/// </remarks>
public sealed record DiffViewLocalization
{
    /// <summary>
    /// Resolves a key to its text, or <c>null</c> to fall through to the bundled translations and
    /// then to English. Outranks everything the library ships, so a host that wires one always wins.
    /// </summary>
    /// <remarks>
    /// The culture is the one resolved for this lookup — <see cref="Culture"/> where it is set and
    /// <see cref="CultureInfo.CurrentUICulture"/> where it is not. It is handed over rather than
    /// read by the resolver so that returning <c>null</c> for a key is a decision made knowing which
    /// language will answer instead.
    /// </remarks>
    public Func<string, CultureInfo, string?>? Resolver { get; init; }

    /// <summary>
    /// The culture to resolve text in; <c>null</c> follows <see cref="CultureInfo.CurrentUICulture"/>.
    /// Set it where the host's language is its own rather than the machine's — an editor embedded in
    /// an application follows that application, not the operating system.
    /// </summary>
    /// <remarks>
    /// This is the <em>text</em> culture. Numbers and dates stay on
    /// <see cref="CultureInfo.CurrentCulture"/>: .NET separates the two deliberately, and so does
    /// this library, because pinning German text is not a request for German decimal separators.
    /// </remarks>
    public CultureInfo? Culture { get; init; }

    /// <summary>
    /// No resolver and no culture: the bundled translations under the machine's UI culture, with
    /// English behind them.
    /// </summary>
    public static DiffViewLocalization Default { get; } = new();
}
