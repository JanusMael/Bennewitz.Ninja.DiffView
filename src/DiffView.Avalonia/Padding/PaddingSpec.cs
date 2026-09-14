using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// Rows of padding a line carries: <see cref="Above"/> before its text, <see cref="Below"/>
/// after it. Only the last line of a document carries <see cref="Below"/> (trailing padding).
/// </summary>
internal readonly record struct PaddingSpec(int Above, int Below)
{
    /// <summary>No padding.</summary>
    public static PaddingSpec None => default;

    /// <summary>Whether the line carries no padding at all.</summary>
    public bool IsEmpty => Above == 0 && Below == 0;

    /// <summary>Rows of padding in total.</summary>
    public int Rows => Above + Below;
}

/// <summary>
/// The vertical metrics a padded line is computed from. Ascent, descent and line gap are the
/// numbers Avalonia's line metrics start from (its <c>TextMetrics</c> of the default run
/// properties); <see cref="LineHeight"/> is <c>TextView.DefaultLineHeight</c>, which includes
/// <c>TextEditorOptions.LineHeightFactor</c>. Measured exact to 10⁻⁶ px in the Phase 1 spike
/// (<c>DECISIONS.md</c>, "Virtual padding: go").
/// </summary>
internal readonly record struct PaddingMetrics(double Ascent, double Descent, double LineGap, double LineHeight)
{
    /// <summary>Natural height of a text line, before the line-height factor.</summary>
    public double TextHeight => Ascent + Descent + LineGap;

    /// <summary>
    /// Half of what the line-height factor adds. A plain line's text is centred in its row, so
    /// this much sits above and below the text; a padded row keeps the same centring.
    /// </summary>
    public double HalfSlack => (LineHeight - TextHeight) / 2;

    /// <summary>The metrics of the text view's default run properties.</summary>
    public static PaddingMetrics From(TextView textView, TextRunProperties properties)
    {
        TextMetrics metrics = new(properties.Typeface.GlyphTypeface, properties.FontRenderingEmSize);
        return new PaddingMetrics(-metrics.Ascent, metrics.Descent, metrics.LineGap, textView.DefaultLineHeight);
    }
}
