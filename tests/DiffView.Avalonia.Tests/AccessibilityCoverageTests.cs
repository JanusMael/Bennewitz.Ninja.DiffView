using System.Reflection;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Every interactive control in the library's templates and the demo's views carries
/// <c>AutomationProperties.Name</c>, so screen readers announce it.
/// </summary>
/// <remarks>
/// <para>
/// The walker this replaced was adapted from ClaudeForge's <c>AxamlAccessibilityCoverageTests</c>
/// (MIT) and carried a per-file ratchet baseline that was empty here — every file at zero. The
/// analysis now comes from <c>Bennewitz.Ninja.XamlQuality</c>, whose <c>XQ1002</c> holds seventeen
/// framework element names and takes more in its constructor.
/// </para>
/// <para>
/// ⛔ <b>The element set is DERIVED from the assembly, not written out.</b> It is every public
/// <see cref="Control"/> this library ships, minus <see cref="Excluded"/>, plus
/// <see cref="ForwardCover"/>. A hand-written list was measured blindable: four of its seven names
/// contributed nothing to the library's count and at most one to the whole scan's, so deleting one
/// line left every floor green while an element stopped being inspected — proven for
/// <c>SideBySideDiffView</c> and <c>InlineDiffView</c>, whose only elements are in the demo. A
/// derived set has no name to delete, and a new control joins it the moment it is public.
/// </para>
/// <para>
/// ⭐ <b>Two holes the hand-rolled walker had, both measured.</b> It compared attribute presence,
/// so <c>AutomationProperties.Name=""</c> passed it; <c>AutomationName.IsDeclaredOn</c> rejects an
/// empty name, and a whitespace-only one. And it asserted only that *files* were found, so an
/// element set that had stopped matching anything read exactly like a clean repository.
/// </para>
/// <para>
/// ⚠ <b>What this gate does not establish.</b> It asserts that an interactive control's name is
/// declared <em>in markup</em>. It does not establish that a screen reader reaches one — no control
/// of this library overrides <c>OnCreateAutomationPeer</c>, so each returns <c>NoneAutomationPeer</c>
/// and a control-view traversal skips it — and it cannot see a control that names itself in code,
/// which is why <see cref="Excluded"/> exists and why the claim each exclusion makes is asserted
/// separately below.
/// </para>
/// <para>
/// ⛔ <b>There is no parse-error assertion here, and that was measured rather than assumed.</b>
/// Every rule iterates <see cref="XamlScanContext.ParsedFiles"/>, which silently drops a file that
/// failed to parse — so unparseable markup would leave this gate green at an unchanged count. It
/// cannot arise: the Avalonia XAML compiler rejects it as <c>AVLN1001</c> before any test runs,
/// proven against both a hand-authored view and a generated compat dictionary, and
/// <c>EnableDefaultAvaloniaItems</c> is set nowhere, so every <c>.axaml</c> under <c>src</c> is a
/// compiled item. An assertion for it would be a gate that cannot fire, which is the thing this one
/// exists to refuse.
/// </para>
/// </remarks>
public sealed class AccessibilityCoverageTests
{
    /// <summary>
    /// Public controls this library ships that the markup scan deliberately does not cover, each
    /// with the reason. An entry here is a decision; an omission from the set would be an accident.
    /// </summary>
    /// <remarks>
    /// Every reason is a claim about something the scan cannot see, so every reason is asserted — see
    /// <see cref="Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check"/>. An exclusion
    /// whose reason nothing checks is how a set starts drifting back towards a hand-written list.
    /// </remarks>
    private static readonly Dictionary<string, string> Excluded = new(StringComparer.Ordinal)
    {
        [nameof(DiffFindBar)] =
            "names itself in its own constructor, from DiffViewStrings.FindBarName, alongside the "
            + "eleven child names it sets there. XQ1002 reads markup declarations and cannot see a "
            + "runtime one, so requiring an attribute here would mean two sources for one string.",
    };

