using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Bennewitz.Ninja.DiffView;

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
/// ⚠ Nothing launches this window yet: <see cref="Program"/> cannot be told a corpus or a seed, so the
/// tests construct it. That start-up is what a real consolidation needs next.
/// </para>
/// </remarks>
internal sealed partial class ReviewWindow : Window
{
    /// <summary>The next command's gesture.</summary>
    internal static readonly KeyGesture NextGesture = new(Key.F8);

    /// <summary>The previous command's gesture.</summary>
    internal static readonly KeyGesture PreviousGesture = new(Key.F8, KeyModifiers.Shift);

    private readonly ReviewCommand[] commands;
    private ReviewSession? session;

    /// <summary>Builds the window with no review in it; <see cref="Review"/> starts one.</summary>
    public ReviewWindow()
    {
        InitializeComponent();

        ReviewCommand next = new(() => Move(forward: true), HasReview);
        ReviewCommand previous = new(() => Move(forward: false), HasReview);
        ReviewCommand take = new(OnTake, HasReview);
        ReviewCommand leave = new(OnLeave, HasReview);
        ReviewCommand defer = new(OnDefer, HasReview);
        ReviewCommand drop = new(OnDrop, HasReview);
        ReviewCommand save = new(OnSave, HasReview);
        commands = [next, previous, take, leave, defer, drop, save];

        NextButton.Command = next;
        PreviousButton.Command = previous;
        TakeButton.Command = take;
        LeaveButton.Command = leave;
        DeferButton.Command = defer;
        DropButton.Command = drop;
        SaveButton.Command = save;

        KeyBindings.Add(new KeyBinding { Gesture = NextGesture, Command = next });
        KeyBindings.Add(new KeyBinding { Gesture = PreviousGesture, Command = previous });

        ChooseSavePath = ChooseInDialogAsync;
    }

    /// <summary>
    /// Asks the operator for the baseline's path, or answers <see langword="null"/> when they choose none —
    /// the platform's save dialog unless a test puts something else here.
    /// </summary>
    internal Func<Task<string?>> ChooseSavePath { get; set; }

    /// <summary>The review this window holds, once <see cref="Review"/> has started one.</summary>
    internal ReviewSession? Session => session;

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
            ShowPosition();
            Say(string.Empty);
            return;
        }

        Say($"\"{review.Current.Candidate.Label}\" is the {(forward ? "last" : "first")} candidate, so there is no {(forward ? "next" : "previous")} one.");
    }

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
        if (review.AccumulatorPathAt(CaretLine(SideBySideDiffView.LeftPanePart)) is not { } path)
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
            SavedBaseline saved = BaselineOutput.Write(review, path);
            Say($"Saved the baseline to {saved.BaselinePath}, its provenance to {saved.ReportPath}, and its decisions to {saved.DecisionsPath}.");
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
