using System.Globalization;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Every user-visible string of the library, behind a swappable <see cref="Resolver"/>. The
/// resolver receives a key and returns the localised text, or <c>null</c> to fall back to the
/// English default. Keys are the constants on this class; the placeholders each takes are in
/// its summary.
/// </summary>
public static class DiffViewStrings
{
    /// <summary>Automation name of the composite control.</summary>
    public const string DiffViewName = "DiffView.Name";

    /// <summary>Automation name of the left pane.</summary>
    public const string LeftPaneName = "Pane.Left.Name";

    /// <summary>Automation name of the right pane.</summary>
    public const string RightPaneName = "Pane.Right.Name";

    /// <summary>Automation name of the unified view's one pane.</summary>
    public const string UnifiedPaneName = "Pane.Unified.Name";

    /// <summary>Automation name of the line-number margin.</summary>
    public const string LineNumbersMarginName = "LineNumbersMargin.Name";

    /// <summary>Automation name of the change-marker margin.</summary>
    public const string ChangeMarkersMarginName = "ChangeMarkersMargin.Name";

    /// <summary>Automation name of the status strip.</summary>
    public const string StatusStripName = "StatusStrip.Name";

    /// <summary>A decorator failed and is disabled: <c>{0}</c> decorator, <c>{1}</c> exception message.</summary>
    public const string RenderFault = "RenderFault";

    /// <summary>A decorator failed on a line and is disabled: <c>{0}</c> decorator, <c>{1}</c> line, <c>{2}</c> exception message.</summary>
    public const string RenderFaultOnLine = "RenderFault.OnLine";

    /// <summary>A decorator failed over something that is not a line — a grammar — and is disabled: <c>{0}</c> decorator, <c>{1}</c> subject, <c>{2}</c> exception message.</summary>
    public const string RenderFaultOnSubject = "RenderFault.OnSubject";

    /// <summary>The left header's title when the source has none.</summary>
    public const string LeftTitle = "Header.Left.Title";

    /// <summary>The right header's title when the source has none.</summary>
    public const string RightTitle = "Header.Right.Title";

    /// <summary>A header with no source.</summary>
    public const string NoContent = "Header.NoContent";

    /// <summary>The header detail line: <c>{0}</c> lines, <c>{1}</c> encoding, <c>{2}</c> line endings, <c>{3}</c> size.</summary>
    public const string HeaderDetail = "Header.Detail";

    /// <summary><c>{0}</c> lines.</summary>
    public const string LineCount = "Header.Lines";

    /// <summary>Exactly one line.</summary>
    public const string LineCountOne = "Header.Lines.One";

    /// <summary><c>{0}</c> characters.</summary>
    public const string CharCount = "Header.Chars";

    /// <summary>The encoding when the source came from text rather than bytes.</summary>
    public const string EncodingText = "Header.Encoding.Text";

    /// <summary>LF line endings.</summary>
    public const string LineEndingLf = "Header.LineEnding.Lf";

    /// <summary>CRLF line endings.</summary>
    public const string LineEndingCrLf = "Header.LineEnding.CrLf";

    /// <summary>CR line endings.</summary>
    public const string LineEndingCr = "Header.LineEnding.Cr";

    /// <summary>Mixed line endings.</summary>
    public const string LineEndingMixed = "Header.LineEnding.Mixed";

    /// <summary>A single line with no terminator.</summary>
    public const string LineEndingNone = "Header.LineEnding.None";

    /// <summary>The header badge of a binary side.</summary>
    public const string BadgeBinary = "Header.Badge.Binary";

    /// <summary>The header badge of an empty side.</summary>
    public const string BadgeEmpty = "Header.Badge.Empty";

    /// <summary>The header badge when the sides are identical.</summary>
    public const string BadgeIdentical = "Header.Badge.Identical";

    /// <summary>The <see cref="DiffViewState.Empty"/> pill.</summary>
    public const string StateEmpty = "State.Empty";

    /// <summary>The <see cref="DiffViewState.Building"/> pill.</summary>
    public const string StateBuilding = "State.Building";

    /// <summary>The <see cref="DiffViewState.Ready"/> pill.</summary>
    public const string StateReady = "State.Ready";

