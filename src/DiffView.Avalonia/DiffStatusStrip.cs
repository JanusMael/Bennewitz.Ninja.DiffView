using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The strip under the panes: the state pill, the stale marker and progress while a build runs,
/// the row counts, the change count, the build time, the options in effect, the caret position
/// of the focused pane, and the transient message lane driven by <see cref="StatusController"/>
/// with a dismiss control for a failure. The composite fills it; it renders. Every pill carries
/// its text as tooltip and automation name, so nothing is colour alone.
/// </summary>
public class DiffStatusStrip : TemplatedControl
{
    /// <summary>Identifies the <see cref="State"/> property.</summary>
    public static readonly StyledProperty<DiffViewState> StateProperty =
        AvaloniaProperty.Register<DiffStatusStrip, DiffViewState>(nameof(State));

    /// <summary>Identifies the <see cref="StateText"/> property.</summary>
    public static readonly StyledProperty<string> StateTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string>(nameof(StateText), string.Empty);

    /// <summary>Identifies the <see cref="IsStale"/> property.</summary>
    public static readonly StyledProperty<bool> IsStaleProperty =
        AvaloniaProperty.Register<DiffStatusStrip, bool>(nameof(IsStale));

    /// <summary>Identifies the <see cref="StaleText"/> property.</summary>
    public static readonly StyledProperty<string> StaleTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string>(nameof(StaleText), string.Empty);

