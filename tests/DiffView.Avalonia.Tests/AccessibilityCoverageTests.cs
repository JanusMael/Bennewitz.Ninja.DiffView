using System.Reflection;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Logging;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Bennewitz.Ninja.DiffView.Tests.Composite;
using Bennewitz.Ninja.DiffView.Tests.Inline;
using Bennewitz.Ninja.DiffView.Tests.Viewer;
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
/// analysis now comes from <c>Bennewitz.Ninja.XamlQuality</c>, whose <c>BNXQ1002</c> holds seventeen
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
/// ⚠ <b>What the name gate does not establish.</b> It asserts that an interactive control's name is
/// declared <em>in markup</em>. Whether a harness or a screen reader can reach the control is a question
/// for its automation peer, which <see cref="Every_themed_control_has_a_peer_of_its_own"/> and
/// <see cref="Every_control_of_ours_on_screen_is_a_control_element_of_its_type"/> ask, since plan 00026.
/// A name is translated, too, so what a harness finds a part by is its <c>AutomationId</c>, which
/// <see cref="Every_part_the_library_places_carries_an_explicit_automation_id"/>,
/// <see cref="Every_id_the_library_declares_is_found_once_within_the_part_that_owns_it"/> and
/// <see cref="Every_entry_of_every_menu_a_surface_opens_carries_an_id_unique_within_it"/> ask — and which id
/// each is, <see cref="Every_id_a_harness_searches_for_is_the_one_the_fixture_pins"/>.
/// And it cannot see a control that names itself in code, which is why <see cref="Excluded"/> exists and
/// why the claim each exclusion makes is asserted separately below.
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
            + "eleven child names it sets there. BNXQ1002 reads markup declarations and cannot see a "
            + "runtime one, so requiring an attribute here would mean two sources for one string.",
    };

    /// <summary>
    /// Framework element names the stock seventeen leave out that are <b>live here</b> — each matches an
    /// element in this repository's markup and is floored one at a time, exactly like a derived control.
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>Menu</c> is here because the demo's menu bar is interactive and nothing else covers it.</b>
    /// Its fifty-three <c>MenuItem</c>s are covered, because <c>MenuItem</c> <em>is</em> one of the stock
    /// seventeen — which is exactly how an unnamed bar can hide among them. Proven by arithmetic rather
    /// than assumed: the demo holds exactly fifty-three <c>MenuItem</c>s and the stock set inspects
    /// exactly fifty-three of its elements, so it would be fifty-four if <c>Menu</c> were among them.
    /// </remarks>
    private static readonly string[] LiveFrameworkElements = [nameof(Menu)];

    /// <summary>
    /// Framework element names the stock seventeen leave out that match <b>nothing</b> here. Neither is
    /// ours, and their inertness is asserted rather than claimed.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>A name is not a gate.</b> An inert <em>rule</em> reports zero and reads as coverage; an
    /// inert <em>name</em> costs nothing and catches the first use. <c>BNXQ1001</c> is declined for the
    /// former reason, which is also what makes <c>Expander</c> safe to carry here — the two would
    /// otherwise report one defect twice, which is why <c>BNXQ1002</c>'s stock set omits it.
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
    /// population of 15.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>It floors the stock names only, because the derived names are floored one at a time
    /// instead</b>, by the per-name coverage in <see cref="Every_interactive_control_has_an_automation_name"/>.
    /// A single number over the whole set is what lets a name be deleted silently.
    /// <para>
    /// ⛔ <b>What it guards is a zero, not a part count.</b> The fifteen are OUR elements matching
    /// upstream's names — eleven are <c>DiffFindBar</c>'s own children, and the other four are the three
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
    /// A floor on the demo's share of the findings scan, well under the 57 it carries so that ordinary
    /// menu edits do not churn it — 53 of those 57 are <c>MenuItem</c>s in one file.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Asserted against the demo's own subject, not folded into a total.</b> As an addend in a
    /// floor over a sum, one term's growth buys silence for the other: measured, a library grown by 41
    /// with a demo at zero passed every assertion.
    /// </remarks>
    private const int DemoInspectedFloor = 41;

    /// <summary>
    /// A floor on what <c>BNXQ1006</c> inspects over the library's markup: 3, against the 7 controls the
    /// library themes today.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>What it guards is a zero, like every floor in this file.</b> The rule checks the controls the
    /// scanned markup themes and the scanned assembly defines, so a scan that has lost the markup inspects
    /// nothing and reports clean. The rule's other blindness — no assembly at all — is its
    /// <see cref="XamlRuleResult.Skipped"/> list's to show, and is asserted before this.
    /// </remarks>
    private const int PeerInspectedFloor = 3;

    /// <summary>
    /// A floor on what <c>BNXQ1007</c>'s stock names inspect over the library's markup: 8, against the 15
    /// elements they match — the same fifteen, and the same zero guarded, as <see cref="StockInspectedFloor"/>.
    /// </summary>
    private const int IdStockInspectedFloor = 8;

    /// <summary>
    /// The control type each control of ours is to a person — plan 00026's table, and the one written
    /// expectation in the peer walk. UIA's control-pattern mapping requires no pattern of any of them.
    /// </summary>
    private static readonly Dictionary<string, AutomationControlType> ExpectedControlTypes = new(StringComparer.Ordinal)
    {
        [nameof(SideBySideDiffView)] = AutomationControlType.Group,
        [nameof(DiffViewer)] = AutomationControlType.Group,
        [nameof(InlineDiffView)] = AutomationControlType.Group,
        [nameof(DiffPanePresenter)] = AutomationControlType.Edit,
        [nameof(DiffPaneHeader)] = AutomationControlType.Header,
        [nameof(DiffFindBar)] = AutomationControlType.ToolBar,
        [nameof(DiffStatusStrip)] = AutomationControlType.StatusBar,
        [nameof(DiffMinimap)] = AutomationControlType.ScrollBar,
        [nameof(ChangeConnectorGutter)] = AutomationControlType.Custom,
        [nameof(DiffLineNumberMargin)] = AutomationControlType.Custom,
        [nameof(ChangeMarkerMargin)] = AutomationControlType.Custom,
    };

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
            $"BNXQ1002's stock element names inspected {stock} elements in the library's own markup, below "
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
            $"This test reads {declared.Count} interactive elements in the library's markup and BNXQ1002 "
            + $"inspects {inspected}. They must agree, or this gate requires less than the markup gate reads.");

        List<string> anonymous = [.. declared.Where(d => d.Part is null).Select(d => d.Owner)];
        Assert.True(
            anonymous.Count == 0,
            "These control themes declare an interactive element with no Name, so the walk below cannot "
            + "tell whether it reached it: " + string.Join(", ", anonymous));

        HashSet<string> interactive = [.. declaredNames, .. Excluded.Keys];
        HashSet<(string Owner, string Part)> visited = [];
        List<string> unnamed = [];
        await WalkEveryState(root => Collect(root, interactive, visited, unnamed));

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
            + "walk's states; do not take it out of the requirement:"
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

    /// <summary>
    /// Brings every part the library's markup declares on screen at least once, handing each view to
    /// <paramref name="visit"/> in six states: each view with its find bar open — the viewer, having none,
    /// simply loaded — and each with a build that fails, which puts the Retry action on the banner and the
    /// failure, with its dismiss button, on the strip.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>One walk, whatever reads it.</b> Every reading that takes it asserts that it reached what it
    /// requires, rather than trusting this list, so a state dropped here fails each reading that needed
    /// it; a second walk would be a second list of states to keep complete. An explicit failure on the
    /// strip in the first state was measured redundant, the failing build already showing one.
    /// </remarks>
    private static async Task WalkEveryState(Action<Visual> visit)
    {
        (string left, string right) = CompositeHost.SmallFixture();

        using (CompositeHost host = new())
        {
            host.Show();
            await host.LoadAsync(left, right);
            host.View.OpenFind();
            CompositeHost.Layout();
            visit(host.View);
        }

        using (CompositeHost host = new())
        {
            host.Show();
            host.View.Builder = CompositeHost.FailingBuilder;
            await host.LoadAsync(left, right);
            CompositeHost.Layout();
            visit(host.View);
        }

        using (InlineHost unified = new())
        {
            unified.Show();
            await unified.LoadAsync(left, right);
            unified.View.OpenFind();
            InlineHost.Layout();
            visit(unified.View);
        }

        using (InlineHost unified = new())
        {
            unified.Show();
            unified.View.Builder = CompositeHost.FailingBuilder;
            await unified.LoadAsync(left, right);
            InlineHost.Layout();
            visit(unified.View);
        }

        using (ViewerHost viewer = new())
        {
            viewer.Show();
            await viewer.LoadAsync(left, right);
            ViewerHost.Layout();
            visit(viewer.View);
        }

        using (ViewerHost viewer = new())
        {
            viewer.Show();
            viewer.View.Builder = CompositeHost.FailingBuilder;
            await viewer.LoadAsync(left, right);
            ViewerHost.Layout();
            visit(viewer.View);
        }
    }

    [Fact]
    public void Every_themed_control_has_a_peer_of_its_own()
    {
        // ⛔ Skipped FIRST, and asserted EMPTY. BNXQ1006 reads each peer from compiled code, so a scan with
        // no assembly skips every themed control and reports clean — measured at 928: 0 inspected, 0
        // findings, 8 skipped. Unlike BNXQ1003's, nothing here is skipped legitimately: a control that did
        // not load, or a name two scanned types carry, is a scan that cannot answer, not a control with
        // nothing to check.
        XamlRuleResult result = new CustomControlPeerRule().Analyze(PeerScan());

        Assert.True(
            result.Skipped.Count == 0,
            "BNXQ1006 read no peer for these themed controls: "
            + string.Join(", ", result.Skipped.Select(s => s.Subject))
            + ". Handed no assembly it skips every one; otherwise a control did not load, or two scanned "
            + "types carry its name.");

        Assert.True(
            result.Inspected >= PeerInspectedFloor,
            $"BNXQ1006 inspected {result.Inspected} themed controls, below the floor of {PeerInspectedFloor}. "
            + "The floor sits well under the population, so no control was merely added or removed: the scan "
            + "has lost the markup whose themes it checks.");

        Assert.True(
            result.Findings.Count == 0,
            "A themed control has no automation peer of its own, so a search of the control view cannot find "
            + "it, its name or its id:" + Environment.NewLine
            + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }

    [AvaloniaFact]
    public async Task Every_control_of_ours_on_screen_is_a_control_element_of_its_type()
    {
        // ⭐ NOTHING HERE IS PICKED BY HAND BUT THE TYPES. What the walk must reach is every concrete control
        // type this library defines, internal ones included — the two margins among them — and the ones
        // the library's markup themes are anchored to what BNXQ1006 inspects, so a derivation that drifts
        // from the rule's own reading fails here before it can quietly shrink what the rest of this test
        // asks.
        TestLogSink.Instance.Clear();
        string[] controls = ConcreteControls();
        string[] themed = ThemedControls();
        int inspected = new CustomControlPeerRule().Analyze(PeerScan()).Inspected;
        Assert.True(
            themed.Length == inspected,
            $"This test reads {themed.Length} themed controls of ours in the library's markup and BNXQ1006 "
            + $"inspects {inspected}. They must agree, or the walk requires less than the rule reads.");

        Assert.True(
            controls.SequenceEqual(ExpectedControlTypes.Keys.Order(StringComparer.Ordinal)),
            $"The controls of ours are {string.Join(", ", controls)}, and {nameof(ExpectedControlTypes)} names "
            + $"{string.Join(", ", ExpectedControlTypes.Keys.Order(StringComparer.Ordinal))}. Every control a "
            + "harness can reach has the control type it is to a person written down, and nothing else is.");

        HashSet<string> reached = [];
        List<string> notControlElements = [];
        List<string> wrongTypes = [];
        List<string> withPatterns = [];
        List<string> missedHits = [];
        List<string> misparented = [];
        await WalkEveryState(root => CollectPeers(
            root, controls, reached, notControlElements, wrongTypes, withPatterns, missedHits, misparented));

        Assert.True(
            notControlElements.Count == 0,
            "These controls are on screen and their peers are not control elements, so a search of the "
            + "control view never finds them: " + string.Join(", ", notControlElements));

        Assert.True(
            wrongTypes.Count == 0,
            "These peers report a control type other than the one their control is to a person:"
            + Environment.NewLine + string.Join(Environment.NewLine, wrongTypes.Select(w => "  " + w)));

        Assert.True(
            withPatterns.Count == 0,
            "These peers advertise a pattern, and plan 00026 advertises none — a pattern a peer does not "
            + "honour is the defect rule 4 exists for:" + Environment.NewLine
            + string.Join(Environment.NewLine, withPatterns.Select(w => "  " + w)));

        Assert.True(
            missedHits.Count == 0,
            "A harness acts at the bounds a peer reports, and a hit-test at the centre of these lands outside "
            + "the control:" + Environment.NewLine + string.Join(Environment.NewLine, missedHits.Select(m => "  " + m)));

        Assert.True(
            misparented.Count == 0,
            "A harness finds a margin within its pane, so in the control view a margin's parent is its pane, "
            + "and these sit under something else:" + Environment.NewLine
            + string.Join(Environment.NewLine, misparented.Select(m => "  " + m)));

        List<string> unreached = [.. controls.Where(t => !reached.Contains(t))];
        Assert.True(
            unreached.Count == 0,
            "The walk never had these controls of ours on screen, so nothing above was asked of them: "
            + string.Join(", ", unreached) + ". Bring each on screen in one of the walk's states.");

        // The walk draws a frame in every state, and a test that renders asserts a clean log (AGENTS.md §5).
        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    /// <summary>
    /// Every concrete control type this library defines, internal ones included, by name, in ordinal order.
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>GetTypes</c>, not <c>GetExportedTypes</c>.</b> The pane's two margins are internal, and a
    /// harness acts on what they draw; the exported types alone would leave them out of every requirement
    /// this derivation feeds.
    /// </remarks>
    private static string[] ConcreteControls() =>
    [
        .. typeof(SideBySideDiffView).Assembly.GetTypes()
            .Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// Every concrete control type of this library that the library's markup gives a
    /// <c>ControlTheme</c>, by name, in ordinal order.
    /// </summary>
    private static string[] ThemedControls()
    {
        HashSet<string> ours = [.. ConcreteControls()];

        return
        [
            .. LibraryWithin(ScanAll()).ParsedFiles
                .SelectMany(f => f.Document!.Descendants())
                .Where(e => e.Name.LocalName == "ControlTheme")
                .Select(e => e.Attribute("TargetType")?.Value)
                .OfType<string>()
                .Select(t => t[(t.IndexOf(':') + 1)..])
                .Where(ours.Contains)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>The library's markup, with the library's assembly — where BNXQ1006 reads each peer from.</summary>
    private static XamlScanContext PeerScan() =>
        LibraryWithin(ScanAll()).WithAssemblies(typeof(SideBySideDiffView).Assembly);

    /// <summary>
    /// Asks the peer of every control under <paramref name="root"/>, the root included, whose type
    /// <paramref name="required"/> names and which is on screen: whether it is a control element, of the
    /// type <see cref="ExpectedControlTypes"/> gives it, advertising no pattern, at bounds whose centre a
    /// hit-test finds inside the control — and, for a margin, whether its parent in the control view is its
    /// pane.
    /// </summary>
    private static void CollectPeers(
        Visual root,
        string[] required,
        HashSet<string> reached,
        List<string> notControlElements,
        List<string> wrongTypes,
        List<string> withPatterns,
        List<string> missedHits,
        List<string> misparented)
    {
        TopLevel? top = TopLevel.GetTopLevel(root);

        // ⛔ A hit-test reads the scene the renderer last composed, not the layout. With the find bar just
        // opened and no frame drawn since, its centre hit-tested to the pane that had been there — measured.
        // A harness on a desktop acts on a window that has drawn, so a frame is drawn first here too.
        (top as Window)?.CaptureRenderedFrame()?.Dispose();

        foreach (Control control in root.GetSelfAndVisualDescendants().OfType<Control>())
        {
            string type = control.GetType().Name;
            if (!required.Contains(type) || control.GetType().Assembly != typeof(SideBySideDiffView).Assembly)
            {
                continue;
            }

            if (!control.IsEffectivelyVisible)
            {
                continue;
            }

            reached.Add(type);
            string where = control.Name is { Length: > 0 } name ? $"{type} '{name}'" : type;
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(control);

            if (!peer.IsControlElement())
            {
                notControlElements.Add(where);
                continue;
            }

            if (peer.GetAutomationControlType() != ExpectedControlTypes[type])
            {
                wrongTypes.Add($"{where}: {peer.GetAutomationControlType()}, where it is a {ExpectedControlTypes[type]}");
            }

            string[] patterns =
            [
                .. peer.GetType().GetInterfaces()
                    .Where(i => i.Namespace == typeof(IInvokeProvider).Namespace)
                    .Select(i => i.Name),
            ];
            if (patterns.Length > 0)
            {
                withPatterns.Add($"{where}: {string.Join(", ", patterns)}");
            }

            Rect bounds = peer.GetBoundingRectangle();
            IInputElement? hit = top?.InputHitTest(bounds.Center);
            if (hit is not Visual visual || (visual != control && !control.IsVisualAncestorOf(visual)))
            {
                missedHits.Add($"{where}: the centre of {bounds} lands on {hit?.GetType().Name ?? "nothing"}");
            }

            // A harness finds a margin within its pane — the guide's §5, step 4 — so in the control view the
            // margin's parent must be the pane, and not whatever holds the margins in the text area's template.
            if (control is DiffMargin margin)
            {
                Control? parent = ControlViewParentOf(peer);
                if (parent != margin.Owner)
                {
                    misparented.Add($"{where}: under {parent?.GetType().Name ?? "nothing"}, not its pane");
                }
            }
        }
    }

    /// <summary>
    /// The control whose peer is <paramref name="peer"/>'s nearest ancestor in the control view — what a
    /// UI Automation client walking that view reports as its parent.
    /// </summary>
    private static Control? ControlViewParentOf(AutomationPeer peer)
    {
        for (AutomationPeer? parent = peer.GetParent(); parent is not null; parent = parent.GetParent())
        {
            if (parent.IsControlElement())
            {
                return (parent as ControlAutomationPeer)?.Owner;
            }
        }

        return null;
    }

    [Fact]
    public void Every_part_the_library_places_carries_an_explicit_automation_id()
    {
        // ⭐ A derivation of its own, not BNXQ1002's. That gate reads the library and the demo together, and
        // over the library alone four of its names would cover nothing — the three views and Menu are placed
        // only by the demo, whose chrome keeps its names and carries no ids. So this one is given every
        // concrete control type the library defines: each the library's markup places must cover an element
        // there, and the rest — the views, which only a host places, and the margins, which code builds —
        // must cover none, so that a type the markup starts to place moves from one side to the other.
        //
        // ⛔ No Expander forward cover and no Skipped guard, and that is measured rather than overlooked:
        // InteractiveAutomationIdRule adds Expander itself and has no skip path at 928, so neither could fail.
        //
        // ⚠ The scan is `markup`, not `library`: the name gate's walk declares `library` the same way, and a
        // second declaration would give its markup-free-ground mutation a second match.
        XamlScanContext markup = LibraryWithin(ScanAll());
        string[] names = ConcreteControls();
        InteractiveAutomationIdRule rule = new(names);

        int stock = new InteractiveAutomationIdRule().Analyze(markup).Inspected;
        Assert.True(
            stock >= IdStockInspectedFloor,
            $"BNXQ1007's stock names inspected {stock} elements in the library's markup, below the floor of "
            + $"{IdStockInspectedFloor}. The scan has lost the markup it checks, and a clean result would no "
            + "longer mean every part carries an id.");

        XamlRuleResult result = rule.Analyze(markup);
        string markupNamespace = "using:" + typeof(DiffViewer).Namespace;
        HashSet<string> placed = [.. OurControlsInMarkup(markup, markupNamespace)];

        foreach (string name in names.Where(placed.Contains))
        {
            int without = WithoutId(names, name, markup);
            Assert.True(
                result.Inspected - without >= 1,
                $"'{name}' is placed by the library's markup and covers nothing in BNXQ1007's reading: dropping "
                + $"it leaves the count at {result.Inspected}. The adopted instance is built from another list, "
                + "or the rule has stopped matching the element.");
        }

        foreach (string name in names.Where(n => !placed.Contains(n)))
        {
            int without = WithoutId(names, name, markup);
            Assert.True(
                result.Inspected - without == 0,
                $"'{name}' is not placed by the library's markup by this test's reading, yet BNXQ1007 inspects "
                + $"{result.Inspected - without} element(s) of it there. The two readings of the markup disagree "
                + "about what the library places.");
        }

        Assert.True(
            result.Findings.Count == 0,
            "A part the library's markup places carries no explicit AutomationId, so a harness can find it only "
            + "by an id a framework derives from its Name, which a rename moves, or by its translated name:"
            + Environment.NewLine + string.Join(Environment.NewLine, result.Findings.Select(f => "  " + f)));
    }

    /// <summary>The count BNXQ1007, given <paramref name="names"/> less <paramref name="drop"/>, inspects over <paramref name="scan"/>.</summary>
    private static int WithoutId(string[] names, string drop, XamlScanContext scan) =>
        new InteractiveAutomationIdRule([.. names.Where(n => n != drop)]).Analyze(scan).Inspected;

    [AvaloniaFact]
    public async Task Every_id_the_library_declares_is_found_once_within_the_part_that_owns_it()
    {
        // ⭐ The requirement is derived: every AutomationId the library's markup declares, with the control
        // theme that declares it. Every element BNXQ1007 inspects carries exactly one id, so the count is
        // anchored to the rule's, and a derivation that drifts fails here first.
        //
        // ⛔ One subject for both, and an empty one refused before they are compared: over ground with no
        // markup the two agree at zero, and a requirement of nothing is met by a walk that finds nothing.
        XamlScanContext declaring = LibraryWithin(ScanAll());
        List<(string Owner, string Id)> declared = DeclaredIds(declaring);
        Assert.True(
            declared.Count > 0,
            "The library's markup declares no AutomationId by this test's reading, so the walk below would be "
            + "required to find nothing. The requirement's subject has lost the markup.");

        int inspected = new InteractiveAutomationIdRule(ConcreteControls()).Analyze(declaring).Inspected;
        Assert.True(
            declared.Count == inspected,
            $"This test reads {declared.Count} ids in the library's markup and BNXQ1007 inspects {inspected} "
            + "elements. They must agree, or the walk requires fewer ids than the markup places.");

        HashSet<(string Owner, string Id)> found = [];
        List<string> withoutId = [];
        List<string> twice = [];
        await WalkEveryState(root => CollectIds(root, found, withoutId, twice));

        Assert.True(
            withoutId.Count == 0,
            "These controls of ours are on screen with no explicit AutomationId, so a harness finds them only "
            + "by a derived id or a translated name: " + string.Join(", ", withoutId.Distinct(StringComparer.Ordinal)));

        Assert.True(
            twice.Count == 0,
            "A harness finds a part with one search within the part that owns it, and these ids are found more "
            + "than once there:" + Environment.NewLine + string.Join(Environment.NewLine, twice.Select(t => "  " + t)));

        List<string> unfound = [.. declared.Where(d => !found.Contains(d)).Select(d => $"{d.Id} in {d.Owner}")];
        Assert.True(
            unfound.Count == 0,
            "The library's markup declares these ids and the walk never found them in UI Automation's control "
            + "view: " + string.Join(", ", unfound) + ". Bring each part on screen in one of the walk's states.");

        // What is not on screen is not in the tree: a part switched off, closed or with nothing to say is not
        // found at all, where one merely shrunk or clipped would be. All three views, because the unified
        // view hides its chrome through code of its own. ⛔ The parts left on come first, because a reading
        // that reached nothing finds none of the hidden ones either.
        Dictionary<string, List<string>> offState = await IdsFoundWithTheOptionalPartsOff();
        Dictionary<string, string[]> leftOn = new(StringComparer.Ordinal)
        {
            [nameof(SideBySideDiffView)] = ["LeftPane", "RightPane", "Gutter"],
            [nameof(InlineDiffView)] = ["Pane"],
            [nameof(DiffViewer)] = ["LeftPane", "RightPane", "Gutter"],
        };
        List<string> leftOnYetMissing =
        [
            .. leftOn.SelectMany(v => v.Value
                .Where(id => !offState.GetValueOrDefault(v.Key, []).Contains(id))
                .Select(id => $"{id} in {v.Key}")),
        ];
        Assert.True(
            leftOnYetMissing.Count == 0,
            "With the optional parts switched off, the control view did not show these parts, which stay on: "
            + string.Join(", ", leftOnYetMissing) + ". The reading reached nothing, so the absence of the hidden "
            + "ones would mean nothing.");

        string[] switchedOff = ["Minimap", "LeftHeader", "RightHeader", "StatusStrip", "FindBar", "BannerAction"];
        List<string> hiddenYetFound =
        [
            .. offState.SelectMany(v => switchedOff.Where(v.Value.Contains).Select(id => $"{id} in {v.Key}")),
        ];
        Assert.True(
            hiddenYetFound.Count == 0,
            "These parts are switched off, closed or empty, and a harness still finds them: "
            + string.Join(", ", hiddenYetFound));
    }

    /// <summary>
    /// Every AutomationId <paramref name="scan"/>'s markup declares, with the local name of the control theme
    /// that declares it — which is the control a part's template owner is.
    /// </summary>
    private static List<(string Owner, string Id)> DeclaredIds(XamlScanContext scan)
    {
        List<(string Owner, string Id)> ids = [];
        foreach (XamlFile file in scan.ParsedFiles)
        {
            foreach (XElement element in file.Document!.Descendants())
            {
                string? id = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.AutomationId")?.Value;
                string? owner = element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "ControlTheme")?.Attribute("TargetType")?.Value;
                if (id is not null && owner is not null)
                {
                    ids.Add((owner[(owner.IndexOf(':') + 1)..], id));
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Reads UI Automation's control view under <paramref name="root"/>. Every control of ours in it but the
    /// view itself must carry an explicit id, and each explicit id is counted within the part that owns it:
    /// the control whose template placed it, or a margin's pane.
    /// </summary>
    private static void CollectIds(
        Visual root,
        HashSet<(string Owner, string Id)> found,
        List<string> withoutId,
        List<string> twice)
    {
        Assembly library = typeof(DiffViewer).Assembly;
        Dictionary<(Control Owner, string Id), int> counts = [];

        foreach (AutomationPeer peer in ControlView(ControlAutomationPeer.CreatePeerForElement((Control)root)))
        {
            if (peer is not ControlAutomationPeer { Owner: var control })
            {
                continue;
            }

            string? id = AutomationProperties.GetAutomationId(control);
            if (string.IsNullOrEmpty(id))
            {
                if (control != root && control.GetType().Assembly == library)
                {
                    withoutId.Add(control.Name is { Length: > 0 } name ? $"{control.GetType().Name} '{name}'" : control.GetType().Name);
                }

                continue;
            }

            if (((control as DiffMargin)?.Owner ?? control.TemplatedParent) is not Control owner
                || owner.GetType().Assembly != library)
            {
                continue;
            }

            found.Add((owner.GetType().Name, id));
            counts[(owner, id)] = counts.GetValueOrDefault((owner, id)) + 1;
        }

        twice.AddRange(counts.Where(c => c.Value > 1).Select(c => $"{c.Key.Id} ×{c.Value} in {c.Key.Owner.GetType().Name}"));
    }

    /// <summary>
    /// Every peer UI Automation's control view shows under <paramref name="peer"/>, the peer itself included —
    /// what a harness searching that view can find. The peer tree already leaves out what is not visible.
    /// </summary>
    private static IEnumerable<AutomationPeer> ControlView(AutomationPeer peer)
    {
        if (peer.IsControlElement())
        {
            yield return peer;
        }

        foreach (AutomationPeer child in peer.GetChildren())
        {
            foreach (AutomationPeer below in ControlView(child))
            {
                yield return below;
            }
        }
    }

    /// <summary>
    /// Every id each view's control view shows, by view, with its optional parts off: the map where it has
    /// one, the headers and the strip switched off, the find bar closed and a build with nothing to say.
    /// </summary>
    private static async Task<Dictionary<string, List<string>>> IdsFoundWithTheOptionalPartsOff()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        Dictionary<string, List<string>> found = new(StringComparer.Ordinal);

        using (CompositeHost host = new())
        {
            host.View.ShowMinimap = false;
            host.View.ShowHeaders = false;
            host.View.ShowStatusStrip = false;
            host.Show();
            await host.LoadAsync(left, right);
            CompositeHost.Layout();
            found[nameof(SideBySideDiffView)] = IdsShownUnder(host.View);
        }

        using (InlineHost unified = new())
        {
            unified.View.ShowHeaders = false;
            unified.View.ShowStatusStrip = false;
            unified.Show();
            await unified.LoadAsync(left, right);
            InlineHost.Layout();
            found[nameof(InlineDiffView)] = IdsShownUnder(unified.View);
        }

        using (ViewerHost viewer = new())
        {
            viewer.View.ShowMinimap = false;
            viewer.View.ShowHeaders = false;
            viewer.View.ShowStatusStrip = false;
            viewer.Show();
            await viewer.LoadAsync(left, right);
            ViewerHost.Layout();
            found[nameof(DiffViewer)] = IdsShownUnder(viewer.View);
        }

        return found;
    }

    /// <summary>Every id UI Automation's control view shows under <paramref name="view"/>.</summary>
    private static List<string> IdsShownUnder(Control view) =>
        [.. ControlView(ControlAutomationPeer.CreatePeerForElement(view)).Select(p => p.GetAutomationId()).OfType<string>()];

    [AvaloniaFact]
    public async Task Every_entry_of_every_menu_a_surface_opens_carries_an_id_unique_within_it()
    {
        // ⭐ The surfaces are derived — every DiffPaneRegion, on each pane, and the header — and the entries
        // are whatever each surface's builder produced, read back through UI Automation the way a harness
        // reads them.
        TestLogSink.Instance.Clear();
        (Dictionary<string, string?[]> menus, List<string> silent) = await ReadEveryMenu();

        Assert.True(
            silent.Count == 0,
            "These surfaces opened no menu, so nothing was asked of their entries: " + string.Join(", ", silent));

        // ⛔ A menu read as no entries passes both checks below, so a reading that sees none has gone blind:
        // every menu the library opens has entries.
        List<string> empty = [.. menus.Where(m => m.Value.Length == 0).Select(m => m.Key)];
        Assert.True(
            empty.Count == 0,
            "These menus opened and UI Automation's control view showed none of their entries, so nothing was "
            + "asked of them: " + string.Join(", ", empty));

        List<string> missing =
        [
            .. menus
                .Where(m => m.Value.Any(string.IsNullOrEmpty))
                .Select(m => $"{m.Key}: {m.Value.Count(string.IsNullOrEmpty)} of {m.Value.Length} entries"),
        ];
        Assert.True(
            missing.Count == 0,
            "These menus hold entries a harness can find only by their translated text:" + Environment.NewLine
            + string.Join(Environment.NewLine, missing.Select(m => "  " + m)));

        List<string> twice =
        [
            .. menus.SelectMany(m => m.Value
                .OfType<string>()
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => $"{m.Key}: {g.Key} ×{g.Count()}")),
        ];
        Assert.True(
            twice.Count == 0,
            "A harness finds a menu entry with one search within the menu, and these ids are there more than "
            + "once:" + Environment.NewLine + string.Join(Environment.NewLine, twice.Select(t => "  " + t)));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    [AvaloniaFact]
    public async Task Every_id_a_harness_searches_for_is_the_one_the_fixture_pins()
    {
        // ⭐ The tests above ask whether each part HAS an id, found once; this one asks which id. Plan 00023's
        // Windows harness hard-codes these strings, so a rename, an addition or a removal edits
        // fixtures/automation-ids.txt in the same change — the bargain fixtures/api strikes for the public
        // surface. What the code declares is read from where it is declared: the templates' markup, the
        // margins a pane builds, and the entries every surface's menu shows.
        TestLogSink.Instance.Clear();
        HashSet<string> pinned = [.. PinnedIds()];
        HashSet<string> declared =
        [
            .. DeclaredIds(LibraryWithin(ScanAll())).Select(d => $"{d.Owner} {d.Id}"),
            .. new DiffPanePresenter().TextArea.LeftMargins
                .OfType<DiffMargin>()
                .Select(m => $"{nameof(DiffPanePresenter)} {AutomationProperties.GetAutomationId(m)}"),
            .. (await ReadEveryMenu()).Menus.Values
                .SelectMany(ids => ids)
                .OfType<string>()
                .Select(id => $"{nameof(DiffMenuItem)} {id}"),
        ];

        List<string> unpinned = [.. declared.Where(d => !pinned.Contains(d)).Order(StringComparer.Ordinal)];
        Assert.True(
            unpinned.Count == 0,
            "The code declares these ids and fixtures/automation-ids.txt does not pin them — an id added, or one "
            + "renamed, which every harness searching by the old name stops finding. Add each to the fixture in "
            + "the same change:" + Environment.NewLine + string.Join(Environment.NewLine, unpinned.Select(u => "  " + u)));

        List<string> stale = [.. pinned.Where(p => !declared.Contains(p)).Order(StringComparer.Ordinal)];
        Assert.True(
            stale.Count == 0,
            "fixtures/automation-ids.txt pins these ids and nothing declares them any more — an id removed, or "
            + "one renamed. Take each out of the fixture in the same change:" + Environment.NewLine
            + string.Join(Environment.NewLine, stale.Select(s => "  " + s)));

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    /// <summary>
    /// The ids <c>fixtures/automation-ids.txt</c> pins, one <c>owner id</c> per line; a blank line or a line
    /// starting <c>#</c> pins nothing. Read by content, whatever byte-order mark or terminator the file has.
    /// </summary>
    private static IEnumerable<string> PinnedIds() =>
        File.ReadAllText(RepoPaths.Source(Path.Combine("fixtures", "automation-ids.txt")))
            .TrimStart('﻿')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));

    /// <summary>
    /// Opens every menu a surface of either view opens and reads its entries' ids through UI Automation, by
    /// surface: each pane region on both sides of the side-by-side view, its connector, its map and a
    /// header, and every region the unified view has. A surface that opened nothing, or that a view has but
    /// gave nowhere to click, is silent.
    /// </summary>
    private static async Task<(Dictionary<string, string?[]> Menus, List<string> Silent)> ReadEveryMenu()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        Dictionary<string, string?[]> menus = new(StringComparer.Ordinal);
        List<string> silent = [];

        using (CompositeHost host = new())
        {
            host.Show();
            await host.LoadAsync(left, right);
            host.Window.CaptureRenderedFrame()?.Dispose();

            // Every surface exists on the side-by-side view, so a region with nowhere to click is a miss too.
            // Each pane is asked, because each offers to copy toward the other side; the connector and the
            // map belong to neither pane, and are asked once.
            foreach (DiffPaneRegion region in Enum.GetValues<DiffPaneRegion>())
            {
                (string Side, DiffPanePresenter Pane)[] panes =
                    region is DiffPaneRegion.ConnectorGutter or DiffPaneRegion.OverviewMap
                        ? [(string.Empty, host.Left)]
                        : [("left ", host.Left), ("right ", host.Right)];
                foreach ((string side, DiffPanePresenter pane) in panes)
                {
                    string surface = $"side-by-side {side}{region}";
                    if (PointOn(region, pane, host.View, host.Window) is { } at)
                    {
                        ReadMenu(surface, at, host.Window, () => host.View.LastMenu, menus, silent);
                    }
                    else
                    {
                        silent.Add(surface);
                    }
                }
            }

            DiffPaneHeader header = host.View.GetVisualDescendants().OfType<DiffPaneHeader>().First();
            if (Centre(header, host.Window) is { } onHeader)
            {
                ReadMenu("side-by-side header", onHeader, host.Window, () => host.View.LastMenu, menus, silent);
            }
            else
            {
                silent.Add("side-by-side header");
            }
        }

        using (InlineHost unified = new())
        {
            unified.Show();
            await unified.LoadAsync(left, right);
            unified.Window.CaptureRenderedFrame()?.Dispose();

            // The unified view has no connector and no map, so those two are not asked; any other surface it
            // has nowhere to click on is a miss, as on the side-by-side view.
            foreach (DiffPaneRegion region in Enum.GetValues<DiffPaneRegion>())
            {
                if (PointOn(region, unified.Pane, unified.View, unified.Window) is { } at)
                {
                    ReadMenu($"unified {region}", at, unified.Window, () => unified.View.LastMenu, menus, silent);
                }
                else if (region is not (DiffPaneRegion.ConnectorGutter or DiffPaneRegion.OverviewMap))
                {
                    silent.Add($"unified {region}");
                }
            }
        }

        return (menus, silent);
    }

    /// <summary>
    /// Right-clicks <paramref name="at"/> and, if a menu opened, records the ids of its entries as UI Automation
    /// reports them; otherwise records the surface as silent.
    /// </summary>
    private static void ReadMenu(
        string surface,
        Point at,
        Window window,
        Func<ContextMenu?> lastMenu,
        Dictionary<string, string?[]> menus,
        List<string> silent)
    {
        lastMenu()?.Close();
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        CompositeHost.Layout();

        if (lastMenu() is { IsOpen: true } menu)
        {
            menus[surface] =
            [
                .. ControlView(ControlAutomationPeer.CreatePeerForElement(menu))
                    .Where(p => p.GetAutomationControlType() == AutomationControlType.MenuItem)
                    .Select(p => p.GetAutomationId()),
            ];
            menu.Close();
        }
        else
        {
            silent.Add(surface);
        }
    }

    /// <summary>
    /// Where in <paramref name="window"/> a right-click reaches <paramref name="region"/>: the middle of the
    /// surface, or of a block's polygon on the connector, the only part of that column with a menu. Null when
    /// the view has no such surface.
    /// </summary>
    private static Point? PointOn(DiffPaneRegion region, DiffPanePresenter pane, Control view, Window window)
    {
        Control? surface = region switch
        {
            DiffPaneRegion.Text => pane.TextArea.TextView,
            DiffPaneRegion.LineNumberMargin => pane.TextArea.LeftMargins.OfType<DiffLineNumberMargin>().FirstOrDefault(),
            DiffPaneRegion.ChangeMarkerMargin => pane.TextArea.LeftMargins.OfType<ChangeMarkerMargin>().FirstOrDefault(),
            DiffPaneRegion.ConnectorGutter => view.GetVisualDescendants().OfType<ChangeConnectorGutter>().FirstOrDefault(),
            DiffPaneRegion.OverviewMap => view.GetVisualDescendants().OfType<DiffMinimap>().FirstOrDefault(),
            _ => null,
        };

        if (surface is not ChangeConnectorGutter gutter)
        {
            return surface is null ? null : Centre(surface, window);
        }

        foreach (ConnectorPolygon polygon in gutter.LastPolygons)
        {
            Point inside = new(gutter.Bounds.Width / 2, (polygon.LeftTop.Y + Math.Max(polygon.LeftBottom.Y, polygon.RightBottom.Y)) / 2);
            if (polygon.Contains(inside))
            {
                return gutter.TranslatePoint(inside, window);
            }
        }

        return null;
    }

    /// <summary>The middle of <paramref name="control"/>, in <paramref name="window"/>'s coordinates.</summary>
    private static Point? Centre(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);

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