    /// <summary>The <see cref="DiffViewState.Degraded"/> pill.</summary>
    public const string StateDegraded = "State.Degraded";

    /// <summary>The <see cref="DiffViewState.Failed"/> pill.</summary>
    public const string StateFailed = "State.Failed";

    /// <summary>The message of the empty state.</summary>
    public const string StateEmptyMessage = "State.Empty.Message";

    /// <summary>The marker on a pane whose edits are not on disk.</summary>
    public const string HeaderDirty = "Header.Dirty";

    /// <summary>Appended to a marker tooltip on a line edited since the source was assigned.</summary>
    public const string MarkerModifiedSinceLoad = "Marker.ModifiedSinceLoad";

    /// <summary>What the copy arrow standing in for a line number would do, copying leftwards.</summary>
    public const string CopyArrowTooltipLeft = "CopyArrow.Tooltip.Left";

    /// <summary>The same, copying rightwards.</summary>
    public const string CopyArrowTooltipRight = "CopyArrow.Tooltip.Right";

    /// <summary>What the selection's arrow, standing in for a line number, would do, leftwards.</summary>
    public const string SelectionArrowTooltipLeft = "SelectionArrow.Tooltip.Left";

    /// <summary>The same, rightwards.</summary>
    public const string SelectionArrowTooltipRight = "SelectionArrow.Tooltip.Right";

    /// <summary>The strip's lane while a side has unsaved edits.</summary>
    public const string StatusDirty = "Status.Dirty";

    /// <summary>Reported when a save succeeds.</summary>
    public const string SaveSucceeded = "Save.Succeeded";

    /// <summary>Reported when a save is asked for on a side that came from no file.</summary>
    public const string SaveNoPath = "Save.NoPath";

    /// <summary>Reported when the file changed on disk since it was read.</summary>
    public const string SaveChangedOnDisk = "Save.ChangedOnDisk";

    /// <summary>Reported when the write itself failed.</summary>
    public const string SaveFailed = "Save.Failed";

    /// <summary>The marker on a result a newer build is about to replace.</summary>
    public const string StatusStale = "Status.Stale";

    /// <summary>The row counts: <c>{0}</c> inserted, <c>{1}</c> deleted, <c>{2}</c> modified.</summary>
    public const string StatusCounts = "Status.Counts";

    /// <summary><c>{0}</c> change blocks.</summary>
    public const string StatusChanges = "Status.Changes";

    /// <summary>Exactly one change block.</summary>
    public const string StatusChangeOne = "Status.Changes.One";

    /// <summary>No change blocks.</summary>
    public const string StatusNoChanges = "Status.Changes.None";

    /// <summary>The caret position: <c>{0}</c> line, <c>{1}</c> column.</summary>
    public const string StatusCaret = "Status.Caret";

    /// <summary>The build time: <c>{0}</c> milliseconds.</summary>
    public const string StatusBuildTime = "Status.BuildTime";

    /// <summary>The option label when whitespace is ignored.</summary>
    public const string OptionIgnoreWhitespace = "Option.IgnoreWhitespace";

    /// <summary>The option label when case is ignored.</summary>
    public const string OptionIgnoreCase = "Option.IgnoreCase";

    /// <summary>The option label when word-level highlighting is off.</summary>
    public const string OptionWordDiffOff = "Option.WordDiff.Off";

    /// <summary>The option label when word-level highlighting is per character.</summary>
    public const string OptionWordDiffCharacter = "Option.WordDiff.Character";

    /// <summary>The option label when alignment is forced.</summary>
    public const string OptionForceAlignment = "Option.ForceAlignment";

    /// <summary>The Retry action on the error banner.</summary>
    public const string BannerRetry = "Banner.Retry";

    /// <summary>The Force action on the too-different banner.</summary>
    public const string BannerForce = "Banner.Force";

    /// <summary>The identical banner.</summary>
    public const string BannerIdentical = "Banner.Identical";

    /// <summary>The dismiss control of a failure message.</summary>
    public const string StatusDismiss = "Status.Dismiss";

    /// <summary>Automation name of the progress indicator shown while a build runs long.</summary>
    public const string StatusProgressName = "Status.Progress.Name";

