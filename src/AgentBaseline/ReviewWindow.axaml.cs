using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// The review as the operator drives it: the accumulator and a candidate side by side, and the commands
/// that move through the queue, decide a section and save — plan 00034 step 7, the loop end to end.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The next command is F8, and Shift+F8 the previous one</b>: one key along from the F7 that walks a
/// candidate's changes, so moving through the changes and moving through the candidates sit side by side.
/// Neither the view's key map nor AvaloniaEdit binds F8. The bindings are the window's, and a window is
/// an ancestor of both panes, so they fire while either pane has the keyboard, as the view's own F7 does.
/// At either end the command says so in the window and moves nothing, as F7 stops at the last change.
/// </para>
/// <para>
/// ⭐ <b>Ctrl+N goes to the next section that differs, and Ctrl+P to the previous one</b> — plan 00038, Brian's of
/// 2026-11-19, Beyond Compare's next and previous difference — as do the two section buttons. A press lands on the
/// section's heading in the pane that has it, the right when both do, with the keyboard there and the heading's row
/// at the top of the view, and the mark and the line follow the caret as they follow a click. It counts from the
/// caret of the pane that has the keyboard, or last had it while a button has it, and from the top until a caret
/// moves on the candidate shown; <see cref="WrapSectionWalk"/> says whether a press past an end comes round.
/// <see cref="SectionWalk"/> says which sections differ and in what order.
/// </para>
/// <para>
/// A verb acts on the section at its own pane's caret: take, leave and defer on the candidate's section at
/// the right's, drop on the accumulator's at the left's. Each pane keeps its caret while the keyboard is on
/// a button, so a click reads the caret the operator left. A caret above a pane's first heading is in no
/// section, and the verb says so and does nothing.
/// </para>
/// <para>
/// Every outcome is said in the window, a refusal included: <see cref="DecisionRefusedException"/> and
/// <see cref="SaveRefusedException"/> name what they refuse, and the window shows that rather than
/// dropping it.
/// </para>
/// <para>
/// ⭐ <b>The line under the panes says what the durable store knows about the section at the right pane's
/// caret</b> — <i>not decided yet</i>, <i>already left, from "janusser6"</i>, or an edited take's <i>your
/// edit stands</i> or <i>re-review</i> — so a run that began from a decision file shows which sections are
/// new. It is said as the caret enters a section and not as it moves within one, so a verb's outcome stays
/// until the operator moves on. Saying it is Brian's choice of 2026-10-29: the library folds matching rows
/// and nothing else, so hiding a decided section is not available to this app.
/// </para>
/// <para>
/// ⭐ <b>A save over stale candidates is made twice</b> — Brian's choice of 2026-10-29 for decision 11, which
/// warns rather than refuses. The first writes nothing and says which candidates are stale where; the second,
/// over the same staleness and the same text, opens the dialog and saves, and the provenance lists what was
/// accepted. Anything changed in between is asked about again, and a save with nothing stale is made once.
/// </para>
/// <para>
/// <see cref="Startup.Open"/> starts the review the command line names in this window.
/// </para>
/// </remarks>
internal sealed partial class ReviewWindow : Window
{
    /// <summary>The next command's gesture.</summary>
    internal static readonly KeyGesture NextGesture = new(Key.F8);

    /// <summary>The previous command's gesture.</summary>
    internal static readonly KeyGesture PreviousGesture = new(Key.F8, KeyModifiers.Shift);

    /// <summary>The next section command's gesture: Beyond Compare's next difference.</summary>
    internal static readonly KeyGesture NextSectionGesture = new(Key.N, KeyModifiers.Control);

    /// <summary>The previous section command's gesture: Beyond Compare's previous difference.</summary>
    internal static readonly KeyGesture PreviousSectionGesture = new(Key.P, KeyModifiers.Control);

    /// <summary>Identifies the <see cref="WrapSectionWalk"/> property.</summary>
    internal static readonly StyledProperty<bool> WrapSectionWalkProperty =
        AvaloniaProperty.Register<ReviewWindow, bool>(nameof(WrapSectionWalk), defaultValue: true);

