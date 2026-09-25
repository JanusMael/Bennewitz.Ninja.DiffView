# 00025 — Nothing adopted that cannot fail

> Status: **approved 2026-09-24**. Supersedes nothing.
> Phases 0–4 are built and mutation-tested; phase 5 is the record. The figures below were measured on
> the built branch, and the mutation and guard counts are the harness's own report.

`Bennewitz.Ninja.XamlQuality` and `Bennewitz.Ninja.AssemblyQuality` hold **eight** rules at the pinned
versions — `XQ1001`–`XQ1004` and `AQ1001`–`AQ1004`. This plan adopts six, declines two, and adds one
hand-rolled gate for a question no rule asks. The question is never whether a rule is good — it is
whether it can **fail here**. One that cannot reports zero, reads as coverage, and is indistinguishable
from a clean codebase. Both libraries say so and carry an `Inspected` count for it; for all three
AssemblyQuality rules adopted here that count is taken before the rule's own test, so it cannot tell a
live configuration from a dead one, which is why this plan leans on positive controls and mutations
instead.

## The standard

**Where a gate can be blinded, some guard must assert something that is false of the blinded state —
and a guard counts as proven only when a committed mutation makes it the first assertion to fail.** A
test stops at its first failing assertion, so "killed by the test it names" proves the assertion that
tripped and nothing after it; the harness therefore attributes every failure to the line it stopped on.

| Failure mode, each measured in this repository | What answers it |
|---|---|
| A rule that inspects nothing reports zero | Adopt only a rule a mutation can make fail here |
| A findings assertion with nothing on how much was inspected | A floor well below the population — what it guards is a zero, not a count |
| A floor is satisfied by any larger population | An equality over the same readings: the two subjects account for the whole scan |
| A hand-written element set can lose a name with every floor green | The set is derived from the assembly, and each name is floored on its own |
| One reading's rule instance built apart from another's | One element set and one adopted instance, and every reading taken from them in one test |
| One edit narrowing a derivation narrows every reading taken from it | A cross-check against an independent source: every control of ours the markup instantiates is derived or excluded |
| A guard parameterised by its subject validates whatever it is handed | Each subject guard asserts a property the other subject lacks |
| A positive guard that holds by coincidence | A negative guard beside it, proven by a mutation that makes the coincidence false first |
| A positive control that proves only that the library works | The control reads the adopted instance, anchored to its fixture by name |
| An expectation printed and never compared | A typed expectation the harness compares: a named test, a named compiler diagnostic, or green |
| A test proven while the assertions after its first are not | Every assertion in the gate files is derived, and each must be tripped first by some mutation |
| A guard written as a `throw`, which a mutation trips and nothing credits | A gate file holds no `throw`, and the harness refuses to start on one |
| A marker that expires in silence | `--guards` fails on an expired marker, and CI runs `--guards` |
| A declared name that nothing fills | A runtime walk that checks names on screen |
| A check that passes because it reached nothing | What the walk must reach is derived from the markup and anchored to the rule's own count |
| A repair that guards exactly the case that prompted it | No hand-picked subject anywhere a derived one exists |

⚠ **A helper that resolves and checks a subject does none of this.** `Scan("DiffView.Avalonia")` passes
its own assertion perfectly well: a guard parameterised by the thing it guards validates whatever it is
asked for. The helper's job is to make the subject appear once; the property guard is what makes the
wrong subject fail.

⚠ **Completeness proves that every assertion can fail. It cannot see an assertion that is missing.** A
gate with no floor has no floor to trip, and the per-assertion check is satisfied. That is why every
new or changed guard gets its blinding mutation in the same change, and why a gate's design is reviewed
for the guards it lacks, not only the ones it has.

⚠ **Every reading of the accessibility gate starts from one derivation, and the markup cross-check is
what stands outside it.** One edit narrowing the derivation trips the cross-check, and a cross-check that
reads nothing trips its floor; a mutation proves each. What still blinds the gate is two coordinated
edits, narrowing the derivation and the cross-check the same way. A third reading would fall to a third
edit, so none is added.

### What stays outside it

Hand-written numbers and lists no derivation protects. Each is held to an assertion that fires when its
claim stops being true, and changing one is a deliberate, visible edit:

| Outside the standard | Value | Held to |
|---|---|---|
| `StockInspectedFloor` | 8, against 14 | narrowing the library subject trips it; the stock accounting stops its reading widening |
| `DemoInspectedFloor` | 41, against 53 | narrowing the demo subject trips it; the full accounting stops its reading widening |
| `XQ1003`'s floor | 20, against 34 | a scan handed no assemblies |
| `XQ1004`'s floor | 12, against 22 | a scan re-rooted off the markup |
| `XQ1003`'s expected `Skipped` | `DiffPaneHeader`, `DiffStatusStrip` | a part-less control gaining a part |
| `XQ1003`'s assemblies | the model and the UI — one floored, one held inert | the model gaining a themed control with a part |
| `LiveFrameworkElements` | `Menu` | per-name coverage |
| `ForwardCover` | `TextEditor`, `Expander` | the inverse: each must stay inert |
| `Excluded` | `DiffFindBar` | an exclusion must name itself or cover no markup |
| `inert-at-pin` markers | one — `XQ1004`'s `Skipped` guard | the marker expires when the pin moves, which fails a full run and `--guards` alike; a full run also fails if any mutation trips its guard |
| The harness's gate files | four test files and the nuspec method | each must yield at least one guard and hold no `throw`, or the run stops |
| The mutation set | 61 | every guard in the gate files tripped first by at least one |