    /// <summary>The success message after a build: <c>{0}</c> rows, <c>{1}</c> milliseconds.</summary>
    public const string BuildCompleted = "Build.Completed";

    /// <summary>The success message after a build that found no difference.</summary>
    public const string BuildIdentical = "Build.Identical";

    /// <summary>The active message while a build runs.</summary>
    public const string BuildRunning = "Build.Running";

    /// <summary>The marker tooltip on a modified row whose line is too long for word-level pieces: <c>{0}</c> the limit.</summary>
    public const string WordDiffSkipped = "WordDiff.Skipped";

    /// <summary>The change count with a current change: <c>{0}</c> current (1-based), <c>{1}</c> total.</summary>
    public const string StatusChangeOf = "Status.Changes.Of";

    /// <summary>Next change at the last block.</summary>
    public const string NavigationNoNext = "Navigation.NoNext";

    /// <summary>Previous change at the first block.</summary>
    public const string NavigationNoPrevious = "Navigation.NoPrevious";

    /// <summary>Navigation with no changes.</summary>
    public const string NavigationNoChanges = "Navigation.NoChanges";

    // The pane menu. Every directional entry is a **whole sentence per direction** rather than one
    // sentence with the side substituted in: a translator cannot inflect a word dropped into
    // someone else's sentence, and several languages need a different form of "left" inside a
    // prepositional phrase than standing alone. Shorter than the gutter's tooltips too — a tooltip
    // can afford the words, a menu row sits beside its accelerator — and a host that wants the
    // long wording back resolves these keys to it.

    /// <summary>The pane menu's selection copy, leftwards.</summary>
    public const string MenuCopySelectionLeft = "Menu.CopySelection.Left";

    /// <summary>The pane menu's selection copy, rightwards.</summary>
    public const string MenuCopySelectionRight = "Menu.CopySelection.Right";

    /// <summary>The pane menu's block copy, leftwards.</summary>
    public const string MenuCopyChangeLeft = "Menu.CopyChange.Left";

    /// <summary>The pane menu's block copy, rightwards.</summary>
    public const string MenuCopyChangeRight = "Menu.CopyChange.Right";

    /// <summary>The pane menu's navigation entries.</summary>
    public const string MenuNextChange = "Menu.NextChange";

    /// <summary>The pane menu's navigation entries.</summary>
    public const string MenuPreviousChange = "Menu.PreviousChange";

    /// <summary>The pane menu's find entry.</summary>
    public const string MenuFind = "Menu.Find";

    /// <summary>Menu entry: fold nothing.</summary>
    public const string MenuShowAllRows = "Menu.ShowAllRows";

    /// <summary>Menu entry: fold every unchanged run.</summary>
    public const string MenuShowDifferencesOnly = "Menu.ShowDifferencesOnly";

    /// <summary>Menu entry: fold the unchanged runs but keep a few rows around each change.</summary>
    public const string MenuShowContext = "Menu.ShowContext";

    /// <summary>Menu entry: give back the run under the pointer or the caret.</summary>
    public const string MenuExpandFold = "Menu.ExpandFold";

    /// <summary>The pane menu's save entry, for the left side.</summary>
    public const string MenuSaveLeft = "Menu.Save.Left";

    /// <summary>The pane menu's save entry, for the right side.</summary>
    public const string MenuSaveRight = "Menu.Save.Right";

    /// <summary>The pane menu's revert entry, for the left side.</summary>
    public const string MenuRevertLeft = "Menu.Revert.Left";

    /// <summary>The pane menu's revert entry, for the right side.</summary>
    public const string MenuRevertRight = "Menu.Revert.Right";

    /// <summary>Make the change under the pointer the current one — the connector's left-click, named.</summary>
    public const string MenuGoToChange = "Menu.GoToChange";

    /// <summary>Select the lines of the change under the pointer.</summary>
    public const string MenuSelectChange = "Menu.SelectChange";

    /// <summary>Scroll to the row under the pointer — the overview map's left-click, named.</summary>
    public const string MenuGoToRow = "Menu.GoToRow";

    /// <summary>The overview map's own entry for taking itself off screen.</summary>
    public const string MenuHideOverviewMap = "Menu.HideOverviewMap";