    /// <summary>
    /// Framework element names the stock seventeen leave out that are <b>live here</b> — each matches an
    /// element in this repository's markup and is floored one at a time, exactly like a derived control.
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>Menu</c> is here because the demo's menu bar is interactive and nothing else covers it.</b>
    /// Its fifty <c>MenuItem</c>s are covered, because <c>MenuItem</c> <em>is</em> one of the stock
    /// seventeen — which is exactly how an unnamed bar can hide among them. Proven by arithmetic rather
    /// than assumed: the demo holds exactly fifty <c>MenuItem</c>s and the stock set inspects exactly
    /// fifty of its elements, so it would be fifty-one if <c>Menu</c> were among them.
    /// </remarks>
    private static readonly string[] LiveFrameworkElements = [nameof(Menu)];

    /// <summary>
    /// Framework element names the stock seventeen leave out that match <b>nothing</b> here. Neither is
    /// ours, and their inertness is asserted rather than claimed.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>A name is not a gate.</b> An inert <em>rule</em> reports zero and reads as coverage; an
    /// inert <em>name</em> costs nothing and catches the first use. <c>XQ1001</c> is declined for the
    /// former reason, which is also what makes <c>Expander</c> safe to carry here — the two would
    /// otherwise report one defect twice, which is why <c>XQ1002</c>'s stock set omits it.
    /// <c>TextEditor</c> is AvaloniaEdit's, and is here because the walker this gate replaced carried
    /// it; dropping it would be a behaviour change wearing the clothes of a refactor.
    /// <para>
    /// ⚠ <b>The split from <see cref="LiveFrameworkElements"/> is what this list means.</b> "An inert
    /// name catches the first use" is true only of a name that IS inert. One that already matches
    /// something is covering elements no floor protects, so it belongs in that list instead — which is
    /// what the inverse assertion here forces the moment either of these starts matching.
    /// </para>
    /// </remarks>
    private static readonly string[] ForwardCover = [nameof(TextEditor), nameof(Expander)];

    /// <summary>
    /// A floor on what the stock seventeen inspect over the library's own markup: 8, against a
    /// population of 14.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>It floors the stock names only, because the derived names are floored one at a time
    /// instead</b>, by the per-name coverage in <see cref="Every_interactive_control_has_an_automation_name"/>.
    /// A single number over the whole set is what lets a name be deleted silently.
    /// <para>
    /// ⛔ <b>What it guards is a zero, not a part count.</b> The fourteen are OUR elements matching
    /// upstream's names — eleven are <c>DiffFindBar</c>'s own children, and the other three are the two
    /// views' banner action buttons and the status strip's dismiss button — so removing a find-bar
    /// toggle is an ordinary product change that moves this count, and a floor at the population would
    /// fire on it with a message asserting a cause it does not establish. A name added upstream can
    /// only raise the count. What this catches is the stock set ceasing to match the library, which is
    /// the same zero <see cref="TemplatePartTests"/>' floor guards, so it sits well below the population
    /// like that one. Being a floor, it is satisfied by any LARGER population; the stock equality in the
    /// same test, over the same reading, is what stops that reading widening.
    /// </para>
    /// </remarks>
    private const int StockInspectedFloor = 8;

    /// <summary>
    /// A floor on the demo's share of the findings scan, well under the 53 it carries so that ordinary
    /// menu edits do not churn it — 50 of those 53 are <c>MenuItem</c>s in one file.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Asserted against the demo's own subject, not folded into a total.</b> As an addend in a
    /// floor over a sum, one term's growth buys silence for the other: measured, a library grown by 41
    /// with a demo at zero passed every assertion.
    /// </remarks>
    private const int DemoInspectedFloor = 41;