## What is adopted, and what proves it

| Gate | Measured | Mutations |
|---|---|---|
| `XQ1002` interactive names, with a runtime walk | 78 inspected over `src` — 25 in the library, 53 in the demo; stock names 14 and 50; 0 findings. The walk reaches all 25 library parts on screen across four states | 31 — 29 killed by a named test, 2 by the build as `AVLN1001` |
| `XQ1003` template parts | 34 inspected against a floor of 20; 0 findings; `DiffPaneHeader` and `DiffStatusStrip` skipped; the model assembly 0 | 4 |
| `XQ1004` fixed grid slots | 22 inspected against a floor of 12; 0 findings; all 22 in the library | 2 |
| `AQ1002 ["DiffPlex"]` over `DiffView.Core` | 781 inspected, 0 findings | 8 |
| `AQ1003 ["Avalonia"]` over `DiffView.Core` | 9 inspected, 0 findings | 7, one green by design |
| `AQ1001` defaulted tokens | 2 inspected, 0 findings after the fix | 4 |
| **The nuspec dependency gate** (hand-rolled) | `DiffPlex`, `Microsoft.Extensions.Logging.Abstractions`; no framework reference | 5, one green by design |

**61 mutations — 57 killed by the test they name, 2 by the build, 2 green by design — and all 44
assertions in the gate files accounted for: 43 tripped first by at least one mutation, and the
forty-fourth marked inert at the current pin.** None is untripped.
They are committed, in `scripts/mutate-gates.{cs,sh,ps1}`, so the proof that a gate can fail is
reproducible by anyone rather than by the session that ran it.

What the harness checks, each of which fails the run:

| Verdict | Meaning |
|---|---|
| `survived` | the gate reported clean over a planted defect |
| `wrong-killer` | a different test failed; the named one stayed green |
| `killed-by-the-build` | the compiler stopped it before the gate ran — or, for the two that exist to be build kills, it failed without the `AVLN1001` they prove |
| `aborted` | the host died: no summary outcome, or `Failed!` with no failing test |
| `no-op` | the edit matched nothing; not a result |
| `unexpectedly red` | a mutation meant to stay green failed, so the complementarity it encodes has moved |
| untripped guard | an assertion in a gate file that no mutation made fail first |
| bad marker | an `inert-at-pin` guard that a mutation tripped, or whose pin has moved |

It refuses to start on a dirty `src` or `tests`, because it reverts both before every mutation, and
while any gate-file line throws, because a guard must be an assertion it can attribute a failure to. Once
mutations have started it rebuilds on every exit it controls, because reverting a mutation does not
unbuild it. And it derives what a mutation may touch from the whole repository's status before and after
the edit, so a mutation that writes outside `src` and `tests` is caught and its stray put back, whether
or not anyone listed the path.

⚠ **The proof is point-in-time.** A full run proves the gates at the commit it ran on, takes minutes,
and is owed after any change to a gate file, a gated subject or a pin. CI holds the standing half:
`--guards`, on Linux, lists the derived guards and fails on an expired `inert-at-pin` marker or a
gate-file `throw` — the two changes that would otherwise land green. It proves no guard.

⚠ **The two green-by-design mutations are asserted green, not tolerated.** Together they are the
evidence for adopting **both** `AQ1003` and the nuspec gate — see below.

⚠ **Two assembly mutations are coarser than the table implies.** *"The model uses an Avalonia type"* and
*"the same violation, caught by `AQ1003` instead"* turn all three assembly tests red, because `AQ1002`'s
and `AQ1001`'s negative subject guards read the same `UiFramework` constant `AQ1003` is configured from.
The complementarity claim survives, but one misspelling of that constant blinds two guards while only
the third test complains.

### Declined, each on a measurement

| Declined | Why |
|---|---|
| `XQ1001` Expander names | **0 inspected.** No `Expander` in this repository's markup. A rule reporting zero reads as coverage — so the *name* goes into `XQ1002`'s forward cover instead, where it costs nothing and catches the first use |
| `AQ1004` namespace shadow ⏳ | **Declined at `2026.3.922`, and expected to be adopted at the next release.** Weaker than `NamespaceConventionTests` on three axes at this pin, stronger on none: it replaces one of that file's two `[Fact]`s; it reads `GetExportedTypes()` where the test reads `GetTypes()`, so an internal-only shadow escapes it; and it **cannot detect its own inertness** — `Inspected` counts our namespace segments, not the referenced roots it compares them against, so a total load failure reports what a clean codebase reports. Measured with the confound controlled: `DiffView.Core`'s 39 exported types give `1×2 + 38×3 = 116`, and a `PackageReference` used only by internal code takes it from 9 referenced assemblies to 10 while `Inspected` stays at **116**. ⚠ A fixture that adds an exported type alongside the reference measures 119 and appears to contradict this; the +3 is the type |

⚠ **`AQ1002`'s stock namespaces cannot be declined at this pin.** `SurfaceLeakRule(IEnumerable<string>)`
concatenates rather than replaces, so adopting `["DiffPlex"]` also ships `System.Text.Json.Nodes` and
`Newtonsoft.Json.Linq` — prefixes nothing here references. They are inert and unavoidable; the positive
control is what proves the prefix that matters is live.