    private readonly ReviewCommand[] commands;
    private ReviewSession? session;
    private MarkdownSection? sectionSpoken;

    /// <summary>
    /// The heading path last said for the left pane's caret, or <see langword="null"/> while none is: the left's
    /// counterpart of <see cref="sectionSpoken"/>, compared by path because the accumulator is read afresh.
    /// </summary>
    private string? leftSectionSaid;

    /// <summary>
    /// The staleness the last save warned of and the accumulator it warned over — a save finding both
    /// unchanged is decision 11's acknowledgement — or <see langword="null"/> while no warning stands.
    /// </summary>
    private (IReadOnlyList<StaleSection> Stale, string Accumulator)? warned;

    /// <summary>The left pane, once the view's template has made it: the section walk moves its caret.</summary>
    private DiffPanePresenter? leftPane;

    /// <summary>The right pane, once the view's template has made it.</summary>
    private DiffPanePresenter? rightPane;

    /// <summary>
    /// The pane that has the keyboard, or last had it while something else has it now: the one whose caret a press of
    /// the section walk counts from (plan 00038 decision 4), F6 included.
    /// </summary>
    private DiffSide? keyboardPane;

    /// <summary>
    /// Whether a caret has moved since the candidate on the right was shown: until one has, a press of the section walk
    /// counts from the top.
    /// </summary>
    private bool caretPlaced;

    /// <summary>Builds the window with no review in it; <see cref="Review"/> starts one.</summary>
    public ReviewWindow()
    {
        InitializeComponent();

        ReviewCommand next = new(() => Move(forward: true), HasReview);
        ReviewCommand previous = new(() => Move(forward: false), HasReview);
        ReviewCommand nextSection = new(() => WalkSections(forward: true), HasReview);
        ReviewCommand previousSection = new(() => WalkSections(forward: false), HasReview);
        ReviewCommand take = new(OnTake, HasReview);
        ReviewCommand leave = new(OnLeave, HasReview);
        ReviewCommand defer = new(OnDefer, HasReview);
        ReviewCommand drop = new(OnDrop, HasReview);
        ReviewCommand save = new(OnSave, HasReview);
        commands = [next, previous, nextSection, previousSection, take, leave, defer, drop, save];

        NextButton.Command = next;
        PreviousButton.Command = previous;
        NextSectionButton.Command = nextSection;
        PreviousSectionButton.Command = previousSection;
        TakeButton.Command = take;
        LeaveButton.Command = leave;
        DeferButton.Command = defer;
        DropButton.Command = drop;
        SaveButton.Command = save;

        KeyBindings.Add(new KeyBinding { Gesture = NextGesture, Command = next });
        KeyBindings.Add(new KeyBinding { Gesture = PreviousGesture, Command = previous });
        KeyBindings.Add(new KeyBinding { Gesture = NextSectionGesture, Command = nextSection });
        KeyBindings.Add(new KeyBinding { Gesture = PreviousSectionGesture, Command = previousSection });

        ThemeButton.Click += OnThemeButtonClick;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ShowThemeLabel();

        ChooseSavePath = ChooseInDialogAsync;
        View.TemplateApplied += OnViewTemplateApplied;
    }

    /// <summary>
    /// Switches the whole application to the theme <see cref="ThemeButton"/> names, so the window's chrome and the view's
    /// tokens move together. Until the first click the application's requested theme is its default, which follows the
    /// system's; a click sets one, and the system is followed no longer.
    /// </summary>
    /// <remarks>
    /// Brian's of 2026-11-16, in the shape of ClaudeForge's theme button, which AppKit was asked the same day to publish
    /// for every app; this gives way to that one once it is out.
    /// </remarks>
    private void OnThemeButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => ShowThemeLabel();

    /// <summary>
    /// Labels <see cref="ThemeButton"/> with the theme a click switches to, never the one shown: a switch labelled with
    /// the theme on screen reads as the other one the moment it is used, which is what Brian found of its first cut.
    /// </summary>
    private void ShowThemeLabel() =>
        ThemeButton.Content = ActualThemeVariant == ThemeVariant.Dark ? "☀ Light" : "☾ Dark";

