using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The find bar above the panes: the query box, the Match case / Whole word / Regex / Changed
/// rows only toggles, the L / R / Both scope buttons, the match count, previous / next / close,
/// and an inline line for a pattern error or a truncation notice. It holds no search state and
/// runs no search — the composite fills it and listens to <see cref="QueryChanged"/>,
/// <see cref="OptionsChanged"/>, <see cref="NextRequested"/>, <see cref="PreviousRequested"/>
/// and <see cref="CloseRequested"/>. Every label and automation name comes from
/// <see cref="DiffViewStrings"/>, resolved per instance, and every control carries one.
/// </summary>
public class DiffFindBar : TemplatedControl
{
    /// <summary>The template part holding the query box.</summary>
    public const string QueryPart = "PART_Query";

    /// <summary>The template part holding the Match case toggle.</summary>
    public const string MatchCasePart = "PART_MatchCase";

    /// <summary>The template part holding the Whole word toggle.</summary>
    public const string WholeWordPart = "PART_WholeWord";

    /// <summary>The template part holding the regular-expression toggle.</summary>
    public const string RegexPart = "PART_Regex";

    /// <summary>The template part holding the Changed rows only toggle.</summary>
    public const string ChangedRowsOnlyPart = "PART_ChangedRowsOnly";

    /// <summary>The template part holding the left-scope button.</summary>
    public const string ScopeLeftPart = "PART_ScopeLeft";

    /// <summary>The template part holding the right-scope button.</summary>
    public const string ScopeRightPart = "PART_ScopeRight";

    /// <summary>The template part holding the both-scope button.</summary>
    public const string ScopeBothPart = "PART_ScopeBoth";

    /// <summary>The template part holding the previous-match button.</summary>
    public const string PreviousPart = "PART_Previous";

    /// <summary>The template part holding the next-match button.</summary>
    public const string NextPart = "PART_Next";

    /// <summary>The template part holding the close button.</summary>
    public const string ClosePart = "PART_Close";

    /// <summary>Identifies the <see cref="Query"/> property.</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(Query), string.Empty);

    /// <summary>Identifies the <see cref="MatchCase"/> property.</summary>
    public static readonly StyledProperty<bool> MatchCaseProperty =
        AvaloniaProperty.Register<DiffFindBar, bool>(nameof(MatchCase));

    /// <summary>Identifies the <see cref="WholeWord"/> property.</summary>
    public static readonly StyledProperty<bool> WholeWordProperty =
        AvaloniaProperty.Register<DiffFindBar, bool>(nameof(WholeWord));

    /// <summary>Identifies the <see cref="UseRegex"/> property.</summary>
    public static readonly StyledProperty<bool> UseRegexProperty =
        AvaloniaProperty.Register<DiffFindBar, bool>(nameof(UseRegex));

    /// <summary>Identifies the <see cref="ChangedRowsOnly"/> property.</summary>
    public static readonly StyledProperty<bool> ChangedRowsOnlyProperty =
        AvaloniaProperty.Register<DiffFindBar, bool>(nameof(ChangedRowsOnly));

    /// <summary>Identifies the <see cref="Scope"/> property.</summary>
    public static readonly StyledProperty<FindScope> ScopeProperty =
        AvaloniaProperty.Register<DiffFindBar, FindScope>(nameof(Scope), FindScope.Both);

    /// <summary>Identifies the <see cref="ShowScope"/> property.</summary>
    public static readonly StyledProperty<bool> ShowScopeProperty =
        AvaloniaProperty.Register<DiffFindBar, bool>(nameof(ShowScope), defaultValue: true);

    /// <summary>Identifies the <see cref="CountText"/> property.</summary>
    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string?>(nameof(CountText));

    /// <summary>Identifies the <see cref="ErrorText"/> property.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string?>(nameof(ErrorText));

    /// <summary>Identifies the <see cref="NoticeText"/> property.</summary>
    public static readonly StyledProperty<string?> NoticeTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string?>(nameof(NoticeText));