## The pin is not a pair

`XamlQuality` is pinned at **`2026.3.924`** and `AssemblyQuality` at **`2026.3.922`**, the newest
release of each on 2026-09-24. 924 carries four things this plan relies on:

- **The digest fix (`4f7bd62`)**, which retires defect A: `docs/theme-audit.md` regenerates once, and the
  ClaudeForge row reads `b7ea0ec438c5` — the value predicted before the release existed, with nothing but
  digests moving.
- **`XQ1003` reads parts from compiled code (`1d9ccac`)**, so its count is 34.
- **`XQ1003` names what it skips (`52e481c`)** instead of returning a silent `Clean(0)` when handed no
  assemblies.
- **`XQ1004` in a published release.**

No `AssemblyQuality` release past `922` existed on 2026-09-24. The bump is phase 0 rather than a later
change because a pin bump is a behaviour change for these gates and belongs in a change that looks at
them.

## The two layering gates are complementary, and that was measured

Neither contains the other, and one fixture proves it — `PrivateAssets="all"` on an `Avalonia`
`PackageReference`, plus one public member returning `Avalonia.Point`:

| Gate | Outcome |
|---|---|
| The nuspec gate | **stays green** — `PrivateAssets="all"` keeps the dependency out of the nuspec entirely |
| `AQ1003` | **goes red** — the assembly reference is emitted, because the type is used |

And the mirror case, an `Avalonia` `PackageReference` nothing uses: the nuspec gate goes red and `AQ1003`
stays green, because Roslyn emits no reference for an unused package. The case `AQ1003` alone catches is
the worse one — a consumer gets a `FileNotFoundException` rather than a fat dependency graph. Both
directions are green-by-design mutations.

⚠ The nuspec gate's coverage depends on `CentralPackageTransitivePinningEnabled` in
`Directory.Packages.props`: without it a transitively acquired package would not appear in the nuspec.

⚠ The nuspec gate asserts the absence of framework references **before** the dependency set. A shared
framework that carries one of the model's packages gets that package pruned from the nuspec's
dependencies too — measured with ASP.NET Core and the logging abstractions — so in the other order a
framework reference could only ever surface as a changed dependency set.

⚠ The nuspec gate reads **`DiffView.Core`'s** nuspec only. A `PackageReference` added to
`DiffView.Avalonia` is ungated; the question this gate asks is whether the model package drags the UI
in, and widening it is a separate decision.

## What the next release changes

⏳ Three reasons recorded above are properties of the pin, not of the rules, and on 2026-09-24 upstream
had fixed them in commits not yet released:

| Recorded reason | Retired by |
|---|---|
| `AQ1004` cannot detect its own inertness | `AssemblyQuality 05a6060` — no referenced roots now contributes 0 |
| `AQ1004` reads exported types only | `AssemblyQuality 044e71a` — `IncludingInternalTypes()`, which also adds `Skipped` and reads forwarded types |
| `AQ1002`'s stock set concatenates and hides a zero behind 781 | `AssemblyQuality 05a6060` — `Only(namespaces)`, under which stock reads 0 |

`05a6060` also reports a token-less overload whose parameters are a tokened sibling's minus the token —
the shape of an overload pair that passes `AQ1001` and passes `Inspected` and is wrong anyway.

And one guard is waiting on upstream rather than retired by it: the pinned `XQ1004` never populates
`Skipped`, so its `Skipped` guard is forward cover marked `inert-at-pin`. The marker expires on the next
`XamlQuality` bump, which is when that guard is owed a mutation — or a new marker, re-measured by
`scripts/xq1004-skips.sh` against the new pin.

**This plan does not wait for either release.** Re-evaluating `AQ1004` and `AQ1002`'s configuration is
its own item, which phase 5 opens in `PROGRESS.md`, not a phase here: the work is a pin bump and a
re-measurement against releases that did not exist when this plan was approved.

## Decisions

