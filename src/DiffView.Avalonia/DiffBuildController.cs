using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;
using Microsoft.Extensions.Logging;
using TextInfo = Bennewitz.Ninja.DiffView.Core.TextInfo;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The build orchestration two sibling diff controls share: the model, the state machine, the
/// template parts, the panes, the chrome, folding and navigation. Not a control — it holds the
/// shared private state as its own fields and drives the surface that owns it through
/// <see cref="IDiffSurface"/>.
/// </summary>
/// <remarks>
/// <para>
/// This exists because derivation cannot narrow. A read-only viewer deriving from the editable
/// control would inherit <c>Save</c>, <c>Revert</c>, <c>CopyBlock</c> and the rest; making the
/// viewer the base instead would publish 194 members as permanent v1 API on a type no consumer
/// had used. So the two controls are unrelated siblings, and what they have in common lives here.
/// </para>
/// <para>
/// State that was private to one control stays private to one class: the fields below moved with
/// the code that uses them, which is what keeps the seam at roughly fifteen members instead of the
/// ninety an extension block would have needed.
/// </para>
/// <para>
/// Directly constructible, so the orchestration can be driven in a test with no visual tree — the
/// build path used to be reachable only through a headless host.
/// </para>
/// </remarks>
internal sealed class DiffBuildController
{
    private readonly IDiffSurface _surface;

    internal DiffBuildController(IDiffSurface surface)
    {
        _surface = surface;
    }

    /// <summary>The control this drives, for the value of a styled property and for the visual.</summary>
    private TemplatedControl Control => _surface.Control;

    private int _currentChangeIndex = -1;

    private double _splitRatio = 0.5;

    private Grid? _headersGrid;

    private Grid? _panesGrid;

    private ChangeConnectorGutter? _gutter;

    private DiffMinimap? _minimap;

    private Border? _headerLeftSpacer;

    private Border? _headerRightSpacer;

    /// <summary>
    /// The columns `PART_Panes` and `PART_Headers` share: a map slot at either end, the two star
    /// columns the split ratio is written into, and the connector gutter between them. The two
    /// grids are laid out alike so a header is exactly as wide as its pane by construction.
    /// </summary>
    private const int LeftMinimapColumn = 0;

    private const int LeftPaneColumn = 1;

    private const int RightPaneColumn = 3;

    private const int RightMinimapColumn = 4;

    private DiffViewState _state = DiffViewState.Empty;

    private string? _stateMessage;

    private SideBySideDocument? _document;

    /// <summary>
    /// How the model's rows sit on screen. The identity until something folds them, which is the
    /// arithmetic every out-of-pane surface did inline before plan 00013.
    /// </summary>
    private RowProjection _projection = RowProjection.Identity(0);

    /// <summary>Rows either side of a change that stay visible; <c>null</c> folds nothing.</summary>
    private int? _foldContextRows;

    private int _foldMinimumRows = FoldPlan.DefaultMinimumFoldedRows;

    /// <summary>
    /// The first row of every run the reader has opened. Cleared with the model: a rebuild makes
    /// new runs, and a row number from the old one means nothing to them.
    /// </summary>
    private readonly HashSet<int> _expandedFolds = [];

    private DiffDiagnostics? _diagnostics;

    private IReadOnlyList<DiffWarning> _warnings = [];

    private int _changeCount;

    private DiffBannerKind _bannerKind;

    private string? _bannerMessage;

    private string? _bannerActionText;

    private bool _isStale;

    private bool _isBuildingSlowly;

    private TextDocument _leftDocument = new();

    private TextDocument _rightDocument = new();

    private DiffSide? _focusedSide;

    private int _caretLine;

    private int _caretColumn;

    private TextInfo? _leftInfo;

    private TextInfo? _rightInfo;

    private StatusController? _status;

    private ILoggerFactory? _loggerFactory;

    private ILogger? _buildLogger;

    private ILogger? _renderLogger;

    private int _generation;

    private CancellationTokenSource? _buildCts;

    private ITimer? _slowTimer;

    private DiffPanePresenter? _leftPane;

    private DiffPanePresenter? _rightPane;

    private DiffPaneHeader? _leftHeader;

    private DiffPaneHeader? _rightHeader;

    private DiffStatusStrip? _statusStrip;

    private Button? _bannerAction;

    private Border? _banner;

    private ScrollSync? _sync;

    private void OnSourceChanged(DiffSide side, PaneSource? source)
    {
        DiffViewLog.SourceAssigned(_buildLogger, side, source);
        TextInfo? info = source is null ? null : TextProbe.Probe(source);
        TextDocument document = new(source?.Text ?? string.Empty);

        // The surface sheds what it held about the old document first — the editor unwires its
        // two handlers, forgets the edit and the modified lines, and re-stamps the file on disk —
        // because the assignment below is what makes the old one unreachable.
        _surface.OnSourceReplaced(side, document);

        if (side == DiffSide.Left)
        {
            _leftInfo = info;
            LeftDocument = document;
        }
        else
        {
            _rightInfo = info;
            RightDocument = document;
        }

        DiffPanePresenter? pane = Pane(side);
        if (pane is not null)
        {
            pane.Document = document;
            // The grammar follows the file, not the build, so it is chosen here.
            pane.SyntaxFileName = SyntaxFileNameOf(source);
        }

        // The old model described the old text; nothing of it applies to the new document.
        RequestBuild(keepModel: false);
    }

    /// <summary>What the grammar is chosen from: the file the side came from, or what it is called.</summary>
    private static string? SyntaxFileNameOf(PaneSource? source)
    {
        return source?.Path ?? source?.Title;
    }

    /// <summary>
    /// What the next build compares: the assigned source, or the pane's live text once the user
    /// has edited it. The encoding, the path and the title come from the source either way —
    /// they are what a save writes back with, and typing does not change them.
    /// </summary>
    private PaneSource? EffectiveSource(DiffSide side)
    {
        PaneSource? source = side == DiffSide.Left ? LeftSource : RightSource;
        if (source is null || !_surface.IsEdited(side))
        {
            return source;
        }

        // Read on the UI thread, like every other text the worker sees.
        TextDocument document = side == DiffSide.Left ? LeftDocument : RightDocument;
        return new PaneSource(document.Text)
        {
            Encoding = source.Encoding,
            Path = source.Path,
            Title = source.Title,
        };
    }

    /// <summary>
    /// What to call a side in a save message: the same name its header shows. The banner is left
    /// alone deliberately — it reports what the *build* did, and a save is not a build.
    /// </summary>
    internal static string HeaderTitle(DiffSide side, PaneSource? source)
    {
        return source?.Title
               ?? (source?.Path is { } path ? System.IO.Path.GetFileName(path) : null)
               ?? DiffViewStrings.Get(side == DiffSide.Left ? DiffViewStrings.LeftTitle : DiffViewStrings.RightTitle);
    }

    private void RequestBuild(bool keepModel)
    {
        PaneSource? left = EffectiveSource(DiffSide.Left);
        PaneSource? right = EffectiveSource(DiffSide.Right);
        if (left is null || right is null)
        {
            CancelBuild();
            ApplyModel(null, [], null, null);
            SetState(DiffViewState.Empty, DiffViewStrings.Get(DiffViewStrings.StateEmptyMessage));
            SetBanner(DiffBannerKind.None, null, null);
            IsStale = false;
            _status?.Dismiss();
            UpdateHeaders();
            UpdateStrip();
            return;
        }

        CancelBuild();
        int generation = ++_generation;
        CancellationTokenSource cts = new();
        _buildCts = cts;
        DiffOptions options = ComposeOptions();

        if (!keepModel)
        {
            ApplyModel(null, [], null, null);
        }

        _leftPane?.ResetFaults();
        _rightPane?.ResetFaults();
        IsStale = Document is not null;
        SetState(DiffViewState.Building, null);
        SetBanner(DiffBannerKind.None, null, null);
        Status.SetActive(DiffViewStrings.Get(DiffViewStrings.BuildRunning));
        StartSlowTimer(generation);
        DiffViewLog.BuildStarted(_buildLogger, generation, options);
        UpdateHeaders();
        UpdateStrip();

        CurrentBuild = RunBuildAsync(generation, left, right, options, cts.Token);
    }

