using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Avalonia;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.LayeredEditors.Avalonia.Diagnostics;
using Serilog;

namespace Bennewitz.Ninja.DiffView.Demo;

public sealed partial class MainWindow : Window
{
    private string? _note;
    private PaneSource? _left;
    private PaneSource? _right;

    public MainWindow()
    {
        InitializeComponent();

        Diff.LoggerFactory = DemoLogging.Factory;
        Diff.BuildCompleted += (_, _) => UpdateStatus();
        Diff.BuildFailed += (_, _) => UpdateStatus();
        Diff.RenderFault += (_, _) => UpdateStatus();

        Unified.LoggerFactory = DemoLogging.Factory;
        Unified.BuildCompleted += (_, _) => UpdateStatus();
        Unified.BuildFailed += (_, _) => UpdateStatus();
        Unified.RenderFault += (_, _) => UpdateStatus();

        // --unified starts in the unified view; the menu item is the same switch.
        UnifiedView.IsChecked = DebugFlags.Unified;
        Unified.IsVisible = DebugFlags.Unified;
        Diff.IsVisible = !DebugFlags.Unified;

        RefreshGestureLabels();

        // The sides load when the window opens, so a host of the window — the smoke snapshot
        // test — can configure the control between construction and the first build.
        Opened += OnOpened;
        UpdateStatus();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        LoadPanes();
        UpdateStatus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            AvaloniaDiagnostics.ToggleLiveLogWindow();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// Loads the two sides — the files named by <c>--left</c> / <c>--right</c>, or the bundled
    /// small fixture — into the composite, which builds on its worker and reports every state
    /// in its own strip. A file that cannot be read falls back to the fixture and says so here
    /// and in the log.
    /// </summary>
    private void LoadPanes()
    {
        _left = LoadSource(DebugFlags.LeftPath, "left.txt");
        _right = LoadSource(DebugFlags.RightPath, "right.txt");
        ApplySources();
    }

    /// <summary>
    /// Hands the sources to the view that is on screen and takes them away from the other, so
    /// only one of the two builds, holds a model and keeps two documents alive.
    /// </summary>
    private void ApplySources()
    {
        if (UnifiedView.IsChecked)
        {
            Diff.LeftSource = null;
            Diff.RightSource = null;
            Unified.LeftSource = _left;
            Unified.RightSource = _right;
        }
        else
        {
            Unified.LeftSource = null;
            Unified.RightSource = null;
            Diff.LeftSource = _left;
            Diff.RightSource = _right;
        }
    }

    private PaneSource LoadSource(string? path, string fixtureFileName)
    {
        if (path is not null)
        {
            try
            {
                return PaneSource.FromFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _note = $"could not read {path}: {ex.Message}";
                Log.Error(ex, "Could not read {Path}; showing the bundled sample instead", path);
            }
        }

        using Stream stream = AssetLoader.Open(new Uri($"avares://DiffView.Demo/Fixtures/small/{fixtureFileName}"));
        using StreamReader reader = new(stream);
        return new PaneSource(reader.ReadToEnd()) { Title = fixtureFileName };
    }

    private async void OnOpenLeft(object? sender, RoutedEventArgs e)
    {
        await OpenIntoAsync(DiffSide.Left);
    }

    private async void OnOpenRight(object? sender, RoutedEventArgs e)
    {
        await OpenIntoAsync(DiffSide.Right);
    }

    /// <summary>
    /// Picks a file and loads it into one side. Reading goes through <see cref="PaneSource.FromFile"/>,
    /// so the encoding and a binary payload are detected there; a read failure is reported in
    /// the composite's status lane and the log, with the path.
    /// </summary>
    private async Task OpenIntoAsync(DiffSide side)
    {
        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = side == DiffSide.Left ? "Open the left file" : "Open the right file",
                AllowMultiple = false,
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            Log.Warning(ex, "The file picker is not available on this platform");
            Status().SetFailure("The file picker is not available here.");
            return;
        }

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        try
        {
            PaneSource source = PaneSource.FromFile(path);
            if (side == DiffSide.Left)
            {
                _left = source;
            }
            else
            {
                _right = source;
            }

            ApplySources();

            Log.Information("Opened {Path} into the {Side} pane ({Length} bytes)", path, side, source.Text.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Could not read {Path}", path);
            Status().SetFailure($"Could not read {path}: {ex.Message}");
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnVariantLight(object? sender, RoutedEventArgs e)
    {
        SetVariant(ThemeVariant.Light);
    }

    private void OnVariantDark(object? sender, RoutedEventArgs e)
    {
        SetVariant(ThemeVariant.Dark);
    }

    private void OnVariantSystem(object? sender, RoutedEventArgs e)
    {
        SetVariant(ThemeVariant.Default);
    }

    private void OnToggleColourBlindPalette(object? sender, RoutedEventArgs e)
    {
        App.UseColourBlindPalette(ColourBlindPalette.IsChecked);
        UpdateStatus();
    }

    /// <summary>
    /// Swaps the two views over. The options travel with the switch, and only the view on screen
    /// keeps the sources, so nothing is built twice.
    /// </summary>
    private void OnToggleUnifiedView(object? sender, RoutedEventArgs e)
    {
        bool unified = UnifiedView.IsChecked;
        Unified.IsVisible = unified;
        Diff.IsVisible = !unified;
        ApplySources();
        UpdateStatus();
    }

    // ── Editing (plan 00003) ───────────────────────────────────────────────────────────────
    //
    // The unified view stays read-only whatever these say: its document is composed from both
    // sides, so half its lines belong to one file and half to the other.

    private void OnToggleEditLeft(object? sender, RoutedEventArgs e)
    {
        Diff.LeftReadOnly = !EditLeft.IsChecked;
        UpdateStatus();
    }

    private void OnToggleEditRight(object? sender, RoutedEventArgs e)
    {
        Diff.RightReadOnly = !EditRight.IsChecked;
        UpdateStatus();
    }

    private void OnCopyToLeft(object? sender, RoutedEventArgs e) => Copy(DiffSide.Left);

    private void OnCopyToRight(object? sender, RoutedEventArgs e) => Copy(DiffSide.Right);

    /// <summary>
    /// The selection when there is one and the current block otherwise — the rule the gutter's
    /// arrows and the copy chord both follow, so the menu cannot promise one and deliver the other.
    /// </summary>
    private void Copy(DiffSide side)
    {
        if (!Diff.CanCopyToward(side))
        {
            Note(Diff.CurrentChangeIndex < 0
                ? $"Nothing to copy onto the {side} side — select some lines, or press {Gesture(DiffCommand.NextChange)} to pick a change block."
                : $"Cannot copy onto the {side} side: it is read-only.");
            return;
        }

        Diff.CopyToward(side);
        UpdateStatus();
    }

    private void OnNextChange(object? sender, RoutedEventArgs e) => Navigate(next: true);

    private void OnPreviousChange(object? sender, RoutedEventArgs e) => Navigate(next: false);

    private void Navigate(bool next)
    {
        if (UnifiedView.IsChecked)
        {
            if (next)
            {
                Unified.NextChange();
            }
            else
            {
                Unified.PreviousChange();
            }
        }
        else if (next)
        {
            Diff.NextChange();
        }
        else
        {
            Diff.PreviousChange();
        }

        UpdateStatus();
    }

    /// <summary>
    /// Rebinding, in code, on the control — which is all plan 00009 offers and all it means to.
    /// Both views take the same two changes, and the menu's accelerators are written back from
    /// <c>GestureFor</c> afterwards, so the labels move with the keys instead of going stale.
    /// </summary>
    private void OnToggleRebindNavigation(object? sender, RoutedEventArgs e)
    {
        KeyGesture? forward = RebindNavigation.IsChecked ? new KeyGesture(Key.Down, KeyModifiers.Control) : new KeyGesture(Key.F7);
        KeyGesture? back = RebindNavigation.IsChecked ? new KeyGesture(Key.Up, KeyModifiers.Control) : new KeyGesture(Key.F7, KeyModifiers.Shift);

        foreach (DiffKeyMap map in (DiffKeyMap[])[Diff.KeyMap, Unified.KeyMap])
        {
            map[DiffCommand.NextChange] = forward;
            map[DiffCommand.PreviousChange] = back;
        }

        RefreshGestureLabels();
        Note($"Next change is now on {Gesture(DiffCommand.NextChange)}.");
    }

    /// <summary>Writes the menu's accelerators from the key map, which is the only place they live.</summary>
    private void RefreshGestureLabels()
    {
        NextChange.InputGesture = Diff.GestureFor(DiffCommand.NextChange);
        PreviousChange.InputGesture = Diff.GestureFor(DiffCommand.PreviousChange);
        CopyToLeft.InputGesture = Diff.GestureFor(DiffCommand.CopyToLeft);
        CopyToRight.InputGesture = Diff.GestureFor(DiffCommand.CopyToRight);
    }

    private string Gesture(DiffCommand command)
    {
        return Diff.GestureFor(command)?.ToString() ?? "nothing";
    }

    private void OnSaveLeft(object? sender, RoutedEventArgs e) => Save(DiffSide.Left);

    private void OnSaveRight(object? sender, RoutedEventArgs e) => Save(DiffSide.Right);

    private void Save(DiffSide side)
    {
        SaveOutcome outcome = Diff.Save(side);
        Note(outcome switch
        {
            SaveOutcome.Saved => $"Saved the {side} side.",
            SaveOutcome.NotDirty => $"The {side} side has no unsaved edits.",
            SaveOutcome.NoPath => $"The {side} side did not come from a file.",
            SaveOutcome.ChangedOnDisk => $"The {side} side changed on disk; nothing was written.",
            _ => $"The {side} side could not be saved; the strip says why.",
        });
    }

    private void OnRevertLeft(object? sender, RoutedEventArgs e) => Revert(DiffSide.Left);

    private void OnRevertRight(object? sender, RoutedEventArgs e) => Revert(DiffSide.Right);

    private void Revert(DiffSide side)
    {
        Diff.Revert(side);
        Note($"Reverted the {side} side to its source.");
    }

    private void Note(string text)
    {
        _note = text;
        UpdateStatus();
    }

    private void OnToggleIgnoreWhitespace(object? sender, RoutedEventArgs e)
    {
        Diff.IgnoreWhitespace = IgnoreWhitespace.IsChecked;
        Unified.IgnoreWhitespace = IgnoreWhitespace.IsChecked;
    }

    private void OnToggleIgnoreCase(object? sender, RoutedEventArgs e)
    {
        Diff.IgnoreCase = IgnoreCase.IsChecked;
        Unified.IgnoreCase = IgnoreCase.IsChecked;
    }

    private void OnToggleSyncHorizontal(object? sender, RoutedEventArgs e)
    {
        Diff.SyncHorizontalScroll = SyncHorizontal.IsChecked;
    }

    private void OnToggleSyntax(object? sender, RoutedEventArgs e)
    {
        // The bundled fixture is .txt, which no grammar claims: open a .cs or .json file, or pass
        // --left / --right, to see this do anything.
        Diff.UseSyntaxHighlighting = UseSyntax.IsChecked;
        Unified.UseSyntaxHighlighting = UseSyntax.IsChecked;
    }

    private void OnToggleShowMinimap(object? sender, RoutedEventArgs e)
    {
        // The unified view has no minimap, so this reaches the side-by-side control only.
        Diff.ShowMinimap = ShowMinimap.IsChecked;
    }

    private void OnToggleMinimapPlacement(object? sender, RoutedEventArgs e)
    {
        Diff.MinimapPlacement = MinimapOnLeft.IsChecked ? MinimapPlacement.Left : MinimapPlacement.Right;
    }

    private void OnToggleShowWhitespace(object? sender, RoutedEventArgs e)
    {
        Diff.ShowWhitespace = ShowWhitespace.IsChecked;
        Unified.ShowWhitespace = ShowWhitespace.IsChecked;
    }

    private void OnToggleShowLineEndings(object? sender, RoutedEventArgs e)
    {
        Diff.ShowLineEndings = ShowLineEndings.IsChecked;
        Unified.ShowLineEndings = ShowLineEndings.IsChecked;
    }

    private void OnTabWidth(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && int.TryParse(tag, CultureInfo.InvariantCulture, out int width))
        {
            Diff.TabWidth = width;
            Unified.TabWidth = width;
        }
    }

    /// <summary>
    /// A size, or the pane theme's own when the item carries no tag. Changing it re-primes the
    /// padded rows, which is what keeps the two panes' extents equal across a font change.
    /// </summary>
    private void OnPaneFontSize(object? sender, RoutedEventArgs e)
    {
        double paneFont = sender is MenuItem { Tag: string tag } && double.TryParse(tag, CultureInfo.InvariantCulture, out double size)
            ? size
            : double.NaN;
        Diff.PaneFontSize = paneFont;
        Unified.PaneFontSize = paneFont;
    }

    private void OnFind(object? sender, RoutedEventArgs e)
    {
        // The control's own Ctrl+F does this too; the item is here so the feature is findable.
        if (UnifiedView.IsChecked)
        {
            Unified.OpenFind();
        }
        else
        {
            Diff.OpenFind();
        }
    }

    /// <summary>The status lane of the view on screen, which is where the demo's own notes go.</summary>
    private StatusController Status()
    {
        return UnifiedView.IsChecked ? Unified.Status : Diff.Status;
    }

    private void OnToggleLiveLog(object? sender, RoutedEventArgs e)
    {
        AvaloniaDiagnostics.ToggleLiveLogWindow();
    }

    private async void OnOpenLogsFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LogPaths.LogsDirectory);
            await Launcher.LaunchUriAsync(new Uri(LogPaths.LogsDirectory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Warning(ex, "Could not open the logs folder {LogDirectory}", LogPaths.LogsDirectory);
        }
    }

    private void OnThrowOnUiThread(object? sender, RoutedEventArgs e)
    {
        // Lands in Dispatcher.UIThread.UnhandledException: logged with a stack, fatal-error dialog shown.
        throw new InvalidOperationException("Deliberate test exception from Debug → Throw on the UI thread.");
    }

    private void OnThrowInUnobservedTask(object? sender, RoutedEventArgs e)
    {
        // The faulted task is dropped on purpose; its exception surfaces through
        // TaskScheduler.UnobservedTaskException once the task is finalized, which the forced
        // collection below brings forward from "eventually" to "now".
        _ = Task.Run(() => throw new InvalidOperationException("Deliberate test exception from Debug → Throw in an unobserved task."));
        _ = ForceFinalizationAsync();
    }

    private static async Task ForceFinalizationAsync()
    {
        await Task.Delay(500);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void SetVariant(ThemeVariant variant)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = variant;
        }

        UpdateStatus();
    }

