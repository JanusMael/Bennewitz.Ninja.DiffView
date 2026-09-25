// scripts/mutate-gates.cs — a .NET 10 file-based app.
//
// Proves that the static-analysis gates can fail. Each mutation below declares what must happen to it —
// a NAMED TEST must fail, or the BUILD must reject it, or it must stay GREEN because it encodes a gate's
// blind spot — and anything else fails the run. A mutation that merely survives means the gate it
// targets reports clean over a codebase that is not, which is the one failure mode a gate cannot recover
// from: zero findings reads as coverage and is indistinguishable from success.
//
// ⭐ `Expect` is compared, not printed. Three verdicts look like success and are not: a kill by a
// different test in the same class, a kill by the compiler before the gate ran, and a test-host abort
// (AGENTS.md §5 — `Failed!` with `failed: 0`). Each is reported separately and each fails the run.
//
// ⭐ A guard is proven only when a mutation makes it the FIRST assertion to fail, because a test stops
// there. Every assertion in the gate files is derived, each failure is attributed to the line it stopped
// on, and a full run fails any assertion no mutation reached first — or a guard marked `inert-at-pin`
// whose pin has moved, or that a mutation tripped anyway.
//
//   dotnet run scripts/mutate-gates.cs                     every mutation, then the guard verdict
//   dotnet run scripts/mutate-gates.cs -- --list           name them and exit, running nothing
//   dotnet run scripts/mutate-gates.cs -- --guards         the derived guards and their markers; fails
//                                                          on an expired marker or a throw — CI runs it
//   dotnet run scripts/mutate-gates.cs -- --only exclusion substring match on the name
//   dotnet run scripts/mutate-gates.cs -- --force          run over a dirty tree (see below)
//
// ⛔ THIS TOOL REVERTS `src` AND `tests` BEFORE EVERY MUTATION. That is how it guarantees each
// mutation is measured alone, and it is also how an earlier generation of these harnesses destroyed
// uncommitted work four times in one day — each time silently reverting a fix that had just been
// reported as applied. So this one REFUSES TO START on a dirty tree unless --force is given. Commit
// first; the commit is the revert point.
//
// ⚠ A mutation whose edit matches nothing prints `no-op`, and that is "not a result" rather than a
// surviving mutation — the gate was never exercised either way. It means the code moved and the mutation
// needs re-pointing, so it FAILS THE RUN: a mutation nobody can apply is not evidence, and a permanently
// unappliable one belongs deleted rather than left printing a reassuring word. When a construct is
// deliberately designed out of existence, the mutation for it is removed and the plan records why.
//
// The .sh and .ps1 wrappers beside this file run it from the repository root for you.

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

const string Solution = "DiffView.slnx";
const string TestProject = "tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj";

const string A11yGate = "tests/DiffView.Avalonia.Tests/AccessibilityCoverageTests.cs";
const string PartsGate = "tests/DiffView.Avalonia.Tests/TemplatePartTests.cs";
const string AssemblyGate = "tests/DiffView.Avalonia.Tests/AssemblyQualityTests.cs";
const string GridGate = "tests/DiffView.Avalonia.Tests/GridSlotTests.cs";
const string CoreSource = "src/DiffView.Core/DiffSearch.cs";
const string CoreProject = "src/DiffView.Core/DiffView.Core.csproj";
const string FindBar = "src/DiffView.Avalonia/DiffFindBar.cs";
const string SbsTheme = "src/DiffView.Avalonia/Themes/SideBySideDiffView.axaml";
const string DemoView = "src/DiffView.Demo/MainWindow.axaml";
const string SbsSource = "src/DiffView.Avalonia/SideBySideDiffView.cs";
const string SbsController = "src/DiffView.Avalonia/DiffBuildController.cs";
const string ViewerSource = "src/DiffView.Avalonia/DiffViewer.cs";
const string InlineSource = "src/DiffView.Avalonia/InlineDiffView.cs";

const string ProbeControl = "src/DiffView.Avalonia/MutationProbeControl.cs";
const string HeaderSource = "src/DiffView.Avalonia/DiffPaneHeader.cs";
const string UiProbe = "src/DiffView.Avalonia/MutationProbe.cs";
const string CompatDictionary = "src/DiffView.Avalonia/Themes/Compat/FluentKeys.Semi.axaml";
const string PackagingGate = "tests/DiffView.Avalonia.Tests/PackagingTests.cs";

// ⛔ The files whose every assertion is a guard this plan adopts, and must therefore be the FIRST
// assertion to fail under at least one mutation. A mutation proves only the assert it trips first —
// a test stops there — so "killed by the test it names" says nothing about the asserts after it.
// PackagingTests is shared with checks this plan does not own, so only the nuspec gate's method is in.
string[] GateFiles = [A11yGate, PartsGate, AssemblyGate, GridGate];
(string File, string Method) NuspecGate = (PackagingGate, "The_model_package_declares_exactly_the_dependencies_it_should");

const string A11yClass = "Bennewitz.Ninja.DiffView.Tests.AccessibilityCoverageTests";
const string PartsClass = "Bennewitz.Ninja.DiffView.Tests.TemplatePartTests";
const string AssemblyClass = "Bennewitz.Ninja.DiffView.Tests.AssemblyQualityTests";
const string PackagingClass = "Bennewitz.Ninja.DiffView.Tests.PackagingTests";
const string GridClass = "Bennewitz.Ninja.DiffView.Tests.GridSlotTests";

bool list = false;
bool force = false;
bool guardsOnly = false;
string? only = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--list":
            list = true;
            break;
        case "--force":
            force = true;
            break;
        case "--guards":
            guardsOnly = true;
            break;
        case "--only" when i + 1 < args.Length:
            only = args[++i];
            break;
        default:
            Console.Error.WriteLine($"mutate-gates: unrecognised argument '{args[i]}'.");
            return 2;
    }
}

// ---------------------------------------------------------------- process helpers

