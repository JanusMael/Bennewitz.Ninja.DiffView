using Avalonia;
using Avalonia.Controls.Primitives;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// One pane's header: the title or file name, a detail line (line count, encoding, line
/// endings, size) and an optional badge — "binary", "empty" or "identical" — coloured by its
/// <see cref="BadgeKind"/> and dual-coded by its text. The composite fills it; it renders.
/// </summary>
public class DiffPaneHeader : TemplatedControl
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DiffPaneHeader, string>(nameof(Title), string.Empty);

    /// <summary>Identifies the <see cref="Detail"/> property.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<DiffPaneHeader, string?>(nameof(Detail));

    /// <summary>Identifies the <see cref="Badge"/> property.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<DiffPaneHeader, string?>(nameof(Badge));

    /// <summary>Identifies the <see cref="BadgeKind"/> property.</summary>
    public static readonly StyledProperty<StatusKind> BadgeKindProperty =
        AvaloniaProperty.Register<DiffPaneHeader, StatusKind>(nameof(BadgeKind));

    /// <summary>Identifies the <see cref="IsPaneFocused"/> property.</summary>
    public static readonly StyledProperty<bool> IsPaneFocusedProperty =
        AvaloniaProperty.Register<DiffPaneHeader, bool>(nameof(IsPaneFocused));

    /// <summary>Identifies the <see cref="IsDirty"/> property.</summary>
    public static readonly StyledProperty<bool> IsDirtyProperty =
        AvaloniaProperty.Register<DiffPaneHeader, bool>(nameof(IsDirty));

    /// <summary>Identifies the <see cref="DirtyMarker"/> property.</summary>
    public static readonly StyledProperty<string?> DirtyMarkerProperty =
        AvaloniaProperty.Register<DiffPaneHeader, string?>(nameof(DirtyMarker));

    /// <summary>Creates a header with its compiled theme merged into its own resources.</summary>
    public DiffPaneHeader()
    {
        Resources.MergedDictionaries.Add(new SideBySideDiffViewTheme());
        UpdatePseudoClasses();
    }

    /// <summary>The title: the source's title, its file name, or the side's name.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The detail line, or "No content".</summary>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>The badge text, or <c>null</c> for none.</summary>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <summary>The badge's severity, which picks its colours.</summary>
    public StatusKind BadgeKind
    {
        get => GetValue(BadgeKindProperty);
        set => SetValue(BadgeKindProperty, value);
    }

    /// <summary>
    /// Whether this side's pane has keyboard focus. The header shows it as an accent along its
    /// bottom edge — the pane's own caret is the other half of the answer, and can be scrolled
    /// out of sight. The accent is an overlay, so showing it moves nothing.
    /// </summary>
    /// <summary>Whether the pane holds edits that are not on disk; drives the <c>:dirty</c> class.</summary>
    public bool IsDirty
    {
        get => GetValue(IsDirtyProperty);
        set => SetValue(IsDirtyProperty, value);
    }

    /// <summary>The word shown while <see cref="IsDirty"/>; the theme hides it when it is null.</summary>
    public string? DirtyMarker
    {
        get => GetValue(DirtyMarkerProperty);
        set => SetValue(DirtyMarkerProperty, value);
    }

    /// <summary>Whether this header's pane has keyboard focus; drives the <c>:pane-focused</c> class.</summary>
    public bool IsPaneFocused
    {
        get => GetValue(IsPaneFocusedProperty);
        set => SetValue(IsPaneFocusedProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BadgeKindProperty || change.Property == IsPaneFocusedProperty || change.Property == IsDirtyProperty)
        {
            UpdatePseudoClasses();
        }
    }

    private void UpdatePseudoClasses()
    {
        StatusKind kind = BadgeKind;
        PseudoClasses.Set(":badge-success", kind == StatusKind.Success);
        PseudoClasses.Set(":badge-warning", kind == StatusKind.Warning);
        PseudoClasses.Set(":badge-failure", kind == StatusKind.Failure);
        PseudoClasses.Set(":badge-active", kind == StatusKind.Active);
        PseudoClasses.Set(":pane-focused", IsPaneFocused);
        PseudoClasses.Set(":dirty", IsDirty);
    }
}