    /// <summary>The left side, in a tooltip.</summary>
    public const string SideLeft = "Side.Left";

    /// <summary>The right side, in a tooltip.</summary>
    public const string SideRight = "Side.Right";

    /// <summary>A line-number tooltip whose counterpart is on the left: <c>{0}</c> line, <c>{1}</c> that line.</summary>
    public const string LineTooltipAlignedLeft = "LineTooltip.Aligned.Left";

    /// <summary>The same, counterpart on the right.</summary>
    public const string LineTooltipAlignedRight = "LineTooltip.Aligned.Right";

    /// <summary>A line-number tooltip with no counterpart on the left: <c>{0}</c> line.</summary>
    public const string LineTooltipAloneLeft = "LineTooltip.Alone.Left";

    /// <summary>The same, none on the right.</summary>
    public const string LineTooltipAloneRight = "LineTooltip.Alone.Right";

    /// <summary>A unified tooltip for a left line with a counterpart: <c>{0}</c> this line, <c>{1}</c> the right line.</summary>
    public const string LineTooltipUnifiedAlignedLeft = "LineTooltip.Unified.Aligned.Left";

    /// <summary>The same for a right line: <c>{0}</c> this line, <c>{1}</c> the left line.</summary>
    public const string LineTooltipUnifiedAlignedRight = "LineTooltip.Unified.Aligned.Right";

    /// <summary>A unified tooltip for a left line with no counterpart: <c>{0}</c> this line.</summary>
    public const string LineTooltipUnifiedAloneLeft = "LineTooltip.Unified.Alone.Left";

    /// <summary>The same for a right line.</summary>
    public const string LineTooltipUnifiedAloneRight = "LineTooltip.Unified.Alone.Right";

    /// <summary>A marker or connector tooltip: <c>{0}</c> change (1-based), <c>{1}</c> total, <c>{2}</c> the block's counts.</summary>
    public const string MarkerTooltip = "Marker.Tooltip";

    /// <summary>The placeholder standing for a folded run: <c>{0}</c> rows hidden behind it.</summary>
    public const string FoldPlaceholder = "Fold.Placeholder";

    /// <summary>The same where exactly one row is hidden, which no plural rule covers for free.</summary>
    public const string FoldPlaceholderOne = "Fold.Placeholder.One";

    /// <summary>A minimap tooltip: <c>{0}</c> row (1-based), <c>{1}</c> rows, <c>{2}</c> kind.</summary>
    public const string MinimapTooltip = "Minimap.Tooltip";

    /// <summary>The overview's tooltip inside the left lane: <c>{0}</c> row, <c>{1}</c> rows, <c>{2}</c> kind.</summary>
    public const string MinimapLaneTooltipLeft = "Minimap.LaneTooltip.Left";

    /// <summary>The same inside the right lane.</summary>
    public const string MinimapLaneTooltipRight = "Minimap.LaneTooltip.Right";

    /// <summary>Automation name of the minimap.</summary>
    public const string MinimapName = "Minimap.Name";

    /// <summary>Automation name of the connector gutter.</summary>
    public const string ConnectorGutterName = "ConnectorGutter.Name";

    /// <summary>Automation name of the find bar.</summary>
    public const string FindBarName = "Find.Name";

    /// <summary>The query box's watermark.</summary>
    public const string FindQueryPlaceholder = "Find.Query.Placeholder";

    /// <summary>Automation name of the query box.</summary>
    public const string FindQueryName = "Find.Query.Name";

    /// <summary>The Match case toggle's label.</summary>
    public const string FindMatchCase = "Find.MatchCase";

    /// <summary>The Match case toggle's tooltip and automation name.</summary>
    public const string FindMatchCaseName = "Find.MatchCase.Name";

    /// <summary>The Whole word toggle's label.</summary>
    public const string FindWholeWord = "Find.WholeWord";

    /// <summary>The Whole word toggle's tooltip and automation name.</summary>
    public const string FindWholeWordName = "Find.WholeWord.Name";

    /// <summary>The regular-expression toggle's label.</summary>
    public const string FindRegex = "Find.Regex";

    /// <summary>The regular-expression toggle's tooltip and automation name.</summary>
    public const string FindRegexName = "Find.Regex.Name";