static (int Code, string Output) Run(string file, params string[] arguments)
{
    ProcessStartInfo info = new()
    {
        FileName = file,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    foreach (string a in arguments)
    {
        info.ArgumentList.Add(a);
    }

    using Process process = Process.Start(info)!;

    // ⛔ Both pipes are drained CONCURRENTLY. Reading stdout to EOF first deadlocks the pair the
    // moment a child writes more to stderr than the OS pipe buffer holds (~64 KiB): the child blocks
    // writing stderr, this process blocks reading stdout, and neither moves again. `dotnet build`
    // under -warnaserror over a badly broken mutation is exactly that shape — and it presents as a
    // HANG, not a failure, so it reads as a slow run rather than as a defect in the evidence.
    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
    Task<string> stderr = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
}

// ⛔ The scope every mutation must stay inside, and the scope Revert() and Dirty() read. They are one
// list because they cannot be allowed to drift: a mutation editing a path outside it would apply, be
// reported `MUTATION DID NOT APPLY` because Dirty() cannot see it, survive the revert, and contaminate
// every later mutation — a confounded result reported as a non-result. Checked after every edit, from
// the repository's own status rather than from a list of the paths mutations were written to touch.
string[] Scope = ["src", "tests"];

// --untracked-files=all rather than the default: `status.showUntrackedFiles=no` in a user's git config
// would otherwise hide a new untracked file under src from the guard below, while `git clean -qfd`
// deletes it regardless. The guard exists for exactly that file.
string Dirty() =>
    Run("git", ["status", "--porcelain", "--untracked-files=all", "--", .. Scope]).Output.Trim();

void Revert()
{
    Run("git", ["checkout", "--", .. Scope]);
    Run("git", ["clean", "-qfd", "--", .. Scope]);
}

// Replace in a file, asserting the pattern matched exactly `expected` times. Returns false — a
// no-op, not a result — when it did not, so a moved construct is reported rather than counted.
static bool Sub(string path, string pattern, string replacement, int expected = 1)
{
    string text = File.ReadAllText(path);
    int seen = Regex.Matches(text, pattern, RegexOptions.Singleline).Count;
    if (seen != expected)
    {
        Console.WriteLine($"    (pattern matched {seen} time(s) in {Path.GetFileName(path)}, expected {expected})");
        return false;
    }

    File.WriteAllText(path, Regex.Replace(text, pattern, replacement, RegexOptions.Singleline));
    return true;
}

static bool Write(string path, string contents)
{
    File.WriteAllText(path, contents);
    return true;
}

static bool Append(string path, string contents)
{
    File.AppendAllText(path, contents);
    return true;
}

// Insert an entry as the first element of the Excluded dictionary initialiser.
static bool AddExclusion(string entry) => Sub(
    A11yGate,
    @"(private static readonly Dictionary<string, string> Excluded = new\(StringComparer\.Ordinal\)\s*\{\s*)",
    "$1" + entry + "\n        ");

// Append a public member to the model assembly. ⚠ Appended, never inserted: an insert before a type
// lands between its doc comment and the type, and CS1591 then kills the mutation at the build rather
// than at the test — which tests nothing and reads like a pass.
static bool AddPublicMember(string body)
{
    string text = File.ReadAllText(CoreSource).TrimEnd();
    File.WriteAllText(
        CoreSource,
        text + "\n\n/// <summary>Planted by scripts/mutate-gates.cs.</summary>\npublic static class MutationProbe\n{\n"
            + body + "\n}\n");
    return true;
}

// Give the model assembly a PackageReference on Avalonia, beside the DiffPlex one it really has.
static bool AddAvaloniaReference(string attributes = "")
{
    string text = File.ReadAllText(CoreProject);
    Match m = Regex.Match(text, @"(\s*)<PackageReference Include=""DiffPlex""[^>]*/>");
    if (!m.Success)
    {
        Console.WriteLine("    (no DiffPlex PackageReference in DiffView.Core.csproj)");
        return false;
    }

    File.WriteAllText(
        CoreProject,
        text[..m.Index] + m.Value + m.Groups[1].Value
            + $@"<PackageReference Include=""Avalonia"" {attributes}/>" + text[(m.Index + m.Length)..]);
    return true;
}

// Swap the nth `Scan("DiffView.Core")` to the UI assembly. ⚠ The nth, not the first: a harness that
// only ever swaps the first occurrence tests one subject guard three times and the other two never.
static bool SwapSubject(int nth)
{
    const string wanted = @"Scan(""DiffView.Core"")";
    string[] parts = File.ReadAllText(AssemblyGate).Split(wanted);
    if (parts.Length <= nth)
    {
        Console.WriteLine($"    (only {parts.Length - 1} occurrence(s) of {wanted}, wanted #{nth})");
        return false;
    }

    File.WriteAllText(
        AssemblyGate,
        string.Join(wanted, parts[..nth]) + @"Scan(""DiffView.Avalonia"")" + string.Join(wanted, parts[nth..]));
    return true;
}

// Replace the nth (1-based) occurrence of a LITERAL. False — a no-op, not a result — when there are
// fewer, so a moved construct is reported rather than silently skipped.
static bool SubNth(string path, string literal, string replacement, int nth)
{
    string[] parts = File.ReadAllText(path).Split(literal);
    if (parts.Length <= nth)
    {
        Console.WriteLine($"    (only {parts.Length - 1} occurrence(s) of the literal in {Path.GetFileName(path)}, wanted #{nth})");
        return false;
    }

    File.WriteAllText(path, string.Join(literal, parts[..nth]) + replacement + string.Join(literal, parts[nth..]));
    return true;
}

const string AnalyzeModel = "AssemblyRuleResult result = rule.Analyze(model);";
const string AnalyzeNothing = "AssemblyRuleResult result = rule.Analyze(AssemblyScanContext.Of());";

const string AvaloniaPointMember =
    "    /// <summary>Planted.</summary>\n    public static global::Avalonia.Point P() => default;";

// ---------------------------------------------------------------- the mutations

List<Mutation> mutations =
[
    // The two-subject accounting. A derived root is not enough on its own: Load(all.Root) is
    // derived, resolves, and makes the subject a superset of itself — measured green on every floor
    // until an equality over the same readings existed. Widening is the equality's to catch.
    new("library subject loses its Path.Combine, becoming the whole scan", A11yClass,
        () => Sub(A11yGate,
            @"XamlScanContext\.Load\(Path\.Combine\(all\.Root, typeof\(SideBySideDiffView\)\.Assembly\.GetName\(\)\.Name!\)\)",
            "XamlScanContext.Load(all.Root)"),
        "Every_interactive_control_has_an_automation_name"),

    new("demo subject loses its Path.Combine", A11yClass,
        () => Sub(A11yGate,
            @"XamlScanContext\.Load\(Path\.Combine\(all\.Root, ""DiffView\.Demo""\)\)",
            "XamlScanContext.Load(all.Root)"),
        "Every_interactive_control_has_an_automation_name"),

    // Narrowing is each floor's to catch, and each floor comes before the equalities so it is the one
    // that trips.
    new("library subject points at markup-free ground", A11yClass,
        () => Sub(A11yGate,
            @"Path\.Combine\(all\.Root, typeof\(SideBySideDiffView\)\.Assembly\.GetName\(\)\.Name!\)",
            @"Path.Combine(all.Root, ""DiffView.Core"")"),
        "Every_interactive_control_has_an_automation_name"),

    new("demo subject points at markup-free ground", A11yClass,
        () => Sub(A11yGate, @"""DiffView\.Demo""", @"""DiffView.Core"""),
        "Every_interactive_control_has_an_automation_name"),

    // The stock floor reads its own count, so widening THAT reading alone leaves the full-set equality
    // balanced — which is why the stock names are held to an accounting of their own.
    new("the stock floor's reading is widened to the whole scan", A11yClass,
        () => Sub(A11yGate, @"int stock = stockRule\.Analyze\(librarySubject\)\.Inspected;",
            "int stock = stockRule.Analyze(allScan).Inspected;"),
        "Every_interactive_control_has_an_automation_name"),

    // The exclusion list is the hand-written element set one level up, so it earns itself or fails.
    new("a covered control is excluded instead of named", A11yClass,
        () => AddExclusion(@"[nameof(DiffPaneHeader)] = ""not interactive enough to bother with"","),
        "Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check"),

    new("an exclusion is given no reason", A11yClass,
        () => AddExclusion(@"[nameof(DiffStatusStrip)] = ""   "","),
        "Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check"),

    // The exclusion test's own floor: with nothing excluded it would check nothing and pass. Every entry
    // goes, not one: while DiffViewer's exclusion stood beside DiffFindBar's, removing the find bar's
    // left the floor holding and the per-name coverage failing instead — a kill by the wrong test.
    new("every exclusion is removed", A11yClass,
        () => Sub(A11yGate,
            @"(private static readonly Dictionary<string, string> Excluded = new\(StringComparer\.Ordinal\)\s*\{).*?(\r?\n    \};)",
            "$1$2"),
        "Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check"),

    new("an exclusion names a type this library does not export", A11yClass,
        () => AddExclusion(@"[""DiffPaneHeadr""] = ""a typo excludes nothing, and hides that it excludes nothing"","),
        "Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check"),

    new("the control an exclusion rests on stops naming itself", A11yClass,
        () => Sub(FindBar,
            @"\n\s*AutomationProperties\.SetName\(this, DiffViewStrings\.Get\(DiffViewStrings\.FindBarName\)\);",
            ""),
        "Every_exclusion_either_names_itself_or_has_no_element_of_ours_to_check"),

    // Per-name coverage: a name in the set that guards nothing. This is the shape plan 00021's
    // DiffViewer arrived in — public, and instantiated by no markup of ours until the demo hosted it.
    new("a public control no markup of ours instantiates", A11yClass,
        () => Write(ProbeControl,
            "namespace Bennewitz.Ninja.DiffView;\n\n"
            + "/// <summary>A public control nothing instantiates, so nothing floors its name.</summary>\n"
            + "public class MutationProbeControl : global::Avalonia.Controls.Control\n{\n}\n"),
        "Every_interactive_control_has_an_automation_name"),

    new("a forward-cover name stops being inert", A11yClass,
        () => Sub(DemoView, @"</Window>",
            "  <Window.Resources>\n"
            + "    <Expander x:Key=\"MutationProbe\" AutomationProperties.Name=\"named, so only inertness moves\"\n"
            + "              Header=\"probe\" />\n"
            + "  </Window.Resources>\n</Window>"),
        "Every_interactive_control_has_an_automation_name"),

    // ⛔ The adopted instance built from a different list than the one per-name coverage proves. As two
    // tests, each building its own instance, this passed the whole suite with the demo's menu bar
    // unnamed — measured. Per-name coverage now reads the adopted instance's own total, so the name the
    // instance lacks appears to cover nothing.
    new("the findings instance is built from a list without Menu", A11yClass,
        () => Sub(A11yGate, @"InteractiveAutomationNameRule rule = new\(names\);",
            "InteractiveAutomationNameRule rule = new([.. names.Where(n => n != nameof(Menu))]);"),
        "Every_interactive_control_has_an_automation_name"),

    // ⛔ One edit narrowing the derivation narrows every reading together. Measured: from Control to
    // TemplatedControl dropped the minimap and the connector gutter with the gate green. The markup
    // cross-check is what trips.
    new("the derived element set is narrowed to templated controls", A11yClass,
        () => Sub(A11yGate, @"\.Where\(t => typeof\(Control\)\.IsAssignableFrom\(t\) && !t\.IsAbstract\)",
            ".Where(t => typeof(Avalonia.Controls.Primitives.TemplatedControl).IsAssignableFrom(t) && !t.IsAbstract)"),
        "Every_interactive_control_has_an_automation_name"),

    new("the markup cross-check reads the wrong namespace", A11yClass,
        () => Sub(A11yGate, @"string ourNamespace = ""using:"" \+ typeof\(SideBySideDiffView\)\.Namespace;",
            @"string ourNamespace = ""using:"" + typeof(SideBySideDiffView).Namespace + "".Nowhere"";"),
        "Every_interactive_control_has_an_automation_name"),

    // The findings assertion itself, on a control this plan brought into the set.
    new("a header loses its automation name", A11yClass,
        () => Sub(SbsTheme, @"\s*AutomationProperties\.Name=""\{TemplateBinding LeftHeaderName\}""", ""),
        "Every_interactive_control_has_an_automation_name"),

    new("an interactive control is planted with no name at all", A11yClass,
        () => Sub(DemoView, @"</Window>",
            "  <Window.Resources>\n    <Button x:Key=\"MutationProbe\" Content=\"probe\" />\n"
            + "  </Window.Resources>\n</Window>"),
        "Every_interactive_control_has_an_automation_name"),

    // The one framework name the stock seventeen leave out that is live here — the demo's menu bar.
    new("the demo's menu bar loses its automation name", A11yClass,
        () => Sub(DemoView, @" AutomationProperties\.Name=""Main menu""", ""),
        "Every_interactive_control_has_an_automation_name"),

    new("an automation name is present but empty", A11yClass,
        () => Sub(SbsTheme, @"AutomationProperties\.Name=""\{TemplateBinding LeftHeaderName\}""",
            @"AutomationProperties.Name="""""),
        "Every_interactive_control_has_an_automation_name"),

    // XQ1003. Its floor sits well under its population deliberately, because what it guards is the
    // silent Clean(0) the rule returns when it is handed no assemblies.
    new("the template-part scan is handed no assemblies", PartsClass,
        () => Sub(PartsGate,
            @"\s*\.WithAssemblies\(typeof\(FindScope\)\.Assembly, typeof\(SideBySideDiffView\)\.Assembly\)", ""),
        "Every_template_part_a_control_looks_up_is_declared_in_its_theme"),

    new("a control looks up a part no theme declares", PartsClass,
        () => Sub(SbsSource, @"MinimapPart = ""PART_Minimap""", @"MinimapPart = ""PART_NoSuchPartInAnyTheme"""),
        "Every_template_part_a_control_looks_up_is_declared_in_its_theme"),

    // The controller looks parts up on its host's template, so the rule credits them to the controller
    // and checks them against no theme; they are covered only while each is a name the view declares.
    // ⚠ The name must differ from every constant: a literal equal to one compiles to the same IL.
    new("the controller looks up a part the view does not declare", PartsClass,
        () => Sub(SbsController, @"e\.NameScope\.Find<DiffMinimap>\(SideBySideDiffView\.MinimapPart\)",
            @"e.NameScope.Find<DiffMinimap>(""PART_Map"")"),
        "Every_template_part_a_control_looks_up_is_declared_in_its_theme"),

    // The same lookups run on every host's template, and the rule checks a host's theme only against
    // what that host declares: the viewer dropping one leaves its theme's copy of the part unchecked
    // while the editor's still is.
    new("the viewer stops declaring a part the controller looks up", PartsClass,
        () => Sub(ViewerSource, @"public const string MinimapPart = SideBySideDiffView\.MinimapPart;",
            @"public const string MinimapPart = ""Minimap"";"),
        "Every_template_part_a_control_looks_up_is_declared_in_its_theme"),

    // Which controls are skipped is the precise form of the blinding guard, so it needs a mutation of
    // its own: one of the two legitimately part-less controls gains a part, and stops being skipped.
    new("a themed control with no parts gains one", PartsClass,
        () => Sub(HeaderSource, @"(public class DiffPaneHeader : TemplatedControl\s*\{)",
            "$1\n    /// <summary>Planted.</summary>\n    public const string ProbePart = \"PART_Probe\";\n"),
        "Every_template_part_a_control_looks_up_is_declared_in_its_theme"),

    // The model assembly is forward cover: it must stay inert, and this is the moment it stops being.
    // ⚠ The control needs a THEME to be inspected. Without one the rule skips it, and the expected-
    // Skipped assertion is what trips — measured — which is a real guard but not this one.
    new("the model gains a themed control with a part", PartsClass,
        () => AddAvaloniaReference()
            && Append(CoreSource,
                "\n/// <summary>Planted by scripts/mutate-gates.cs.</summary>\n"
                + "public class ProbeControl : global::Avalonia.Controls.Primitives.TemplatedControl\n{\n"
                + "    /// <summary>Planted.</summary>\n"
                + "    public const string ProbePart = \"PART_Probe\";\n}\n")
            // xmlns on the root only: the XAML compiler rejects one on a nested element as AXN0003.
            && Sub(SbsTheme, @"xmlns:dv=""using:Bennewitz\.Ninja\.DiffView""",
                "xmlns:dv=\"using:Bennewitz.Ninja.DiffView\" xmlns:core=\"using:Bennewitz.Ninja.DiffView.Core\"")
            && Sub(SbsTheme, @"</ResourceDictionary>(\s*)$",
                "  <ControlTheme"
                + " x:Key=\"{x:Type core:ProbeControl}\" TargetType=\"core:ProbeControl\">\n"
                + "    <Setter Property=\"Template\">\n      <ControlTemplate>\n"
                + "        <Border Name=\"PART_Probe\" />\n      </ControlTemplate>\n    </Setter>\n"
                + "  </ControlTheme>\n</ResourceDictionary>$1"),
        "Every_template_part_a_control_looks_up_is_declared_in_its_theme"),

    // ---- The seam where a declared name stops being a real one. The markup attribute stays; only the
    // code that fills the property goes. The markup scan cannot see this — without the runtime check,
    // both of these leave the whole suite green, measured rather than argued.
    new("the side-by-side view stops filling its header names", A11yClass,
        () => Sub(SbsController,
            @"\n\s*Control\.SetCurrentValue\(SideBySideDiffView\.(?:Left|Right)HeaderNameProperty, DiffViewStrings\.Get\(DiffViewStrings\.(?:Left|Right)HeaderName\)\);",
            "", 2),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    new("the unified view stops filling its header names", A11yClass,
        () => Sub(InlineSource,
            @"\n\s*SetCurrentValue\((?:Left|Right)HeaderNameProperty, DiffViewStrings\.Get\(DiffViewStrings\.(?:Left|Right)HeaderName\)\);",
            "", 2),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    // ⛔ The find bar fills ELEVEN names the same way, in the same shape, each with the same
    // string.Empty default — and while the runtime check read only the headers, deleting these eleven
    // left the WHOLE SUITE GREEN while the query box, the four option toggles, the three scope buttons
    // and previous / next / close went unnamed: 11 of the 14 elements the stock names inspect in this
    // library. The check therefore takes no subject from anyone's list.
    new("the find bar stops filling its children's names", A11yClass,
        () => Sub(FindBar,
            @"\n\s*SetCurrentValue\(\w+NameProperty, DiffViewStrings\.Get\(DiffViewStrings\.Find\w+Name\)\);",
            "", 11),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    // The dismiss button shows a bare "×" and takes its name from a property defaulting to empty, and it
    // is visible only while a failure shows — which is exactly why hiding what is off screen needed the
    // walk to put every part on screen, and the coverage assertion to hold it to that.
    new("the status strip stops filling its dismiss name", A11yClass,
        () => Sub(SbsController, @"\n\s*strip\.DismissText = DiffViewStrings\.Get\(DiffViewStrings\.StatusDismiss\);", "")
            && Sub(InlineSource, @"\n\s*strip\.DismissText = DiffViewStrings\.Get\(DiffViewStrings\.StatusDismiss\);", ""),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    // ---- The runtime walk BLINDED, each way it can be. A check that asserts only "nothing unnamed"
    // passes whenever it reaches nothing, so each of these must be caught by the coverage assertion —
    // the markup declares the parts, and the walk has to have checked every one of them on screen.
    // Edits here replace a statement with a comment on the same line, so no guard below it moves.
    new("the walk never opens the find bar", A11yClass,
        () => Sub(A11yGate, @"(?:host|unified)\.View\.OpenFind\(\);", "/* the find bar is left closed */", 2),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    new("the walk never fails a build", A11yClass,
        () => Sub(A11yGate,
            @"(?:host|unified|viewer)\.View\.Builder = CompositeHost\.FailingBuilder;",
            "/* the build succeeds */", 3),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    // The viewer's parts are declared by its own theme, and only the viewer's own states put them on
    // screen — which is what dropping both shows.
    new("the walk never shows a viewer", A11yClass,
        () => Sub(A11yGate, @"Collect\(viewer\.View, interactive, visited, unnamed\);",
            "/* the viewer is left out */", 2),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    new("the walk's notion of ours is the test assembly", A11yClass,
        () => Sub(A11yGate, @"Assembly ours = typeof\(SideBySideDiffView\)\.Assembly;",
            "Assembly ours = typeof(AccessibilityCoverageTests).Assembly;"),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    // The requirement is derived from the markup, so it is anchored to upstream's own count: a reading
    // that loses the stock names would otherwise shrink what the walk must reach, silently.
    new("the walk's requirement stops reading the stock names", A11yClass,
        () => Sub(A11yGate,
            @"declaredNames = \[\.\. InteractiveAutomationNameRule\.FrameworkInteractiveElements, \.\. ElementNames\(\)\];",
            "declaredNames = [.. ElementNames()];"),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    new("an interactive part is declared with no Name", A11yClass,
        () => Sub(SbsTheme, @"<Button Name=""PART_BannerAction""", "<Button"),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    new("the walk's requirement is derived from markup-free ground", A11yClass,
        () => Sub(A11yGate, @"XamlScanContext library = LibraryWithin\(ScanAll\(\)\);",
            @"XamlScanContext library = XamlScanContext.Load(RepoPaths.Source(""src/DiffView.Core""));"),
        "A_name_declared_by_a_template_binding_is_not_empty_at_runtime"),

    // ---- XQ1004. ⚠ The rule is about a child asking for more room than its FIXED slot gives it, not
    // about an out-of-range index: a column index past a five-column grid SURVIVES this gate, measured.
    // The 16-pixel spacer between the panes is the one fixed slot in the library's grids, so it is the
    // only place this can be shown.
    new("a control asks for more room than its fixed grid slot", GridClass,
        () => Sub(SbsTheme,
            @"<Border Grid\.Column=""2"" Background=""\{DynamicResource DiffView\.HeaderBackgroundBrush\}"" />",
            @"<Border Grid.Column=""2"" MinWidth=""40"" Background=""{DynamicResource DiffView.HeaderBackgroundBrush}"" />"),
        "No_control_declares_a_size_larger_than_its_fixed_grid_slot"),

    new("the grid scan is pointed at markup-free ground", GridClass,
        () => Sub(GridGate,
            @"XamlScanContext\.Load\(RepoPaths\.Source\(""src""\)\)",
            @"XamlScanContext.Load(RepoPaths.Source(""src/DiffView.Core""))"),
        "No_control_declares_a_size_larger_than_its_fixed_grid_slot"),

    // ---- The parse-error claim. The a11y gate deliberately has NO assertion that markup parsed,
    // because every rule iterates ParsedFiles and silently drops a file that did not — so such an
    // assertion could never fire. What makes that safe is the XAML compiler, and these two are the
    // proof: both must be KILLED BY THE BUILD, one hand-authored view and one generated dictionary.
    new("a hand-authored view stops being well-formed XML", A11yClass,
        () => Sub(DemoView, @"</Window>", "</Windo>"),
        "AVLN1001", Expect.Build),

    new("a generated compat dictionary stops being well-formed XML", A11yClass,
        // The last closing tag, at end of file: this dictionary nests three of them.
        () => Sub(CompatDictionary,
            @"</ResourceDictionary>(\s*)$", "</ResourceDictionar>$1"),
        "AVLN1001", Expect.Build),

    // ---- AQ1002: the diff engine must not reach the model's public surface.
    new("a DiffPlex type appears on the model's public surface", AssemblyClass,
        () => AddPublicMember("    /// <summary>Planted.</summary>\n    public static DiffPlex.Model.DiffResult? Leak() => null;"),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    new("AQ1002's namespace prefix is misspelled", AssemblyClass,
        () => Sub(AssemblyGate, @"DiffEngine = ""DiffPlex""", @"DiffEngine = ""DifPlex"""),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    // The subject helper's own assertion: a name resolved to the wrong assembly is caught where the
    // subject is made, before any gate reads it.
    new("the subject helper resolves the model's name to the UI assembly", AssemblyClass,
        () => Sub(AssemblyGate, @"""DiffView\.Core"" => typeof\(FindScope\)\.Assembly,",
            @"""DiffView.Core"" => typeof(SideBySideDiffView).Assembly,"),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    // A name the helper does not know stops at the helper, as an assertion the run can attribute.
    new("a gate asks the subject helper for an assembly it does not gate", AssemblyClass,
        () => SubNth(AssemblyGate, @"Scan(""DiffView.Core"")", @"Scan(""DiffView.Nowhere"")", 1),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    // ⛔ The negative subject guard exists because the positive one is a coincidence: the UI assembly
    // does not reference DiffPlex TODAY. A plain swap stops on the coincidence first, so this makes the
    // coincidence false before swapping — which is the only state the negative guard exists for.
    new("the UI assembly uses DiffPlex, and AQ1002's subject is swapped to it", AssemblyClass,
        () => Write(UiProbe,
                "namespace Bennewitz.Ninja.DiffView;\n\n"
                + "/// <summary>Planted by scripts/mutate-gates.cs.</summary>\n"
                + "internal static class MutationProbe\n{\n"
                + "    internal static DiffPlex.Model.DiffResult? Probe() => null;\n}\n")
            && SwapSubject(1),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    // Each rule's Inspected > 0 guard, reached by handing that rule an empty context — the analogue of
    // XQ1003 handed no assemblies. The nth, because each test phrases its call identically.
    new("AQ1002 is handed nothing to read", AssemblyClass,
        () => SubNth(AssemblyGate, AnalyzeModel, AnalyzeNothing, 1),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    // The positive control reads the ADOPTED instance, so an instance configured apart from the constant
    // its subject guard reads is what it exists to catch.
    new("AQ1002's adopted instance names a prefix nothing uses", AssemblyClass,
        () => Sub(AssemblyGate, @"SurfaceLeakRule rule = new\(\[DiffEngine\]\);",
            @"SurfaceLeakRule rule = new([""DiffPlex.NoSuchNamespace""]);"),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    // ---- AQ1003: the model must bind against no UI framework.
    new("the model uses an Avalonia type", AssemblyClass,
        () => AddAvaloniaReference() && AddPublicMember(AvaloniaPointMember),
        "The_model_layer_binds_against_no_ui_framework"),

    new("AQ1003's namespace prefix is misspelled", AssemblyClass,
        () => Sub(AssemblyGate, @"UiFramework = ""Avalonia""", @"UiFramework = ""Avalonai"""),
        "The_model_layer_binds_against_no_ui_framework"),

    new("AQ1003 is handed nothing to read", AssemblyClass,
        () => SubNth(AssemblyGate, AnalyzeModel, AnalyzeNothing, 2),
        "The_model_layer_binds_against_no_ui_framework"),

    new("AQ1003's adopted instance forbids a prefix nothing references", AssemblyClass,
        () => Sub(AssemblyGate, @"ForbiddenReferenceRule rule = new\(\[UiFramework\]\);",
            @"ForbiddenReferenceRule rule = new([""Avalonia.NoSuchAssembly""]);"),
        "The_model_layer_binds_against_no_ui_framework"),

    // ---- The nuspec gate, and the pair that is the whole evidence for adopting both it and AQ1003:
    // one violation, two mechanisms, each blind to what the other sees.
    new("the model package gains a dependency nothing uses", PackagingClass,
        () => AddAvaloniaReference(),
        "The_model_package_declares_exactly_the_dependencies_it_should"),

    // ASP.NET Core's shared framework carries Microsoft.Extensions.Logging.Abstractions, so referencing
    // it makes the model's own package reference prunable: NU1510 under -warnaserror stops the build
    // before the gate runs unless suppressed, and pack then prunes that package from the nuspec's
    // dependencies as well — both measured. So this mutation moves the dependency set too, and the gate
    // asserts the framework reference FIRST, which is the only order in which this can reach it.
    new("the model package declares a framework reference", PackagingClass,
        () => Sub(CoreProject, "</Project>",
            "  <PropertyGroup>\n    <NoWarn>$(NoWarn);NU1510</NoWarn>\n  </PropertyGroup>\n"
            + "  <ItemGroup>\n    <FrameworkReference Include=\"Microsoft.AspNetCore.App\" />\n  </ItemGroup>\n</Project>"),
        "The_model_package_declares_exactly_the_dependencies_it_should"),

    // ---- The gate's own plumbing: it must be reading THE package's one nuspec, from a pack that ran.
    // A pack failure the build does not see: the readme the package names is not there to pack.
    new("the model package's readme cannot be packed", PackagingClass,
        () => Sub(CoreProject, @"<PackageReadmeFile>hosting-diffview\.md</PackageReadmeFile>",
            "<PackageReadmeFile>no-such-readme.md</PackageReadmeFile>"),
        "The_model_package_declares_exactly_the_dependencies_it_should"),

    // A second .nupkg beside the real one: legacy symbol packages share the extension.
    new("the model packs a second, legacy symbols package", PackagingClass,
        () => Sub(CoreProject, "<IsPackable>true</IsPackable>",
            "<IsPackable>true</IsPackable><IncludeSymbols>true</IncludeSymbols>"
            + "<SymbolPackageFormat>symbols.nupkg</SymbolPackageFormat>"),
        "The_model_package_declares_exactly_the_dependencies_it_should"),


    new("GREEN BY DESIGN: PrivateAssets hides a used type from the nuspec", PackagingClass,
        () => AddAvaloniaReference(@"PrivateAssets=""all"" ") && AddPublicMember(AvaloniaPointMember),
        "nothing — the nuspec cannot see it, which is why AQ1003 is adopted too", Expect.Green),

    new("the same violation, caught by AQ1003 instead", AssemblyClass,
        () => AddAvaloniaReference(@"PrivateAssets=""all"" ") && AddPublicMember(AvaloniaPointMember),
        "The_model_layer_binds_against_no_ui_framework"),

    new("GREEN BY DESIGN: an unused reference is invisible to AQ1003", AssemblyClass,
        () => AddAvaloniaReference(),
        "nothing — Roslyn emits no reference for an unused package, which is why the nuspec gate is adopted too",
        Expect.Green),

    // ---- Every gate's subject, one at a time. Not the first three times over.
    new("AQ1002's subject is swapped for the UI assembly", AssemblyClass,
        () => SwapSubject(1),
        "The_model_does_not_expose_the_diff_engine_on_its_public_surface"),

    new("AQ1003's subject is swapped for the UI assembly", AssemblyClass,
        () => SwapSubject(2),
        "The_model_layer_binds_against_no_ui_framework"),

    new("AQ1001's subject is swapped for the UI assembly", AssemblyClass,
        () => SwapSubject(3),
        "No_public_entry_point_defaults_its_cancellation_token"),

    // ---- AQ1001. A NEW method, because re-ordering the existing signature back would break 71 call
    // sites and be killed by the build, testing nothing.
    new("a public method defaults its cancellation token", AssemblyClass,
        () => AddPublicMember(
            "    /// <summary>Planted.</summary>\n"
            + "    public static void Probe(CancellationToken cancellationToken = default)\n    {\n    }"),
        "No_public_entry_point_defaults_its_cancellation_token"),

    new("AQ1001 is handed nothing to read", AssemblyClass,
        () => SubNth(AssemblyGate, AnalyzeModel, AnalyzeNothing, 3),
        "No_public_entry_point_defaults_its_cancellation_token"),

    new("AQ1001's control fixture stops defaulting its token", AssemblyClass,
        () => Sub(AssemblyGate, @"public static void Defaults\(CancellationToken cancellationToken = default\)",
            "public static void Defaults(CancellationToken cancellationToken)"),
        "No_public_entry_point_defaults_its_cancellation_token"),
];

if (only is not null)
{
    mutations = [.. mutations.Where(m => m.Name.Contains(only, StringComparison.OrdinalIgnoreCase))];
    if (mutations.Count == 0)
    {
        Console.Error.WriteLine($"mutate-gates: --only '{only}' matched no mutation. Try --list.");
        return 2;
    }
}

if (list)
{
    Console.WriteLine($"{mutations.Count} mutation(s):");
    foreach (Mutation m in mutations)
    {
        Console.WriteLine($"  {m.Name}");
        Console.WriteLine($"      expects: {m.ExpectedKiller}");
    }

    return 0;
}

// ---------------------------------------------------------------- the run

// ⛔ Run from the repository root, or `git clean -qfd -- src tests` cleans someone else's repository.
// Every path here is relative, and the .sh/.ps1 wrappers cd for you — but the header advertises a bare
// `dotnet run`, so this is the check that makes that safe.
if (!File.Exists(Solution))
{
    Console.Error.WriteLine($"mutate-gates: no {Solution} here. Run this from the repository root, or");
    Console.Error.WriteLine("use the .sh / .ps1 wrapper beside it, which changes directory for you.");
    return 2;
}

// The guards: every line of a gate file that makes an assertion. Derived, not listed, so a guard added
// to a gate test is owed a mutation from the moment it exists.
List<Guard> guards = [.. GateFiles.SelectMany(f => GuardsIn(f, File.ReadAllLines(f), null))];
guards.AddRange(GuardsIn(NuspecGate.File, File.ReadAllLines(NuspecGate.File), NuspecGate.Method));
foreach (string gateFile in (string[])[.. GateFiles, NuspecGate.File])
{
    if (!guards.Any(g => g.File == gateFile))
    {
        Console.Error.WriteLine($"mutate-gates: found no assertion in {gateFile} — the guard set is derived from");
        Console.Error.WriteLine("it, so an empty reading would excuse every guard there. Check the path.");
        return 2;
    }
}

// ⛔ Only an assertion is counted as a guard, so a guard written as a `throw` — `?? throw`, a switch
// arm that throws — would be tripped by mutations and credited to nothing, invisible to the check that
// every guard is proven. A gate file therefore holds no `throw` at all: setup that must fail, like the
// runtime walk's failing builder, lives in a shared helper. Checked here, so `--guards` enforces it too.
List<string> throwing = [.. GateFiles.SelectMany(f => ThrowsIn(f, File.ReadAllLines(f), null))];
throwing.AddRange(ThrowsIn(NuspecGate.File, File.ReadAllLines(NuspecGate.File), NuspecGate.Method));
if (throwing.Count > 0)
{
    Console.Error.WriteLine("mutate-gates: these gate-file lines throw. A guard must be an assertion, which the harness");
    Console.Error.WriteLine("can attribute a failure to; a throw it cannot see. Rewrite each as an Assert:");
    foreach (string line in throwing)
    {
        Console.Error.WriteLine("  " + line);
    }

    return 2;
}

// --guards: the derived set, read-only, with each inert-at-pin marker judged against the current pin —
// the marker half of a full run's judgement, in a fraction of a second. ⛔ It exits non-zero on an
// expired marker, because CI runs it: a pin bump is otherwise exactly the change that lands green while
// the one excused guard stops being excused.
if (guardsOnly)
{
    Console.WriteLine($"{guards.Count} guard(s):");
    int expired = 0;
    foreach (Guard g in guards)
    {
        string marker = "";
        if (g.InertPin is { } pin)
        {
            string? pinned = PinnedVersion(pin.Package);
            expired += pinned == pin.Version ? 0 : 1;
            marker = pinned == pin.Version
                ? $"   [inert at {pin.Package} {pin.Version}]"
                : $"   [EXPIRED: measured at {pin.Version}, pinned at {pinned ?? "nothing"}]";
        }

        Console.WriteLine($"  {Path.GetFileName(g.File)}:{g.Line}  {g.Text}{marker}");
    }

    if (expired > 0)
    {
        Console.Error.WriteLine($"mutate-gates: {expired} inert-at-pin marker(s) no longer match the pin. Re-measure —");
        Console.Error.WriteLine("scripts/xq1004-skips.sh for XQ1004 — then re-mark the guard or give it a mutation.");
        return 1;
    }

    return 0;
}

string dirty = Dirty();
if (dirty.Length > 0 && !force)
{
    Console.Error.WriteLine("mutate-gates: src or tests has uncommitted changes, and this tool");
    Console.Error.WriteLine("reverts both before every mutation — it would destroy them. Commit first,");
    Console.Error.WriteLine("or pass --force if you have decided they are expendable.");
    Console.Error.WriteLine();
    Console.Error.WriteLine(dirty);
    return 2;
}

// ⛔ What each mutation is allowed to touch is DERIVED from what it actually touched: the whole
// repository's status now, compared after every edit. A list of target paths checked against Scope
// covers only the targets someone remembered to add to it — two literals once escaped exactly that
// way — while this sees any path, named or not. Its one blind spot: a file outside Scope that is
// already dirty and gets dirtier leaves its status line unchanged.
HashSet<string> baseline = RepoStatus();

Dictionary<string, string[]> cleanGateText =
    guards.Select(g => g.File).Distinct().ToDictionary(f => f, f => File.ReadAllLines(f));
Dictionary<(string File, int Line), List<string>> trippedBy = [];

Dictionary<string, string> results = [];

bool rebuildFailed = false;
try
{
    foreach (Mutation mutation in mutations)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {mutation.Name}");
        Revert();

        bool applied;
        try
        {
            applied = mutation.Apply();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    *** the edit threw {ex.GetType().Name}: {ex.Message} ***");
            applied = false;
        }

        // ⛔ Before the no-op test, not after it: an edit that lands only outside Scope leaves Dirty() empty,
        // would be reported as a no-op, and would then survive a Revert() that cannot reach it. Whatever it
        // touched was clean a moment ago, so putting exactly those paths back cannot cost anyone's work.
        string[] strays = [.. RepoStatus().Except(baseline).Where(s => !InScope(PathOf(s)))];
        if (strays.Length > 0)
        {
            foreach (string stray in strays)
            {
                if (stray.StartsWith("??", StringComparison.Ordinal))
                {
                    File.Delete(PathOf(stray));
                }
                else
                {
                    Run("git", "checkout", "--", PathOf(stray));
                }
            }

            Revert();
            Console.Error.WriteLine();
            Console.Error.WriteLine($"mutate-gates: '{mutation.Name}' wrote outside {string.Join(" and ", Scope)},");
            Console.Error.WriteLine("where no revert reaches. Those paths have been put back; fix the mutation:");
            foreach (string stray in strays)
            {
                Console.Error.WriteLine("  " + PathOf(stray));
            }

            return 2;
        }

        if (!applied || Dirty().Length == 0)
        {
            Console.WriteLine("    *** MUTATION DID NOT APPLY — the edit matched nothing. Not a result. ***");
            Revert();
            results[mutation.Name] = "no-op";
            continue;
        }

        // The gate files as this mutation left them, so a failing line can be mapped back to the clean text.
        Dictionary<string, string[]> mutatedGateText =
            cleanGateText.Keys.ToDictionary(f => f, f => File.ReadAllLines(f));

        (int buildCode, string buildLog) = Run("dotnet", "build", Solution, "-warnaserror", "-v", "q", "--nologo");
        if (buildCode != 0)
        {
            string first = buildLog.Split('\n').FirstOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)) ?? "";
            // The whole line, never a prefix: MSBuild puts the file's path first and the diagnostic last,
            // so a cut keeps the path and loses the cause.
            string detail = first.Trim();
            Console.WriteLine($"    killed by the BUILD: {detail}");

            // ⛔ A build kill is only a result for the two mutations that exist to be one. For every other,
            // the compiler stopped the mutation before the gate could see it, so the gate never ran and a
            // pass here would be manufactured. The harness's own AddPublicMember comment records how easily
            // this happens: one new analyzer under -warnaserror silently retires five of these.
            //
            // ⭐ And for the two that ARE meant to be killed here, the DIAGNOSTIC must be the one the claim
            // rests on. The "no parse-error assertion" decision rests on the XAML compiler specifically;
            // accepting any non-zero exit certified it on whatever happened to fail — one new analyzer, or
            // a CS1591 on either edited file, and AVLN1001 never fires while the mutation still reads as
            // proof. A kill by *something* is not a kill by the gate, for the build as for a test.
            bool byTheRightDiagnostic = buildLog.Contains(mutation.ExpectedKiller, StringComparison.Ordinal);
            if (mutation.Expect == Expect.Build && !byTheRightDiagnostic)
            {
                Console.WriteLine($"    ... but NOT as {mutation.ExpectedKiller}, which is what it exists to prove.");
            }

            results[mutation.Name] = mutation.Expect == Expect.Build && byTheRightDiagnostic
                ? "build"
                : "killed-by-the-build";
            Revert();
            continue;
        }

        (_, string testLog) = Run("dotnet", "test", TestProject, "--no-build", "--filter-class", mutation.Targets);

        // ⛔ Judge the summary's outcome word, never the counts. A test-host abort prints
        // `Test run summary: Failed!` with `failed: 0` and a short total (AGENTS.md §5), so "not Passed!"
        // is not the same as "a test failed" — without this an abort reads as a kill for all 24 of the
        // mutations that matter, with an empty failure list.
        bool passed = testLog.Contains("Test run summary: Passed!", StringComparison.Ordinal);
        bool failedOutcome = testLog.Contains("Test run summary: Failed!", StringComparison.Ordinal);

        // ⛔ AGENTS.md §5's abort prints `Failed!` WITH `failed: 0` — so "no summary outcome" is not the
        // only shape one takes; read as a kill it would be diagnosed `wrong-killer`, naming a cause the
        // failure does not establish. A run that says it failed while reporting no failing test did not run
        // the gate either — a genuine kill always reports at least one. ⚠ An abort that follows a genuine
        // failure is still read as that failure: the run fails either way, but the diagnosis can be wrong.
        bool noFailuresReported = Regex.IsMatch(testLog, @"^\s*failed:\s*0\s*$", RegexOptions.Multiline);

        IEnumerable<string> failed = Regex.Matches(testLog, @"\b((?:Every|The|A|No)_\w{8,})")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Order();

        // ⭐ Which ASSERTION each failing test stopped on, mapped back to the clean gate file. This is what
        // turns "killed by the test it names" into "tripped this guard": a test stops at its first failing
        // assert, so every assert after it is proven by nothing unless some other mutation trips it first.
        List<(string File, int Line)> tripped = TrippedGuards(testLog, mutatedGateText, cleanGateText);
        foreach ((string File, int Line) at in tripped)
        {
            if (!trippedBy.TryGetValue(at, out List<string>? names))
            {
                trippedBy[at] = names = [];
            }

            names.Add(mutation.Name);
        }

        if (tripped.Count > 0)
        {
            Console.WriteLine($"    tripped: {string.Join(", ", tripped.Select(t => $"{Path.GetFileName(t.File)}:{t.Line}"))}");
        }

        if (!passed && (!failedOutcome || noFailuresReported))
        {
            Console.WriteLine(failedOutcome
                ? "    *** `Failed!` WITH `failed: 0` — the host died mid-run (AGENTS.md §5). No test failed."
                : "    *** NO SUMMARY OUTCOME — the test host did not report Passed! or Failed!.");
            Console.WriteLine("    *** Treat as an aborted run, not a kill. Re-run; scripts/catch-crash.sh --check judges a log.");
            results[mutation.Name] = "aborted";
            Revert();
            continue;
        }

        if (mutation.Expect == Expect.Green)
        {
            // A green is this mutation's result, so a red is the failure — the claim it encodes has moved.
            if (passed)
            {
                Console.WriteLine("    green, as this mutation is supposed to be");
                results[mutation.Name] = "green";
            }
            else
            {
                Console.WriteLine($"    UNEXPECTEDLY KILLED -> {string.Join(", ", failed)}");
                Console.WriteLine($"    This was meant to stay green: {mutation.ExpectedKiller}");
                results[mutation.Name] = "unexpected-red";
            }
        }
        else if (passed)
        {
            Console.WriteLine($"    SURVIVED — green. Expected {mutation.ExpectedKiller} to fail.");
            results[mutation.Name] = "survived";
        }
        else if (mutation.Expect == Expect.Build)
        {
            Console.WriteLine($"    KILLED BY A TEST, but this mutation exists to be killed by the BUILD -> {string.Join(", ", failed)}");
            results[mutation.Name] = "wrong-killer";
        }
        else if (!failed.Contains(mutation.ExpectedKiller, StringComparer.Ordinal))
        {
            // ⭐ The guarantee the harness went without: a kill by *something* is not a kill by the gate.
            // Without this, `killed` means only "the filtered class went not-Passed!", and every "killed by"
            // claim the plan makes rests on a coincidence nobody checked.
            Console.WriteLine($"    KILLED BY THE WRONG TEST -> {string.Join(", ", failed)}");
            Console.WriteLine($"    Expected {mutation.ExpectedKiller}, which stayed green.");
            results[mutation.Name] = "wrong-killer";
        }
        else
        {
            Console.WriteLine($"    killed -> {string.Join(", ", failed)}");
            results[mutation.Name] = "killed";
        }

        Revert();

        // ⛔ A revert that silently failed would confound every mutation after this one, and `git` is not
        // checked for exit status anywhere above. This is the only place that can notice.
        string residue = Dirty();
        if (residue.Length > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("mutate-gates: THE REVERT DID NOT CLEAN UP. Every later result would be");
            Console.Error.WriteLine("confounded by this residue, so the run stops here.");
            Console.Error.WriteLine(residue);
            return 2;
        }
    }
}
finally
{
    // ⛔ Leave the BUILD OUTPUT agreeing with the source, not just the source with itself — on every
    // exit once a mutation has been built, the two that stop the run early included. Reverting a
    // mutation's files does not unbuild it: its assemblies stay in bin/, so the next
    // `dotnet test --no-build` runs mutated code against reverted markup and reports a defect that is
    // not there. Measured: a gate read 0 where it measures 22, and the gate was briefly suspected.
    Console.WriteLine();
    Console.WriteLine("Rebuilding, so the output agrees with the reverted source.");
    (int finalBuild, _) = Run("dotnet", "build", Solution, "-v", "q", "--nologo");
    if (finalBuild != 0)
    {
        Console.Error.WriteLine("mutate-gates: the tree was reverted but does not build. Check it before trusting");
        Console.Error.WriteLine("any --no-build test run.");
        rebuildFailed = true;
    }
}

if (rebuildFailed)
{
    return 2;
}

Console.WriteLine();
Console.WriteLine("=== summary ===");
foreach ((string name, string verdict) in results)
{
    Console.WriteLine($"  {verdict,-20} {name}");
}

int killed = results.Values.Count(v => v == "killed");
int byBuild = results.Values.Count(v => v == "build");
int greenByDesign = results.Values.Count(v => v == "green");
int survived = results.Values.Count(v => v == "survived");
int noop = results.Values.Count(v => v == "no-op");
int unexpectedRed = results.Values.Count(v => v == "unexpected-red");
int wrongKiller = results.Values.Count(v => v == "wrong-killer");
int killedByBuild = results.Values.Count(v => v == "killed-by-the-build");
int aborted = results.Values.Count(v => v == "aborted");

Console.WriteLine();
Console.WriteLine($"as expected — killed by a test: {killed}   by the build: {byBuild}   green by design: {greenByDesign}");
Console.WriteLine($"not as expected — survived: {survived}   no-op: {noop}   unexpectedly red: {unexpectedRed}"
    + $"   wrong killer: {wrongKiller}   killed by the build: {killedByBuild}   aborted: {aborted}");

// ⭐ Completeness, judged over the whole set only: a partial run proves only the guards it reached.
// An inert-at-pin guard is excused only while its package is still pinned where it was measured; one a
// mutation trips anyway was never inert, and its marker is the defect.
List<Guard> untripped = [];
List<Guard> inertAtPin = [];
List<string> markerDefects = [];
if (only is null)
{
    foreach (Guard g in guards)
    {
        bool wasTripped = trippedBy.ContainsKey((g.File, g.Line));
        if (g.InertPin is not { } pin)
        {
            if (!wasTripped)
            {
                untripped.Add(g);
            }

            continue;
        }

        string? pinned = PinnedVersion(pin.Package);
        if (wasTripped)
        {
            markerDefects.Add($"{Path.GetFileName(g.File)}:{g.Line} is marked inert at {pin.Package} {pin.Version}, "
                + $"but '{trippedBy[(g.File, g.Line)][0]}' tripped it — remove the marker");
        }
        else if (pinned != pin.Version)
        {
            markerDefects.Add($"{Path.GetFileName(g.File)}:{g.Line} was measured inert at {pin.Package} {pin.Version}, "
                + $"and the pin is now {pinned ?? "absent"} — re-measure, then trip it or re-mark it");
        }
        else
        {
            inertAtPin.Add(g);
        }
    }
}

Console.WriteLine();
if (only is null)
{
    Console.WriteLine($"guards — {guards.Count} assertions in the gate files: "
        + $"{guards.Count - untripped.Count - inertAtPin.Count - markerDefects.Count} tripped first by a mutation, "
        + $"{inertAtPin.Count} inert at the current pin, {untripped.Count} untripped, {markerDefects.Count} with a bad marker");
    foreach (Guard g in inertAtPin)
    {
        Console.WriteLine($"  inert at {g.InertPin!.Value.Package} {g.InertPin.Value.Version}  {Path.GetFileName(g.File)}:{g.Line}");
    }

    foreach (Guard g in untripped)
    {
        Console.WriteLine($"  UNTRIPPED  {Path.GetFileName(g.File)}:{g.Line}  {g.Text}");
    }

    foreach (string defect in markerDefects)
    {
        Console.WriteLine($"  BAD MARKER  {defect}");
    }
}
else
{
    Console.WriteLine("guards — not judged: --only ran a subset, which proves only the guards it reached.");
}

if (untripped.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("AN UNTRIPPED GUARD is an assertion no mutation has made fail first. Its test may be proven,");
    Console.WriteLine("but that assertion is not: add a mutation that trips it, or take the assertion out.");
}

if (survived > 0)
{
    Console.WriteLine();
    Console.WriteLine("A SURVIVING MUTATION means that gate reports clean over a codebase that is not.");
}

if (wrongKiller > 0)
{
    Console.WriteLine();
    Console.WriteLine("A MUTATION KILLED BY THE WRONG TEST proves nothing about the gate it names: the");
    Console.WriteLine("gate stayed green while something else failed.");
}

if (killedByBuild > 0)
{
    Console.WriteLine();
    Console.WriteLine("A MUTATION KILLED BY THE BUILD never reached its gate, so the gate was not exercised.");
    Console.WriteLine("Rewrite the edit so it compiles — see AddPublicMember on why an insert does not.");
}

if (aborted > 0)
{
    Console.WriteLine();
    Console.WriteLine("AN ABORTED RUN is not a kill. The host died — it printed no summary outcome, or printed");
    Console.WriteLine("Failed! with no failing test; re-run, and judge a captured log with scripts/catch-crash.sh --check.");
}

if (unexpectedRed > 0)
{
    Console.WriteLine();
    Console.WriteLine("A MUTATION MEANT TO STAY GREEN went red: the complementarity claim has moved.");
}

return survived + noop + unexpectedRed + wrongKiller + killedByBuild + aborted + untripped.Count + markerDefects.Count > 0
    ? 1
    : 0;

// The version Directory.Packages.props pins a package at, or null when it pins none.
static string? PinnedVersion(string package)
{
    Match m = Regex.Match(
        File.ReadAllText("Directory.Packages.props"),
        $@"<PackageVersion\s+Include=""{Regex.Escape(package)}""\s+Version=""(?<version>[^""]+)""");
    return m.Success ? m.Groups["version"].Value : null;
}

// ---------------------------------------------------------------- guard and scope helpers

// The whole repository's status, one entry per path. -z so that no path is ever quoted; a rename's
// second token carries no status and is dropped by the shape test.
HashSet<string> RepoStatus() =>
    [.. Run("git", "status", "--porcelain=v1", "-z", "--untracked-files=all").Output
        .Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Where(e => e.Length > 3 && e[2] == ' ')];

bool InScope(string path) => Scope.Any(s => path.StartsWith(s + "/", StringComparison.Ordinal));

static string PathOf(string statusEntry) => statusEntry[3..];

// Every line of `lines` that makes an assertion, restricted to one method when `method` is given. A
// comment line never counts; an assertion anywhere else on a line does — `var x = Assert.Single(…)`
// is as much a guard as a statement that begins with one.
static IEnumerable<Guard> GuardsIn(string file, string[] lines, string? method)
{
    int from = 0;
    int to = lines.Length;
    if (method is not null)
    {
        from = Array.FindIndex(lines, l => l.Contains($" {method}(", StringComparison.Ordinal));
        if (from < 0)
        {
            yield break;
        }

        int end = Array.FindIndex(lines, from, l => l == "    }");
        to = end < 0 ? lines.Length : end;
    }

    for (int i = from; i < to; i++)
    {
        string text = lines[i].TrimStart();
        if (text.StartsWith("//", StringComparison.Ordinal) || text.StartsWith('*'))
        {
            continue;
        }

        if (Regex.IsMatch(lines[i], @"\bAssert\.\w+\("))
        {
            Match inert = i > 0
                ? Regex.Match(lines[i - 1], @"^\s*//\s*inert-at-pin:\s*(?<package>\S+)\s+(?<version>\S+)\s*$")
                : Match.Empty;
            yield return new Guard(
                file,
                i + 1,
                text,
                inert.Success ? (inert.Groups["package"].Value, inert.Groups["version"].Value) : null);
        }
    }
}

// Every non-comment line of `lines` holding a `throw`, restricted to one method when `method` is given,
// as "file:line  text" — the same range GuardsIn reads.
static IEnumerable<string> ThrowsIn(string file, string[] lines, string? method)
{
    int from = 0;
    int to = lines.Length;
    if (method is not null)
    {
        from = Array.FindIndex(lines, l => l.Contains($" {method}(", StringComparison.Ordinal));
        if (from < 0)
        {
            yield break;
        }

        int end = Array.FindIndex(lines, from, l => l == "    }");
        to = end < 0 ? lines.Length : end;
    }

    for (int i = from; i < to; i++)
    {
        string text = lines[i].TrimStart();
        if (!text.StartsWith("//", StringComparison.Ordinal) && !text.StartsWith('*')
            && Regex.IsMatch(lines[i], @"\bthrow\b"))
        {
            yield return $"{Path.GetFileName(file)}:{i + 1}  {text}";
        }
    }
}

// The gate-file line each failing test stopped on, mapped back to the clean text. Each failure's trace
// is read down to the first frame in a gate file — the innermost of ours, which is the assertion or the
// helper it sits in; xUnit already hides its own frames.
static List<(string File, int Line)> TrippedGuards(
    string log, Dictionary<string, string[]> mutated, Dictionary<string, string[]> clean)
{
    List<(string File, int Line)> tripped = [];
    bool inFailure = false;
    bool taken = false;
    foreach (string raw in log.Split('\n'))
    {
        string line = raw.TrimEnd('\r');
        if (line.StartsWith("failed ", StringComparison.Ordinal))
        {
            inFailure = true;
            taken = false;
            continue;
        }

        if (!inFailure || taken)
        {
            continue;
        }

        Match frame = Regex.Match(line, @"^\s+at .+ in (?<path>.+):(?<line>\d+)\s*$");
        if (!frame.Success)
        {
            continue;
        }

        string path = frame.Groups["path"].Value.Replace('\\', '/');
        string? file = clean.Keys.FirstOrDefault(f => path.EndsWith("/" + f, StringComparison.Ordinal));
        if (file is null)
        {
            continue;
        }

        taken = true;
        int reported = int.Parse(frame.Groups["line"].Value, CultureInfo.InvariantCulture);
        if (MapToClean(clean[file], mutated[file], reported) is int cleanLine)
        {
            tripped.Add((file, cleanLine));
        }
    }

    return tripped;
}

// Maps a line of a mutated gate file back to the clean one. Lines before the first difference map to
// themselves, lines after the last one shift by the change in length, and a line inside the edited
// region maps to nothing — so a guard is only ever credited when the attribution is certain.
static int? MapToClean(string[] clean, string[] mutated, int line)
{
    int shortest = Math.Min(clean.Length, mutated.Length);
    int prefix = 0;
    while (prefix < shortest && clean[prefix] == mutated[prefix])
    {
        prefix++;
    }

    if (prefix == clean.Length && prefix == mutated.Length)
    {
        return line;
    }

    int suffix = 0;
    while (suffix < shortest - prefix && clean[^(suffix + 1)] == mutated[^(suffix + 1)])
    {
        suffix++;
    }

    if (line - 1 < prefix)
    {
        return line;
    }

    return line - 1 >= mutated.Length - suffix ? line + (clean.Length - mutated.Length) : null;
}

/// <summary>What a mutation's result is supposed to be. Anything else is a failure of the run.</summary>
/// <remarks>
/// ⭐ <b>Typed rather than free text, because the harness has to compare its verdict against it.</b> An
/// expectation that is printed and never read makes <c>killed</c> mean only "the filtered class went
/// not-<c>Passed!</c>" — a kill by an unrelated assertion, by the compiler, or by a test-host abort would
/// all certify the gate equally.
/// </remarks>
enum Expect
{
    /// <summary>A named test must fail, and it must be the one this mutation names.</summary>
    Test,

    /// <summary>
    /// The compiler must reject it. Only for the two that exist to prove the parse-error decision: every
    /// rule iterates <c>ParsedFiles</c> and drops what did not parse, so an assertion for unparseable
    /// markup could never fire, and <c>AVLN1001</c> is what makes that safe.
    /// </summary>
    Build,

    /// <summary>
    /// ⚠ It must stay <b>green</b>, and that is the result. Two of them exist, and together they are the
    /// whole evidence for adopting both <c>AQ1003</c> and the nuspec gate rather than treating either as a
    /// superset of the other: <c>PrivateAssets="all"</c> hides a used type from the nuspec, and Roslyn
    /// emits no reference for an unused package, so each gate is blind to exactly what the other catches.
    /// A green here is asserted, not tolerated — if one starts failing, the claim has moved.
    /// </summary>
    Green,
}

/// <summary>A mutation: what it does, the edit, the class to run, and what must happen to it.</summary>
/// <param name="ExpectedKiller">
/// For <see cref="Expect.Test"/>, the test method name that must appear among the failures. For
/// <see cref="Expect.Build"/>, the diagnostic id the compiler must reject it with. Both are compared, not
/// merely printed. For <see cref="Expect.Green"/>, prose saying why no test is expected to fail.
/// </param>
sealed record Mutation(
    string Name,
    string Targets,
    Func<bool> Apply,
    string ExpectedKiller,
    Expect Expect = Expect.Test);

/// <summary>An assertion in a gate file: a guard that some mutation must be the first to make fail.</summary>
/// <param name="InertPin">
/// Set when the line above the assertion reads <c>// inert-at-pin: &lt;package&gt; &lt;version&gt;</c>: forward
/// cover that no mutation can trip at that version of that package. It excuses the guard only while the
/// pin is still that version, and a mutation that trips it anyway makes the marker itself a failure.
/// </param>
sealed record Guard(string File, int Line, string Text, (string Package, string Version)? InertPin);