| Decision | Why |
|---|---|
| ⚠ **What this gate does and does not establish** | It asserts that every interactive control in the markup *carries* an `AutomationProperties.Name`, and that each one the library declares is named on screen. It does **not** establish that a screen reader reaches one: no control of this library overrides `OnCreateAutomationPeer`, so each returns `NoneAutomationPeer` with `ControlType` `None`, and a control-view traversal skips it. Automation peers, `AutomationId`s and `OverlayPopups` are separate work, recorded in `PROGRESS.md` as *an agent cannot drive this library*; this gate is their precondition, not their substitute |
| **`XQ1002` replaces the walker in place, keeping the class name** | Its stock seventeen are byte-identical to the walker's, and keeping the name leaves every citation resolving. ⚠ This diverges from `PROGRESS.md`'s open item *Adopt `XQ1002` and delete `AccessibilityCoverageTests`*, which says *delete* the class |
| **`AutomationName.IsDeclaredOn` is the reason, not the mechanism** | It rejects `AutomationProperties.Name=""` — measured: the walker survives that mutation and this gate kills it — and a whitespace-only name too |
| ⭐ **The element set is derived from the assembly** | Every public `Control` this library ships, minus a reasoned exclusion list, plus framework names split into live and inert. A written list can lose a name with every floor green — measured for `SideBySideDiffView` and `InlineDiffView`, whose only elements are the demo's two. A derived set has no name to delete, and a new public control joins it at once. ⛔ So a new control turns the gate red until it is named or excluded, which is the intended cost |
| ⭐ **An exclusion has to earn itself** | Adding a control to `Excluded` drops its elements out of the scan, and no floor sees it — measured against `DiffPaneHeader`, whose exclusion would have cost four elements in silence. So only two reasons can stand, and both are checked by constructing the control: it **names itself** where the scan cannot read, or **nothing of ours instantiates it** |
| **`DiffFindBar` is the only exclusion** | It names itself in its own constructor, from `DiffViewStrings.FindBarName`, alongside the eleven child names it sets there. Requiring a markup attribute too would mean two sources for one string. Self-naming is an established pattern — `DiffFindBar`, `DiffMargin`, `DiffPaneMenu` — and only this one surfaces, because margins are not markup elements |
| ⭐ **`DiffPaneHeader` gains a name, and that is a localization cascade** | Four elements across both views carried none. Every `AutomationProperties.Name` in the library's themes is a `{TemplateBinding …}`, because §6 routes every user-visible string through `DiffViewStrings` — so it is two keys (`Header.Left.Name`, `Header.Right.Name`, separate sentences per §6), two English defaults whose summaries pass both `StringCatalogueTests` gates, the regenerated neutral `Strings.resx`, **sixteen translations across the eight shipped locales**, **eight regenerated `docs/locale-review/*.md`**, a `StyledProperty` pair on each view, and `RefreshStrings` wiring in both |
| **`DiffStatusStrip` joins the set although it was already named** | A set fitted to the markup contains exactly the controls that happen to be named; a derived set contains the ones that are interactive |
| ⭐ **Per-name coverage floors each name, from the adopted instance** | Each derived name must inspect at least one element, asserted by dropping it and comparing against the adopted instance's own total, in the test that asserts that instance's findings. Read from an instance of its own, in a test of its own, it proved a list the findings never used: measured, the findings instance built without `Menu`, over a demo whose menu bar had lost its name, passed the whole suite — 622 of 622. The inverse holds for forward cover: `TextEditor` and `Expander` must stay inert, and one that starts matching belongs where something floors it |
| ⭐ **The derivation is cross-checked against the markup** | Every reading of the gate derives from one element set, so one edit narrowing the derivation narrows all of them: measured, `Control` narrowed to `TemplatedControl` dropped the minimap and the connector gutter with the gate green. The markup is the independent source — every element it instantiates from the library's own namespace must be a derived control or an exclusion — and the cross-check is floored, so it cannot pass by reading nothing |
| **`TextEditor` and `Expander` are forward cover, and neither is ours** | ⭐ **A name is not a gate.** An inert rule reports zero and reads as coverage; an inert name costs nothing and catches the first use. Declining `XQ1001` is what makes `Expander` safe here — the two would report one defect twice. `TextEditor` is AvaloniaEdit's, carried because the walker this replaced carried it |
| **`Menu` is live, not forward cover** | The demo's menu bar is interactive and nothing else covers it; its fifty `MenuItem`s are stock. Proven by arithmetic: fifty `MenuItem`s, fifty stock inspections, fifty-one if `Menu` were among them |
| ⭐ **One derivation per subject, and every floor's reading inside an equality** | The library and demo subjects are derived once, from the findings scan's own root, in the test that floors them. The stock floor, the demo floor and the findings all read counts that also sit in an accounting equality — the library and the demo account for the whole scan, for the full set and for the stock names. A floor in one test and an equality in another, each deriving its own subject, leave every floor open to widening one call at a time. Narrowing trips a floor; widening trips an equality; the direction of an equality's mismatch names which subject moved |
| **No floor sits at its exact population** | A floor at population buys no detection and demands a visible edit on every legitimate change, with failure text asserting a cause the failure does not establish. Every floor here guards a zero: `StockInspectedFloor` 8 against 14 — eleven of the fourteen are `DiffFindBar`'s children and the other three are the banner action buttons and the dismiss button, so removing a toggle moves the count, while a name added upstream can only raise it; the demo's 41 against 53; `XQ1003`'s 20 against 34; `XQ1004`'s 12 against 22 |
| **No parse-error assertion** | It could never fire: every rule iterates `ParsedFiles`, which drops a file that did not parse. What makes that safe is the compiler — `AVLN1001` rejects unparseable markup, and `EnableDefaultAvaloniaItems` is set nowhere, so every `.axaml` under `src` is compiled. Two mutations prove it — one hand-authored view, one generated dictionary — and the harness requires the build to fail **with** `AVLN1001`, not merely to fail |
| **Both layering gates** | Above, with the blind spot of each stated where it is relied on |
| **`AQ1002`'s control reads the adopted instance, anchored to its fixture by name** | Its `Inspected` is 781 whether the prefix is live or dead, because `SurfaceLeakRule` counts candidates before testing them. The control must find a leak naming `LeakControl`, not merely any leak, because the stock namespaces would satisfy a count-only control with the fixture deleted. A mutation configures the instance with a prefix nothing uses, and the control is the guard that trips |
| ⭐ **`AQ1002`'s negative subject guard** | The positive guard — the subject references DiffPlex — is a coincidence: DiffPlex is a declared dependency of the UI package, so one `using DiffPlex.Model;` there makes it true of the wrong subject too. The negative — the subject references no UI framework — cannot drift that way. A plain subject swap stops on the positive guard first, so the mutation that proves the negative makes the coincidence false first: the UI assembly uses a DiffPlex type, and then the subject is swapped |
| **All three assembly gates resolve their subject once and guard a property the wrong subject lacks** | Each swaps its **own** occurrence in the harness, never the first three times over. The subject helper's own assertions are proven by resolving a name to the wrong assembly and by asking it for one it does not gate, and each rule's `Inspected > 0` by handing it an empty context |
| ⭐ **`AQ1001` has a positive control** | `CancellationTokenRule`'s `Inspected` is a candidate count like the others', measured at `inspected=2 findings=1` over a fixture, so a predicate that has stopped firing would report today's `2 / 0` exactly. `TokenDefaultControl` is the control, anchored by name, and a fixture that stops defaulting its token is the mutation |
| **`AQ1001` is adopted rather than declined** | It is a policy rule. The case that the window to change a public signature for free closes at the first publish is true but does not force the call, because the first publish is on hold with no date; this is a decision taken in session on 2026-09-23, recorded in `DECISIONS.md` in phase 5 |
| **The fix moves the token ahead of the optional parameter** | `Build(left, right, CancellationToken, DiffOptions? = null)`. An overload pair would hand an omitting caller a token it never chose, which is what the rule forbids; dropping both defaults leaves 44 call sites writing a literal `null`, where the reorder leaves 1. **71 call sites across 17 files**, build 0/0, `AQ1001` at 2 inspected / 0 findings |
| ⭐ **The mutations are committed, and the harness refuses to eat your work** | `scripts/mutate-gates.{cs,sh,ps1}`, in the convention `scripts/` uses — C#, because a regex in a C# string literal is never re-read by a shell. It refuses a dirty `src` or `tests` unless `--force`, because it reverts both before every mutation, and a harness that does so on a dirty tree destroys whatever was uncommitted |
| ⭐ **A guard is proven only when a mutation trips it first** | The harness derives every assertion in the four gate files and the nuspec method, reads each failing test's trace to the first line in a gate file, maps that line back through any edit the mutation made to the file, and on a full run fails every assertion no mutation reached first. Measured: 44 assertions, 43 tripped first by at least one mutation, the forty-fourth marked inert at the pin. Where an assertion cannot be reached first, the answer is a change of shape — an assertion order, an extraction not dressed as a guard — never a weaker guard |
| ⛔ **A guard is an assertion, and a gate file holds no `throw`** | A `?? throw` or a throwing switch arm is tripped by mutations and credited to nothing, which puts a guard beyond the completeness check. So the subject helper and the exclusion test assert instead; setup that must fail — the runtime walk's failing build — is `CompositeHost.FailingBuilder`, outside the gate files; and the harness refuses to start while any gate-file line throws |
| **The proof is point-in-time, and CI holds its standing half** | A full run is owed after any change to a gate file, a gated subject or a pin, and `AGENTS.md` §5 says so. CI runs `--guards` on Linux, which fails on an expired `inert-at-pin` marker and on a gate-file `throw`. A full run in CI is not adopted here: its runtime on a hosted runner is unmeasured, and a job that has never run is not evidence |
| **Forward cover that cannot fail at the pin is marked, not claimed** | Measured over nine shapes — bindings, resources, unparseable and bound definitions, a bound index, a bound size, a span — the pinned `XQ1004` never populates `Skipped`; an undecidable placement is counted as inspected and reported clean. So `GridSlotTests`' `Skipped` guard cannot fail at 924. It stays, as forward cover for the release that starts skipping, under an `inert-at-pin` marker the harness reads: it excuses that one guard only while `XamlQuality` is pinned at the version it was measured against, and fails the run if any mutation trips it. `scripts/xq1004-skips.{cs,sh,ps1}` is the measurement, committed so anyone can repeat it: its package reference carries no version, so it reads whatever `Directory.Packages.props` pins, and it exits 0 when the claim holds, 1 when the rule can now skip, and 2 when its control finds nothing — a rule that reads nothing also skips nothing |
| ⛔ **A declared name is only as real as whatever fills it** | The markup gate reads *declarations*, so `AutomationProperties.Name="{TemplateBinding LeftHeaderName}"` is worth exactly what sets `LeftHeaderName`, and every such property registers `string.Empty` as its default. Measured, each with the whole suite green: delete a view's header-name fills, `DiffFindBar`'s eleven child-name fills, or the status strip's `DismissText` fill, and those controls go unnamed |
| ⭐ **So the runtime walk takes nothing from anyone's list** | It walks what each view realises and checks every control whose type the markup gate treats as interactive — upstream's public `FrameworkInteractiveElements`, plus `ElementNames()`, plus `Excluded`, because an exclusion is a claim about what the *markup scan* cannot see and this reading sees exactly that. And it must **reach** every interactive part the library's markup declares, as (owning control, part name) — derived from the markup, anchored to `XQ1002`'s own count, and required to be non-empty. A walk that asserts only "nothing unnamed" passes by reaching nothing: measured, dropping two `OpenFind()` calls, or pointing "ours" at the test assembly, let eleven unnamed controls through the whole suite |
| **The walk checks only what is on screen, and puts everything on screen** | A control that is realised but hidden is not announced — the banner's action button sits collapsed with an empty name whenever a build has nothing to say, which is correct. On its own that rule is a blind spot: it hid the dismiss button, visible only while a failure shows. It is safe because the walk runs four states — each view with its find bar open, and each with a build that fails, which puts the Retry action on the banner and the failure, with its dismiss button, on the strip — and the coverage assertion requires every declared part to have been checked on screen in one of them. Every part of that setup is load-bearing, and mutations remove the open find bars and the failing builds in turn. It starts at each view rather than the window, because the host constructs the view and the demo's markup is what names it |
| ⭐ **`XQ1003` is guarded by which controls it skipped** | A themed control with no template parts is skipped legitimately, and two of ours are, so "`Skipped` is empty" is the wrong assertion. Handed no assemblies the rule skips all seven themed controls; the real configuration skips exactly `DiffPaneHeader` and `DiffStatusStrip`. Keyed on `XamlSkip.Subject`, because `Reason` is prose |
| **`XQ1003`'s forward-cover assertion comes first** | The model assembly must stay inert. Anything that makes it contribute a part also puts an undeclared part in the main scan, so asserted after the findings it could never be the first to fail. And a model control is only *inspected* if it has a theme: without one the rule skips it, and the expected-`Skipped` assertion is what trips — measured — so the two assertions between them cover both ways the model can stop being inert |
| ⭐ **`XQ1004` is adopted on what it does, which is size, not indices** | A child asking for more room than its **fixed** slot keeps that size in the automation tree while the screen may clip it. A column index past a five-column grid survives this gate, measured — so it is adopted on the defect it can see. The 16-pixel spacer between the panes is the one fixed slot in the library's grids |
| **Forward cover cannot be protected against deletion, and that is stated** | A thing contributing nothing contributes nothing whether or not it is passed, so no assertion notices its removal. What is detectable is the moment it stops being inert, and every forward cover here asserts exactly that |
| ⛔ **Reverting a mutation does not unbuild it** | Once mutations have started, the harness rebuilds on every exit it controls, from a `finally`. Otherwise the last mutation's assemblies sit in `bin/` while the source is reverted, and the next `dotnet test --no-build` runs mutated code against clean markup — measured, `GridSlotTests` read 0 inspected where it measures 22. A run killed outright cannot rebuild, so a `--no-build` test run after one is rebuilt first |
| ⛔ **`--expect` is derived from discovery, never written down** | `catch-crash`'s `Judge` compares the run's total with `!=`, so a number one below the suite does double harm: every clean run reports a false abort, and the one-test-short run the flag exists to catch passes. Any written count goes stale at the next test added. `--expect auto` reads the total from `dotnet test --list-tests`, and no document carries the number |

