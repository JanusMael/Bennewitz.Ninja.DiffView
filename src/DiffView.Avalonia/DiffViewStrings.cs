using System.Globalization;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Every user-visible string of the library, behind a swappable <see cref="Resolver"/>. The
/// resolver receives a key and returns the localised text, or <c>null</c> to fall back to the
/// English default. Keys are the constants on this class.
/// </summary>
public static class DiffViewStrings
{
    /// <summary>Automation name of the line-number margin.</summary>
    public const string LineNumbersMarginName = "LineNumbersMargin.Name";

    /// <summary>Automation name of the change-marker margin.</summary>
    public const string ChangeMarkersMarginName = "ChangeMarkersMargin.Name";

    /// <summary>A decorator failed and is disabled: <c>{0}</c> decorator, <c>{1}</c> exception message.</summary>
    public const string RenderFault = "RenderFault";

    /// <summary>A decorator failed on a line and is disabled: <c>{0}</c> decorator, <c>{1}</c> line, <c>{2}</c> exception message.</summary>
    public const string RenderFaultOnLine = "RenderFault.OnLine";

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [LineNumbersMarginName] = "Line numbers",
        [ChangeMarkersMarginName] = "Change markers",
        [RenderFault] = "{0} failed and was disabled: {1}",
        [RenderFaultOnLine] = "{0} failed on line {1} and was disabled: {2}",
    };

    /// <summary>The active resolver; <c>null</c> for English.</summary>
    public static Func<string, string?>? Resolver { get; set; }

    /// <summary>The text for <paramref name="key"/>; the key itself when neither the resolver nor the defaults know it.</summary>
    public static string Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Resolver?.Invoke(key) ?? (English.TryGetValue(key, out string? text) ? text : key);
    }

    /// <summary>The text for <paramref name="key"/> formatted with <paramref name="arguments"/> in the current culture.</summary>
    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }

    /// <summary>Restores English. Test cleanup hook.</summary>
    public static void ResetForTesting()
    {
        Resolver = null;
    }
}