    /// <summary>
    /// Asks the operator for the baseline's path, or answers <see langword="null"/> when they choose none —
    /// the platform's save dialog unless a test puts something else here.
    /// </summary>
    internal Func<Task<string?>> ChooseSavePath { get; set; }

    /// <summary>The review this window holds, once <see cref="Review"/> has started one.</summary>
    internal ReviewSession? Session => session;

    /// <summary>
    /// Whether a press of the section walk with nothing further that way comes round to the other end — plan 00038
    /// decision 5, Brian's of 2026-11-19: <see langword="true"/> by default, and a styled property that a setting or a
    /// style may bind, though nothing binds it yet. Set <see langword="false"/>, a press at an end says so and moves
    /// nothing, as F8 does at the last candidate.
    /// </summary>
    internal bool WrapSectionWalk
    {
        get => GetValue(WrapSectionWalkProperty);
        set => SetValue(WrapSectionWalkProperty, value);
    }

    /// <summary>
    /// Starts reviewing <paramref name="queue"/>: its seed on the left as the accumulator, its first
    /// candidate on the right, and <paramref name="earlier"/> — the decisions earlier runs made, read back
    /// from the decision file a save wrote — in its durable store. A window holds one review.
    /// </summary>
    /// <exception cref="InvalidOperationException">This window already holds a review.</exception>
    /// <exception cref="CorpusRefusedException">The queue holds nothing to review; the window is untouched.</exception>
    internal ReviewSession Review(ReviewQueue queue, IReadOnlyList<SectionDecision>? earlier = null)
    {
        if (session is not null)
        {
            throw new InvalidOperationException("this window already holds a review, and a second one is a second window");
        }

        session = new ReviewSession(queue, View, earlier);
        ShowPosition();
        foreach (ReviewCommand command in commands)
        {
            command.RaiseCanExecuteChanged();
        }

        return session;
    }

    private bool HasReview() => session is not null;

    private void Move(bool forward)
    {
        ReviewSession review = session!;
        if (forward ? review.Next() : review.Previous())
        {
            // The right shows another candidate, whose lines a mark there never named, and the mark goes with the
            // line it agrees with, on either side.
            View.LeftEmphasis = null;
            View.RightEmphasis = null;
            sectionSpoken = null;
            leftSectionSaid = null;
            ShowPosition();
            Say(string.Empty);

            // The section walk counts from the top of the candidate now shown until a caret moves on it, and the
            // reset of the right's caret that showing it made is not such a move.
            caretPlaced = false;
            return;
        }

        Say($"\"{review.Current.Candidate.Label}\" is the {(forward ? "last" : "first")} candidate, so there is no {(forward ? "next" : "previous")} one.");
    }

    /// <summary>
    /// Goes to the next or the previous section that differs — plan 00038 — or says why there is none to go to.
    /// </summary>
    private void WalkSections(bool forward)
    {
        ReviewSession review = session!;
        DiffSide? from = caretPlaced ? keyboardPane : null;
        int line = from is { } side ? PaneOf(side)?.TextArea.Caret.Line ?? 0 : 0;
        WalkStep step = forward
            ? review.SectionAfter(from, line, WrapSectionWalk)
            : review.SectionBefore(from, line, WrapSectionWalk);
        string label = review.Current.Candidate.Label;
        switch (step.Outcome)
        {
            case WalkOutcome.Found or WalkOutcome.CameRound:
                Land(step, forward);
                break;
            case WalkOutcome.AtEnd:
                Say($"No section of \"{label}\" {(forward ? "after" : "before")} this point differs from the baseline.");
                break;
            case WalkOutcome.NoneDiffers:
                Say($"No section of \"{label}\" differs from the baseline.");
                break;
            default:
                Say($"Not yet: \"{label}\" is still being compared with the baseline.");
                break;
        }
    }