## Scope

**In.** `Bennewitz.Ninja.XamlQuality` **`2026.3.924`** and `Bennewitz.Ninja.AssemblyQuality`
**`2026.3.922`**, with the `Directory.Packages.props` comment on why the two differ; `XQ1002`, `XQ1003`,
`XQ1004`, `AQ1002`, `AQ1003`, `AQ1001`; the nuspec gate; the two signature changes; `Header.Left.Name`,
`Header.Right.Name` and everything downstream of them — the neutral `Strings.resx`, the eight locale
`.resx` files, the eight `docs/locale-review/*.md`, a `StyledProperty` pair on each view, and both
`RefreshStrings` methods; `CompositeHost.FailingBuilder`; `ChromeToggleTests`' comment on `MenuItem`;
`scripts/mutate-gates.{cs,sh,ps1}`; `scripts/xq1004-skips.{cs,sh,ps1}`; `scripts/catch-crash.cs`'s
`--expect auto`; the concurrent pipe reads in `PackagingTests`' and `CatchCrashTests`' subprocess
helpers; the `--guards` step in `.github/workflows/ci.yml`; `.gitignore`'s rule for `reference/`;
`docs/theme-audit.md`, regenerated once by 924's digest fix; `AGENTS.md` §5; `PROGRESS.md`;
`DECISIONS.md`; `CHANGELOG.md`.