    /// <summary>The one root every scan below is built from.</summary>
    /// <remarks>
    /// ⛔ <b>Derived, not asserted, and that distinction is half the guard.</b> Two independently
    /// written roots can be edited apart, and no comparison between two <em>results</em> closes it:
    /// a count check passes whenever the findings scan is merely bigger, and a containment check
    /// passes whenever both are wrong together. The other half is the accounting equality in
    /// <see cref="Every_interactive_control_has_an_automation_name"/> — deriving the subject roots from
    /// this one is not enough on its own, because <c>Load(all.Root)</c> resolves perfectly well and
    /// makes a subject its own superset.
    /// </remarks>
    private static string AllRoot => RepoPaths.Source("src");

    /// <summary>Everything the findings assertion covers: the library and the demo.</summary>
    private static XamlScanContext ScanAll() => XamlScanContext.Load(AllRoot);

    /// <summary>The markup this repository ships, as a subject of the scan above.</summary>
    private static XamlScanContext LibraryWithin(XamlScanContext all) =>
        XamlScanContext.Load(Path.Combine(all.Root, typeof(SideBySideDiffView).Assembly.GetName().Name!));

    /// <summary>The demo's markup, the other subject, derived the same way.</summary>
    private static XamlScanContext DemoWithin(XamlScanContext all) =>
        XamlScanContext.Load(Path.Combine(all.Root, "DiffView.Demo"));

    /// <summary>Every public control this library ships, minus the reasoned exclusions.</summary>
    private static string[] DerivedControls() =>
    [
        .. typeof(SideBySideDiffView).Assembly
            .GetExportedTypes()
            .Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .Where(n => !Excluded.ContainsKey(n))
            .OrderBy(n => n, StringComparer.Ordinal),
    ];

    /// <summary>What the rule is given: the derived controls plus the framework forward cover.</summary>
    private static string[] ElementNames() =>
        [.. DerivedControls(), .. LiveFrameworkElements, .. ForwardCover];

    /// <summary>
    /// Every name that must inspect at least one element — the derived controls and the framework names
    /// declared live. The remainder is <see cref="ForwardCover"/>, which must inspect none.
    /// </summary>
    private static string[] MustCoverSomething() => [.. DerivedControls(), .. LiveFrameworkElements];

