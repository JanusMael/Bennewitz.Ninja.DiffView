using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using Bennewitz.Ninja.DiffView.Avalonia;
using Bennewitz.Ninja.ThemeAudit;
using Semi.Avalonia;
using ThemeVariant = Avalonia.Styling.ThemeVariant;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// The audit's static view checked against the runtime (plan 00001 §Phase 2): every
/// <c>DiffView.*</c> token resolves through <c>TryGetResource</c> under all ten targets; every
/// key AvaloniaEdit's Fluent and Simple theme files reference resolves under all six Semi
/// variants with the compat dictionaries merged, and without them the gaps are exactly the ones
/// <c>docs/theme-audit.md</c> names; and a plain <c>TextEditor</c> with its Fluent search panel
/// renders under each Semi variant with the compat dictionary and no binding warning.
/// </summary>
[Trait("Category", "Reference")]
public sealed class ThemeResolutionTests
{
    private static readonly Uri BaseUri = new("avares://DiffView.Avalonia.Tests/");

    private static readonly string[] FluentThemeGaps =
        ["ContentControlThemeFontFamily", "ControlContentThemeFontSize", "SystemAccentColor", "SystemBaseLowColor", "SystemChromeMediumColor", "ToolTipBorderThemeThickness"];

    private static readonly string[] SimpleThemeGaps =
        ["ContentControlThemeFontFamily", "FontSizeNormal", "HighlightColor", "ThemeBackgroundBrush", "ThemeBackgroundColor", "ThemeBorderLowColor", "ThemeBorderMidBrush", "ThemeBorderThickness", "ThemeForegroundColor"];

    public static TheoryData<string, string> Targets => new()
    {
        { "Fluent", "Light" }, { "Fluent", "Dark" },
        { "Simple", "Light" }, { "Simple", "Dark" },
        { "Semi", "Light" }, { "Semi", "Dark" }, { "Semi", "Aquatic" }, { "Semi", "Desert" }, { "Semi", "Dusk" }, { "Semi", "NightSky" },
    };

    public static TheoryData<string> SemiVariants => ["Light", "Dark", "Aquatic", "Desert", "Dusk", "NightSky"];

    [AvaloniaTheory]
    [MemberData(nameof(Targets))]
    public void Every_DiffView_token_resolves_in_both_palettes(string theme, string variant)
    {
        using ThemeSwap swap = ThemeSwap.To(theme, variant);
        IReadOnlyList<string> keys = TokenKeys("DiffView.Tokens.axaml");
        Assert.NotEmpty(keys);
        Assert.Equal(keys, TokenKeys("DiffView.Tokens.ColorBlind.axaml")); // the sibling palette defines the same keys

        Assert.Empty(Missing(keys, swap.Variant));

        // The colour-blind palette merged after the default one replaces every token.
        ResourceInclude colourBlind = new(BaseUri) { Source = DiffViewResources.ColorBlindTokensUri };
        Application.Current!.Resources.MergedDictionaries.Add(colourBlind);
        try
        {
            Assert.Empty(Missing(keys, swap.Variant));
            Assert.True(Application.Current.TryGetResource("DiffView.MarkerInsertedBrush", swap.Variant, out object? marker));
            Color inserted = Assert.IsType<SolidColorBrush>(marker).Color;
            Assert.True(inserted.B > inserted.R, $"the colour-blind inserted marker should be blue, got {inserted}");
        }
        finally
        {
            Application.Current.Resources.MergedDictionaries.Remove(colourBlind);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(SemiVariants))]
    public void AvaloniaEdit_theme_keys_resolve_under_Semi_only_with_the_compat_dictionaries(string variant)
    {
        using ThemeSwap swap = ThemeSwap.To("Semi", variant);
        IReadOnlyList<string> fluentKeys = AvaloniaEditKeys("Fluent");
        IReadOnlyList<string> simpleKeys = AvaloniaEditKeys("Simple");
        Assert.NotEmpty(fluentKeys);
        Assert.NotEmpty(simpleKeys);

        // Without the compat dictionaries: exactly the gaps the audit names. Semi's high-contrast
        // variants define HighlightColor themselves.
        bool highContrast = variant is "Aquatic" or "Desert" or "Dusk" or "NightSky";
        Assert.Equal(FluentThemeGaps, Missing(fluentKeys, swap.Variant));
        Assert.Equal(highContrast ? SimpleThemeGaps.Where(k => k != "HighlightColor") : SimpleThemeGaps, Missing(simpleKeys, swap.Variant));

        // With them: nothing missing, and Semi's own high-contrast value survives the merge.
        ResourceInclude fluentCompat = new(BaseUri) { Source = DiffViewResources.FluentCompatUri };
        ResourceInclude simpleCompat = new(BaseUri) { Source = DiffViewResources.SimpleCompatUri };
        Application.Current!.Resources.MergedDictionaries.Add(fluentCompat);
        Application.Current.Resources.MergedDictionaries.Add(simpleCompat);
        try
        {
            Assert.Empty(Missing(fluentKeys, swap.Variant));
            Assert.Empty(Missing(simpleKeys, swap.Variant));

            Assert.True(Application.Current.TryGetResource("HighlightColor", swap.Variant, out object? highlight));
            Color expected = variant switch
            {
                "NightSky" => Color.Parse("#D6B4FD"),
                "Aquatic" => Color.Parse("#8EE3F0"),
                "Light" => Color.Parse("#0064FA"),  // SemiBlue5Color through the mapping
                "Dark" => Color.Parse("#54A9FF"),
                _ => Assert.IsType<Color>(highlight),
            };
            Assert.Equal(expected, Assert.IsType<Color>(highlight));

            // The mapped accent follows Semi's primary.
            Assert.True(Application.Current.TryGetResource("SystemAccentColor", swap.Variant, out object? accent));
            Assert.True(Application.Current.TryGetResource("SemiBlue5Color", swap.Variant, out object? blue5));
            Assert.Equal(blue5, accent);
        }
        finally
        {
            Application.Current.Resources.MergedDictionaries.Remove(simpleCompat);
            Application.Current.Resources.MergedDictionaries.Remove(fluentCompat);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(SemiVariants))]
    public void A_TextEditor_with_its_Fluent_search_panel_renders_under_Semi_with_the_compat_dictionary(string variant)
    {
        using ThemeSwap swap = ThemeSwap.To("Semi", variant);
        TestLogSink.Instance.Clear();

        Application app = Application.Current!;
        ResourceInclude compat = new(BaseUri) { Source = DiffViewResources.FluentCompatUri };
        StyleInclude editorTheme = new(BaseUri) { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") };
        app.Resources.MergedDictionaries.Add(compat);
        app.Styles.Add(editorTheme);
        try
        {
            TextEditor editor = new()
            {
                Document = new TextDocument("alpha\nbeta\ngamma\n"),
                FontFamily = new FontFamily(TestFonts.MonoFamilyName),
            };
            Window window = new() { Width = 600, Height = 300, Content = editor };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            SearchPanel panel = SearchPanel.Install(editor);
            panel.Open();
            Dispatcher.UIThread.RunJobs();
            Assert.True(panel.IsOpened);
            Assert.True(panel.Bounds.Width > 0 && panel.Bounds.Height > 0, "the search panel has no size");

            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);

            // The panel paints something other than the editor background: it is not invisible.
            Point origin = panel.TranslatePoint(new Point(0, 0), window) ?? throw new InvalidOperationException("panel is not in the window");
            PixelRect area = PixelProbe.Inside(origin.X, origin.Y, origin.X + panel.Bounds.Width, origin.Y + panel.Bounds.Height);
            Color background = PixelProbe.At(frame, 2, (int)window.Height - 3);
            int painted = PixelProbe.Count(frame, area, c => c != background);
            Assert.True(painted > 0, $"the search panel under Semi {variant} painted nothing over {background}");

            window.Close();
            TestLogSink.AssertNoWarnings(LogArea.Binding);
        }
        finally
        {
            app.Styles.Remove(editorTheme);
            app.Resources.MergedDictionaries.Remove(compat);
        }
    }