    /// <summary>The Changed rows only toggle's label.</summary>
    public const string FindChangedRowsOnly = "Find.ChangedRowsOnly";

    /// <summary>The Changed rows only toggle's tooltip and automation name.</summary>
    public const string FindChangedRowsOnlyName = "Find.ChangedRowsOnly.Name";

    /// <summary>The left-scope button's label.</summary>
    public const string FindScopeLeft = "Find.Scope.Left";

    /// <summary>The left-scope button's tooltip and automation name.</summary>
    public const string FindScopeLeftName = "Find.Scope.Left.Name";

    /// <summary>The right-scope button's label.</summary>
    public const string FindScopeRight = "Find.Scope.Right";

    /// <summary>The right-scope button's tooltip and automation name.</summary>
    public const string FindScopeRightName = "Find.Scope.Right.Name";

    /// <summary>The both-scope button's label.</summary>
    public const string FindScopeBoth = "Find.Scope.Both";

    /// <summary>The both-scope button's tooltip and automation name.</summary>
    public const string FindScopeBothName = "Find.Scope.Both.Name";

    /// <summary>The word for both sides, in the status strip.</summary>
    public const string FindScopeBothWord = "Find.Scope.Both.Word";

    /// <summary>The previous-match button's label.</summary>
    public const string FindPrevious = "Find.Previous";

    /// <summary>The previous-match button's tooltip and automation name.</summary>
    public const string FindPreviousName = "Find.Previous.Name";

    /// <summary>The next-match button's label.</summary>
    public const string FindNext = "Find.Next";

    /// <summary>The next-match button's tooltip and automation name.</summary>
    public const string FindNextName = "Find.Next.Name";

    /// <summary>The close button's label.</summary>
    public const string FindClose = "Find.Close";

    /// <summary>The close button's tooltip and automation name.</summary>
    public const string FindCloseName = "Find.Close.Name";

    /// <summary>The count with a current match: <c>{0}</c> current (1-based), <c>{1}</c> total, <c>{2}</c> left, <c>{3}</c> right.</summary>
    public const string FindMatchOf = "Find.MatchOf";

    /// <summary>The count without a current match: <c>{0}</c> total, <c>{1}</c> left, <c>{2}</c> right.</summary>
    public const string FindMatches = "Find.Matches";

    /// <summary>A query that matched nothing.</summary>
    public const string FindNoMatches = "Find.NoMatches";

    /// <summary>The cap was reached: <c>{0}</c> the cap.</summary>
    public const string FindTruncated = "Find.Truncated";

    /// <summary>The strip's find lane: <c>{0}</c> the count, <c>{1}</c> the scope.</summary>
    public const string StatusFind = "Status.Find";

    /// <summary>The strip's find lane with nothing searched for yet: <c>{0}</c> the scope.</summary>
    public const string StatusFindScope = "Status.Find.Scope";

    /// <summary>A search that could not run at all, shown inline in the find bar.</summary>
    public const string FindFailedMessage = "Find.Failed";

    /// <summary>The strip's count with a current match: <c>{0}</c> current (1-based), <c>{1}</c> total.</summary>
    public const string StatusFindMatchOf = "Status.Find.MatchOf";

    /// <summary>The strip's count without a current match: <c>{0}</c> total.</summary>
    public const string StatusFindMatches = "Status.Find.Matches";

    /// <summary>The strip's find lane where there is no scope to name — the unified view: <c>{0}</c> the count.</summary>
    public const string StatusFindNoScope = "Status.Find.NoScope";

    /// <summary>An unchanged row's kind.</summary>
    public const string KindUnchanged = "Kind.Unchanged";

    /// <summary>An inserted row's kind.</summary>
    public const string KindInserted = "Kind.Inserted";

    /// <summary>A deleted row's kind.</summary>
    public const string KindDeleted = "Kind.Deleted";