    private void UpdateStatus()
    {
        ThemeVariant requested = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default;
        VariantLight.IsChecked = requested == ThemeVariant.Light;
        VariantDark.IsChecked = requested == ThemeVariant.Dark;
        VariantSystem.IsChecked = requested == ThemeVariant.Default;

        // No machine-specific text in the rendered status: the snapshot tests compare this window
        // across machines. The diff's own state, counts and timings live in its status strip; the
        // logs path is one hover away, in the Debug menu, and in the log itself. A note carries a
        // path only when a flag named one, which no snapshot does.
        string palette = ColourBlindPalette.IsChecked ? "colour-blind" : "default";
        string layout = UnifiedView.IsChecked ? "unified" : "side by side";
        string note = _note is null ? string.Empty : $"   ·   {_note}";
        string editable = (EditLeft.IsChecked, EditRight.IsChecked) switch
        {
            (true, true) => "both",
            (true, false) => "left",
            (false, true) => "right",
            _ => "neither",
        };
        string dirty = (Diff.IsDirty(DiffSide.Left), Diff.IsDirty(DiffSide.Right)) switch
        {
            (true, true) => "   ·   unsaved: both",
            (true, false) => "   ·   unsaved: left",
            (false, true) => "   ·   unsaved: right",
            _ => string.Empty,
        };
        StatusText.Text = $"Theme: {DebugFlags.Theme}   ·   Variant: {requested} (actual {ActualThemeVariant})   ·   Palette: {palette}   ·   View: {layout}   ·   Editable: {editable}{dirty}{note}   ·   F12: live log";
        ToolTip.SetTip(StatusText, $"Logs: {LogPaths.LogsDirectory}");
    }
}