    private async Task RunBuildAsync(int generation, PaneSource left, PaneSource right, DiffOptions options, CancellationToken token)
    {
        DiffBuildResult? result = null;
        DiffBuildException? failure = null;
        bool cancelled = false;
        try
        {
            // Text was captured on the UI thread; the worker never sees a TextDocument.
            result = await Task.Run(() => Builder(left, right, options, token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (DiffBuildException ex)
        {
            failure = ex;
        }
        catch (Exception ex)
        {
            failure = new DiffBuildException(DiffBuildErrorCode.DiffFailed, ex.Message, ex);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Complete(generation, result, failure, cancelled, options);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => Complete(generation, result, failure, cancelled, options));
        }
    }

    private void Complete(int generation, DiffBuildResult? result, DiffBuildException? failure, bool cancelled, DiffOptions options)
    {
        if (generation != _generation)
        {
            // Latest wins: a newer build superseded this one; its outcome is discarded silently.
            DiffViewLog.BuildSuperseded(_buildLogger, generation, _generation);
            return;
        }

        StopSlowTimer();
        CurrentBuild = null;
        if (cancelled)
        {
            DiffViewLog.BuildCancelled(_buildLogger, generation);
            return;
        }

        if (failure is not null)
        {
            ApplyFailure(generation, failure);
        }
        else
        {
            ApplyResult(generation, result!, options);
        }
    }

    private void ApplyResult(int generation, DiffBuildResult result, DiffOptions options)
    {
        // A decorator that faulted while this build ran — a grammar that would not install, say —
        // keeps the control Degraded once it lands. It is read before the model is applied,
        // because applying one re-enables every decorator and forgets its faults.
        RenderFaultEventArgs? fault = PaneFault();
        ApplyModel(result.Document, result.Warnings, result.Diagnostics, options);
        IsStale = false;
        foreach (DiffWarning warning in result.Warnings)
        {
            DiffViewLog.BuildWarning(_buildLogger, warning);
        }

        DiffViewLog.BuildCompleted(_buildLogger, generation, result.Diagnostics, result.Warnings.Count);

        DiffWarning? tooDifferent = result.Warnings.FirstOrDefault(w => w.Code == DiffWarningCode.TooDifferentToAlign);
        if (tooDifferent is not null)
        {
            SetBanner(DiffBannerKind.TooDifferentToAlign, tooDifferent.Message, DiffViewStrings.Get(DiffViewStrings.BannerForce));
        }
        else if (result.Diagnostics.Identical)
        {
            SetBanner(DiffBannerKind.Identical, DiffViewStrings.Get(DiffViewStrings.BannerIdentical), null);
        }
        else
        {
            SetBanner(DiffBannerKind.None, null, null);
        }

        string? message = result.Warnings.Count == 0 ? fault?.Message : string.Join(" ", result.Warnings.Select(w => w.Message));
        SetState(message is null ? DiffViewState.Ready : DiffViewState.Degraded, message);

        if (result.Warnings.Count > 0)
        {
            Status.SetWarning(message!);
        }
        else if (fault is not null)
        {
            Status.SetFailure(message!);
        }
        else if (result.Diagnostics.Identical)
        {
            Status.SetSuccess(DiffViewStrings.Get(DiffViewStrings.BuildIdentical));
        }
        else
        {
            Status.SetSuccess(DiffViewStrings.Format(
                DiffViewStrings.BuildCompleted,
                result.Diagnostics.RowCount.ToString("N0", CultureInfo.CurrentCulture),
                result.Diagnostics.BuildTime.TotalMilliseconds.ToString("F0", CultureInfo.CurrentCulture)));
        }

        UpdateHeaders();
        UpdateStrip();
        _surface.RaiseBuildCompleted(new DiffBuildCompletedEventArgs(result));
    }

    private void ApplyFailure(int generation, DiffBuildException failure)
    {
        DiffViewLog.BuildFailed(_buildLogger, generation, failure);
        IsStale = false;
        SetBanner(DiffBannerKind.Error, failure.Message, DiffViewStrings.Get(DiffViewStrings.BannerRetry));
        SetState(DiffViewState.Failed, failure.Message);
        Status.SetFailure(failure.Message);
        UpdateHeaders();
        UpdateStrip();
        _surface.RaiseBuildFailed(new DiffBuildFailedEventArgs(failure));
    }

    /// <summary>
    /// The first fault either pane is still carrying: one its decorators raised since they were
    /// last re-enabled, or the one that turned its syntax highlighting off, which outlives a
    /// rebuild because a rebuild is not what would fix it.
    /// </summary>
    private RenderFaultEventArgs? PaneFault()
    {
        return _leftPane?.Faults.FirstOrDefault()
               ?? _rightPane?.Faults.FirstOrDefault()
               ?? _leftPane?.SyntaxFault
               ?? _rightPane?.SyntaxFault;
    }

    private void ApplyModel(SideBySideDocument? document, IReadOnlyList<DiffWarning> warnings, DiffDiagnostics? diagnostics, DiffOptions? options)
    {
        Document = document;
        Warnings = warnings;
        Diagnostics = diagnostics;
        ChangeCount = document?.Blocks.Count ?? 0;
        // One lookup per result, bound to the options the build ran under, over the live documents.
        WordDiffLookup = document is null || options is null
            ? null
            : new WordDiffLookup(new WordDiffCache(options), LeftDocument, RightDocument, document);
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is null)
            {
                continue;
            }

            pane.DiffDocument = document;
            pane.WordDiffLookup = WordDiffLookup;
        }

        // A new model is a new row space. The option carries over, the runs the reader opened do
        // not: a row number from the old model names a different run in this one.
        _expandedFolds.Clear();
        _projection = RowProjection.Identity(document?.Rows.Count ?? 0);

        if (_gutter is not null)
        {
            _gutter.Document = document;
            _gutter.Projection = _projection;
        }

        if (_minimap is not null)
        {
            _minimap.Document = document;
            _minimap.Projection = _projection;
        }

        // The matches were found over rows the old model defined. What that costs the surface is
        // its own: the editor re-runs an open search against the new row space, the viewer has none.
        _surface.OnModelApplied();

        // The blocks are new: no current change until the user picks one.
        SetCurrentChange(-1, scroll: false);

        // And the runs are new, so they are computed again for this model rather than carried.
        RefreshFolds();
    }

    private void CancelBuild()
    {
        StopSlowTimer();
        if (_buildCts is { } cts)
        {
            _buildCts = null;
            cts.Cancel();
            cts.Dispose();
        }
    }

    private DiffOptions ComposeOptions()
    {
        return new DiffOptions
        {
            IgnoreWhitespace = IgnoreWhitespace,
            IgnoreCase = IgnoreCase,
            WordDiff = WordDiff,
            MaxWordDiffLineLength = MaxWordDiffLineLength,
            ForceAlignment = ForceAlignment,
        };
    }