**Out.** `XQ1001` and `AQ1004`, declined above. `NamespaceConventionTests` stays unchanged. Widening the
nuspec gate to `DiffView.Avalonia`. Running the full mutation harness in CI.

**Recorded as findings, not corrected here.** Two statements that `XQ1002`'s element set leaves out
`MenuItem`. Both are false — `MenuItem` is one of the stock seventeen, which is exactly why the demo's
fifty menu items are inspected at all — and both are records rather than claims about the code:

- `PROGRESS.md`, the row cell containing *"`AccessibilityCoverageTests`' element set is a hardcoded
  literal that does not include `MenuItem`, so it would not have caught this"*. That sits in plan 00022's
  testing record, which is history, so it stays as written.
- `plans/00022-three-pieces-of-chrome-you-can-turn-off.md`, which is approved and never edited.

`ChromeToggleTests`' comment making the same claim is code, so it is corrected, and says what that test
adds: that the three entries exist, under the names their handlers read. Each record is quoted rather
than cited by line, because this plan edits `PROGRESS.md`.

### What this does to plan 00021

Plan 00021 is approved with phase 2 open and specifies *"`"DiffViewer"` added to
`AccessibilityCoverageTests.InteractiveControlElements`."* That field does not survive phase 1, and an
approved plan is never edited, so this one states the retarget: **what goes away is the element-set edit,
not the obligation — and there are now two.**

- **The markup gate.** A public control no markup of ours instantiates covers nothing, and per-name
  coverage turns the gate red — which is `DiffViewer`'s arrival shape, and a mutation proves it. A
  top-level view's theme declares a `ControlTheme`; it does not instantiate a `<DiffViewer>`. So phase 2
  must host a `DiffViewer` in the demo or add it to `Excluded` with a reason.