    /// <summary>
    /// Puts the keyboard in the pane <paramref name="step"/> goes to and its caret at the start of the section's
    /// heading, with nothing selected, and scrolls the heading's row to the top of the view — plan 00038 decisions 2,
    /// 7 and 8. The mark and the line follow the caret, as for a click.
    /// </summary>
    private void Land(WalkStep step, bool forward)
    {
        if (PaneOf(step.Side) is not { } pane || step.Section is not { } section)
        {
            return;
        }

        // A landing names its section whether or not the line named it last.
        sectionSpoken = null;
        leftSectionSaid = null;
        pane.TextArea.Focus();
        pane.TextArea.ClearSelection();
        Caret caret = pane.TextArea.Caret;
        if (caret.Line != section.FirstLine || caret.Column != 1)
        {
            caret.Position = new TextViewPosition(section.FirstLine, 1);
        }
        else if (step.Side == DiffSide.Left)
        {
            // A caret already at the heading does not move, so the handler a move would run is run here.
            OnLeftCaretMoved(caret, EventArgs.Empty);
        }
        else
        {
            OnRightCaretMoved(caret, EventArgs.Empty);
        }

        caretPlaced = true;

        // The heading's own row, below any padding the other pane's lines put above it.
        pane.ScrollTo(section.FirstLine, 1, VisualYPosition.TextTop, 0, 0);
        if (step.Outcome == WalkOutcome.CameRound)
        {
            Say($"{OutcomeText.Text} Came round to the {(forward ? "first" : "last")} section that differs.");
        }
    }

    /// <summary>The pane on <paramref name="side"/>, once the view's template has made it.</summary>
    private DiffPanePresenter? PaneOf(DiffSide side) => side == DiffSide.Left ? leftPane : rightPane;

    private void OnTake()
    {
        ReviewSession review = session!;
        if (CandidateSectionAtCaret(review, "take") is not { } section)
        {
            return;
        }

        string from = review.Current.Candidate.Label;
        Decide(() => review.Take(section) switch
        {
            TakeResult.Unchanged => $"Nothing changed: {MarkdownOutline.Spoken(section.HeadingPath)} from \"{from}\" is already the baseline's version.",
            TakeResult.Inserted => $"Took {MarkdownOutline.Spoken(section.HeadingPath)} from \"{from}\", new to the baseline.",
            _ => $"Took {MarkdownOutline.Spoken(section.HeadingPath)} from \"{from}\" in place of the baseline's version.",
        });
    }

    private void OnLeave()
    {
        ReviewSession review = session!;
        if (CandidateSectionAtCaret(review, "leave") is not { } section)
        {
            return;
        }

        Decide(() =>
        {
            review.Leave(section);
            return $"Left {MarkdownOutline.Spoken(section.HeadingPath)} from \"{review.Current.Candidate.Label}\" out of the baseline.";
        });
    }

    private void OnDefer()
    {
        ReviewSession review = session!;
        if (CandidateSectionAtCaret(review, "defer") is not { } section)
        {
            return;
        }

        Decide(() =>
        {
            review.Defer(section);
            return $"Put {MarkdownOutline.Spoken(section.HeadingPath)} from \"{review.Current.Candidate.Label}\" off for this session.";
        });
    }

    private void OnDrop()
    {
        ReviewSession review = session!;
        if (review.AccumulatorSectionAt(CaretLine(SideBySideDiffView.LeftPanePart))?.HeadingPath is not { } path)
        {
            Say("Nothing to drop: the caret in the left pane is above its first heading, in no section.");
            return;
        }

        Decide(() =>
        {
            review.Drop(path);
            return $"Dropped {MarkdownOutline.Spoken(path)} from the baseline.";
        });
    }