    /// <summary>A modified row's kind.</summary>
    public const string KindModified = "Kind.Modified";

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [DiffViewName] = "Side-by-side diff",
        [LeftPaneName] = "Left pane",
        [RightPaneName] = "Right pane",
        [UnifiedPaneName] = "Unified pane",
        [LineNumbersMarginName] = "Line numbers",
        [ChangeMarkersMarginName] = "Change markers",
        [StatusStripName] = "Status",
        [RenderFault] = "{0} failed and was disabled: {1}",
        [RenderFaultOnLine] = "{0} failed on line {1} and was disabled: {2}",
        [RenderFaultOnSubject] = "{0} failed for {1} and was disabled: {2}",
        [LeftTitle] = "Left",
        [RightTitle] = "Right",
        [HeaderDirty] = "Unsaved",
        [MarkerModifiedSinceLoad] = "Edited in this session",
        [CopyArrowTooltipLeft] = "Copy this change to the left side",
        [CopyArrowTooltipRight] = "Copy this change to the right side",
        [SelectionArrowTooltipLeft] = "Copy the selected lines to the left side",
        [SelectionArrowTooltipRight] = "Copy the selected lines to the right side",
        [StatusDirty] = "Unsaved changes in {0}",
        [SaveSucceeded] = "Saved {0}",
        [SaveNoPath] = "{0} did not come from a file, so there is nowhere to save it.",
        [SaveChangedOnDisk] = "{0} changed on disk since it was read; nothing was written.",
        [SaveFailed] = "{0} could not be saved: {1}",
        [NoContent] = "No content",
        [HeaderDetail] = "{0} · {1} · {2} · {3}",
        [LineCount] = "{0} lines",
        [LineCountOne] = "1 line",
        [CharCount] = "{0} chars",
        [EncodingText] = "text",
        [LineEndingLf] = "LF",
        [LineEndingCrLf] = "CRLF",
        [LineEndingCr] = "CR",
        [LineEndingMixed] = "mixed line endings",
        [LineEndingNone] = "no line ending",
        [BadgeBinary] = "binary",
        [BadgeEmpty] = "empty",
        [BadgeIdentical] = "identical",
        [StateEmpty] = "Empty",
        [StateBuilding] = "Building…",
        [StateReady] = "Ready",
        [StateDegraded] = "Degraded",
        [StateFailed] = "Failed",
        [StateEmptyMessage] = "Load both sides to compare",
        [StatusStale] = "stale",
        [StatusCounts] = "+{0} −{1} ~{2}",
        [StatusChanges] = "{0} changes",
        [StatusChangeOne] = "1 change",
        [StatusNoChanges] = "no changes",
        [StatusCaret] = "Ln {0}, Col {1}",
        [StatusBuildTime] = "{0} ms",
        [OptionIgnoreWhitespace] = "ignore whitespace",
        [OptionIgnoreCase] = "ignore case",
        [OptionWordDiffOff] = "no word diff",
        [OptionWordDiffCharacter] = "character diff",
        [OptionForceAlignment] = "forced alignment",
        [BannerRetry] = "Retry",
        [BannerForce] = "Force alignment",
        [BannerIdentical] = "Files are identical",
        [StatusDismiss] = "Dismiss",
        [StatusProgressName] = "Building",
        [BuildCompleted] = "Compared {0} rows in {1} ms",
        [BuildIdentical] = "Files are identical",
        [BuildRunning] = "Comparing…",
        [WordDiffSkipped] = "Word-level highlighting skipped: a line is longer than {0} characters",
        [StatusChangeOf] = "change {0} of {1}",
        [NavigationNoNext] = "No next change",
        [NavigationNoPrevious] = "No previous change",
        [NavigationNoChanges] = "No changes to navigate",
        [MenuCopySelectionLeft] = "Copy selection to the left",
        [MenuCopySelectionRight] = "Copy selection to the right",
        [MenuCopyChangeLeft] = "Copy change to the left",
        [MenuCopyChangeRight] = "Copy change to the right",
        [MenuNextChange] = "Next change",
        [MenuPreviousChange] = "Previous change",
        [MenuFind] = "Find…",
        [MenuShowAllRows] = "Show all rows",
        [MenuShowDifferencesOnly] = "Show differences only",
        [MenuShowContext] = "Show differences with context",
        [MenuExpandFold] = "Show the rows hidden here",
        [MenuSaveLeft] = "Save the left side",
        [MenuSaveRight] = "Save the right side",
        [MenuRevertLeft] = "Revert the left side",
        [MenuRevertRight] = "Revert the right side",
        // "Change" and "row" rather than "block": the menu says change everywhere else, and the
        // map's own tooltip has said "Row n of m" since plan 00007. A menu that named the model's
        // vocabulary would be the only place in the library that did.
        [MenuGoToChange] = "Go to this change",
        [MenuSelectChange] = "Select this change",
        [MenuGoToRow] = "Go to this row",
        [MenuHideOverviewMap] = "Hide the overview map",
        [SideLeft] = "left",
        [SideRight] = "right",
        [LineTooltipAlignedLeft] = "Line {0} · left line {1}",
        [LineTooltipAlignedRight] = "Line {0} · right line {1}",
        [LineTooltipAloneLeft] = "Line {0} · no left line",
        [LineTooltipAloneRight] = "Line {0} · no right line",
        [LineTooltipUnifiedAlignedLeft] = "Line {0} on the left · line {1} on the right",
        [LineTooltipUnifiedAlignedRight] = "Line {0} on the right · line {1} on the left",
        [LineTooltipUnifiedAloneLeft] = "Line {0} on the left · no right line",
        [LineTooltipUnifiedAloneRight] = "Line {0} on the right · no left line",
        [MarkerTooltip] = "Change {0} of {1} · {2}",
        // A leading space as well as the outline: the placeholder shares its line with that
        // line's own text, and the two should not run together even where nothing is drawn.
        [FoldPlaceholder] = " ⋯ {0} matching rows hidden ",
        [FoldPlaceholderOne] = " ⋯ 1 matching row hidden ",
        [MinimapTooltip] = "Row {0} of {1} · {2}",
        [MinimapLaneTooltipLeft] = "Row {0} of {1} · {2} · left side",
        [MinimapLaneTooltipRight] = "Row {0} of {1} · {2} · right side",
        [MinimapName] = "Overview",
        [ConnectorGutterName] = "Change connectors",
        [FindBarName] = "Find",
        [FindQueryPlaceholder] = "Find",
        [FindQueryName] = "Find what",
        [FindMatchCase] = "Aa",
        [FindMatchCaseName] = "Match case",
        [FindWholeWord] = "Word",
        [FindWholeWordName] = "Whole word",
        [FindRegex] = ".*",
        [FindRegexName] = "Regular expression",
        [FindChangedRowsOnly] = "Changed",
        [FindChangedRowsOnlyName] = "Changed rows only",
        [FindScopeLeft] = "L",
        [FindScopeLeftName] = "Search the left pane",
        [FindScopeRight] = "R",
        [FindScopeRightName] = "Search the right pane",
        [FindScopeBoth] = "Both",
        [FindScopeBothName] = "Search both panes",
        [FindScopeBothWord] = "both",
        [FindPrevious] = "Prev",
        [FindPreviousName] = "Previous match",
        [FindNext] = "Next",
        [FindNextName] = "Next match",
        [FindClose] = "×",
        [FindCloseName] = "Close the find bar",
        [FindMatchOf] = "match {0} of {1} (L {2} · R {3})",
        [FindMatches] = "{0} matches (L {1} · R {2})",
        [FindNoMatches] = "no matches",
        [FindTruncated] = "Showing the first {0} matches",
        [FindFailedMessage] = "The search could not run.",
        [StatusFind] = "find {0} · {1}",
        [StatusFindScope] = "find · {0}",
        [StatusFindMatchOf] = "{0} of {1}",
        [StatusFindMatches] = "{0} matches",
        [StatusFindNoScope] = "find {0}",
        [KindUnchanged] = "unchanged",
        [KindInserted] = "inserted",
        [KindDeleted] = "deleted",
        [KindModified] = "modified",
    };

    /// <summary>The name of <paramref name="kind"/> for a tooltip.</summary>
    public static string KindName(Core.DiffLineKind kind)
    {
        return Get(kind switch
        {
            Core.DiffLineKind.Inserted => KindInserted,
            Core.DiffLineKind.Deleted => KindDeleted,
            Core.DiffLineKind.Modified => KindModified,
            _ => KindUnchanged,
        });
    }

    /// <summary>The name of <paramref name="side"/> for a tooltip.</summary>
    /// <remarks>
    /// **Not for building a sentence out of.** Every string of this library that names a side is a
    /// whole sentence per direction, because a translator cannot inflect a word dropped into
    /// someone else's sentence — German needs <em>linke Zeile</em> beside <em>nach links</em>, and
    /// pasting one bare word cannot produce both. This is here for a host that wants the word on
    /// its own, and `No_string_of_the_library_is_built_by_pasting_a_side_word_into_it` is what
    /// stops it creeping back into ours.
    /// </remarks>
    public static string SideName(Core.DiffSide side)
    {
        return Get(side == Core.DiffSide.Left ? SideLeft : SideRight);
    }

    /// <summary>What the copy arrow would do, worded for the side receiving the change.</summary>
    public static string CopyArrowTooltip(Core.DiffSide toSide)
    {
        return Get(toSide == Core.DiffSide.Left ? CopyArrowTooltipLeft : CopyArrowTooltipRight);
    }

    /// <summary>What the selection's arrow would do, worded for the side receiving the lines.</summary>
    public static string SelectionArrowTooltip(Core.DiffSide toSide)
    {
        return Get(toSide == Core.DiffSide.Left ? SelectionArrowTooltipLeft : SelectionArrowTooltipRight);
    }

    /// <summary>A line-number tooltip, worded for the side the counterpart is on.</summary>
    public static string LineTooltipAligned(Core.DiffSide otherSide, string line, string otherLine)
    {
        return Format(otherSide == Core.DiffSide.Left ? LineTooltipAlignedLeft : LineTooltipAlignedRight, line, otherLine);
    }

    /// <summary>A line-number tooltip, worded for the side that has no counterpart.</summary>
    public static string LineTooltipAlone(Core.DiffSide otherSide, string line)
    {
        return Format(otherSide == Core.DiffSide.Left ? LineTooltipAloneLeft : LineTooltipAloneRight, line);
    }

    /// <summary>A unified line-number tooltip, worded for the side the line belongs to.</summary>
    public static string LineTooltipUnifiedAligned(Core.DiffSide side, string line, string otherLine)
    {
        return Format(side == Core.DiffSide.Left ? LineTooltipUnifiedAlignedLeft : LineTooltipUnifiedAlignedRight, line, otherLine);
    }

    /// <summary>The same, where the line has no counterpart.</summary>
    public static string LineTooltipUnifiedAlone(Core.DiffSide side, string line)
    {
        return Format(side == Core.DiffSide.Left ? LineTooltipUnifiedAloneLeft : LineTooltipUnifiedAloneRight, line);
    }

    /// <summary>The overview's tooltip inside a lane, worded for the lane's side.</summary>
    public static string MinimapLaneTooltip(Core.DiffSide side, string row, string rows, string kind)
    {
        return Format(side == Core.DiffSide.Left ? MinimapLaneTooltipLeft : MinimapLaneTooltipRight, row, rows, kind);
    }

    /// <summary>
    /// The pane menu's selection copy, worded for the side receiving it. Picking between two whole
    /// sentences rather than formatting one with a side word in it: the word is part of the
    /// sentence, not a runtime value, and a translator needs the whole sentence to inflect.
    /// </summary>
    public static string MenuCopySelection(Core.DiffSide toSide)
    {
        return Get(toSide == Core.DiffSide.Left ? MenuCopySelectionLeft : MenuCopySelectionRight);
    }

    /// <summary>The pane menu's block copy, worded for the side receiving it.</summary>
    public static string MenuCopyChange(Core.DiffSide toSide)
    {
        return Get(toSide == Core.DiffSide.Left ? MenuCopyChangeLeft : MenuCopyChangeRight);
    }

    /// <summary>The pane menu's save entry, worded for the side it saves.</summary>
    public static string MenuSave(Core.DiffSide side)
    {
        return Get(side == Core.DiffSide.Left ? MenuSaveLeft : MenuSaveRight);
    }

    /// <summary>The pane menu's revert entry, worded for the side it reverts.</summary>
    public static string MenuRevert(Core.DiffSide side)
    {
        return Get(side == Core.DiffSide.Left ? MenuRevertLeft : MenuRevertRight);
    }

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
