using System.Globalization;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using AvaloniaEdit.Editing;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;
using Bennewitz.Ninja.XamlQuality.ThemeAudit;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>docs/template-parts.md</c> is what the code says: every control this library defines, the template
/// parts each has, the <c>AutomationId</c> a harness finds a part by, and what looks each part up. Plan
/// 00030.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The reference reads what the gates read and copies none of it.</b> The controls and their peer
/// types are <see cref="AccessibilityCoverageTests"/>' own, the scan is <see cref="TemplatePartTests"/>', and
/// the parts and their lookups are <see cref="TemplatePartRule.ReadParts"/>, the reading <c>BNXQ1003</c>
/// derives its findings from, so the reference cannot disagree with the gate.
/// </para>
/// <para>
/// ⚠ <b>The strip and the header come from that reading too.</b> Their code declares no part and nothing
/// looks one up on their templates, and since XamlQuality <c>2026.4.1109</c> the reading still gives each an
/// entry, its themes' parts and nothing else. Until then this test read their themes from the scan's markup
/// itself, held to the rule's reading wherever both read; that reading went with the pin that made it
/// unnecessary, so an older XamlQuality fails the check below rather than leaving the two out.
/// </para>
/// <para>
/// ⛔ <b>There is no write mode.</b> A switch that made this test rewrite the reference and pass would turn
/// the gate off. On drift the fresh reference lands beside the committed one and
/// <c>scripts/gen-template-parts</c> moves it over. The check runs first and writes nothing, because what a
/// failed check would write is a wrong reference, and a received copy invites someone to accept it.
/// </para>
/// </remarks>
public sealed class TemplatePartReferenceTests
{
    private const string Absent = "—";

    private const string Present = "✓";

    /// <summary>Where the library's markup sits under the scan's root, as a theme's file names it.</summary>
    private const string LibraryMarkup = "DiffView.Avalonia/";

    /// <summary>The views table's controls, in the order of its columns.</summary>
    private static readonly string[] Views = [nameof(SideBySideDiffView), nameof(DiffViewer), nameof(InlineDiffView)];

    /// <summary>The find bar's table.</summary>
    private static readonly string[] FindBar = [nameof(DiffFindBar)];

    /// <summary>The strip's and the header's table, the strip's parts first: two controls whose code declares no part.</summary>
    private static readonly string[] StripAndHeader = [nameof(DiffStatusStrip), nameof(DiffPaneHeader)];

    /// <summary>The presenter's table: its own theme, then the one it applies to its text area.</summary>
    private static readonly string[] Presenter = [nameof(DiffPanePresenter), nameof(TextArea)];

    /// <summary>Every control whose parts a table shows.</summary>
    private static string[] Placed => [.. Views, .. FindBar, .. StripAndHeader, .. Presenter];

    [AvaloniaFact]
    public void The_committed_reference_equals_a_fresh_reading()
    {
        Reading reading = new(TemplatePartRule.ReadParts(TemplatePartTests.Scan()));
        string[] controls = AccessibilityCoverageTests.ConcreteControls();

        AssertEveryControlIsPlaced(reading, controls);

        string fresh = Render(reading, controls);
        string committed = RepoPaths.Source(Path.Combine("docs", "template-parts.md"));
        string received = OutputDrift.ReceivedPathOf(committed);
        DriftState state = OutputDrift.Check(committed, fresh);

        // ⚠ Check writes a received copy for a stale file and none for a missing one, and a missing
        // reference is the same drift: the failure below says the fresh one is beside it.
        if (state == DriftState.Missing)
        {
            File.WriteAllText(received, fresh);
        }

        Assert.True(
            state == DriftState.UpToDate,
            $"docs/template-parts.md is not what the code says ({state}). The fresh reference is written to "
            + $"{Path.GetFileName(received)} beside it. Run scripts/gen-template-parts, read what moved, and "
            + "commit it.");
    }

