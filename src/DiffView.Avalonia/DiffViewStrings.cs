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

    /// <summary>What the copy arrow standing in for a line number would do.</summary>
    public const string CopyArrowTooltip = "CopyArrow.Tooltip";

    /// <summary>What the selection's arrow, standing in for a line number, would do.</summary>
    public const string SelectionArrowTooltip = "SelectionArrow.Tooltip";

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

    /// <summary>The pane menu's navigation entries.</summary>
    public const string MenuNextChange = "Menu.NextChange";

    /// <summary>The pane menu's navigation entries.</summary>
    public const string MenuPreviousChange = "Menu.PreviousChange";

    /// <summary>The pane menu's find entry.</summary>
    public const string MenuFind = "Menu.Find";

    /// <summary>The pane menu's save entry: <c>{0}</c> this side.</summary>
    public const string MenuSave = "Menu.Save";

    /// <summary>The pane menu's revert entry: <c>{0}</c> this side.</summary>
    public const string MenuRevert = "Menu.Revert";

    /// <summary>The left side, in a tooltip.</summary>
    public const string SideLeft = "Side.Left";

    /// <summary>The right side, in a tooltip.</summary>
    public const string SideRight = "Side.Right";

    /// <summary>A line-number tooltip with a counterpart: <c>{0}</c> line, <c>{1}</c> other side, <c>{2}</c> other line.</summary>
    public const string LineTooltipAligned = "LineTooltip.Aligned";

    /// <summary>A line-number tooltip without a counterpart: <c>{0}</c> line, <c>{1}</c> other side.</summary>
    public const string LineTooltipAlone = "LineTooltip.Alone";

    /// <summary>A unified line-number tooltip with a counterpart: <c>{0}</c> this side, <c>{1}</c> this line, <c>{2}</c> other side, <c>{3}</c> other line.</summary>
    public const string LineTooltipUnifiedAligned = "LineTooltip.Unified.Aligned";

    /// <summary>A unified line-number tooltip without a counterpart: <c>{0}</c> this side, <c>{1}</c> this line, <c>{2}</c> other side.</summary>
    public const string LineTooltipUnifiedAlone = "LineTooltip.Unified.Alone";

    /// <summary>A marker or connector tooltip: <c>{0}</c> change (1-based), <c>{1}</c> total, <c>{2}</c> the block's counts.</summary>
    public const string MarkerTooltip = "Marker.Tooltip";

    /// <summary>A minimap tooltip: <c>{0}</c> row (1-based), <c>{1}</c> rows, <c>{2}</c> kind.</summary>
    public const string MinimapTooltip = "Minimap.Tooltip";

    /// <summary>The overview's tooltip inside one side's lane, which names that side.</summary>
    public const string MinimapLaneTooltip = "Minimap.LaneTooltip";

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
        [CopyArrowTooltip] = "Copy this change to the {0} side",
        [SelectionArrowTooltip] = "Copy the selected lines to the {0} side",
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
        [MenuNextChange] = "Next change",
        [MenuPreviousChange] = "Previous change",
        [MenuFind] = "Find…",
        [MenuSave] = "Save the {0} side",
        [MenuRevert] = "Revert the {0} side",
        [SideLeft] = "left",
        [SideRight] = "right",
        [LineTooltipAligned] = "Line {0} · {1} line {2}",
        [LineTooltipAlone] = "Line {0} · no {1} line",
        [LineTooltipUnifiedAligned] = "Line {1} on the {0} · line {3} on the {2}",
        [LineTooltipUnifiedAlone] = "Line {1} on the {0} · no {2} line",
        [MarkerTooltip] = "Change {0} of {1} · {2}",
        [MinimapTooltip] = "Row {0} of {1} · {2}",
        [MinimapLaneTooltip] = "Row {0} of {1} · {2} · {3} side",
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
    public static string SideName(Core.DiffSide side)
    {
        return Get(side == Core.DiffSide.Left ? SideLeft : SideRight);
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