    private async void OnSave()
    {
        // ⚠ async void, as a click handler is: a command returns nothing to await. Each way a save is
        // expected to fail is caught and said in the window; anything else is a defect, and reaches the
        // dispatcher as an exception from any other click would.
        ReviewSession review = session!;

        // ⭐ Decision 11 warns rather than refuses. A save over stale candidates says where they are stale and
        // writes nothing; saved again over the same staleness and the same text, it goes ahead, and anything
        // changed in between is asked about again.
        IReadOnlyList<StaleSection> stale = review.Staleness();
        string accumulator = review.Accumulator;
        bool acknowledged = warned is { } warning
            && string.Equals(warning.Accumulator, accumulator, StringComparison.Ordinal)
            && warning.Stale.SequenceEqual(stale);
        if (stale.Count > 0 && !acknowledged)
        {
            warned = (stale, accumulator);
            Say($"Not saved yet: the baseline changed after these were reviewed, where each carries its own version — {stale.Spoken()}. "
                + "Save again to accept that, or review them first.");
            return;
        }

        string? path;
        try
        {
            path = await ChooseSavePath();
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            Say("Not saved: there is no file dialog here.");
            return;
        }

        if (path is null)
        {
            Say("Not saved: no file was chosen.");
            return;
        }

        try
        {
            SavedBaseline saved = BaselineOutput.Write(review, path, stale);
            warned = null;
            Say($"Saved the baseline to {saved.BaselinePath}, its provenance to {saved.ReportPath}, and its decisions to {saved.DecisionsPath}."
                + (stale.Count > 0 ? " Its provenance lists the staleness accepted." : string.Empty));
        }
        catch (Exception ex) when (ex is SaveRefusedException or IOException or UnauthorizedAccessException)
        {
            Say($"Not saved: {ex.Message}");
        }
    }

    /// <summary>Asks where the baseline goes, in the platform's save dialog.</summary>
    private async Task<string?> ChooseInDialogAsync()
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the baseline",
            SuggestedFileName = session is { } review ? System.IO.Path.GetFileName(review.Queue.Seed.Path) : null,
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>Runs a decision and says what it did, or says the refusal that stopped it.</summary>
    private void Decide(Func<string> decision)
    {
        try
        {
            Say(decision());
        }
        catch (DecisionRefusedException refused)
        {
            Say($"Not done: {refused.Message}");
        }
    }

    /// <summary>The candidate's section at the right pane's caret, or none — said in the window.</summary>
    private MarkdownSection? CandidateSectionAtCaret(ReviewSession review, string verb)
    {
        if (review.CandidateSectionAt(CaretLine(SideBySideDiffView.RightPanePart)) is { } section)
        {
            return section;
        }

        Say($"Nothing to {verb}: the caret in the right pane is above its first heading, in no section.");
        return null;
    }