    /// <summary>
    /// The check, made before anything is written: every control of the set and every entry of the reading
    /// whose theme is in the library's markup has its place. One with a theme has a table to be in, one with
    /// none really is drawn, and each control a table shows has one theme to show, in the library's markup.
    /// </summary>
    /// <remarks>
    /// A control with a theme and no table would be missing from the reference without a word, and the
    /// controls table calls a control with no theme drawn, which of a templated control would be false: that
    /// is the assertion an older XamlQuality fails, its reading having no entry for the strip or the header.
    /// The last assertion is what makes <see cref="LibraryMarkup"/> able to fail: a literal that stopped naming
    /// the library's folder would match nothing in the first, and pass.
    /// </remarks>
    private static void AssertEveryControlIsPlaced(Reading reading, string[] controls)
    {
        string[] unplaced =
        [
            .. controls
                .Where(c => reading.ThemesOf(c).Count > 0)
                .Concat(reading.Rule.Controls
                    .Where(c => c.Themes.Any(t => t.File.StartsWith(LibraryMarkup, StringComparison.Ordinal)))
                    .Select(c => c.Name))
                .Except(Placed, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        Assert.True(
            unplaced.Length == 0,
            "These controls have a theme and no table in docs/template-parts.md: "
            + string.Join(", ", unplaced) + ". Place each in a table of this test.");

        string[] templatedWithNoTheme =
        [
            .. controls.Where(c => reading.ThemesOf(c).Count == 0 && typeof(TemplatedControl).IsAssignableFrom(TypeOf(c))),
        ];
        Assert.True(
            templatedWithNoTheme.Length == 0,
            "These controls are templated and have no theme, so the reference would call them drawn: "
            + string.Join(", ", templatedWithNoTheme));

        string[] notOneTheme =
        [
            .. Placed.Where(c => reading.ThemesOf(c) is not [{ File: var file }]
                || !file.StartsWith(LibraryMarkup, StringComparison.Ordinal)),
        ];
        Assert.True(
            notOneTheme.Length == 0,
            "A table shows one theme of each control, in the library's markup, and these have none, several, "
            + "or one elsewhere: " + string.Join(", ", notOneTheme));
    }

    /// <summary>The reference. Nothing in it depends on the culture or the machine: a theme by its file name, never its path.</summary>
    private static string Render(Reading reading, string[] controls)
    {
        StringBuilder text = new();
        text.Append("# Template parts\n\n");
        text.Append("**Generated by `TemplatePartReferenceTests`. Do not edit this file.** A correction belongs in the theme or the code it reads. `scripts/gen-template-parts` rewrites this document, and the test fails while the two differ.\n\n");
        text.Append("Every control this library defines, the template parts each has, the `AutomationId` a harness finds a part by, and whether code looks the part up. Each table follows the order the template declares its parts.\n\n");
        text.Append($"*Looked up in code* reads **{Present}** where this library's code looks the part up, the control's own or, for `SideBySideDiffView` and `DiffViewer`, `DiffBuildController`'s, which they hand their templates to; ***AvaloniaEdit*** where only code AvaloniaEdit defines does; and **{Absent}** where nothing does. A part nothing looks up serves only its theme's selectors and bindings, so a host's own template may rename it or leave it out. A part something looks up is found by its name, and a template without it loses whatever that code does with it.\n\n");

        AppendControls(text, reading, controls);
        AppendViews(text, reading);

        text.Append("## `DiffFindBar`\n\n");
        Row(text, "Part", "Element", "`AutomationId`", "Looked up in code");
        Row(text, "---", "---", "---", ":-:");
        foreach (string control in FindBar)
        {
            foreach (DeclaredPart part in reading.PartsOf(control))
            {
                Row(text, Code(part.Name), Code(part.ElementType), IdOf(part), reading.LookedUp(control, part.Name));
            }
        }

        text.Append("\n## `DiffStatusStrip` and `DiffPaneHeader`\n\n");
        AppendByControl(text, reading, "Control", StripAndHeader);

        text.Append("\n## `DiffPanePresenter`\n\n");
        text.Append("Its own theme's parts, then those of the theme it applies to its text area, which targets AvaloniaEdit's `TextArea`.\n\n");
        AppendByControl(text, reading, "Theme", Presenter);

        text.Append("\n## Not read\n\n");
        if (reading.Rule.Skipped.Count == 0)
        {
            text.Append("Nothing.\n");
        }

        foreach (XamlSkip skip in reading.Rule.Skipped)
        {
            text.Append("- ").Append(Code(skip.Subject)).Append(": ").Append(skip.Reason).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The controls table: the three views in the views table's order, then the other themed controls,
    /// then the drawn ones, each of the last two in the ordinal order <paramref name="controls"/> has.
    /// </summary>
    private static void AppendControls(StringBuilder text, Reading reading, string[] controls)
    {
        Dictionary<string, string?> paneMargins = PaneMarginIds();

        text.Append("## The controls\n\n");
        text.Append("*Own `AutomationId`* is the id the control's own code gives it. A control a template places takes the id its part declares.\n\n");
        Row(text, "Control", "Visibility", "Base class", "Template", "Automation peer type", "Own `AutomationId`");
        Row(text, "---", "---", "---", "---", "---", "---");

        string[] themed = [.. controls.Where(c => !Views.Contains(c) && reading.ThemesOf(c).Count > 0)];
        string[] drawn = [.. controls.Where(c => reading.ThemesOf(c).Count == 0)];
        foreach (string control in (string[])[.. Views, .. themed, .. drawn])
        {
            Type type = TypeOf(control);
            Row(
                text,
                Code(control),
                type.IsVisible ? "public" : "internal",
                BaseOf(type),
                TemplateOf(reading, type, paneMargins),
                Code(AccessibilityCoverageTests.ExpectedControlTypes[control].ToString()),
                paneMargins.GetValueOrDefault(control) is { } id ? Code(id) : Absent);
        }

        text.Append('\n');
    }

    /// <summary>
    /// The views table, one row per part any of them declares. A part the views declare differently is
    /// shown, not failed: a theme rule is a gate's to enforce, and the reference describes the templates as
    /// they are.
    /// </summary>
    private static void AppendViews(StringBuilder text, Reading reading)
    {
        text.Append("## The views\n\n");
        text.Append("Their templates share part names, so they are compared side by side, in the side-by-side view's order; a part only another view declares follows the part before it in that view's template. Where the views declare a part differently, its cell names each view's value.\n\n");
        Row(text, ["Part", "Element", .. Views.Select(Code), "`AutomationId`", "Looked up in code"]);
        Row(text, ["---", "---", .. Views.Select(_ => ":-:"), "---", ":-:"]);

        foreach (string part in InTemplateOrder(Views.Select(v => reading.PartsOf(v).Select(p => p.Name))))
        {
            (string View, DeclaredPart Part)[] declaring =
            [
                .. Views.SelectMany(v => reading.PartsOf(v).Where(p => p.Name == part).Select(p => (v, p))),
            ];

            Row(
                text,
                [
                    Code(part),
                    Agreed(declaring.Select(d => (d.View, Code(d.Part.ElementType)))),
                    .. Views.Select(v => declaring.Any(d => d.View == v) ? Present : Absent),
                    Agreed(declaring.Select(d => (d.View, IdOf(d.Part)))),
                    LookedUpAcross(reading, [.. declaring.Select(d => d.View)], part),
                ]);
        }

        text.Append('\n');
    }

    /// <summary>A table of several controls' parts, the first column saying whose.</summary>
    private static void AppendByControl(StringBuilder text, Reading reading, string firstColumn, string[] controls)
    {
        Row(text, firstColumn, "Part", "Element", "`AutomationId`", "Looked up in code");
        Row(text, "---", "---", "---", "---", ":-:");
        foreach (string control in controls)
        {
            foreach (DeclaredPart part in reading.PartsOf(control))
            {
                Row(text, Code(control), Code(part.Name), Code(part.ElementType), IdOf(part), reading.LookedUp(control, part.Name));
            }
        }
    }

    /// <summary>
    /// The order of several templates' parts: the first template's, and a part only a later one declares
    /// follows the part before it in that template.
    /// </summary>
    private static List<string> InTemplateOrder(IEnumerable<IEnumerable<string>> templates)
    {
        List<string> merged = [];
        foreach (IEnumerable<string> template in templates)
        {
            string? previous = null;
            foreach (string part in template)
            {
                if (!merged.Contains(part))
                {
                    merged.Insert(previous is null ? 0 : merged.IndexOf(previous) + 1, part);
                }

                previous = part;
            }
        }

        return merged;
    }

    /// <summary>The one value the views that declare a part agree on, or else each view's own.</summary>
    private static string Agreed(IEnumerable<(string View, string Value)> values)
    {
        (string View, string Value)[] each = [.. values];
        return each.Select(v => v.Value).Distinct(StringComparer.Ordinal).Count() == 1
            ? each[0].Value
            : string.Join("; ", each.Select(v => $"{Code(v.View)}: {v.Value}"));
    }

    /// <summary>
    /// The cell the views that declare a part agree on; where they differ, the views whose code looks it up.
    /// </summary>
    private static string LookedUpAcross(Reading reading, string[] views, string part)
    {
        string[] cells = [.. views.Select(v => reading.LookedUp(v, part)).Distinct(StringComparer.Ordinal)];
        return cells.Length == 1
            ? cells[0]
            : string.Join(", ", views.Where(v => reading.LookedUp(v, part) != Absent).Select(Code));
    }

    /// <summary>A base class, with the one it comes from where it is this library's own.</summary>
    private static string BaseOf(Type type)
    {
        Type parent = type.BaseType!;
        return parent.Assembly == type.Assembly ? $"{Named(parent)} ({Named(parent.BaseType!)})" : Named(parent);

        static string Named(Type named) =>
            named.Assembly == typeof(AvaloniaEdit.TextEditor).Assembly ? $"AvaloniaEdit {Code(named.Name)}" : Code(named.Name);
    }

    /// <summary>A control's theme file and its part count, or what it is instead of templated.</summary>
    private static string TemplateOf(Reading reading, Type type, Dictionary<string, string?> paneMargins)
    {
        if (reading.ThemesOf(type.Name) is not [ThemeParts theme])
        {
            return paneMargins.ContainsKey(type.Name) ? "*drawn, built by the pane*" : "*drawn, no template*";
        }

        string file = theme.File[(theme.File.LastIndexOf('/') + 1)..];
        string cell = $"{Code(file)}, {Count(theme.Declared.Count)}";
        return type == typeof(DiffPanePresenter)
            ? $"{cell} (plus its text area's theme, {Count(reading.PartsOf(nameof(TextArea)).Length)})"
            : cell;

        static string Count(int parts) =>
            parts.ToString(CultureInfo.InvariantCulture) + (parts == 1 ? " part" : " parts");
    }

    /// <summary>
    /// The margins a constructed pane builds, each with the id its own code gives it, which the fixture files
    /// under the pane, so it cannot say which carries which.
    /// </summary>
    private static Dictionary<string, string?> PaneMarginIds() => new DiffPanePresenter().TextArea.LeftMargins
        .OfType<DiffMargin>()
        .ToDictionary(m => m.GetType().Name, m => AutomationProperties.GetAutomationId(m), StringComparer.Ordinal);

    private static Type TypeOf(string control) =>
        typeof(SideBySideDiffView).Assembly.GetTypes().Single(t => t.Name == control);

    private static string IdOf(DeclaredPart part) => part.AutomationId is { } id ? Code(id) : Absent;

    private static string Code(string text) => $"`{text}`";

    private static void Row(StringBuilder text, params IEnumerable<string> cells) =>
        text.Append("| ").AppendJoin(" | ", cells).Append(" |\n");

    /// <summary>The rule's reading, and what the tables ask of it.</summary>
    private sealed record Reading(TemplatePartReading Rule)
    {
        /// <summary>A control's themes as the rule read them, and none where it has no entry, as a drawn control has none.</summary>
        public IReadOnlyList<ThemeParts> ThemesOf(string control) =>
            Rule.Controls.FirstOrDefault(c => c.Name == control)?.Themes ?? [];

        public DeclaredPart[] PartsOf(string control) => [.. ThemesOf(control).SelectMany(t => t.Declared)];

        /// <summary>
        /// The <i>Looked up in code</i> cell, a projection of each lookup's assembly: code in AvaloniaEdit's
        /// and nowhere else, code of this library's, or none.
        /// </summary>
        public string LookedUp(string control, string part)
        {
            string avaloniaEdit = typeof(AvaloniaEdit.TextEditor).Assembly.GetName().Name!;
            TemplatePartLookup[] lookups =
            [
                .. Rule.Controls.Where(c => c.Name == control).SelectMany(c => c.Lookups).Where(l => l.Part == part),
            ];

            return lookups.Length == 0 ? Absent
                : lookups.All(l => l.Assembly == avaloniaEdit) ? "*AvaloniaEdit*"
                : Present;
        }
    }
}
