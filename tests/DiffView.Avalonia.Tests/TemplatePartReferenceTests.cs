using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Avalonia.Automation;
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
/// ⚠ <b>One reading of its own, held to the rule's.</b> The rule has no entry for a control whose code
/// declares no part and on whose template nothing looks one up, which is the strip and the header, so their
/// parts come from their themes in the scan's parsed markup. That reading is made of every theme, and has to
/// equal the rule's wherever both read. A release that gives those two an entry moves them onto the rule's
/// reading, and nothing here changes.
/// </para>
/// <para>
/// ⛔ <b>There is no write mode.</b> A switch that made this test rewrite the reference and pass would turn
/// the gate off. On drift the fresh reference lands beside the committed one and
/// <c>scripts/gen-template-parts</c> moves it over. The two checks run first and write nothing, because what
/// a failed check would write is a wrong reference, and a received copy invites someone to accept it.
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

    /// <summary>The table of the two controls the rule has no entry for, the strip's parts first.</summary>
    private static readonly string[] StripAndHeader = [nameof(DiffStatusStrip), nameof(DiffPaneHeader)];

    /// <summary>The presenter's table: its own theme, then the one it applies to its text area.</summary>
    private static readonly string[] Presenter = [nameof(DiffPanePresenter), nameof(TextArea)];

    /// <summary>Every control whose parts a table shows.</summary>
    private static string[] Placed => [.. Views, .. FindBar, .. StripAndHeader, .. Presenter];

    [AvaloniaFact]
    public void The_committed_reference_equals_a_fresh_reading()
    {
        XamlScanContext scan = TemplatePartTests.Scan();
        Reading reading = new(TemplatePartRule.ReadParts(scan), ThemesInMarkup(scan));
        string[] controls = AccessibilityCoverageTests.ConcreteControls();

        AssertEveryControlIsPlaced(reading, controls);
        AssertTheDeclarationsAgree(reading);

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
    /// The first check: every themed control of the set and every entry of the reading whose theme is in
    /// the library's markup has a table to be in, and each control a table shows has one theme to show.
    /// </summary>
    /// <remarks>
    /// A control with no theme is drawn, and the controls table says so; one with a theme and no table
    /// would be missing from the reference without a word.
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
            "These controls have a theme in the library's markup and no table in docs/template-parts.md: "
            + string.Join(", ", unplaced) + ". Place each in a table of this test.");

        string[] notOneTheme = [.. Placed.Where(c => reading.ThemesOf(c).Count != 1)];
        Assert.True(
            notOneTheme.Length == 0,
            "A table shows one theme of each control, and these have none or several: "
            + string.Join(", ", notOneTheme));
    }

    /// <summary>
    /// The second check: this test's reading of a theme equals the rule's wherever both read, by name,
    /// element and id, in order. It passes over nothing only while the rule read the themes of every control
    /// a table takes from it, which is asserted first.
    /// </summary>
    private static void AssertTheDeclarationsAgree(Reading reading)
    {
        string[] unread =
        [
            .. Placed
                .Except(StripAndHeader, StringComparer.Ordinal)
                .Except(reading.Rule.Controls.Where(c => c.Themes.Count > 0).Select(c => c.Name), StringComparer.Ordinal),
        ];
        Assert.True(
            unread.Length == 0,
            "The rule read no theme of these controls, so nothing here can be compared with its reading: "
            + string.Join(", ", unread));

        string[] disagreeing =
        [
            .. reading.Rule.Controls
                .Where(c => !c.Themes.SelectMany(t => t.Declared)
                    .SequenceEqual(reading.Markup[c.Name].SelectMany(t => t.Declared)))
                .Select(c => c.Name),
        ];
        Assert.True(
            disagreeing.Length == 0,
            "This test's reading of these controls' themes differs from BNXQ1003's, which it is held to: "
            + string.Join(", ", disagreeing));
    }

    /// <summary>The reference. Nothing in it depends on the culture or the machine: a theme by its file name, never its path.</summary>
    private static string Render(Reading reading, string[] controls)
    {
        StringBuilder text = new();
        text.Append("# Template parts\n\n");
        text.Append("**Generated by `TemplatePartReferenceTests`. Do not edit this file.** A correction belongs in the theme or the code it reads. `scripts/gen-template-parts` rewrites this document, and the test fails while the two differ.\n\n");
        text.Append("Every control this library defines, the template parts each has, the `AutomationId` a harness finds a part by, and whether code looks the part up. Each table follows the order the template declares its parts.\n\n");
        text.Append($"*Looked up in code* reads **{Present}** where this library's code looks the part up, the control's own or `DiffBuildController`'s, which the views hand their templates to; ***AvaloniaEdit*** where only code AvaloniaEdit defines does; and **{Absent}** where nothing does. A part nothing looks up serves only its theme's selectors and bindings, so a host's own template may rename it or leave it out. A part something looks up is found by its name, and a template without it loses whatever that code does with it.\n\n");

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
        Dictionary<string, string?> ownIds = MarginIds();

        text.Append("## The controls\n\n");
        text.Append("*Own `AutomationId`* is the id the control's own code gives it. A control a template places takes the id its part declares.\n\n");
        Row(text, "Control", "Visibility", "Base class", "Template", "Automation peer type", "Own `AutomationId`");
        Row(text, "---", "---", "---", "---", "---", "---");

        string[] themed = [.. controls.Where(c => !Views.Contains(c) && reading.ThemesOf(c).Count > 0)];
        string[] drawn = [.. controls.Where(c => reading.ThemesOf(c).Count == 0)];
        foreach (string control in (string[])[.. Views, .. themed, .. drawn])
        {
            Type type = typeof(SideBySideDiffView).Assembly.GetTypes().Single(t => t.Name == control);
            Row(
                text,
                Code(control),
                type.IsPublic ? "public" : "internal",
                BaseOf(type),
                TemplateOf(reading, type),
                Code(AccessibilityCoverageTests.ExpectedControlTypes[control].ToString()),
                ownIds.GetValueOrDefault(control) is { } id ? Code(id) : Absent);
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
    private static string TemplateOf(Reading reading, Type type)
    {
        if (reading.ThemesOf(type.Name) is not [ThemeParts theme])
        {
            return typeof(DiffMargin).IsAssignableFrom(type) ? "*drawn, built by the pane*" : "*drawn, no template*";
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
    /// The ids the control's own code gives it: the two margins a pane builds, which the fixture files both
    /// under the pane, so it cannot say which carries which.
    /// </summary>
    private static Dictionary<string, string?> MarginIds()
    {
        DiffPanePresenter pane = new();
        DiffMargin[] margins = [pane.LineNumberMargin, pane.ChangeMarkerMargin];
        return margins.ToDictionary(m => m.GetType().Name, m => AutomationProperties.GetAutomationId(m), StringComparer.Ordinal);
    }

    /// <summary>Every theme in the scan's parsed markup, by the control it targets.</summary>
    private static ILookup<string, ThemeParts> ThemesInMarkup(XamlScanContext scan) => scan.ParsedFiles
        .SelectMany(file => file.Document!.Descendants()
            .Where(e => e.Name.LocalName == "ControlTheme" && e.Attribute("TargetType") is not null)
            .Select(theme => (
                Target: TargetOf(theme.Attribute("TargetType")!.Value),
                Parts: new ThemeParts(file.RelativePath, null, [.. theme.Descendants().Select(PartOf).OfType<DeclaredPart>()]))))
        .ToLookup(t => t.Target, t => t.Parts, StringComparer.Ordinal);

    private static string TargetOf(string targetType) => targetType[(targetType.IndexOf(':') + 1)..];

    /// <summary>The part an element declares, if it declares one: its name, element and id, as the rule reads them.</summary>
    private static DeclaredPart? PartOf(XElement element)
    {
        string? name = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
        if (name is null || !name.StartsWith(TemplatePartRule.PartPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string? id = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value;
        return new DeclaredPart(name, element.Name.LocalName, id);
    }

    private static string IdOf(DeclaredPart part) => part.AutomationId is { } id ? Code(id) : Absent;

    private static string Code(string text) => $"`{text}`";

    private static void Row(StringBuilder text, params IEnumerable<string> cells) =>
        text.Append("| ").AppendJoin(" | ", cells).Append(" |\n");

    /// <summary>The rule's reading, and this test's own reading of every theme in the scan's markup.</summary>
    private sealed record Reading(TemplatePartReading Rule, ILookup<string, ThemeParts> Markup)
    {
        /// <summary>A control's themes: the rule's reading of them, or the markup's where it has no entry.</summary>
        public IReadOnlyList<ThemeParts> ThemesOf(string control) =>
            Rule.Controls.FirstOrDefault(c => c.Name == control)?.Themes ?? [.. Markup[control]];

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