    /// <summary>
    /// Follows both panes' carets, which the view's template makes: this window applies no theme of
    /// its own, so the template is applied once, and a pane a later one replaced would move no caret.
    /// The section walk follows the panes' keyboard focus too, and keeps the panes it moves.
    /// </summary>
    private void OnViewTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<DiffPanePresenter>(SideBySideDiffView.RightPanePart) is { } right)
        {
            right.TextArea.Caret.PositionChanged += OnRightCaretMoved;
            right.TextArea.Caret.PositionChanged += OnRightCaretPlaced;
            right.TextArea.GotFocus += OnRightPaneFocused;
            rightPane = right;
        }

        if (e.NameScope.Find<DiffPanePresenter>(SideBySideDiffView.LeftPanePart) is { } left)
        {
            left.TextArea.Caret.PositionChanged += OnLeftCaretMoved;
            left.TextArea.Caret.PositionChanged += OnLeftCaretPlaced;
            left.TextArea.GotFocus += OnLeftPaneFocused;
            leftPane = left;
        }
    }

    /// <summary>A caret moved on the candidate shown, so a press of the section walk counts from a caret now.</summary>
    private void OnLeftCaretPlaced(object? sender, EventArgs e) => caretPlaced = true;

    /// <inheritdoc cref="OnLeftCaretPlaced"/>
    private void OnRightCaretPlaced(object? sender, EventArgs e) => caretPlaced = true;

    /// <summary>The left pane has the keyboard, so a press of the section walk counts from its caret.</summary>
    private void OnLeftPaneFocused(object? sender, RoutedEventArgs e) => keyboardPane = DiffSide.Left;

    /// <summary>The right pane has the keyboard, so a press of the section walk counts from its caret.</summary>
    private void OnRightPaneFocused(object? sender, RoutedEventArgs e) => keyboardPane = DiffSide.Right;

    /// <summary>
    /// Marks the candidate's section at the right pane's caret, and says what the durable store knows about
    /// it once the caret is in a section other than the one last said.
    /// </summary>
    private void OnRightCaretMoved(object? sender, EventArgs e)
    {
        if (session is not { } review || sender is not Caret caret)
        {
            return;
        }

        MarkdownSection? section = review.CandidateSectionAt(caret.Line);
        Mark(DiffSide.Right, section);
        if (ReferenceEquals(section, sectionSpoken))
        {
            return;
        }

        sectionSpoken = section;
        if (section is not null)
        {
            Say($"{MarkdownOutline.Spoken(section.HeadingPath)} — {review.StandingOf(section).Spoken}.");
            leftSectionSaid = null;
        }
    }

    /// <summary>
    /// Marks the accumulator's section at the left pane's caret, the one Drop acts on, and names it once the
    /// caret is in a section other than the one last said for it; and once the left has spoken, the right's next
    /// move speaks again. A take or a drop moves this caret while it edits, and the verb's outcome is said after
    /// that, so the section the edit left the caret in is remembered and its outcome stands. The mark is read
    /// again on every move, typing included, because typing can move a section's end under the same heading.
    /// </summary>
    private void OnLeftCaretMoved(object? sender, EventArgs e)
    {
        if (session is not { } review || sender is not Caret caret)
        {
            return;
        }

        MarkdownSection? section = review.AccumulatorSectionAt(caret.Line);
        Mark(DiffSide.Left, section);
        string? path = section?.HeadingPath;
        if (string.Equals(path, leftSectionSaid, StringComparison.Ordinal))
        {
            return;
        }

        leftSectionSaid = path;
        if (path is not null)
        {
            Say($"{MarkdownOutline.Spoken(path)} — in the baseline; Drop removes it.");
            sectionSpoken = null;
        }
    }

    /// <summary>
    /// Marks <paramref name="section"/> in <paramref name="side"/>'s pane, from its heading to its last line of
    /// text, and clears the other pane's: the mark is in the pane whose caret moved last, so it and the line
    /// under the panes name one section. A caret in no section, the preamble's among them, marks nothing.
    /// </summary>
    private void Mark(DiffSide side, MarkdownSection? section)
    {
        LineRange? range = section is null ? null : new LineRange(section.FirstLine - 1, section.ContentLastLine - section.FirstLine + 1);
        View.LeftEmphasis = side == DiffSide.Left ? range : null;
        View.RightEmphasis = side == DiffSide.Right ? range : null;
    }

    /// <summary>
    /// The caret line of the pane the view publishes as <paramref name="part"/>, 1-based — or 0, in no
    /// section, while the view has no such pane.
    /// </summary>
    private int CaretLine(string part) =>
        View.GetVisualDescendants().OfType<DiffPanePresenter>().FirstOrDefault(p => p.Name == part)?.TextArea.Caret.Line ?? 0;

    /// <summary>Where the right pane is in the queue, and how far its candidate is from the seed.</summary>
    private void ShowPosition()
    {
        ReviewSession review = session!;
        QueuedCandidate current = review.Current;
        PositionText.Text =
            $"Candidate {review.Position + 1} of {review.Queue.Items.Count}: \"{current.Candidate.Label}\", "
            + $"{current.Changes} {(current.Changes == 1 ? "change" : "changes")} against the seed \"{review.Queue.Seed.Label}\"";
    }

    private void Say(string outcome) => OutcomeText.Text = outcome;

    /// <summary>The window's own command type; the library's is internal, as a host's would be its own.</summary>
    private sealed class ReviewCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter)
        {
            if (canExecute())
            {
                execute();
            }
        }

        internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