    /// <summary>Identifies the <see cref="QueryPlaceholder"/> property.</summary>
    public static readonly StyledProperty<string> QueryPlaceholderProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(QueryPlaceholder), string.Empty);

    /// <summary>Identifies the <see cref="QueryName"/> property.</summary>
    public static readonly StyledProperty<string> QueryNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(QueryName), string.Empty);

    /// <summary>Identifies the <see cref="MatchCaseText"/> property.</summary>
    public static readonly StyledProperty<string> MatchCaseTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(MatchCaseText), string.Empty);

    /// <summary>Identifies the <see cref="MatchCaseName"/> property.</summary>
    public static readonly StyledProperty<string> MatchCaseNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(MatchCaseName), string.Empty);

    /// <summary>Identifies the <see cref="WholeWordText"/> property.</summary>
    public static readonly StyledProperty<string> WholeWordTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(WholeWordText), string.Empty);

    /// <summary>Identifies the <see cref="WholeWordName"/> property.</summary>
    public static readonly StyledProperty<string> WholeWordNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(WholeWordName), string.Empty);

    /// <summary>Identifies the <see cref="RegexText"/> property.</summary>
    public static readonly StyledProperty<string> RegexTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(RegexText), string.Empty);

    /// <summary>Identifies the <see cref="RegexName"/> property.</summary>
    public static readonly StyledProperty<string> RegexNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(RegexName), string.Empty);

    /// <summary>Identifies the <see cref="ChangedRowsOnlyText"/> property.</summary>
    public static readonly StyledProperty<string> ChangedRowsOnlyTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ChangedRowsOnlyText), string.Empty);

    /// <summary>Identifies the <see cref="ChangedRowsOnlyName"/> property.</summary>
    public static readonly StyledProperty<string> ChangedRowsOnlyNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ChangedRowsOnlyName), string.Empty);

    /// <summary>Identifies the <see cref="ScopeLeftText"/> property.</summary>
    public static readonly StyledProperty<string> ScopeLeftTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ScopeLeftText), string.Empty);

    /// <summary>Identifies the <see cref="ScopeLeftName"/> property.</summary>
    public static readonly StyledProperty<string> ScopeLeftNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ScopeLeftName), string.Empty);

    /// <summary>Identifies the <see cref="ScopeRightText"/> property.</summary>
    public static readonly StyledProperty<string> ScopeRightTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ScopeRightText), string.Empty);

    /// <summary>Identifies the <see cref="ScopeRightName"/> property.</summary>
    public static readonly StyledProperty<string> ScopeRightNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ScopeRightName), string.Empty);

    /// <summary>Identifies the <see cref="ScopeBothText"/> property.</summary>
    public static readonly StyledProperty<string> ScopeBothTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ScopeBothText), string.Empty);

    /// <summary>Identifies the <see cref="ScopeBothName"/> property.</summary>
    public static readonly StyledProperty<string> ScopeBothNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(ScopeBothName), string.Empty);

    /// <summary>Identifies the <see cref="PreviousText"/> property.</summary>
    public static readonly StyledProperty<string> PreviousTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(PreviousText), string.Empty);

    /// <summary>Identifies the <see cref="PreviousName"/> property.</summary>
    public static readonly StyledProperty<string> PreviousNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(PreviousName), string.Empty);

    /// <summary>Identifies the <see cref="NextText"/> property.</summary>
    public static readonly StyledProperty<string> NextTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(NextText), string.Empty);

    /// <summary>Identifies the <see cref="NextName"/> property.</summary>
    public static readonly StyledProperty<string> NextNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(NextName), string.Empty);

    /// <summary>Identifies the <see cref="CloseText"/> property.</summary>
    public static readonly StyledProperty<string> CloseTextProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(CloseText), string.Empty);

    /// <summary>Identifies the <see cref="CloseName"/> property.</summary>
    public static readonly StyledProperty<string> CloseNameProperty =
        AvaloniaProperty.Register<DiffFindBar, string>(nameof(CloseName), string.Empty);

    private TextBox? _queryBox;
    private ToggleButton? _matchCase;
    private ToggleButton? _wholeWord;
    private ToggleButton? _regex;
    private ToggleButton? _changedRowsOnly;
    private ToggleButton? _scopeLeft;
    private ToggleButton? _scopeRight;
    private ToggleButton? _scopeBoth;
    private Button? _previous;
    private Button? _next;
    private Button? _close;
    private bool _updatingParts;

    /// <summary>Creates a bar with its compiled theme merged into its own resources.</summary>
    public DiffFindBar()
    {
        Resources.MergedDictionaries.Add(new SideBySideDiffViewTheme());
        RefreshStrings();
    }

    /// <summary><see cref="Query"/> changed, from the query box or from a host.</summary>
    public event EventHandler? QueryChanged;

    /// <summary>A toggle or the scope changed, from a click or from a host.</summary>
    public event EventHandler? OptionsChanged;

    /// <summary>The user asked for the next match: the button, or Enter in the query box.</summary>
    public event EventHandler? NextRequested;

    /// <summary>The user asked for the previous match: the button, or Shift+Enter in the query box.</summary>
    public event EventHandler? PreviousRequested;

    /// <summary>The user asked to close the bar.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>What to search for.</summary>
    public string Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    /// <summary>Whether letter case matters.</summary>
    public bool MatchCase
    {
        get => GetValue(MatchCaseProperty);
        set => SetValue(MatchCaseProperty, value);
    }

    /// <summary>Whether a match must stand as a whole word.</summary>
    public bool WholeWord
    {
        get => GetValue(WholeWordProperty);
        set => SetValue(WholeWordProperty, value);
    }

    /// <summary>Whether the query is a regular expression.</summary>
    public bool UseRegex
    {
        get => GetValue(UseRegexProperty);
        set => SetValue(UseRegexProperty, value);
    }

    /// <summary>Whether only changed rows are searched.</summary>
    public bool ChangedRowsOnly
    {
        get => GetValue(ChangedRowsOnlyProperty);
        set => SetValue(ChangedRowsOnlyProperty, value);
    }

    /// <summary>Which pane or panes are searched.</summary>
    public FindScope Scope
    {
        get => GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }

    /// <summary>
    /// Whether the L / R / Both scope buttons are shown. On by default; a host with one pane —
    /// <see cref="InlineDiffView"/> — turns them off, there being no side to choose between.
    /// </summary>
    public bool ShowScope
    {
        get => GetValue(ShowScopeProperty);
        set => SetValue(ShowScopeProperty, value);
    }

    /// <summary>The match count, or <c>null</c> with no query.</summary>
    public string? CountText
    {
        get => GetValue(CountTextProperty);
        set => SetValue(CountTextProperty, value);
    }

    /// <summary>The pattern error, or <c>null</c> when the query ran.</summary>
    public string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    /// <summary>The truncation notice, or <c>null</c> when every match is shown.</summary>
    public string? NoticeText
    {
        get => GetValue(NoticeTextProperty);
        set => SetValue(NoticeTextProperty, value);
    }

    /// <summary>The query box's watermark.</summary>
    public string QueryPlaceholder
    {
        get => GetValue(QueryPlaceholderProperty);
        set => SetValue(QueryPlaceholderProperty, value);
    }

    /// <summary>The query box's automation name.</summary>
    public string QueryName
    {
        get => GetValue(QueryNameProperty);
        set => SetValue(QueryNameProperty, value);
    }

    /// <summary>The Match case toggle's label.</summary>
    public string MatchCaseText
    {
        get => GetValue(MatchCaseTextProperty);
        set => SetValue(MatchCaseTextProperty, value);
    }

    /// <summary>The Match case toggle's tooltip and automation name.</summary>
    public string MatchCaseName
    {
        get => GetValue(MatchCaseNameProperty);
        set => SetValue(MatchCaseNameProperty, value);
    }

    /// <summary>The Whole word toggle's label.</summary>
    public string WholeWordText
    {
        get => GetValue(WholeWordTextProperty);
        set => SetValue(WholeWordTextProperty, value);
    }

    /// <summary>The Whole word toggle's tooltip and automation name.</summary>
    public string WholeWordName
    {
        get => GetValue(WholeWordNameProperty);
        set => SetValue(WholeWordNameProperty, value);
    }

    /// <summary>The regular-expression toggle's label.</summary>
    public string RegexText
    {
        get => GetValue(RegexTextProperty);
        set => SetValue(RegexTextProperty, value);
    }

    /// <summary>The regular-expression toggle's tooltip and automation name.</summary>
    public string RegexName
    {
        get => GetValue(RegexNameProperty);
        set => SetValue(RegexNameProperty, value);
    }

    /// <summary>The Changed rows only toggle's label.</summary>
    public string ChangedRowsOnlyText
    {
        get => GetValue(ChangedRowsOnlyTextProperty);
        set => SetValue(ChangedRowsOnlyTextProperty, value);
    }

    /// <summary>The Changed rows only toggle's tooltip and automation name.</summary>
    public string ChangedRowsOnlyName
    {
        get => GetValue(ChangedRowsOnlyNameProperty);
        set => SetValue(ChangedRowsOnlyNameProperty, value);
    }

    /// <summary>The left-scope button's label.</summary>
    public string ScopeLeftText
    {
        get => GetValue(ScopeLeftTextProperty);
        set => SetValue(ScopeLeftTextProperty, value);
    }

    /// <summary>The left-scope button's tooltip and automation name.</summary>
    public string ScopeLeftName
    {
        get => GetValue(ScopeLeftNameProperty);
        set => SetValue(ScopeLeftNameProperty, value);
    }

    /// <summary>The right-scope button's label.</summary>
    public string ScopeRightText
    {
        get => GetValue(ScopeRightTextProperty);
        set => SetValue(ScopeRightTextProperty, value);
    }

    /// <summary>The right-scope button's tooltip and automation name.</summary>
    public string ScopeRightName
    {
        get => GetValue(ScopeRightNameProperty);
        set => SetValue(ScopeRightNameProperty, value);
    }

    /// <summary>The both-scope button's label.</summary>
    public string ScopeBothText
    {
        get => GetValue(ScopeBothTextProperty);
        set => SetValue(ScopeBothTextProperty, value);
    }

    /// <summary>The both-scope button's tooltip and automation name.</summary>
    public string ScopeBothName
    {
        get => GetValue(ScopeBothNameProperty);
        set => SetValue(ScopeBothNameProperty, value);
    }

    /// <summary>The previous-match button's label.</summary>
    public string PreviousText
    {
        get => GetValue(PreviousTextProperty);
        set => SetValue(PreviousTextProperty, value);
    }

    /// <summary>The previous-match button's tooltip and automation name.</summary>
    public string PreviousName
    {
        get => GetValue(PreviousNameProperty);
        set => SetValue(PreviousNameProperty, value);
    }

    /// <summary>The next-match button's label.</summary>
    public string NextText
    {
        get => GetValue(NextTextProperty);
        set => SetValue(NextTextProperty, value);
    }

    /// <summary>The next-match button's tooltip and automation name.</summary>
    public string NextName
    {
        get => GetValue(NextNameProperty);
        set => SetValue(NextNameProperty, value);
    }

    /// <summary>The close button's label.</summary>
    public string CloseText
    {
        get => GetValue(CloseTextProperty);
        set => SetValue(CloseTextProperty, value);
    }

    /// <summary>The close button's tooltip and automation name.</summary>
    public string CloseName
    {
        get => GetValue(CloseNameProperty);
        set => SetValue(CloseNameProperty, value);
    }

    /// <summary>The query box, once the template has applied.</summary>
    internal TextBox? QueryBox => _queryBox;

    /// <summary>
    /// Puts the caret in the query box and selects what is there. Returns whether the box took
    /// focus: a box that has just become visible has not been laid out yet, and an unmeasured
    /// control cannot take focus, so the caller retries after the layout pass.
    /// </summary>
    public bool FocusQuery()
    {
        if (_queryBox is null || !_queryBox.Focus())
        {
            return false;
        }

        _queryBox.SelectAll();
        return true;
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _queryBox = e.NameScope.Find<TextBox>(QueryPart);
        _matchCase = e.NameScope.Find<ToggleButton>(MatchCasePart);
        _wholeWord = e.NameScope.Find<ToggleButton>(WholeWordPart);
        _regex = e.NameScope.Find<ToggleButton>(RegexPart);
        _changedRowsOnly = e.NameScope.Find<ToggleButton>(ChangedRowsOnlyPart);
        _scopeLeft = e.NameScope.Find<ToggleButton>(ScopeLeftPart);
        _scopeRight = e.NameScope.Find<ToggleButton>(ScopeRightPart);
        _scopeBoth = e.NameScope.Find<ToggleButton>(ScopeBothPart);
        _previous = e.NameScope.Find<Button>(PreviousPart);
        _next = e.NameScope.Find<Button>(NextPart);
        _close = e.NameScope.Find<Button>(ClosePart);

        if (_queryBox is not null)
        {
            _queryBox.TextChanged += OnQueryBoxTextChanged;
        }

        // Click, not IsCheckedChanged: it fires only for user activation, so pushing the
        // composite's state into the toggles below never bounces back as an option change.
        foreach (ToggleButton? toggle in new[] { _matchCase, _wholeWord, _regex, _changedRowsOnly })
        {
            if (toggle is not null)
            {
                toggle.Click += OnToggleClicked;
            }
        }

        foreach (ToggleButton? scope in new[] { _scopeLeft, _scopeRight, _scopeBoth })
        {
            if (scope is not null)
            {
                scope.Click += OnScopeClicked;
            }
        }

        if (_previous is not null)
        {
            _previous.Click += OnPreviousClicked;
        }

        if (_next is not null)
        {
            _next.Click += OnNextClicked;
        }

        if (_close is not null)
        {
            _close.Click += OnCloseClicked;
        }

        UpdateParts();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QueryProperty)
        {
            UpdateParts();
            QueryChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == MatchCaseProperty
                 || change.Property == WholeWordProperty
                 || change.Property == UseRegexProperty
                 || change.Property == ChangedRowsOnlyProperty
                 || change.Property == ScopeProperty)
        {
            UpdateParts();
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Enter and Shift+Enter walk the matches; the query box does not consume them.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Enter)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            PreviousRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }

    private void RefreshStrings()
    {
        AutomationProperties.SetName(this, DiffViewStrings.Get(DiffViewStrings.FindBarName));
        SetCurrentValue(QueryPlaceholderProperty, DiffViewStrings.Get(DiffViewStrings.FindQueryPlaceholder));
        SetCurrentValue(QueryNameProperty, DiffViewStrings.Get(DiffViewStrings.FindQueryName));
        SetCurrentValue(MatchCaseTextProperty, DiffViewStrings.Get(DiffViewStrings.FindMatchCase));
        SetCurrentValue(MatchCaseNameProperty, DiffViewStrings.Get(DiffViewStrings.FindMatchCaseName));
        SetCurrentValue(WholeWordTextProperty, DiffViewStrings.Get(DiffViewStrings.FindWholeWord));
        SetCurrentValue(WholeWordNameProperty, DiffViewStrings.Get(DiffViewStrings.FindWholeWordName));
        SetCurrentValue(RegexTextProperty, DiffViewStrings.Get(DiffViewStrings.FindRegex));
        SetCurrentValue(RegexNameProperty, DiffViewStrings.Get(DiffViewStrings.FindRegexName));
        SetCurrentValue(ChangedRowsOnlyTextProperty, DiffViewStrings.Get(DiffViewStrings.FindChangedRowsOnly));
        SetCurrentValue(ChangedRowsOnlyNameProperty, DiffViewStrings.Get(DiffViewStrings.FindChangedRowsOnlyName));
        SetCurrentValue(ScopeLeftTextProperty, DiffViewStrings.Get(DiffViewStrings.FindScopeLeft));
        SetCurrentValue(ScopeLeftNameProperty, DiffViewStrings.Get(DiffViewStrings.FindScopeLeftName));
        SetCurrentValue(ScopeRightTextProperty, DiffViewStrings.Get(DiffViewStrings.FindScopeRight));
        SetCurrentValue(ScopeRightNameProperty, DiffViewStrings.Get(DiffViewStrings.FindScopeRightName));
        SetCurrentValue(ScopeBothTextProperty, DiffViewStrings.Get(DiffViewStrings.FindScopeBoth));
        SetCurrentValue(ScopeBothNameProperty, DiffViewStrings.Get(DiffViewStrings.FindScopeBothName));
        SetCurrentValue(PreviousTextProperty, DiffViewStrings.Get(DiffViewStrings.FindPrevious));
        SetCurrentValue(PreviousNameProperty, DiffViewStrings.Get(DiffViewStrings.FindPreviousName));
        SetCurrentValue(NextTextProperty, DiffViewStrings.Get(DiffViewStrings.FindNext));
        SetCurrentValue(NextNameProperty, DiffViewStrings.Get(DiffViewStrings.FindNextName));
        SetCurrentValue(CloseTextProperty, DiffViewStrings.Get(DiffViewStrings.FindClose));
        SetCurrentValue(CloseNameProperty, DiffViewStrings.Get(DiffViewStrings.FindCloseName));
    }

    private void UpdateParts()
    {
        if (_updatingParts)
        {
            return;
        }

        _updatingParts = true;
        try
        {
            if (_queryBox is not null && !string.Equals(_queryBox.Text ?? string.Empty, Query, StringComparison.Ordinal))
            {
                _queryBox.Text = Query;
            }

            SetChecked(_matchCase, MatchCase);
            SetChecked(_wholeWord, WholeWord);
            SetChecked(_regex, UseRegex);
            SetChecked(_changedRowsOnly, ChangedRowsOnly);
            SetChecked(_scopeLeft, Scope == FindScope.Left);
            SetChecked(_scopeRight, Scope == FindScope.Right);
            SetChecked(_scopeBoth, Scope == FindScope.Both);
        }
        finally
        {
            _updatingParts = false;
        }
    }

    private static void SetChecked(ToggleButton? button, bool value)
    {
        if (button is not null && button.IsChecked != value)
        {
            button.IsChecked = value;
        }
    }

    private void DetachParts()
    {
        if (_queryBox is not null)
        {
            _queryBox.TextChanged -= OnQueryBoxTextChanged;
        }

        foreach (ToggleButton? toggle in new[] { _matchCase, _wholeWord, _regex, _changedRowsOnly })
        {
            if (toggle is not null)
            {
                toggle.Click -= OnToggleClicked;
            }
        }

        foreach (ToggleButton? scope in new[] { _scopeLeft, _scopeRight, _scopeBoth })
        {
            if (scope is not null)
            {
                scope.Click -= OnScopeClicked;
            }
        }

        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClicked;
        }

        if (_next is not null)
        {
            _next.Click -= OnNextClicked;
        }

        if (_close is not null)
        {
            _close.Click -= OnCloseClicked;
        }
    }

    private void OnQueryBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingParts)
        {
            return;
        }

        // Setting the property raises QueryChanged; the composite drives the search from there.
        SetCurrentValue(QueryProperty, _queryBox?.Text ?? string.Empty);
    }

    private void OnToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (_updatingParts)
        {
            return;
        }

        SetCurrentValue(MatchCaseProperty, _matchCase?.IsChecked == true);
        SetCurrentValue(WholeWordProperty, _wholeWord?.IsChecked == true);
        SetCurrentValue(UseRegexProperty, _regex?.IsChecked == true);
        SetCurrentValue(ChangedRowsOnlyProperty, _changedRowsOnly?.IsChecked == true);
    }

    private void OnScopeClicked(object? sender, RoutedEventArgs e)
    {
        if (_updatingParts)
        {
            return;
        }

        // The three buttons are one segmented control: the clicked one wins, whatever its own
        // checked state ended up as, and the other two follow from the scope.
        SetCurrentValue(
            ScopeProperty,
            ReferenceEquals(sender, _scopeLeft) ? FindScope.Left
            : ReferenceEquals(sender, _scopeRight) ? FindScope.Right
            : FindScope.Both);
        UpdateParts();
    }

    private void OnPreviousClicked(object? sender, RoutedEventArgs e)
    {
        PreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