    private static IReadOnlyList<string> Missing(IReadOnlyList<string> keys, ThemeVariant variant)
    {
        return keys.Where(k => !Application.Current!.TryGetResource(k, variant, out _)).Order(StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<string> TokenKeys(string file)
    {
        return ThemeDefinitionScanner.ScanFile(RepoPaths.Source(Path.Combine("src", "DiffView.Avalonia", "Themes", file)))
            .Select(d => d.Key)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The keys an AvaloniaEdit theme file references and does not define itself.</summary>
    private static IReadOnlyList<string> AvaloniaEditKeys(string theme)
    {
        string directory = RepoPaths.Source(Path.Combine("reference", "AvaloniaEdit", "src", "AvaloniaEdit", "Themes", theme));
        HashSet<string> own = ThemeDefinitionScanner.Scan(directory).Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        return ResourceReferenceScanner.Scan(directory)
            .Select(r => r.Key)
            .Where(k => !own.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Puts the named theme in place of the test application's Semi and requests a variant;
    /// disposing restores Semi and the previous variant, so the snapshot tests keep their host.
    /// </summary>
    private sealed class ThemeSwap : IDisposable
    {
        private readonly IStyle _original;
        private readonly IStyle _replacement;
        private readonly ThemeVariant? _previousVariant;

        private ThemeSwap(IStyle original, IStyle replacement, ThemeVariant? previousVariant, ThemeVariant variant)
        {
            _original = original;
            _replacement = replacement;
            _previousVariant = previousVariant;
            Variant = variant;
        }

        public ThemeVariant Variant { get; }

        public static ThemeSwap To(string theme, string variant)
        {
            Application app = Application.Current!;
            IStyle original = app.Styles[0];
            Assert.IsType<SemiTheme>(original);

            IStyle replacement = theme switch
            {
                "Fluent" => new FluentTheme(),
                "Simple" => new SimpleTheme(),
                "Semi" => original,
                _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "unknown theme"),
            };
            if (!ReferenceEquals(replacement, original))
            {
                app.Styles.RemoveAt(0);
                app.Styles.Insert(0, replacement);
            }

            ThemeVariant requested = variant switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                "Aquatic" => SemiTheme.Aquatic,
                "Desert" => SemiTheme.Desert,
                "Dusk" => SemiTheme.Dusk,
                "NightSky" => SemiTheme.NightSky,
                _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "unknown variant"),
            };
            ThemeVariant? previous = app.RequestedThemeVariant;
            app.RequestedThemeVariant = requested;
            return new ThemeSwap(original, replacement, previous, requested);
        }

        public void Dispose()
        {
            Application app = Application.Current!;
            if (!ReferenceEquals(_replacement, _original))
            {
                app.Styles.RemoveAt(0);
                app.Styles.Insert(0, _original);
            }

            app.RequestedThemeVariant = _previousVariant;
        }
    }
}