    /// <summary>Identifies the <see cref="DirtyText"/> property.</summary>
    public static readonly StyledProperty<string?> DirtyTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(DirtyText));

    /// <summary>Identifies the <see cref="IsBuildingSlowly"/> property.</summary>
    public static readonly StyledProperty<bool> IsBuildingSlowlyProperty =
        AvaloniaProperty.Register<DiffStatusStrip, bool>(nameof(IsBuildingSlowly));

    /// <summary>Identifies the <see cref="ProgressName"/> property.</summary>
    public static readonly StyledProperty<string> ProgressNameProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string>(nameof(ProgressName), string.Empty);

    /// <summary>Identifies the <see cref="CountsText"/> property.</summary>
    public static readonly StyledProperty<string?> CountsTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(CountsText));

    /// <summary>Identifies the <see cref="ChangesText"/> property.</summary>
    public static readonly StyledProperty<string?> ChangesTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(ChangesText));

    /// <summary>Identifies the <see cref="BuildTimeText"/> property.</summary>
    public static readonly StyledProperty<string?> BuildTimeTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(BuildTimeText));

    /// <summary>Identifies the <see cref="FindText"/> property.</summary>
    public static readonly StyledProperty<string?> FindTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(FindText));

    /// <summary>Identifies the <see cref="OptionsText"/> property.</summary>
    public static readonly StyledProperty<string?> OptionsTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(OptionsText));

    /// <summary>Identifies the <see cref="CaretText"/> property.</summary>
    public static readonly StyledProperty<string?> CaretTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(CaretText));

    /// <summary>Identifies the <see cref="TransientText"/> property.</summary>
    public static readonly StyledProperty<string?> TransientTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string?>(nameof(TransientText));

    /// <summary>Identifies the <see cref="TransientKind"/> property.</summary>
    public static readonly StyledProperty<StatusKind> TransientKindProperty =
        AvaloniaProperty.Register<DiffStatusStrip, StatusKind>(nameof(TransientKind));

    /// <summary>Identifies the <see cref="IsTransientDismissible"/> property.</summary>
    public static readonly StyledProperty<bool> IsTransientDismissibleProperty =
        AvaloniaProperty.Register<DiffStatusStrip, bool>(nameof(IsTransientDismissible));

    /// <summary>Identifies the <see cref="DismissText"/> property.</summary>
    public static readonly StyledProperty<string> DismissTextProperty =
        AvaloniaProperty.Register<DiffStatusStrip, string>(nameof(DismissText), string.Empty);

    /// <summary>Identifies the <see cref="DismissCommand"/> property.</summary>
    public static readonly DirectProperty<DiffStatusStrip, ICommand> DismissCommandProperty =
        AvaloniaProperty.RegisterDirect<DiffStatusStrip, ICommand>(nameof(DismissCommand), o => o.DismissCommand);

    private readonly DelegateCommand _dismiss;

    /// <summary>Creates a strip with its compiled theme merged into its own resources.</summary>
    public DiffStatusStrip()
    {
        Resources.MergedDictionaries.Add(new SideBySideDiffViewTheme());
        _dismiss = new DelegateCommand(() => DismissRequested?.Invoke(this, EventArgs.Empty));
        UpdatePseudoClasses();
    }

    /// <summary>The dismiss control was used.</summary>
    public event EventHandler? DismissRequested;

    /// <summary>The state the pill shows.</summary>
    public DiffViewState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>The state pill's text.</summary>
    public string StateText
    {
        get => GetValue(StateTextProperty);
        set => SetValue(StateTextProperty, value);
    }

    /// <summary>Whether the result on screen is about to be replaced.</summary>
    public bool IsStale
    {
        get => GetValue(IsStaleProperty);
        set => SetValue(IsStaleProperty, value);
    }

    /// <summary>The stale marker's text.</summary>
    public string StaleText
    {
        get => GetValue(StaleTextProperty);
        set => SetValue(StaleTextProperty, value);
    }

    /// <summary>Whether the build has run long enough to show progress.</summary>
    public bool IsBuildingSlowly
    {
        get => GetValue(IsBuildingSlowlyProperty);
        set => SetValue(IsBuildingSlowlyProperty, value);
    }

    /// <summary>The progress indicator's automation name.</summary>
    public string ProgressName
    {
        get => GetValue(ProgressNameProperty);
        set => SetValue(ProgressNameProperty, value);
    }

    /// <summary>Which sides hold unsaved edits, or <c>null</c> when none do.</summary>
    public string? DirtyText
    {
        get => GetValue(DirtyTextProperty);
        set => SetValue(DirtyTextProperty, value);
    }

    /// <summary>The row counts, or <c>null</c> without a result.</summary>
    public string? CountsText
    {
        get => GetValue(CountsTextProperty);
        set => SetValue(CountsTextProperty, value);
    }

    /// <summary>The change count, or <c>null</c> without a result.</summary>
    public string? ChangesText
    {
        get => GetValue(ChangesTextProperty);
        set => SetValue(ChangesTextProperty, value);
    }

    /// <summary>The build time, or <c>null</c> without a result.</summary>
    public string? BuildTimeText
    {
        get => GetValue(BuildTimeTextProperty);
        set => SetValue(BuildTimeTextProperty, value);
    }

    /// <summary>The find count and scope while the find bar is open, or <c>null</c> when it is closed.</summary>
    public string? FindText
    {
        get => GetValue(FindTextProperty);
        set => SetValue(FindTextProperty, value);
    }

    /// <summary>The options in effect, or <c>null</c> when all are default.</summary>
    public string? OptionsText
    {
        get => GetValue(OptionsTextProperty);
        set => SetValue(OptionsTextProperty, value);
    }

    /// <summary>The focused pane's caret position, or <c>null</c> when no pane has focus.</summary>
    public string? CaretText
    {
        get => GetValue(CaretTextProperty);
        set => SetValue(CaretTextProperty, value);
    }

    /// <summary>The transient message, or <c>null</c> for none.</summary>
    public string? TransientText
    {
        get => GetValue(TransientTextProperty);
        set => SetValue(TransientTextProperty, value);
    }

    /// <summary>The transient message's severity, which picks its colours.</summary>
    public StatusKind TransientKind
    {
        get => GetValue(TransientKindProperty);
        set => SetValue(TransientKindProperty, value);
    }

    /// <summary>Whether the transient message shows a dismiss control.</summary>
    public bool IsTransientDismissible
    {
        get => GetValue(IsTransientDismissibleProperty);
        set => SetValue(IsTransientDismissibleProperty, value);
    }

    /// <summary>The dismiss control's name.</summary>
    public string DismissText
    {
        get => GetValue(DismissTextProperty);
        set => SetValue(DismissTextProperty, value);
    }

    /// <summary>Raises <see cref="DismissRequested"/>.</summary>
    public ICommand DismissCommand => _dismiss;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty || change.Property == TransientKindProperty || change.Property == IsStaleProperty)
        {
            UpdatePseudoClasses();
        }
    }

    private void UpdatePseudoClasses()
    {
        DiffViewState state = State;
        PseudoClasses.Set(":state-empty", state == DiffViewState.Empty);
        PseudoClasses.Set(":state-building", state == DiffViewState.Building);
        PseudoClasses.Set(":state-ready", state == DiffViewState.Ready);
        PseudoClasses.Set(":state-degraded", state == DiffViewState.Degraded);
        PseudoClasses.Set(":state-failed", state == DiffViewState.Failed);
        PseudoClasses.Set(":stale", IsStale);

        StatusKind kind = TransientKind;
        PseudoClasses.Set(":transient-active", kind == StatusKind.Active);
        PseudoClasses.Set(":transient-success", kind == StatusKind.Success);
        PseudoClasses.Set(":transient-warning", kind == StatusKind.Warning);
        PseudoClasses.Set(":transient-failure", kind == StatusKind.Failure);
        PseudoClasses.Set(":transient-state", kind == StatusKind.State);
    }
}