    private void StartSlowTimer(int generation)
    {
        StopSlowTimer();
        IsBuildingSlowly = false;
        _slowTimer = TimeProvider.CreateTimer(
            _ => Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation && State == DiffViewState.Building)
                {
                    IsBuildingSlowly = true;
                    UpdateStrip();
                }
            }),
            state: null,
            SideBySideDiffView.SlowBuildThreshold,
            Timeout.InfiniteTimeSpan);
    }

    private void StopSlowTimer()
    {
        _slowTimer?.Dispose();
        _slowTimer = null;
        IsBuildingSlowly = false;
    }

    private void SetState(DiffViewState state, string? message)
    {
        SetStateCore(state, message, log: true);
    }

    private void SetStateCore(DiffViewState state, string? message, bool log)
    {
        DiffViewState previous = State;
        bool changed = previous != state || StateMessage != message;
        State = state;
        StateMessage = message;
        if (changed && log)
        {
            DiffViewLog.StateChanged(_buildLogger, previous, state, message);
        }
    }

    private void SetBanner(DiffBannerKind kind, string? message, string? action)
    {
        BannerKind = kind;
        BannerMessage = message;
        BannerActionText = action;
        UpdateBanner();
    }

    private void UpdateBanner()
    {
        if (_bannerAction is not null)
        {
            _bannerAction.Content = BannerActionText;
        }
    }

    /// <summary>
    /// The two parts whose visibility this control owns outright. Called from the template
    /// path and from the property-change path, because a host that sets a flag in XAML is
    /// wired by the first and never reaches the second — the gap plan 00004 fixed for
    /// <c>CanCopyOut</c>. The banner is not here: its visibility belongs to a style, and a
    /// write from code would outrank that style permanently.
    /// </summary>
    private void ApplyChromeVisibility()
    {
        if (_headersGrid is not null)
        {
            _headersGrid.IsVisible = ShowHeaders;
        }

        if (_statusStrip is not null)
        {
            _statusStrip.IsVisible = ShowStatusStrip;
        }
    }

    private void UpdatePseudoClasses()
    {
        // ShowBanner joins the class that already owns the banner's visibility rather than
        // writing IsVisible on the part: a code write lands at LocalValue, outranks the style
        // for the life of the control, and would strand an empty banner on screen the next
        // time a build had nothing to say. So :banner-none now means "not shown", which is
        // wider than "nothing to say" — the only writer is this method.
        _surface.PseudoClasses.Set(":banner-none", BannerKind == DiffBannerKind.None || !ShowBanner);
        _surface.PseudoClasses.Set(":banner-error", BannerKind == DiffBannerKind.Error);
        _surface.PseudoClasses.Set(":banner-degraded", BannerKind == DiffBannerKind.TooDifferentToAlign);
        _surface.PseudoClasses.Set(":banner-identical", BannerKind == DiffBannerKind.Identical);
        _surface.PseudoClasses.Set(":empty", State == DiffViewState.Empty);
        _surface.PseudoClasses.Set(":building", State == DiffViewState.Building);
        _surface.PseudoClasses.Set(":ready", State == DiffViewState.Ready);
        _surface.PseudoClasses.Set(":degraded", State == DiffViewState.Degraded);
        _surface.PseudoClasses.Set(":failed", State == DiffViewState.Failed);
    }

    private void RefreshStrings()
    {
        Control.SetCurrentValue(SideBySideDiffView.LeftPaneNameProperty, DiffViewStrings.Get(DiffViewStrings.LeftPaneName));
        Control.SetCurrentValue(SideBySideDiffView.RightPaneNameProperty, DiffViewStrings.Get(DiffViewStrings.RightPaneName));
        Control.SetCurrentValue(SideBySideDiffView.StatusStripNameProperty, DiffViewStrings.Get(DiffViewStrings.StatusStripName));
        Control.SetCurrentValue(SideBySideDiffView.GutterNameProperty, DiffViewStrings.Get(DiffViewStrings.ConnectorGutterName));
        Control.SetCurrentValue(SideBySideDiffView.MinimapNameProperty, DiffViewStrings.Get(DiffViewStrings.MinimapName));
        Control.SetCurrentValue(SideBySideDiffView.LeftHeaderNameProperty, DiffViewStrings.Get(DiffViewStrings.LeftHeaderName));
        Control.SetCurrentValue(SideBySideDiffView.RightHeaderNameProperty, DiffViewStrings.Get(DiffViewStrings.RightHeaderName));
    }

    internal void UpdateHeaders()
    {
        UpdateHeader(_leftHeader, DiffSide.Left, LeftSource, _leftInfo);
        UpdateHeader(_rightHeader, DiffSide.Right, RightSource, _rightInfo);
    }

    private void UpdateHeader(DiffPaneHeader? header, DiffSide side, PaneSource? source, TextInfo? info)
    {
        if (header is null)
        {
            return;
        }

        header.IsPaneFocused = FocusedSide == side;
        header.IsDirty = _surface.IsDirty(side);
        header.DirtyMarker = header.IsDirty ? DiffViewStrings.Get(DiffViewStrings.HeaderDirty) : null;
        header.Title = source?.Title
                       ?? (source?.Path is { } path ? Path.GetFileName(path) : null)
                       ?? DiffViewStrings.Get(side == DiffSide.Left ? DiffViewStrings.LeftTitle : DiffViewStrings.RightTitle);

        if (source is null || info is null)
        {
            header.Detail = DiffViewStrings.Get(DiffViewStrings.NoContent);
            header.Badge = null;
            header.BadgeKind = StatusKind.None;
            return;
        }

        header.Detail = DiffViewStrings.Format(
            DiffViewStrings.HeaderDetail,
            info.LineCount == 1 ? DiffViewStrings.Get(DiffViewStrings.LineCountOne) : DiffViewStrings.Format(DiffViewStrings.LineCount, info.LineCount.ToString("N0", CultureInfo.CurrentCulture)),
            info.Encoding?.WebName.ToUpperInvariant() ?? DiffViewStrings.Get(DiffViewStrings.EncodingText),
            LineEndingText(info.LineEnding),
            DiffViewStrings.Format(DiffViewStrings.CharCount, info.Length.ToString("N0", CultureInfo.CurrentCulture)));

        if (info.IsBinary)
        {
            header.Badge = DiffViewStrings.Get(DiffViewStrings.BadgeBinary);
            header.BadgeKind = StatusKind.Failure;
        }
        else if (info.Length == 0)
        {
            header.Badge = DiffViewStrings.Get(DiffViewStrings.BadgeEmpty);
            header.BadgeKind = StatusKind.Warning;
        }
        else if (Diagnostics is { Identical: true } && !IsStale)
        {
            header.Badge = DiffViewStrings.Get(DiffViewStrings.BadgeIdentical);
            header.BadgeKind = StatusKind.Success;
        }
        else
        {
            header.Badge = null;
            header.BadgeKind = StatusKind.None;
        }
    }

    private static string LineEndingText(LineEnding ending)
    {
        return DiffViewStrings.Get(ending switch
        {
            LineEnding.Lf => DiffViewStrings.LineEndingLf,
            LineEnding.CrLf => DiffViewStrings.LineEndingCrLf,
            LineEnding.Cr => DiffViewStrings.LineEndingCr,
            LineEnding.Mixed => DiffViewStrings.LineEndingMixed,
            _ => DiffViewStrings.LineEndingNone,
        });
    }

    /// <summary>Which sides hold unsaved edits, named as their headers name them; null for none.</summary>
    private string? DirtySidesText()
    {
        bool left = _surface.IsDirty(DiffSide.Left);
        bool right = _surface.IsDirty(DiffSide.Right);
        if (!left && !right)
        {
            return null;
        }

        string names = left && right
            ? HeaderTitle(DiffSide.Left, LeftSource) + ", " + HeaderTitle(DiffSide.Right, RightSource)
            : HeaderTitle(left ? DiffSide.Left : DiffSide.Right, left ? LeftSource : RightSource);
        return DiffViewStrings.Format(DiffViewStrings.StatusDirty, names);
    }

    internal void UpdateStrip()
    {
        DiffStatusStrip? strip = _statusStrip;
        if (strip is null)
        {
            return;
        }

        strip.State = State;
        strip.StateText = DiffViewStrings.Get(State switch
        {
            DiffViewState.Empty => DiffViewStrings.StateEmpty,
            DiffViewState.Building => DiffViewStrings.StateBuilding,
            DiffViewState.Ready => DiffViewStrings.StateReady,
            DiffViewState.Degraded => DiffViewStrings.StateDegraded,
            _ => DiffViewStrings.StateFailed,
        });
        strip.DirtyText = DirtySidesText();
        strip.IsStale = IsStale;
        strip.StaleText = DiffViewStrings.Get(DiffViewStrings.StatusStale);
        strip.IsBuildingSlowly = IsBuildingSlowly;
        strip.ProgressName = DiffViewStrings.Get(DiffViewStrings.StatusProgressName);
        strip.DismissText = DiffViewStrings.Get(DiffViewStrings.StatusDismiss);

        if (Diagnostics is { } diagnostics)
        {
            strip.CountsText = DiffViewStrings.Format(DiffViewStrings.StatusCounts, diagnostics.Inserted, diagnostics.Deleted, diagnostics.Modified);
            strip.ChangesText = CurrentChangeIndex >= 0
                ? DiffViewStrings.Format(DiffViewStrings.StatusChangeOf, (CurrentChangeIndex + 1).ToString("N0", CultureInfo.CurrentCulture), ChangeCount.ToString("N0", CultureInfo.CurrentCulture))
                : ChangeCount switch
                {
                    0 => DiffViewStrings.Get(DiffViewStrings.StatusNoChanges),
                    1 => DiffViewStrings.Get(DiffViewStrings.StatusChangeOne),
                    _ => DiffViewStrings.Format(DiffViewStrings.StatusChanges, ChangeCount.ToString("N0", CultureInfo.CurrentCulture)),
                };
            strip.BuildTimeText = DiffViewStrings.Format(DiffViewStrings.StatusBuildTime, diagnostics.BuildTime.TotalMilliseconds.ToString("F0", CultureInfo.CurrentCulture));
        }
        else
        {
            strip.CountsText = null;
            strip.ChangesText = null;
            strip.BuildTimeText = null;
        }

        List<string> options = [];
        if (IgnoreWhitespace)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionIgnoreWhitespace));
        }

        if (IgnoreCase)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionIgnoreCase));
        }

        if (WordDiff == WordDiffMode.Off)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionWordDiffOff));
        }
        else if (WordDiff == WordDiffMode.Character)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionWordDiffCharacter));
        }

        if (ForceAlignment)
        {
            options.Add(DiffViewStrings.Get(DiffViewStrings.OptionForceAlignment));
        }

        strip.OptionsText = options.Count == 0 ? null : string.Join(" · ", options);
        strip.FindText = _surface.FindStripText();
        strip.CaretText = FocusedSide is null ? null : DiffViewStrings.Format(DiffViewStrings.StatusCaret, CaretLine, CaretColumn);

        // The field, never the lazy getter. A refresh must not build a controller for a view
        // that has left the tree — that is what re-armed the timer the detach had released.
        // With no controller the getter would have built one reading exactly these defaults.
        StatusController? status = _status;
        strip.TransientText = status?.Text;
        strip.TransientKind = status?.Kind ?? StatusKind.None;
        strip.IsTransientDismissible = status?.IsDismissible ?? false;
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        UpdateStrip();
    }

    private void OnDismissRequested(object? sender, EventArgs e)
    {
        Status.Dismiss();
    }

    private void OnBannerActionClicked(object? sender, RoutedEventArgs e)
    {
        switch (BannerKind)
        {
            case DiffBannerKind.Error:
                Retry();
                break;
            case DiffBannerKind.TooDifferentToAlign:
                ForceAlign();
                break;
            default:
                break;
        }
    }

    private void AttachPane(DiffPanePresenter? pane, DiffSide side)
    {
        if (pane is null)
        {
            return;
        }

        pane.Side = side;
        pane.Document = side == DiffSide.Left ? LeftDocument : RightDocument;
        pane.DiffDocument = Document;
        pane.WordDiffLookup = WordDiffLookup;
        pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled;
        // The logger first: assigning the file name may install a grammar, which logs.
        pane.Logger = _renderLogger;
        pane.UseSyntaxHighlighting = UseSyntaxHighlighting;
        pane.SyntaxFileName = SyntaxFileNameOf(side == DiffSide.Left ? LeftSource : RightSource);
        ApplyDisplayOptions(pane);
        ApplyPaneFont(pane);
        // The left bar is hidden and the right one reflects both: after priming the extents are equal.
        pane.VerticalScrollBarVisibility = side == DiffSide.Left ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        pane.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        pane.RenderFault += OnPaneRenderFault;
        pane.FoldExpandRequested += OnFoldExpandRequested;
        pane.TemplateApplied += OnPaneTemplateApplied;
        pane.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        pane.TextArea.GotFocus += OnPaneGotFocus;
        pane.TextArea.LostFocus += OnPaneLostFocus;

        // Read-only, the copy arrows, the modified-line marks and the two menu hooks are the
        // editor's answer: the viewer has no verb any of them would offer.
        _surface.OnPaneAttached(pane, side);
    }

    /// <summary>Runs <paramref name="action"/> over whichever panes the template has produced.</summary>
    private void ForEachPane(Action<DiffPanePresenter> action)
    {
        if (_leftPane is not null)
        {
            action(_leftPane);
        }

        if (_rightPane is not null)
        {
            action(_rightPane);
        }
    }

    private void ApplyDisplayOptions(DiffPanePresenter pane)
    {
        pane.ShowWhitespace = ShowWhitespace;
        pane.ShowLineEndings = ShowLineEndings;
        pane.TabWidth = TabWidth;
    }

    /// <summary>
    /// The font, when this control names one: an unset size or family leaves the pane's own
    /// theme in charge, so the local value is cleared rather than overwritten with a default.
    /// </summary>
    private void ApplyPaneFont(DiffPanePresenter pane)
    {
        if (double.IsNaN(PaneFontSize))
        {
            pane.ClearValue(TemplatedControl.FontSizeProperty);
        }
        else
        {
            pane.FontSize = PaneFontSize;
        }

        if (PaneFontFamily is { } family)
        {
            pane.FontFamily = family;
        }
        else
        {
            pane.ClearValue(TemplatedControl.FontFamilyProperty);
        }
    }

    private void DetachParts()
    {
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is null)
            {
                continue;
            }

            pane.RenderFault -= OnPaneRenderFault;
            pane.TemplateApplied -= OnPaneTemplateApplied;
            pane.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            pane.TextArea.GotFocus -= OnPaneGotFocus;
            pane.TextArea.LostFocus -= OnPaneLostFocus;
        }

        if (_statusStrip is not null)
        {
            _statusStrip.DismissRequested -= OnDismissRequested;
        }

        if (_bannerAction is not null)
        {
            _bannerAction.Click -= OnBannerActionClicked;
        }

        if (_gutter is not null)
        {
            _gutter.BlockClicked -= OnGutterBlockClicked;
            _gutter.ResizeDragged -= OnGutterResizeDragged;
        }

        if (_minimap is not null)
        {
            _minimap.JumpRequested -= OnMinimapJumpRequested;
        }

        _surface.OnPartsDetaching();

        _sync?.Dispose();
        _sync = null;
    }

    private void OnPaneTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        TryWireScrollSync();
    }

    private void TryWireScrollSync()
    {
        if (_sync is not null || _leftPane?.PaneScrollViewer is not { } left || _rightPane?.PaneScrollViewer is not { } right)
        {
            return;
        }

        _sync = new ScrollSync(left, right) { SyncHorizontal = SyncHorizontalScroll };
        left.ScrollChanged += OnPaneScrollChanged;
        right.ScrollChanged += OnPaneScrollChanged;
        UpdateHorizontalScrollBars();
    }

    private void OnPaneScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta != default || e.ViewportDelta != default)
        {
            UpdateHorizontalScrollBars();
        }

        UpdateOverview();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        UpdateOverview();
    }

    /// <summary>Feeds the gutter and the minimap the panes' row geometry and scroll position.</summary>
    private void UpdateOverview()
    {
        if (_leftPane is null)
        {
            return;
        }

        double lineHeight = _leftPane.TextArea.TextView.DefaultLineHeight;
        if (lineHeight <= 0)
        {
            return;
        }

        if (_gutter is not null)
        {
            _gutter.RowHeight = lineHeight;
            _gutter.VerticalOffset = _leftPane.VerticalOffset;
            _gutter.ContentOffset = _leftPane.TextArea.TextView.TranslatePoint(new Point(0, 0), _gutter)?.Y ?? 0;
        }

        if (_minimap is not null)
        {
            _minimap.ViewportStartRow = _leftPane.VerticalOffset / lineHeight;
            _minimap.ViewportRowCount = _leftPane.ViewportHeight / lineHeight;
        }
    }

    /// <summary>
    /// Folds the model's unchanged runs, keeping <paramref name="contextRows"/> rows either side
    /// of every change; <c>null</c> unfolds everything. Returns the projection now in force.
    /// </summary>
    internal RowProjection ApplyFolds(int? contextRows, int minimumFoldedRows = FoldPlan.DefaultMinimumFoldedRows)
    {
        _foldContextRows = contextRows;
        _foldMinimumRows = minimumFoldedRows;
        return RefreshFolds();
    }

    /// <summary>
    /// Recomputes the folds for the current model and the runs the reader has expanded, and
    /// applies them to the panes and the two surfaces outside them.
    /// </summary>
    private RowProjection RefreshFolds()
    {
        SideBySideDocument? document = Document;
        IReadOnlyList<FoldedRun> planned = document is null || _foldContextRows is null
            ? []
            : [.. FoldPlan.For(document, _foldContextRows.Value, _foldMinimumRows)
                          .Where(run => !_expandedFolds.Contains(run.FirstRow))];

        _projection = RowProjection.Of(document?.Rows.Count ?? 0, planned);

        // The line ranges come from the projection's own folds rather than from the plan. A run
        // the projection declined is a run neither pane may collapse, and taking them from two
        // different lists is how the panes would come to disagree.
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is null)
            {
                continue;
            }

            List<(int First, int Last)> ranges = [];
            if (document is not null)
            {
                for (int fold = 0; fold < _projection.FoldCount; fold++)
                {
                    if (FoldPlan.LinesOf(document, _projection.FoldAt(fold), pane.Side) is { } range)
                    {
                        ranges.Add(range);
                    }
                }
            }

            pane.SetCollapsedLines(ranges);
        }

        if (_gutter is not null)
        {
            _gutter.Projection = _projection;
        }

        if (_minimap is not null)
        {
            _minimap.Projection = _projection;
        }

        UpdateOverview();
        _surface.OnFoldsChanged();
        return _projection;
    }

    /// <summary>
    /// The run the caret is on, which is the only run a keyboard can name: a row behind a
    /// placeholder is not on screen and the caret cannot reach it, so what the caret can be on is
    /// the placeholder's own line.
    /// </summary>
    private FoldedRun? FoldAtCaret()
    {
        // A caret is in exactly one pane; where nothing is focused, the left is the side whose
        // line numbers the strip and the key map already speak of first.
        DiffSide side = FocusedSide ?? DiffSide.Left;
        if (Document is not { } document || Pane(side) is not { } pane)
        {
            return null;
        }

        int caretLine = pane.TextArea.Caret.Line;
        for (int fold = 0; fold < _projection.FoldCount; fold++)
        {
            FoldedRun run = _projection.FoldAt(fold);
            if (FoldPlan.LinesOf(document, run, side) is { } lines && lines.First - 1 == caretLine)
            {
                return run;
            }
        }

        return null;
    }

    internal bool CanExpandFoldAtCaret() => FoldAtCaret() is not null;

    /// <summary>
    /// Opens the run hiding <paramref name="modelRow"/>, if one is. Find walks to matches the
    /// model has, and a match the reader is being taken to has to be a match they can see — the
    /// alternative is a count that means something different once anything is folded.
    /// </summary>
    internal void RevealRow(int modelRow)
    {
        // A match on a placeholder's own row is already on screen, so nothing needs opening.
        if (!_projection.IsHidden(modelRow))
        {
            return;
        }

        ExpandFoldContaining(modelRow);
    }

    /// <summary>
    /// Opens the run <paramref name="modelRow"/> belongs to, the placeholder's own row included —
    /// which is the row a reader points at when they ask for the rows hidden <em>here</em>.
    /// </summary>
    internal void ExpandFoldContaining(int modelRow)
    {
        int fold = _projection.FoldContaining(modelRow);
        if (fold < 0)
        {
            return;
        }

        _expandedFolds.Add(_projection.FoldAt(fold).FirstRow);
        RefreshFolds();
    }

    internal void ExpandFoldAtCaret()
    {
        if (FoldAtCaret() is { } run)
        {
            _expandedFolds.Add(run.FirstRow);
            RefreshFolds();
        }
    }

    /// <summary>
    /// A placeholder was clicked. The pane reports a line on its own side; which run that is is a
    /// row range, so the fold is found by asking each taken fold what it collapses on that side.
    /// </summary>
    private void OnFoldExpandRequested(object? sender, int firstCollapsedLine)
    {
        if (sender is not DiffPanePresenter pane || Document is not { } document)
        {
            return;
        }

        for (int fold = 0; fold < _projection.FoldCount; fold++)
        {
            FoldedRun run = _projection.FoldAt(fold);
            if (FoldPlan.LinesOf(document, run, pane.Side) is { } lines && lines.First == firstCollapsedLine)
            {
                _expandedFolds.Add(run.FirstRow);
                RefreshFolds();
                return;
            }
        }
    }

    private void OnGutterBlockClicked(object? sender, int blockIndex)
    {
        SetCurrentChange(blockIndex, scroll: true);
    }

    private void OnGutterResizeDragged(object? sender, double delta)
    {
        if (_leftPane is null || _rightPane is null)
        {
            return;
        }

        double panes = _leftPane.Bounds.Width + _rightPane.Bounds.Width;
        if (panes <= 0)
        {
            return;
        }

        SplitRatio = (_leftPane.Bounds.Width + delta) / panes;
    }

    /// <summary>
    /// Moves the map between the panes grid's two <c>Auto</c> slots and tells it which of its own
    /// edges now faces the panes. The empty slot takes no width, so the arrangement it is not in
    /// costs nothing.
    /// </summary>
    private void ApplyMinimapPlacement()
    {
        if (_minimap is null)
        {
            return;
        }

        bool onLeft = MinimapPlacement == MinimapPlacement.Left;
        Grid.SetColumn(_minimap, onLeft ? LeftMinimapColumn : RightMinimapColumn);
        _minimap.MirrorEdges = onLeft;

        // The header row has a slot at each end too, and the one over the map has to be exactly
        // as wide as the map is — or the header stops lining up with the pane under it, which is
        // the failure §6 has warned about since the gutter first moved.
        double reserved = ShowMinimap ? DiffMinimap.MapWidth : 0;
        if (_headerLeftSpacer is not null)
        {
            _headerLeftSpacer.Width = onLeft ? reserved : 0;
        }

        if (_headerRightSpacer is not null)
        {
            _headerRightSpacer.Width = onLeft ? 0 : reserved;
        }
    }

    private void OnMinimapJumpRequested(object? sender, int row)
    {
        ScrollToRow(row);
    }

    private void ApplySplit()
    {
        foreach (Grid? grid in new[] { _headersGrid, _panesGrid })
        {
            if (grid is null || grid.ColumnDefinitions.Count <= RightPaneColumn)
            {
                continue;
            }

            grid.ColumnDefinitions[LeftPaneColumn].Width = new GridLength(SplitRatio, GridUnitType.Star);
            grid.ColumnDefinitions[RightPaneColumn].Width = new GridLength(1 - SplitRatio, GridUnitType.Star);
        }
    }

    internal void SetCurrentChange(int index, bool scroll)
    {
        int clamped = ChangeCount == 0 ? -1 : Math.Clamp(index, -1, ChangeCount - 1);
        _surface.SetAndRaise(SideBySideDiffView.CurrentChangeIndexProperty, ref _currentChangeIndex, clamped);
        ChangeBlock? block = clamped < 0 || Document is null ? null : Document.Blocks[clamped];
        foreach (DiffPanePresenter? pane in new[] { _leftPane, _rightPane })
        {
            if (pane is not null)
            {
                pane.CurrentBlock = block;
            }
        }

        if (_gutter is not null)
        {
            _gutter.CurrentChangeIndex = clamped;
        }

        if (_minimap is not null)
        {
            _minimap.CurrentChangeIndex = clamped;
        }

        if (scroll && block is not null)
        {
            ScrollToRows(block.FirstRow, block.RowCount);
        }

        UpdateStrip();
        _surface.OnChangeSetMoved();
    }

    /// <summary>
    /// Scrolls both panes so the rows sit at the centre of the viewport. Every <i>visible</i> row
    /// is one line height once primed, so the rows are projected before they are multiplied.
    /// </summary>
    internal void ScrollToRows(int firstRow, int rowCount)
    {
        if (_leftPane?.PaneScrollViewer is not { } viewer)
        {
            return;
        }

        double lineHeight = _leftPane.TextArea.TextView.DefaultLineHeight;
        double viewport = viewer.Viewport.Height;
        double extent = viewer.Extent.Height;
        int firstVisible = _projection.VisibleRowOf(firstRow);
        int endVisible = _projection.VisibleRowOf(Math.Max(firstRow, firstRow + rowCount - 1)) + 1;
        double top = firstVisible * lineHeight;
        double height = Math.Max(0, endVisible - firstVisible) * lineHeight;
        double target = top - Math.Max(0, (viewport - height) / 2);
        target = Math.Clamp(target, 0, Math.Max(0, extent - viewport));
        viewer.Offset = new Vector(viewer.Offset.X, target);
    }

    /// <summary>
    /// Both panes show a horizontal bar, or neither: a bar takes height from its viewport, and
    /// the panes must keep equal viewports.
    /// </summary>
    private void UpdateHorizontalScrollBars()
    {
        if (_leftPane is null || _rightPane is null)
        {
            return;
        }

        const double slack = 0.5;
        bool needed = _leftPane.ExtentWidth > _leftPane.ViewportWidth + slack
                      || _rightPane.ExtentWidth > _rightPane.ViewportWidth + slack;
        ScrollBarVisibility visibility = needed ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
        if (_leftPane.HorizontalScrollBarVisibility != visibility)
        {
            _leftPane.HorizontalScrollBarVisibility = visibility;
        }

        if (_rightPane.HorizontalScrollBarVisibility != visibility)
        {
            _rightPane.HorizontalScrollBarVisibility = visibility;
        }
    }

    private void OnPaneRenderFault(object? sender, RenderFaultEventArgs e)
    {
        // The pane has logged it under the Render category; here it becomes state.
        if (State is DiffViewState.Ready or DiffViewState.Degraded)
        {
            SetState(DiffViewState.Degraded, e.Message);
        }

        Status.SetFailure(e.Message);
        UpdateStrip();
        _surface.RaiseRenderFault(e);
    }

    private void OnPaneGotFocus(object? sender, RoutedEventArgs e)
    {
        FocusedSide = ReferenceEquals(sender, _leftPane?.TextArea) ? DiffSide.Left : DiffSide.Right;
        UpdateCaret();
    }

    private void OnPaneLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((FocusedSide == DiffSide.Left && ReferenceEquals(sender, _leftPane?.TextArea))
            || (FocusedSide == DiffSide.Right && ReferenceEquals(sender, _rightPane?.TextArea)))
        {
            FocusedSide = null;
            UpdateCaret();
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        UpdateCaret();
    }

    private void UpdateCaret()
    {
        DiffPanePresenter? pane = FocusedSide is { } side ? Pane(side) : null;
        if (pane is null)
        {
            CaretLine = 0;
            CaretColumn = 0;
        }
        else
        {
            CaretLine = pane.TextArea.Caret.Line;
            CaretColumn = pane.TextArea.Caret.Column;
        }

        // Which pane has focus is a header state as well as a caret: the accent says so where the
        // caret cannot, having been scrolled away.
        if (_leftHeader is not null)
        {
            _leftHeader.IsPaneFocused = FocusedSide == DiffSide.Left;
        }

        if (_rightHeader is not null)
        {
            _rightHeader.IsPaneFocused = FocusedSide == DiffSide.Right;
        }

        UpdateStrip();
    }

    // ── The state that moved, still notified on the control ────────────────────────────────

    // Storage here, notification there: a DirectProperty is registered against a control's own
    // type and a sibling's is not in this one's registry, so the field is the controller's and
    // SetAndRaise is re-exposed rather than reimplemented.

    /// <summary>The one state the surface is in.</summary>
    internal DiffViewState State
    {
        get => _state;
        private set => _surface.SetAndRaise(SideBySideDiffView.StateProperty, ref _state, value);
    }

    /// <summary>What the state is about, or <c>null</c>.</summary>
    internal string? StateMessage
    {
        get => _stateMessage;
        private set => _surface.SetAndRaise(SideBySideDiffView.StateMessageProperty, ref _stateMessage, value);
    }

    /// <summary>The model the panes are showing.</summary>
    internal SideBySideDocument? Document
    {
        get => _document;
        private set => _surface.SetAndRaise(SideBySideDiffView.DocumentProperty, ref _document, value);
    }

    /// <summary>The last build's measurements.</summary>
    internal DiffDiagnostics? Diagnostics
    {
        get => _diagnostics;
        private set => _surface.SetAndRaise(SideBySideDiffView.DiagnosticsProperty, ref _diagnostics, value);
    }

    /// <summary>What the last build had to say.</summary>
    internal IReadOnlyList<DiffWarning> Warnings
    {
        get => _warnings;
        private set => _surface.SetAndRaise(SideBySideDiffView.WarningsProperty, ref _warnings, value);
    }

    /// <summary>Change blocks in the model.</summary>
    internal int ChangeCount
    {
        get => _changeCount;
        private set => _surface.SetAndRaise(SideBySideDiffView.ChangeCountProperty, ref _changeCount, value);
    }

    /// <summary>Which banner is showing, if any.</summary>
    internal DiffBannerKind BannerKind
    {
        get => _bannerKind;
        private set => _surface.SetAndRaise(SideBySideDiffView.BannerKindProperty, ref _bannerKind, value);
    }

    /// <summary>The banner's text.</summary>
    internal string? BannerMessage
    {
        get => _bannerMessage;
        private set => _surface.SetAndRaise(SideBySideDiffView.BannerMessageProperty, ref _bannerMessage, value);
    }

    /// <summary>The banner button's label.</summary>
    internal string? BannerActionText
    {
        get => _bannerActionText;
        private set => _surface.SetAndRaise(SideBySideDiffView.BannerActionTextProperty, ref _bannerActionText, value);
    }

    /// <summary>Whether the model on screen predates the sources.</summary>
    internal bool IsStale
    {
        get => _isStale;
        private set => _surface.SetAndRaise(SideBySideDiffView.IsStaleProperty, ref _isStale, value);
    }

    /// <summary>Whether the build has passed the slow threshold.</summary>
    internal bool IsBuildingSlowly
    {
        get => _isBuildingSlowly;
        private set => _surface.SetAndRaise(SideBySideDiffView.IsBuildingSlowlyProperty, ref _isBuildingSlowly, value);
    }

    /// <summary>Which pane holds focus.</summary>
    internal DiffSide? FocusedSide
    {
        get => _focusedSide;
        private set => _surface.SetAndRaise(SideBySideDiffView.FocusedSideProperty, ref _focusedSide, value);
    }

    /// <summary>The focused pane's caret line.</summary>
    internal int CaretLine
    {
        get => _caretLine;
        private set => _surface.SetAndRaise(SideBySideDiffView.CaretLineProperty, ref _caretLine, value);
    }

    /// <summary>The focused pane's caret column.</summary>
    internal int CaretColumn
    {
        get => _caretColumn;
        private set => _surface.SetAndRaise(SideBySideDiffView.CaretColumnProperty, ref _caretColumn, value);
    }

    // ── The styled properties the shared code reads ────────────────────────────────────────

    // Read back rather than owned: a StyledProperty's value lives in the property system, where
    // a host's binding, style or setter can reach it. AddOwner returns the same instance for
    // these, so one static serves both siblings.

    internal PaneSource? LeftSource => Control.GetValue(SideBySideDiffView.LeftSourceProperty);

    internal PaneSource? RightSource => Control.GetValue(SideBySideDiffView.RightSourceProperty);

    internal bool IgnoreWhitespace => Control.GetValue(SideBySideDiffView.IgnoreWhitespaceProperty);

    internal bool IgnoreCase => Control.GetValue(SideBySideDiffView.IgnoreCaseProperty);

    internal WordDiffMode WordDiff => Control.GetValue(SideBySideDiffView.WordDiffProperty);

    internal int MaxWordDiffLineLength => Control.GetValue(SideBySideDiffView.MaxWordDiffLineLengthProperty);

    internal bool ForceAlignment => Control.GetValue(SideBySideDiffView.ForceAlignmentProperty);

    internal bool SyncHorizontalScroll => Control.GetValue(SideBySideDiffView.SyncHorizontalScrollProperty);

    internal bool IsCaretBlinkEnabled => Control.GetValue(SideBySideDiffView.IsCaretBlinkEnabledProperty);

    internal bool UseSyntaxHighlighting => Control.GetValue(SideBySideDiffView.UseSyntaxHighlightingProperty);

    internal int? UnchangedContextRows => Control.GetValue(SideBySideDiffView.UnchangedContextRowsProperty);

    internal bool ShowWhitespace => Control.GetValue(SideBySideDiffView.ShowWhitespaceProperty);

    internal bool ShowLineEndings => Control.GetValue(SideBySideDiffView.ShowLineEndingsProperty);

    internal int TabWidth => Control.GetValue(SideBySideDiffView.TabWidthProperty);

    internal double PaneFontSize => Control.GetValue(SideBySideDiffView.PaneFontSizeProperty);

    internal FontFamily? PaneFontFamily => Control.GetValue(SideBySideDiffView.PaneFontFamilyProperty);

    internal bool ShowMinimap => Control.GetValue(SideBySideDiffView.ShowMinimapProperty);

    internal bool ShowHeaders => Control.GetValue(SideBySideDiffView.ShowHeadersProperty);

    internal bool ShowStatusStrip => Control.GetValue(SideBySideDiffView.ShowStatusStripProperty);

    internal bool ShowBanner => Control.GetValue(SideBySideDiffView.ShowBannerProperty);

    internal MinimapPlacement MinimapPlacement => Control.GetValue(SideBySideDiffView.MinimapPlacementProperty);

    // ── The two documents ──────────────────────────────────────────────────────────────────

    // The storage is the controller's; the notification is the surface's, because the editor
    // publishes these and the viewer keeps them internal — the one place the two genuinely differ
    // about a property rather than about a behaviour.

    /// <summary>The left pane's document.</summary>
    internal TextDocument LeftDocument
    {
        get => _leftDocument;
        private set
        {
            TextDocument previous = _leftDocument;
            if (ReferenceEquals(previous, value))
            {
                return;
            }

            _leftDocument = value;
            _surface.OnDocumentReplaced(DiffSide.Left, previous, value);
        }
    }

    /// <summary>The right pane's document.</summary>
    internal TextDocument RightDocument
    {
        get => _rightDocument;
        private set
        {
            TextDocument previous = _rightDocument;
            if (ReferenceEquals(previous, value))
            {
                return;
            }

            _rightDocument = value;
            _surface.OnDocumentReplaced(DiffSide.Right, previous, value);
        }
    }

    // ── The two properties with logic of their own ─────────────────────────────────────────

    /// <summary>Where the split sits, clamped; setting it re-lays the shared columns.</summary>
    internal double SplitRatio
    {
        get => _splitRatio;
        set
        {
            double clamped = Math.Clamp(value, 0.1, 0.9);
            if (_surface.SetAndRaise(SideBySideDiffView.SplitRatioProperty, ref _splitRatio, clamped))
            {
                ApplySplit();
            }
        }
    }

    /// <summary>The block the surface is on, -1 for none. Moved through <see cref="SetCurrentChange"/>.</summary>
    internal int CurrentChangeIndex => _currentChangeIndex;

    // ── What the surface owns publicly and this holds ──────────────────────────────────────

    /// <summary>The clock behind the transient messages' auto-clear and the slow-build threshold.</summary>
    internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// The transient message lane, built on first use. It does not outlive the surface's place in
    /// the visual tree: <see cref="OnDetached"/> releases it and the next access builds a fresh one.
    /// </summary>
    internal StatusController Status
    {
        get
        {
            if (_status is null)
            {
                _status = new StatusController(TimeProvider);
                _status.Changed += OnStatusChanged;
            }

            return _status;
        }
    }

    // The controller as it stands, without building one: the Status getter is lazy, so asking it
    // whether the field was released would create what it was asked about.
    internal StatusController? StatusOrNull => _status;

    /// <summary>The four category loggers, rebuilt when the factory is set.</summary>
    internal ILoggerFactory? LoggerFactory
    {
        get => _loggerFactory;
        set
        {
            _loggerFactory = value;
            _buildLogger = value?.CreateLogger(DiffViewLogCategories.Build);
            _renderLogger = value?.CreateLogger(DiffViewLogCategories.Render);
            if (_leftPane is not null)
            {
                _leftPane.Logger = _renderLogger;
            }

            if (_rightPane is not null)
            {
                _rightPane.Logger = _renderLogger;
            }
        }
    }

    /// <summary>The build routine; tests replace it to make a build slow or throw.</summary>
    internal Func<PaneSource, PaneSource, DiffOptions, CancellationToken, DiffBuildResult> Builder { get; set; } =
        static (left, right, options, token) => DiffDocumentBuilder.Build(left, right, token, options);

    /// <summary>The in-flight build, completing when its outcome has been applied or discarded.</summary>
    internal Task? CurrentBuild { get; private set; }

    /// <summary>The word-level lookup of the current model, bound to the options its build ran under.</summary>
    internal WordDiffLookup? WordDiffLookup { get; private set; }

    /// <summary>How the model's rows sit on screen once folding has had its say.</summary>
    internal RowProjection Projection => _projection;

    /// <summary>What the left side was probed to be; the surface's save path amends it.</summary>
    internal TextInfo? LeftInfo
    {
        get => _leftInfo;
        set => _leftInfo = value;
    }

    /// <summary>What the right side was probed to be.</summary>
    internal TextInfo? RightInfo
    {
        get => _rightInfo;
        set => _rightInfo = value;
    }

    internal ILogger? BuildLogger => _buildLogger;

    internal ILogger? RenderLogger => _renderLogger;

    // ── The template's parts ───────────────────────────────────────────────────────────────

    internal DiffPanePresenter? LeftPane => _leftPane;

    internal DiffPanePresenter? RightPane => _rightPane;

    internal DiffPaneHeader? LeftHeader => _leftHeader;

    internal DiffPaneHeader? RightHeader => _rightHeader;

    internal DiffStatusStrip? StatusStrip => _statusStrip;

    internal Button? BannerAction => _bannerAction;

    internal Border? Banner => _banner;

    internal Grid? HeadersGrid => _headersGrid;

    internal Grid? PanesGrid => _panesGrid;

    internal ChangeConnectorGutter? Gutter => _gutter;

    internal DiffMinimap? Minimap => _minimap;

    internal ScrollSync? Sync => _sync;

    /// <summary>Whichever pane the template produced for <paramref name="side"/>.</summary>
    internal DiffPanePresenter? Pane(DiffSide side) => side == DiffSide.Left ? _leftPane : _rightPane;

    // ── The shared verbs ───────────────────────────────────────────────────────────────────

    /// <summary>Runs the build again with the current sources and options.</summary>
    internal void Retry()
    {
        RequestBuild(keepModel: true);
    }

    /// <summary>Sets <c>ForceAlignment</c> from the too-different banner.</summary>
    internal void ForceAlign()
    {
        Control.SetCurrentValue(SideBySideDiffView.ForceAlignmentProperty, true);
    }

    /// <summary>Brings <paramref name="row"/> into view in both panes.</summary>
    internal void ScrollToRow(int row)
    {
        ScrollToRows(row, 1);
    }

    /// <summary>Moves to the next change; at the last one it stays and the strip says so.</summary>
    internal void NextChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        if (CurrentChangeIndex >= ChangeCount - 1)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoNext));
            return;
        }

        SetCurrentChange(CurrentChangeIndex + 1, scroll: true);
    }

    /// <summary>Moves to the previous change; at the first one, or before any, it stays and the strip says so.</summary>
    internal void PreviousChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        if (CurrentChangeIndex <= 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoPrevious));
            return;
        }

        SetCurrentChange(CurrentChangeIndex - 1, scroll: true);
    }

    /// <summary>Moves to the first change.</summary>
    internal void FirstChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        SetCurrentChange(0, scroll: true);
    }

    /// <summary>Moves to the last change.</summary>
    internal void LastChange()
    {
        if (ChangeCount == 0)
        {
            Status.SetWarning(DiffViewStrings.Get(DiffViewStrings.NavigationNoChanges));
            return;
        }

        SetCurrentChange(ChangeCount - 1, scroll: true);
    }

    /// <summary>Makes block <paramref name="index"/> the current change and scrolls to it; an index the model does not have does nothing.</summary>
    internal void GoToChange(int index)
    {
        if (Document is { } model && index >= 0 && index < model.Blocks.Count)
        {
            SetCurrentChange(index, scroll: true);
        }
    }

    /// <summary>
    /// Selects block <paramref name="index"/>'s lines on <paramref name="side"/>, or on both
    /// sides where it is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// A side the block has no lines on — the near half of an insertion or a deletion — has its
    /// selection cleared rather than left standing. After "select this change" a selection
    /// elsewhere would be describing a different change, and the copy arrows read the selection.
    /// </remarks>
    internal void SelectChange(int index, DiffSide? side)
    {
        if (Document is not { } model || index < 0 || index >= model.Blocks.Count)
        {
            return;
        }

        ChangeBlock block = model.Blocks[index];
        foreach (DiffSide each in (DiffSide[])[DiffSide.Left, DiffSide.Right])
        {
            if (side is { } only && only != each)
            {
                continue;
            }

            SelectLines(each, block.LinesFor(each));
        }
    }

    /// <summary>
    /// Selects whole lines <paramref name="lines"/> — the model's 0-based counting — on
    /// <paramref name="side"/>, clearing the selection for an empty range.
    /// </summary>
    private void SelectLines(DiffSide side, LineRange lines)
    {
        if (Pane(side) is not { } pane || pane.Document is not { } document)
        {
            return;
        }

        // The model can be ahead of a document an edit has shortened, so the range is clamped
        // rather than trusted; a range entirely past the end reads as empty.
        int first = lines.Start + 1;
        int last = Math.Min(lines.End, document.LineCount);
        if (lines.IsEmpty || first > last)
        {
            pane.TextArea.ClearSelection();
            return;
        }

        int start = document.GetLineByNumber(first).Offset;
        pane.Select(start, document.GetLineByNumber(last).EndOffset - start);
    }

    /// <summary>Rebuilds now from the panes' live text, whatever the debounce was doing.</summary>
    internal void ReDiff(bool keepModel)
    {
        RequestBuild(keepModel);
    }

    // ── The surface's lifecycle, in the order the control sees it ──────────────────────────

    /// <summary>
    /// The shared half of the surface's constructor. Called once the surface can answer, because
    /// the state machine's first transition already reads through the seam.
    /// </summary>
    internal void Initialize()
    {
        Control.LayoutUpdated += OnLayoutUpdated;
        RefreshStrings();
        UpdatePseudoClasses();
        SetStateCore(DiffViewState.Empty, DiffViewStrings.Get(DiffViewStrings.StateEmptyMessage), log: false);
    }

    /// <summary>
    /// Finds the shared parts, wires what the controller owns, and hands the surface its turn at
    /// <see cref="IDiffSurface.OnPartsAttached"/> — before the chrome is refreshed, because the
    /// editor's find bar has to be wired by the time its lane is written.
    /// </summary>
    internal void ApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();

        _leftPane = e.NameScope.Find<DiffPanePresenter>(SideBySideDiffView.LeftPanePart);
        _rightPane = e.NameScope.Find<DiffPanePresenter>(SideBySideDiffView.RightPanePart);
        _leftHeader = e.NameScope.Find<DiffPaneHeader>(SideBySideDiffView.LeftHeaderPart);
        _rightHeader = e.NameScope.Find<DiffPaneHeader>(SideBySideDiffView.RightHeaderPart);
        _statusStrip = e.NameScope.Find<DiffStatusStrip>(SideBySideDiffView.StatusStripPart);
        _bannerAction = e.NameScope.Find<Button>(SideBySideDiffView.BannerActionPart);
        _banner = e.NameScope.Find<Border>(SideBySideDiffView.BannerPart);
        _headersGrid = e.NameScope.Find<Grid>(SideBySideDiffView.HeadersPart);
        _headerLeftSpacer = e.NameScope.Find<Border>(SideBySideDiffView.HeaderLeftSpacerPart);
        _headerRightSpacer = e.NameScope.Find<Border>(SideBySideDiffView.HeaderRightSpacerPart);
        _panesGrid = e.NameScope.Find<Grid>(SideBySideDiffView.PanesPart);
        _gutter = e.NameScope.Find<ChangeConnectorGutter>(SideBySideDiffView.GutterPart);
        _minimap = e.NameScope.Find<DiffMinimap>(SideBySideDiffView.MinimapPart);

        ApplyChromeVisibility();

        AttachPane(_leftPane, DiffSide.Left);
        AttachPane(_rightPane, DiffSide.Right);
        if (_statusStrip is not null)
        {
            _statusStrip.DismissRequested += OnDismissRequested;
        }

        if (_bannerAction is not null)
        {
            _bannerAction.Click += OnBannerActionClicked;
        }

        if (_gutter is not null)
        {
            // Both paths, because a host that sets the option in XAML is wired here and never
            // reaches the property-change handler — the gap plan 00004 had to fix for CanCopyOut.
            _foldContextRows = UnchangedContextRows;
            _gutter.Document = Document;
            _gutter.Projection = _projection;
            _gutter.CurrentChangeIndex = CurrentChangeIndex;
            _gutter.BlockClicked += OnGutterBlockClicked;
            _gutter.ResizeDragged += OnGutterResizeDragged;
        }

        if (_minimap is not null)
        {
            _minimap.Document = Document;
            _minimap.Projection = _projection;
            _minimap.CurrentChangeIndex = CurrentChangeIndex;
            // Both paths, because a host that sets the flag in XAML is wired here and never
            // reaches the property-change handler — the gap plan 00004 had to fix for CanCopyOut.
            _minimap.IsVisible = ShowMinimap;
            ApplyMinimapPlacement();
            _minimap.JumpRequested += OnMinimapJumpRequested;
        }

        _surface.OnPartsAttached(e);

        ApplySplit();
        TryWireScrollSync();
        UpdateHeaders();
        UpdateStrip();
        UpdateBanner();
        UpdateOverview();
    }

    /// <summary>
    /// The surface has left the visual tree. The order is load-bearing: <c>Dispose</c> raises
    /// <c>Changed</c> synchronously into <see cref="UpdateStrip"/>, which reads the field, so the
    /// field must still hold the controller being disposed. Nulled first, these two lines release
    /// one controller and immediately build another.
    /// </summary>
    internal void OnDetached()
    {
        _status?.Dispose();
        _status = null;
    }

    /// <summary>
    /// The property changes the shared half answers. The surface handles its own first and passes
    /// the rest here; a property neither of them claims falls through untouched.
    /// </summary>
    internal void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SideBySideDiffView.LeftSourceProperty)
        {
            OnSourceChanged(DiffSide.Left, change.GetNewValue<PaneSource?>());
        }
        else if (change.Property == SideBySideDiffView.RightSourceProperty)
        {
            OnSourceChanged(DiffSide.Right, change.GetNewValue<PaneSource?>());
        }
        else if (change.Property == SideBySideDiffView.IgnoreWhitespaceProperty
                 || change.Property == SideBySideDiffView.IgnoreCaseProperty
                 || change.Property == SideBySideDiffView.WordDiffProperty
                 || change.Property == SideBySideDiffView.MaxWordDiffLineLengthProperty
                 || change.Property == SideBySideDiffView.ForceAlignmentProperty)
        {
            RequestBuild(keepModel: true);
        }
        else if (change.Property == SideBySideDiffView.SyncHorizontalScrollProperty && _sync is not null)
        {
            _sync.SyncHorizontal = SyncHorizontalScroll;
            _sync.Align();
        }
        else if (change.Property == SideBySideDiffView.IsCaretBlinkEnabledProperty)
        {
            ForEachPane(pane => pane.IsCaretBlinkEnabled = IsCaretBlinkEnabled);
        }
        else if (change.Property == SideBySideDiffView.UseSyntaxHighlightingProperty)
        {
            ForEachPane(pane => pane.UseSyntaxHighlighting = UseSyntaxHighlighting);
        }
        else if (change.Property == SideBySideDiffView.ShowMinimapProperty)
        {
            if (_minimap is not null)
            {
                _minimap.IsVisible = ShowMinimap;
            }

            ApplyMinimapPlacement();
        }
        else if (change.Property == SideBySideDiffView.MinimapPlacementProperty)
        {
            ApplyMinimapPlacement();
        }
        else if (change.Property == SideBySideDiffView.ShowWhitespaceProperty
                 || change.Property == SideBySideDiffView.ShowLineEndingsProperty
                 || change.Property == SideBySideDiffView.TabWidthProperty)
        {
            ForEachPane(ApplyDisplayOptions);
        }
        else if (change.Property == SideBySideDiffView.UnchangedContextRowsProperty)
        {
            // A change of option opens every run the reader had opened: they were opened against
            // a different set of folds, and keeping them would leave gaps the option did not ask
            // for.
            _expandedFolds.Clear();
            ApplyFolds(UnchangedContextRows);
        }
        else if (change.Property == SideBySideDiffView.PaneFontSizeProperty
                 || change.Property == SideBySideDiffView.PaneFontFamilyProperty)
        {
            ForEachPane(ApplyPaneFont);
        }
        else if (change.Property == SideBySideDiffView.ShowHeadersProperty
                 || change.Property == SideBySideDiffView.ShowStatusStripProperty)
        {
            ApplyChromeVisibility();
        }
        else if (change.Property == SideBySideDiffView.StateProperty
                 || change.Property == SideBySideDiffView.BannerKindProperty
                 || change.Property == SideBySideDiffView.ShowBannerProperty)
        {
            UpdatePseudoClasses();
        }
    }
}
