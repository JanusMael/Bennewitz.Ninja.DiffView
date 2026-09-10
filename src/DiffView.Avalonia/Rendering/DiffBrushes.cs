using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>The brushes the presenter's decorators draw with, each a <c>DiffView.*</c> token.</summary>
internal enum DiffBrush
{
    PaneBackground,
    PaneForeground,
    GutterBackground,
    LineNumberForeground,
    Caret,
    Selection,
    Inserted,
    Deleted,
    Modified,
    Padding,
    MarkerInserted,
    MarkerDeleted,
    MarkerModified,
    MarkerChipInserted,
    MarkerChipDeleted,
    MarkerChipModified,
    WordInserted,
    WordDeleted,
    CurrentBlockBorder,
    Connector,
    GutterArrow,
    GutterArrowFill,
    SelectionArrow,
    SelectionArrowFill,
    SelectionArrowBar,
    ModifiedSinceLoad,
    FindMatch,
    FindCurrentMatch,
}

/// <summary>
/// Resolves the <c>DiffView.*</c> tokens the renderers and margins draw with, from the presenter's
/// position in the tree under its actual theme variant, with a hard fallback per token (the
/// Light palette) so a host that forgot the theme include still gets a visible pane. Resolved on
/// attach and again whenever the resources or the theme variant change.
/// </summary>
internal sealed class DiffBrushes
{
    private static readonly (string Key, Color Fallback)[] Tokens =
    [
        ("DiffView.PaneBackgroundBrush", Color.Parse("#FFFFFF")),
        ("DiffView.PaneForegroundBrush", Color.Parse("#1C1F23")),
        ("DiffView.GutterBackgroundBrush", Color.Parse("#F3F4F6")),
        ("DiffView.LineNumberForegroundBrush", Color.Parse("#6B7075")),
        ("DiffView.CaretBrush", Color.Parse("#1C1F23")),
        ("DiffView.SelectionBrush", Color.Parse("#593390FF")),
        ("DiffView.InsertedBrush", Color.Parse("#262E7D32")),
        ("DiffView.DeletedBrush", Color.Parse("#26C62828")),
        ("DiffView.ModifiedBrush", Color.Parse("#26D96A00")),
        ("DiffView.PaddingBrush", Color.Parse("#269E9E9E")),
        ("DiffView.MarkerInsertedBrush", Color.Parse("#2E7D32")),
        ("DiffView.MarkerDeletedBrush", Color.Parse("#C62828")),
        ("DiffView.MarkerModifiedBrush", Color.Parse("#D96A00")),
        ("DiffView.MarkerChipInsertedBrush", Color.Parse("#E6EFE6")),
        ("DiffView.MarkerChipDeletedBrush", Color.Parse("#F8E5E5")),
        ("DiffView.MarkerChipModifiedBrush", Color.Parse("#FAEDE0")),
        ("DiffView.WordInsertedBrush", Color.Parse("#592E7D32")),
        ("DiffView.WordDeletedBrush", Color.Parse("#59C62828")),
        ("DiffView.CurrentBlockBorderBrush", Color.Parse("#1565C0")),
        ("DiffView.ConnectorBrush", Color.Parse("#757575")),
        ("DiffView.GutterArrowBrush", Color.Parse("#7A5C00")),
        ("DiffView.GutterArrowFillBrush", Color.Parse("#AB8000")),
        ("DiffView.SelectionArrowBrush", Color.Parse("#0D47A1")),
        ("DiffView.SelectionArrowFillBrush", Color.Parse("#1976D2")),
        ("DiffView.SelectionArrowBarBrush", Colors.Transparent),
        ("DiffView.ModifiedSinceLoadBrush", Color.Parse("#F57C00")),
        ("DiffView.FindMatchBrush", Color.Parse("#80FFD54F")),
        ("DiffView.FindCurrentMatchBrush", Color.Parse("#99FF8F00")),
    ];

    private readonly IBrush[] _brushes;

    public DiffBrushes()
    {
        _brushes = new IBrush[Tokens.Length];
        for (int i = 0; i < Tokens.Length; i++)
        {
            _brushes[i] = new ImmutableSolidColorBrush(Tokens[i].Fallback);
        }
    }

    /// <summary>The resource key of <paramref name="brush"/>.</summary>
    public static string KeyOf(DiffBrush brush) => Tokens[(int)brush].Key;

    /// <summary>The brush currently resolved for <paramref name="brush"/>.</summary>
    public IBrush this[DiffBrush brush] => _brushes[(int)brush];

    /// <summary>The row tint for <paramref name="kind"/>; transparent for an unchanged row.</summary>
    public IBrush ForKind(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Inserted => this[DiffBrush.Inserted],
            DiffLineKind.Deleted => this[DiffBrush.Deleted],
            DiffLineKind.Modified => this[DiffBrush.Modified],
            _ => Brushes.Transparent,
        };
    }

    /// <summary>The word-level highlight for a piece of <paramref name="kind"/>; transparent for an unchanged piece.</summary>
    public IBrush ForPiece(PieceKind kind)
    {
        return kind switch
        {
            PieceKind.Inserted => this[DiffBrush.WordInserted],
            PieceKind.Deleted => this[DiffBrush.WordDeleted],
            _ => Brushes.Transparent,
        };
    }

    /// <summary>The marker brush for <paramref name="kind"/>; transparent for an unchanged row.</summary>
    public IBrush MarkerFor(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Inserted => this[DiffBrush.MarkerInserted],
            DiffLineKind.Deleted => this[DiffBrush.MarkerDeleted],
            DiffLineKind.Modified => this[DiffBrush.MarkerModified],
            _ => Brushes.Transparent,
        };
    }

    /// <summary>The ground <paramref name="kind"/>'s marker glyph is drawn on; transparent for an unchanged line.</summary>
    public IBrush MarkerChipFor(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Inserted => this[DiffBrush.MarkerChipInserted],
            DiffLineKind.Deleted => this[DiffBrush.MarkerChipDeleted],
            DiffLineKind.Modified => this[DiffBrush.MarkerChipModified],
            _ => Brushes.Transparent,
        };
    }

    /// <summary>
    /// Re-resolves every token from <paramref name="host"/> under its actual theme variant.
    /// Returns whether any brush changed, so the caller can invalidate what draws with them.
    /// </summary>
    public bool Resolve(StyledElement host)
    {
        ArgumentNullException.ThrowIfNull(host);
        bool changed = false;
        for (int i = 0; i < Tokens.Length; i++)
        {
            IBrush brush = host.TryFindResource(Tokens[i].Key, host.ActualThemeVariant, out object? value) && value is IBrush resolved
                ? resolved
                : new ImmutableSolidColorBrush(Tokens[i].Fallback);
            if (!Equals(brush, _brushes[i]))
            {
                _brushes[i] = brush;
                changed = true;
            }
        }

        return changed;
    }
}