    [Fact]
    public void Every_interactive_control_has_an_automation_name()
    {
        // ⛔ Every count below reads one of these three subjects, each derived ONCE. A floor guards its
        // subject against narrowing and is satisfied by any larger population, so each floor's own
        // reading also sits in an equality — the library and the demo account for the whole scan. Floors
        // in one test and an equality in another, each deriving its own subject, leave every floor open
        // to widening one call at a time; here no count reaches an assertion without passing through one.
        XamlScanContext allScan = ScanAll();
        XamlScanContext librarySubject = LibraryWithin(allScan);
        XamlScanContext demoSubject = DemoWithin(allScan);

        // ⛔ ONE element set and ONE adopted instance, and every reading below is taken from them — the
        // floors, both accountings, the per-name coverage and the findings. Per-name coverage in a test
        // of its own read an instance of its own, so the instance the findings came from could be built
        // from a different list with every guard green: measured, an instance built without Menu, over a
        // demo whose menu bar had lost its name, passed the whole suite.
        string[] names = ElementNames();
        InteractiveAutomationNameRule rule = new(names);
        InteractiveAutomationNameRule stockRule = new();

        int stock = stockRule.Analyze(librarySubject).Inspected;
        Assert.True(
            stock >= StockInspectedFloor,
            $"XQ1002's stock element names inspected {stock} elements in the library's own markup, below "
            + $"the floor of {StockInspectedFloor}. Either the library subject has narrowed onto ground "
            + "with less markup, or the stock names have stopped matching it — and a clean result would "
            + "no longer mean the markup is clean.");

        int demo = rule.Analyze(demoSubject).Inspected;
        Assert.True(
            demo >= DemoInspectedFloor,
            $"The demo's own scan inspected {demo} elements, below its floor of {DemoInspectedFloor}. "
            + "Either the demo subject has narrowed onto ground with less markup, or that markup lost most "
            + "of its interactive elements.");

        // ⛔ This is what a derived root does not give you. Replace LibraryWithin's body with
        // Load(all.Root) and the root is still derived, still resolves, and the library subject becomes
        // the whole scan — measured green on every floor, because a superset clears any floor its subset
        // does. An equality is false of a subject that has swallowed the other one, and the direction
        // of the mismatch says which cause it is.
        int library = rule.Analyze(librarySubject).Inspected;
        XamlRuleResult result = rule.Analyze(allScan);
        int sum = library + demo;
        Assert.True(
            result.Inspected == sum,
            sum > result.Inspected
                ? $"The two subjects over-count the findings scan: library {library} + demo {demo} = {sum}, "
                  + $"against the scan's {result.Inspected}. A subject has WIDENED until it overlaps the "
                  + "other — which is what Load(all.Root) does, and the failure this assertion exists for."
                : $"The two subjects under-count the findings scan: library {library} + demo {demo} = {sum}, "
                  + $"against the scan's {result.Inspected}. Either a subject has NARROWED onto ground with "
                  + "less markup, or a THIRD markup-bearing project has appeared under src — which then "
                  + "needs its own subject and its own floor, exactly as the demo has.");

        // The same accounting for the stock names, because the stock floor reads its own count.
        int stockSum = stock + stockRule.Analyze(demoSubject).Inspected;
        int stockAll = stockRule.Analyze(allScan).Inspected;
        Assert.True(
            stockAll == stockSum,
            $"The stock names' readings do not account for the scan: library {stock} + demo "
            + $"{stockSum - stock} = {stockSum}, against the scan's {stockAll}. The stock floor's reading "
            + "has left the library subject, and a floor cannot see a subject that widened.");

        // ⛔ The derivation itself, checked against the markup. Every reading in this gate derives from
        // ElementNames(), so one edit narrowing DerivedControls() narrows all of them together: measured,
        // narrowing it from Control to TemplatedControl dropped the minimap and the connector gutter with
        // this gate green. The markup is the independent source — every element it instantiates from this
        // library's own namespace must be a control the derivation carries, or one Excluded names.
        string ourNamespace = "using:" + typeof(SideBySideDiffView).Namespace;
        List<string> oursInMarkup = [.. OurControlsInMarkup(allScan, ourNamespace)];
        Assert.True(
            oursInMarkup.Count > 0,
            $"No element in the markup is a control from {ourNamespace}, so the cross-check below reads "
            + "nothing and would pass whatever the derivation had dropped.");

        string[] derived = DerivedControls();
        List<string> unaccounted = [.. oursInMarkup.Where(n => !derived.Contains(n) && !Excluded.ContainsKey(n))];
        Assert.True(
            unaccounted.Count == 0,
            "The markup instantiates these controls of ours and the derived element set carries none of "
            + $"them, so nothing inspects their elements: {string.Join(", ", unaccounted)}. Either the "
            + $"derivation has narrowed, or the control belongs in {nameof(Excluded)} with its reason.");

        // ⭐ Per-name coverage, taken from the ADOPTED instance's own total. Built from any other list than
        // `names`, the instance makes some name appear to cover nothing, which is what trips here.
        foreach (string name in MustCoverSomething())
        {
            int without = Without(names, name, allScan);
            Assert.True(
                result.Inspected - without >= 1,
                $"'{name}' is in the element set and covers nothing: dropping it leaves the count at "
                + $"{result.Inspected}. Nothing here is asserting anything about it, so this gate would stay "
                + "green if it were removed from the set or if its elements lost their names. Either give "
                + $"it an element to guard, or add it to {nameof(Excluded)} with the reason.");
        }

        foreach (string name in ForwardCover)
        {
            int without = Without(names, name, allScan);
            Assert.True(
                result.Inspected - without == 0,
                $"'{name}' is declared forward cover — inert by design — but it now inspects "
                + $"{result.Inspected - without} element(s). It has become live, so it is no longer free: "
                + $"move it out of {nameof(ForwardCover)} so that something floors it.");
        }

        Assert.True(
            result.Findings.Count == 0,
            "Accessibility coverage regression — every interactive control needs AutomationProperties.Name:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }

    /// <summary>The count <paramref name="names"/> less <paramref name="drop"/> inspects over <paramref name="scan"/>.</summary>
    private static int Without(string[] names, string drop, XamlScanContext scan) =>
        new InteractiveAutomationNameRule([.. names.Where(n => n != drop)]).Analyze(scan).Inspected;

    /// <summary>
    /// The distinct local names of the elements in <paramref name="scan"/>'s markup that are controls of
    /// this library, instantiated under <paramref name="ourNamespace"/>.
    /// </summary>
    private static IEnumerable<string> OurControlsInMarkup(XamlScanContext scan, string ourNamespace)
    {
        Assembly library = typeof(SideBySideDiffView).Assembly;
        string prefix = typeof(SideBySideDiffView).Namespace + ".";
        return scan.ParsedFiles
            .SelectMany(f => f.Document!.Descendants())
            .Where(e => e.Name.NamespaceName == ourNamespace)
            .Select(e => e.Name.LocalName)
            .Where(n => library.GetType(prefix + n) is { } t && typeof(Control).IsAssignableFrom(t))
            .Distinct(StringComparer.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_name_declared_by_a_template_binding_is_not_empty_at_runtime()
    {
        // ⛔ The markup gate reads DECLARATIONS, so a name bound to a property is worth exactly whatever
        // sets that property — and every one of these properties is registered with string.Empty as its
        // default, so a missing setter is silent. Measured with the whole suite green each time: delete
        // the header-name fills from a view's RefreshStrings and its headers go unnamed; delete
        // DiffFindBar's eleven child-name fills and the query box, the four option toggles, the three
        // scope buttons and previous / next / close go unnamed; delete the status strip's DismissText
        // fill and the dismiss button goes unnamed whenever a failure is showing.
        //
        // ⭐ NOTHING HERE IS PICKED BY HAND. The control types are the markup gate's own —
        // FrameworkInteractiveElements plus ElementNames() — plus the exclusions, because an exclusion is
        // a claim about what the MARKUP SCAN cannot see and this reading sees exactly that. And what the
        // walk must REACH is derived from the library's markup too. A walk that asserts only "nothing
        // unnamed" passes whenever it reaches nothing: measured, dropping the two OpenFind() calls, or
        // pointing "ours" at the test assembly, let eleven unnamed controls through the whole suite.
        HashSet<string> declaredNames = [.. InteractiveAutomationNameRule.FrameworkInteractiveElements, .. ElementNames()];
        XamlScanContext library = LibraryWithin(ScanAll());
        List<(string Owner, string? Part)> declared = DeclaredInteractiveParts(library, declaredNames);

        // ⛔ A requirement derived from ground with no markup is empty, and an empty requirement is met by
        // a walk that reaches nothing — the zero every floor in this repository exists to refuse. The
        // markup gate's floors do not reach this subject, which is derived here, so it is refused here.
        Assert.True(
            declared.Count > 0,
            "The library's markup declares no interactive element by this test's reading, so the walk "
            + "below would be required to reach nothing. The requirement's subject has lost the markup.");

        // ⛔ The derivation is anchored to upstream's own count, so a reading that drifts from what the
        // rule inspects — a name set that lost the stock seventeen, a walk that stopped descending —
        // fails here instead of quietly shrinking what the rest of this test requires.
        int inspected = new InteractiveAutomationNameRule(ElementNames()).Analyze(library).Inspected;
        Assert.True(
            declared.Count == inspected,
            $"This test reads {declared.Count} interactive elements in the library's markup and XQ1002 "
            + $"inspects {inspected}. They must agree, or this gate requires less than the markup gate reads.");

        List<string> anonymous = [.. declared.Where(d => d.Part is null).Select(d => d.Owner)];
        Assert.True(
            anonymous.Count == 0,
            "These control themes declare an interactive element with no Name, so the walk below cannot "
            + "tell whether it reached it: " + string.Join(", ", anonymous));

        HashSet<string> interactive = [.. declaredNames, .. Excluded.Keys];
        HashSet<(string Owner, string Part)> visited = [];
        List<string> unnamed = [];
        (string left, string right) = CompositeHost.SmallFixture();

        // Every declared part is on screen in at least one of these four states: each view with its find
        // bar open, and each with a build that fails — which puts the Retry action on the banner and the
        // failure, with its dismiss button, on the strip. The coverage assertion at the end is what holds
        // the test to that, rather than this comment; an explicit failure on the strip in the first state
        // was measured redundant, the failing build already showing one.
        using (CompositeHost host = new())
        {
            host.Show();
            await host.LoadAsync(left, right);
            host.View.OpenFind();
            CompositeHost.Layout();
            Collect(host.View, interactive, visited, unnamed);
        }

        using (CompositeHost host = new())
        {
            host.Show();
            host.View.Builder = CompositeHost.FailingBuilder;
            await host.LoadAsync(left, right);
            CompositeHost.Layout();
            Collect(host.View, interactive, visited, unnamed);
        }

        using (InlineHost unified = new())
        {
            unified.Show();
            await unified.LoadAsync(left, right);
            unified.View.OpenFind();
            InlineHost.Layout();
            Collect(unified.View, interactive, visited, unnamed);
        }

        using (InlineHost unified = new())
        {
            unified.Show();
            unified.View.Builder = CompositeHost.FailingBuilder;
            await unified.LoadAsync(left, right);
            InlineHost.Layout();
            Collect(unified.View, interactive, visited, unnamed);
        }

        Assert.True(
            unnamed.Count == 0,
            "These controls are on screen and carry no automation name. Each declares "
            + "AutomationProperties.Name in markup as a {TemplateBinding …}, so what is missing is whatever "
            + "fills that property — which the markup scan cannot see:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, unnamed.Distinct(StringComparer.Ordinal)
                .OrderBy(u => u, StringComparer.Ordinal)
                .Select(u => "  " + u)));

        List<string> unreached =
        [
            .. declared
                .Where(d => !visited.Contains((d.Owner, d.Part!)))
                .Select(d => $"{d.Part} in {d.Owner}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(u => u, StringComparer.Ordinal),
        ];
        Assert.True(
            unreached.Count == 0,
            "The library's markup declares these interactive parts and the walk never checked them on "
            + "screen, so a missing name on any of them would pass. Bring each one on screen in one of the "
            + "states above; do not take it out of the requirement:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, unreached.Select(u => "  " + u)));
    }

    /// <summary>
    /// Every element in <paramref name="scan"/>'s markup whose local name is in <paramref name="names"/>,
    /// as the control theme that declares it and the Name it is declared with.
    /// </summary>
    private static List<(string Owner, string? Part)> DeclaredInteractiveParts(
        XamlScanContext scan, HashSet<string> names)
    {
        List<(string Owner, string? Part)> parts = [];
        foreach (XamlFile file in scan.ParsedFiles)
        {
            foreach (XElement element in file.Document!.Descendants())
            {
                if (!names.Contains(element.Name.LocalName))
                {
                    continue;
                }

                string? target = element.Ancestors()
                    .FirstOrDefault(a => a.Name.LocalName == "ControlTheme")
                    ?.Attribute("TargetType")?.Value;
                string owner = target is null ? "no control theme" : target[(target.IndexOf(':') + 1)..];
                string? part = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
                parts.Add((owner, part));
            }
        }

        return parts;
    }

    /// <summary>
    /// Walks what <paramref name="root"/> realises. Every control of ours that the markup gate treats as
    /// interactive and that is on screen is recorded in <paramref name="visited"/> as the (owner, part)
    /// pair it was reached as, and described in <paramref name="unnamed"/> when it has no automation name.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>"Ours" is the runtime analogue of the markup scan's root</b>: the control's own type comes
    /// from this library's assembly, or its templated parent's does, which is what a control realised
    /// from one of our control themes looks like. Without it the walk reaches AvaloniaEdit's template and
    /// the framework's chrome, and this gate would be asserting other people's markup.
    /// <para>
    /// ⛔ <b>Only what is on screen is checked</b>, because a control that is realised but hidden is not
    /// announced: the error banner's action button sits collapsed with an empty name whenever a build has
    /// nothing to say, and that is correct. On its own that rule is a blind spot — it once hid the dismiss
    /// button, which is visible only while a failure shows. It is safe only because the caller must bring
    /// every declared part on screen at least once, and asserts that it did.
    /// </para>
    /// </remarks>
    private static void Collect(
        Visual root,
        HashSet<string> interactive,
        HashSet<(string Owner, string Part)> visited,
        List<string> unnamed)
    {
        Assembly ours = typeof(SideBySideDiffView).Assembly;

        foreach (Control control in root.GetVisualDescendants().OfType<Control>())
        {
            Control? owner = control.TemplatedParent as Control;
            if (!interactive.Contains(control.GetType().Name)
                || (control.GetType().Assembly != ours && owner?.GetType().Assembly != ours)
                || !control.IsEffectivelyVisible)
            {
                continue;
            }

            string ownerName = owner?.GetType().Name ?? "no template";
            string part = control.Name is { Length: > 0 } name ? name : "(no Name)";
            visited.Add((ownerName, part));

            if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
            {
                unnamed.Add($"{control.GetType().Name} '{part}' in {ownerName}");
            }
        }
    }

    [AvaloniaFact]
    public void Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check()
    {
        // ⛔ Without this, Excluded is the hand-written list all over again, one level up: adding a
        // control to it drops that control's elements out of the findings scan and no floor here can
        // see it — not the stock one, which counts other names; not the demo's, for a library
        // control; not the accounting equality, which still balances. Measured against
        // DiffPaneHeader, whose exclusion would have cost four elements in silence.
        //
        // So an exclusion has to earn itself. Only two reasons can be true: the control names itself
        // somewhere this scan cannot read, or nothing in our markup instantiates it, in which case
        // excluding it costs nothing. Both are checkable, and this checks whichever applies.
        XamlScanContext allScan = ScanAll();
        string[] names = ElementNames();
        int baseline = new InteractiveAutomationNameRule(names).Analyze(allScan).Inspected;

        Assert.NotEmpty(Excluded);

        foreach ((string name, string reason) in Excluded)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(reason),
                $"'{name}' is excluded with no reason given. An exclusion is a decision and has to "
                + "read as one.");

            // An assertion rather than a `?? throw`, so the mutation harness can attribute a failure to it:
            // it counts only assertions as guards, and a guard it cannot see is one it cannot prove.
            Type? type = typeof(SideBySideDiffView).Assembly.GetExportedTypes().SingleOrDefault(t => t.Name == name);
            Assert.True(
                type is not null,
                $"'{name}' is excluded but is not a public type of this library — a stale entry or a typo, "
                + "either of which excludes nothing and hides that it excludes nothing.");

            int elements = new InteractiveAutomationNameRule([.. names, name]).Analyze(allScan).Inspected - baseline;
            if (elements == 0)
            {
                // Nothing of ours instantiates it, so the exclusion covers no markup either way.
                continue;
            }

            Control control = (Control)Activator.CreateInstance(type!)!;
            string? automationName = AutomationProperties.GetName(control);

            Assert.False(
                string.IsNullOrWhiteSpace(automationName),
                $"'{name}' is excluded from the markup scan, but {elements} element(s) of ours "
                + "instantiate it and a fresh one carries no automation name. Nothing is checking "
                + "those elements. Either it names itself — which is the only reason this exclusion "
                + "can stand — or it belongs back in the derived set.");
        }
    }
}