- **The runtime walk.** If `DiffViewer`'s theme declares interactive parts, they are in the walk's
  requirement, and the walk must realise a `DiffViewer` in a state that puts each on screen.

**And the two branches touch the same code.** Plan 00021 phase 1, pushed on
`feat/the-viewer-beside-the-editor`, moves the side-by-side view's `RefreshStrings`, its `DismissText`
fill and its default builder into `DiffBuildController`, and that builder calls
`DiffDocumentBuilder.Build` in the argument order phase 4 changes. Whichever plan lands second carries
the other's change: the header-name fills follow the moved `RefreshStrings`, the call takes the token
before the options, and two mutations re-point at `DiffBuildController.cs` — the side-by-side view's
header-name fills, and the side-by-side half of the dismiss-name fill. Neither can pass silently: the
old argument order does not compile, and a mutation whose pattern no longer matches is a `no-op`, which
fails the run.

Phase 5 records the two obligations and the collision in `DECISIONS.md`.

## Phases

**Phases 0–4** are built and proven in a scratch worktree; for them the work is transcription rather than
discovery. Phase 5 is the record.

| Phase | Size | Content | Mutations |
|---|---|---|---|
| **0 — The pin** | S | `XamlQuality` `2026.3.920` → **`2026.3.924`**, `AssemblyQuality` at `2026.3.922`. **Not cosmetic**: `920` ships `ExpanderAutomationNameRule` and nothing else, so phases 1–2 do not compile without it; and `docs/theme-audit.md` regenerates once, retiring defect A — the ClaudeForge row reads `b7ea0ec438c5`. `.gitignore` ignores everything under `reference/` but its three versioned files, because a checkout linked in from elsewhere is a symlink, which `reference/*/` does not match | — |
| **1 — The a11y gate** | M | `XQ1002` with the derived set, the exclusion guard, per-name coverage and findings from one adopted instance, the markup cross-check, one derivation per subject with its floors and both accountings, the live/inert framework split, and the walker gone; the runtime walk with its derived requirement, its four states and `CompositeHost.FailingBuilder`; the two header keys and the localization cascade; the demo's menu bar named, and `ChromeToggleTests`' comment corrected | 31 — 29 by a test, 2 by the build |
| **2 — Template parts** | XS | `XQ1003` with `.WithAssemblies(…)`, its forward-cover assertion first, a blinding floor of 20, and the expected `Skipped` subjects | 4 |
| **2b — Fixed grid slots** | XS | `XQ1004` with a blinding floor of 12, and its `Skipped` guard marked `inert-at-pin` | 2 |
| **3 — Surface and layering** | S | `AQ1002 ["DiffPlex"]` with its fixture control and its negative subject guard; `AQ1003 ["Avalonia"]` with its control; the nuspec gate; one `Scan(name)` resolver so each subject appears once, asserting on a name it does not gate. Adds the `AssemblyQuality` reference, and the `Directory.Packages.props` comment on why the two pins differ | 20, two green by design |
| **4 — `AQ1001`** | S | The token moved ahead of `options`, 71 call sites, the gate, and its positive control | 4 |
| **5 — The record** | S | `PROGRESS.md` — the open items on this plan, on adopting `XQ1002` and on `AQ1001`'s two findings, closed, and one opened for re-evaluating `AQ1004` and `AQ1002`'s configuration at the next `AssemblyQuality` release; `DECISIONS.md` — this plan's decisions, what it does to plan 00021, and the review history kept out of this plan; `CHANGELOG.md`; the resume anchor | — |

**Each phase moves `PROGRESS.md` with it:** the open item on this plan says which phases have landed, in
the same change as the phase. Phase 5 closes it.

**The mutation harness is committed as its own change**, with `AGENTS.md` §5's entries for it,
`catch-crash`'s `--expect auto`, `scripts/xq1004-skips.{cs,sh,ps1}`, the concurrent pipe reads and CI's
`--guards` step: it covers the gates of phases 1–4 and cannot land before them.

## Testing

| Test | Asserts |
|---|---|
| **A planted offender is found** | An unnamed control, an empty name, a header that lost its name, a find bar that lost its children's names, a dismiss button that lost its name, an unnamed menu bar, a live forward-cover name, a part no theme declares, a part-less control gaining a part, a child past its fixed grid slot, a leaked `DiffPlex` type, a used `Avalonia.Point`, an unused dependency or a framework reference in the nuspec, a defaulted token, a public control nothing instantiates |
| **A blinded gate goes red** | Each subject narrowed onto markup-free ground trips its own floor; each widened to the whole scan trips an accounting; the stock floor's reading widened alone trips the stock accounting. The findings instance built without a live name trips per-name coverage; the derivation narrowed to templated controls trips the markup cross-check; the cross-check pointed at another namespace trips its floor. `XQ1003` handed no assemblies trips its floor; `XQ1004` re-rooted off the markup trips its floor; each assembly rule handed an empty context trips its `Inspected > 0` |
| **The runtime walk's blindings go red** | The find bar never opened, no failing build, "ours" pointed at the test assembly, the requirement derived from markup-free ground, the requirement read without the stock names, and a declared part with no name — each trips the assertion written for it. The walk realises the library's views; the demo's markup is held by the markup gate alone |
| **An exclusion cannot hide markup** | A covered control excluded; an exclusion with no reason; an exclusion naming a type this library does not export; the control an exclusion rests on ceasing to name itself |
| **A misspelled or misconfigured prefix goes red** | For both AQ rules: the constant misspelled trips the subject guard; the adopted instance configured apart from the constant trips the positive control |
| **Every subject swap goes red** | All three, each swapping its own occurrence; `AQ1002`'s negative guard by a swap after the UI assembly uses DiffPlex; the subject helper by resolving a name to the wrong assembly, and by being asked for one it does not gate |
| **The two layering gates are complementary, in both directions** | `PrivateAssets="all"` plus a used type: the nuspec gate green, `AQ1003` red. An unused `PackageReference`: the nuspec gate red, `AQ1003` green. Both greens are asserted |
| **The nuspec gate reads the right nuspec** | A pack failure the build does not see, and a second `.nupkg`, each trip the assertion that guards against it. The single `.nuspec` inside the package is NuGet's guarantee rather than this test's — measured: a second `.nuspec` packed as content never reaches the package — so it is extraction, not a guard |
| **Unparseable markup is the compiler's job** | Two mutations, both required to fail the build as `AVLN1001` |
| **Every guard is proven** | All 44 assertions in the gate files: 43 tripped first by at least one mutation, 1 marked inert at the current pin, none untripped — the harness's own verdict on a full run |
| **The standing half fails when it should** | `--guards` exits 1 on a marker moved off the pin, and the harness exits 2 on a gate-file `throw` — each planted by hand and reverted, because a check on a run as a whole cannot be proven by a mutation inside one |
| **The suite** | **621, with no failures**, judged healthy by `catch-crash --expect auto` against the discovered total |

## Risks

| Risk | Assessment |
|---|---|
| **Completeness cannot see a guard that is missing** | Every assertion is proven able to fail; an assertion that was never written is not in the set. Answered by the rule that every new or changed guard gets its blinding mutation in the same change, and by reviewing each gate for the guards it lacks |
| **The harness is load-bearing evidence, so its defects are the plan's** | Its verdicts, its revert and its per-assertion attribution are exercised by every run. Three of its checks act on a run as a whole rather than on one mutation, so no committed mutation can prove them, and each was verified by hand: the out-of-scope check — two probe mutations, one writing a new file outside `src` and `tests` and one editing a tracked file there, each stopped the run with the stray put back; the marker expiry — a marker moved off the pin reads as expired, and `--guards` exits 1; and the `throw` refusal — a planted `throw` stops it with exit 2. It drains a child's stdout and stderr concurrently — reading stdout to its end first deadlocks once a child fills the stderr pipe, which presents as a hang — as do `catch-crash` and the two test helpers the gates run subprocesses through |
| **The proof is point-in-time** | A full run proves the gates at one commit, and CI does not make one. `--guards` in CI catches a pin that moves under a marker and a `throw` in a gate file. What it cannot catch is a gated subject that moves out from under a mutation's pattern, which surfaces as a `no-op` at the next full run — owed, per `AGENTS.md` §5, after any such change |
| **Plan 00021's branch moves code this plan edits** | Whichever plan lands second carries the other's change, and both ways it can go wrong are loud — the build and the harness. See *What this does to plan 00021* |
| **A derived set turns a new public control into a red gate** | Intended: adding a public `Control` requires naming its elements or excluding it with a reason, and — when its theme declares interactive parts — realising it in the runtime walk |
| **The runtime walk runs four hosts** | It is the one gate here that realises views. It stays in the serial suite like every other headless test |
| **The localization cascade is sixteen unreviewed translations** | Consistent with `PROGRESS.md`'s open item *The eight locales ship unread* — they already do, with the caveat owed to the consumer. `LocaleParity` holds the two new keys to every structural check |
| **`AssemblyQuality` had one published version** | `2026.3.922` on 2026-09-24, restored and verified to expose all four rules. Pinning it back would undo phase 3 but **not** phase 4, which changed two public signatures and 71 call sites |
| **The nuspec gate is hand-rolled** | Justified because no packaged rule asks its question, and measured complementary rather than assumed so |
| **68% of the findings scan is demo chrome** | 53 of 78, which is why the demo has its own subject and its own floor rather than a share of a total |
| **`XQ1002`'s stock set is upstream public API** | A name added there can turn green markup red, so a pin bump is a behaviour change and belongs in a change that looks at the gates |
| **A `CancellationToken` in the middle of a signature is unconventional** | The BCL puts it last so it can carry a default. Accepted: the rule forbids the default, and the alternative sprayed 44 meaningless `null`s |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. An approved plan is committed before
implementation and never edited; drift goes to `DECISIONS.md`.

**These phases go on `refactor/themeaudit-moves-to-xamlquality`, the open pull request** — a standing
instruction given in session on 2026-09-23, which phase 5 records. That branch introduces the XamlQuality
reference phases 1–2 need and carries the pin phase 0 bumps. It is rebased onto `main` before the phases
land, and the rebase and the phases are published together. The pull request is renamed and re-described
once its content is complete.

**A local green is not evidence.** Each phase is proven by its mutations, and this plan is done when
`Build & Test (ubuntu-latest)` agrees. Two things to read CI with: the `Culture Leg (de-DE)` failed only on
defect A, the same test as `ubuntu-latest`, and was never a localization defect; and Windows and macOS stay
red on defect C — plan 00024 phase 3 — which nothing here touches.
