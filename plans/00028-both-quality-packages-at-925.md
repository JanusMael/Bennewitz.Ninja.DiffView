# 00028 — Both quality packages at 925, every rule re-read here

> Status: **approved 2026-09-28**. Supersedes nothing.

`Bennewitz.Ninja.XamlQuality` and `Bennewitz.Ninja.AssemblyQuality` both released `2026.3.925` on
2026-09-25. DiffView pins `2026.3.924` and `2026.3.922`. Brian decided the same day to take both
together, in one plan and one branch, after plan 00021 lands and before plan 00023's phases 2–3;
`DECISIONS.md` *The `925` pin bumps go before plan 00023's phases 2–3* has why.

Both releases rename every rule id, and both change what a rule counts as inspected. This plan holds
plan 00025's standard unchanged: **a rule is adopted only if it can fail here, and a guard counts only
when a committed mutation makes it the first assertion to fail**. What changes is the reading each
rule gives at the new pins, and every decision below is made on that reading — including the readings
of the mutations that decide whether a rule can still fail.

## What 925 reads here

Measured over plan 00021's tip, `c465c65`, with both pins at `2026.3.925` and nothing else changed:
the build is clean under `-warnaserror` — the `[RequiresUnreferencedCode]` now on every
AssemblyQuality `Analyze` raises nothing in the test build — and the suite runs 658 with exactly two
failures, `TemplatePartTests` and `GridSlotTests`, both predicted. The theme-audit drift test passes,
so `docs/theme-audit.md` does not move. A scratch probe read every rule of both packages, and a
scratch runner applied the mutations the decisions turn on, restoring each file by content:

| Rule | At the current pin | At 925 | So |
|---|---|---|---|
| `BNXQ1001` expander names | declined, inspects 0 | inspects 0 | stays declined |
| `BNXQ1002` interactive names | adopted | the gate passes unchanged | the new id only |
| `BNXQ1003` template parts | adopted; skips `DiffBuildController`, `DiffPaneHeader`, `DiffStatusStrip` | 47 inspected, 0 findings; skips only `DiffPaneHeader` and `DiffStatusStrip` | `TemplatePartTests` narrows, and its controller-part assertion retires |
| `BNXQ1004` grid slots | adopted: 22 inspected against a floor of 12 | 0 inspected on today's markup — but a sized child in the fixed spacer is **caught** (1 inspected, 1 finding), and one whose size markup cannot evaluate is **skipped** | **kept, on a new blinding guard** |
| `BNXQ1005` key-binding focus | new | **0 inspected** — no markup here declares a key binding; the views build theirs in code | **declined** |
| `BNXQ1006` automation peers | new | 7 inspected, 7 findings: every themed control | plan 00026's |
| `BNAQ1001` cancellation tokens | adopted: 2 inspected | 2, 0 findings — the new token-less-overload check finds none | the new id only |
| `BNAQ1002` surface leak | adopted as stock plus `DiffPlex`: 781 | 781; the stock set alone reads 0 here, and **finds** a planted `System.Text.Json.Nodes.JsonNode` leak | **kept as stock plus `DiffPlex`, with a control for the stock half** |
| `BNAQ1003` forbidden reference | adopted: 9 | 9, 0 findings | the new id only |
| `BNAQ1004` namespace shadow | declined at `2026.3.922` | public types 116 and 86, every type 140 and 264, in `DiffView.Core` and `DiffView.Avalonia`; 0 findings | **adopted**, replacing a hand-rolled test |

`CodeQuality 2026.3.925`, the source-side counterparts of three AssemblyQuality rules, is not
considered here.

## `BNXQ1003` does what the controller assertion did

At `2026.3.924` the rule credited a `PART_` lookup to the type whose code made it, so the lookups
`DiffBuildController` makes on its host's template were checked against no theme at all, and the
controller was skipped. `TemplatePartTests` compensated by hand: it read the parts out of the skip's
reason and asserted every host declares each as a constant. At `2026.3.925` the rule credits a lookup
to the control whose template it is made on — XamlQuality's #30, which this repository proposed — and
checks it against that control's own theme. The compensation is now the rule's own behaviour.

- The expected skips become `DiffPaneHeader` and `DiffStatusStrip`, the two themed controls that
  legitimately have no parts. That assertion stays the precise form of the blinding guard.
- The controller-part assertion retires, with `PartsNamedIn`, `HostsOfTheController` and
  `PartConstantsOf`.
- The harness mutation *the controller looks up a part the view does not declare* is killed by the
  findings assertion now, in the same test.
- The mutation *the viewer stops declaring a part the controller looks up* **survives** at 925 —
  measured: the viewer's own constant is no longer what the rule reads. It is deleted, and
  `DECISIONS.md` records why, as `AGENTS.md` §5 requires of a construct designed out of existence.
- **Its twin replaces it**, so what it proved stays proven: *the viewer's theme stops declaring a
  part the controller looks up* — `PART_Minimap` renamed in `DiffViewer.axaml` — is reported at 925
  against `DiffViewer`, measured. That is the property the deleted mutation guarded: every control
  that hosts the controller has each of its parts checked against its own theme.

## `BNXQ1004` stays, on a guard 925 did not take away; `BNXQ1005` is declined

From `2026.3.925` `BNXQ1004` counts a placement only when it measured it against fixed slots, and
today no grid child here is measured that way: the library's one fixed slot is the 16-pixel spacer
column, and the children placed in it declare no size. XamlQuality's session measured the same over
`ec61a10`: 22 at `924`, 0 at `925`, no findings and no skips at either.

**Reading 0 is not being unable to fail.** The rule's own harness mutation — `MinWidth="40"` on the
`Border` in the spacer column — is still caught at 925: 1 inspected, 1 finding, measured. The rule has
a live subject here, the fixed slot and the children in it, which is what separates it from
`XQ1001`, declined by plan 00025 because this repository has no `Expander` for it to read at all.

**What 925 did take away is the blinding guard.** `GridSlotTests` floored `Inspected` at 12, because
pointing the scan at ground with no markup reads 0 inspected and 0 findings — what a clean repository
reads. At 925 a clean repository reads 0 inspected too, so that floor cannot hold. The guard moves to
the scan instead of the rule: **a floor on the grid children the scan's own parsed markup holds**,
read from the same `XamlScanContext` the rule reads — 37 here, counted as the probe counted them, and
none on ground with no markup. The harness mutation *the grid scan is pointed at markup-free ground*
is killed by the new floor.

**The `Skipped` guard is live at 925.** A child of the fixed slot whose size markup cannot evaluate —
`MinWidth="{DynamicResource …}"` on that `Border` — is named in `Skipped`, measured. So the
`inert-at-pin` marker comes off, that mutation proves the guard, and `scripts/xq1004-skips.{cs,sh,ps1}`
go: their one purpose was re-measuring whether the marker still held, and at 925 it does not. The
harness's `inert-at-pin` machinery stays — it is generic, and CI's `--guards` step still runs it with
no marker to judge — and the one line of its hint that names the script goes.

`BNXQ1005` inspects nothing here from the start, and has no subject to plant one in: the views build
their key bindings in code. It is declined as `XQ1001` was.

## `BNAQ1004` is adopted, and the hand-rolled test it matches retires

Plan 00025 declined `AQ1004` at `2026.3.922` for three reasons. Two were properties of that pin, and
`2026.3.925` retires both: `NamespaceShadowRule.IncludingInternalTypes()` reads internal types
(`044e71a`), and `Inspected` counts only candidates that could produce a finding, so a scan with no
referenced roots reads 0 instead of passing (`05a6060`). The third — that it would replace one of
`NamespaceConventionTests`' two tests — is what adopting it means.

**Over the two shipped assemblies, the rule sees every root the test sees.** Measured by calling each
derivation on `DiffView.Core` and `DiffView.Avalonia`: the test's roots are `DiffPlex` and `System`
for the first, and `Avalonia`, `AvaloniaEdit`, `CompiledAvaloniaXaml`, `Microsoft`, `SimpleJSON`,
`System` and `TextMateSharp` for the second; the rule's are the same, plus `Microsoft` for the first —
reached through a forwarding facade — and `Bennewitz` for the second, over which it reports nothing.
And a scratch assembly with shadows planted in it measured what each finds:

| Planted case | Public types only | `IncludingInternalTypes()` | The test's own derivation of referenced roots |
|---|---|---|---|
| A public type under `.System`, whose root is reachable only through the `System.Runtime` facade | found | found | **misses it** — `GetExportedTypes()` does not return forwarded types |
| An internal type under `.Avalonia` | missed | found | sees the root |
| A public type under `.Media`, which is no referenced root | not reported | not reported | no root either |

So `IncludingInternalTypes()` finds everything the test finds, and a shadow through a facade that it
does not. It is adopted over both shipped assemblies in `AssemblyQualityTests`, in the shape of the
three rules already there: a floor, the findings assertion, and a standing control — a type the test
assembly declares under a namespace nested deeply enough to shadow nothing that compiles, anchored by
name. `NamespaceConventionTests.No_namespace_carries_a_segment_that_shadows_a_referenced_root`
retires; `Everything_outside_the_repository_prefix_is_generated` stays, because it asserts something
the rule does not — that every root namespace in the shipped assemblies is ours or generated. The
documents that cite the retired test as evidence — `AGENTS.md` §1's shadowing row and a plan 00017
verification row in `PROGRESS.md` — say so, because `DocumentationCitationTests` resolves every test a
document cites.

⚠ A mutation that plants a real shadow in shipped code must still compile, or the build kills it
before the gate runs. The probe shows how: the shadowing segment sits under a namespace no other code
is inside, so nothing resolves a qualified name through it.

## `BNAQ1002` keeps its stock set, and a control proves it

Plan 00025 recorded that the stock leak-prone set concatenated with `DiffPlex` hid a zero behind 781.
At `2026.3.925` the stock set — `System.Text.Json.Nodes` and `Newtonsoft.Json.Linq` — reads 0 here
because nothing it covers is in `DiffView.Core`'s reach, not because it is dead. `Only(["DiffPlex"])`
would give the same 781 today and give up the day `DiffView.Core` takes one of those dependencies and
leaks a type from it. So the configuration stays stock plus `DiffPlex`, and the zero plan 00025 could
not see behind the 781 gets a guard of its own: **a stock control** — a public member of the test
assembly naming `System.Text.Json.Nodes.JsonNode`, which the adopted instance reports, measured, and
which needs no package — anchored by name, the way `LeakControl` anchors the `DiffPlex` half.

## Decisions

| Decision | Why |
|---|---|
| Both pins to `2026.3.925`, in one change | Brian, 2026-09-25: one rename pass and one full harness run instead of two |
| `BNXQ1003` kept; its expected skips narrow to the two part-less controls; the controller-part assertion retires | The rule now checks the controller's lookups against each host's theme itself |
| The viewer-constant mutation deleted, its theme-side twin added | The first survives at 925 by design; the second proves the property it guarded, measured |
| `BNXQ1004` kept; its blinding floor moves from `Inspected` to the grid children the scan holds; its `Skipped` guard gets a mutation and loses its marker; `scripts/xq1004-skips.*` go | It can still fail here, measured; 925 removed only the floor's footing, and made the `Skipped` guard trippable |
| `BNXQ1005` declined | It inspects nothing here and has no subject to plant |
| `BNXQ1006` not adopted | Plan 00026 gives the seven controls their peers and adopts it |
| `BNXQ1001` stays declined | Still inspects nothing here |
| `BNAQ1002` kept as stock plus `DiffPlex`, with a stock control | The stock half is forward cover that reads 0 only for want of a subject; the control proves it alive |
| `BNAQ1004` adopted with `IncludingInternalTypes()`; the shadow test in `NamespaceConventionTests` retires | Every root the test sees, measured over the shipped assemblies; stronger through a facade; both declined-for reasons retired |
| Every rule id in messages, comments, mutation names, scripts and current documents becomes `BNXQ100n` / `BNAQ100n` | No assertion compares an id — measured — so the rename is text; plan 00025 is frozen and `DECISIONS.md`'s written entries are history, so both keep the old ids |

Dismissed:

- **Two bumps on two branches.** Decided against by Brian: twice the rename pass and twice the
  harness run over the same gate files.
- **Declining `BNXQ1004`.** Draft one of this plan. It equated reading 0 with being unable to fail;
  the rule's own mutation is still caught at 925.
- **Re-flooring `BNXQ1004`'s `Inspected` at zero.** A floor of zero guards nothing, which is the
  failure mode plan 00025 exists to refuse; the floor moves to the scan instead.
- **`SurfaceLeakRule.Only(["DiffPlex"])`.** Draft one. The same reading today, bought by giving up
  the stock set's forward cover.
- **Deleting the viewer mutation without a twin.** Draft one. It would leave the viewer's theme
  unproven against the controller's lookups.
- **Keeping the shadow test beside `BNAQ1004`.** Two gates for one property, and the test is the
  weaker of the two.
- **Adopting `BNXQ1006` now.** It reports every themed control, which is plan 00026's work; adopted
  here it would hold this bump hostage to that plan.

## Scope

**In.** `Directory.Packages.props` and its comment on the two pins; `TemplatePartTests`;
`GridSlotTests` reworked — the scan floor, the live `Skipped` guard, the findings assertion; the
removal of `scripts/xq1004-skips.*`; `AssemblyQualityTests` — the stock control, and `BNAQ1004` with its
control; `NamespaceConventionTests`' shadow test retired; `scripts/mutate-gates.cs` — mutations added
for every new guard, deleted for every retired one, re-pointed where the killer moved; the rename
across tests, scripts and current documents; `AGENTS.md` §1's shadowing row and §5's `xq1004-skips`
passage; `PROGRESS.md`, `DECISIONS.md`, `CHANGELOG.md`.

**Out.** `BNXQ1006` and any automation peer (plan 00026). `CodeQuality`. Any change to shipped code —
every change here is in tests, scripts, build properties and documents. The text of plan 00025.

## Phases

| Phase | Size | What | Verified by |
|---|---|---|---|
| 0 The pins and the renames | S | Both pins; the id rename everywhere it is current text | Build clean under `-warnaserror`; the suite red on exactly `TemplatePartTests` and `GridSlotTests`, as measured; `docs/theme-audit.md` unmoved |
| 1 `BNXQ1003` | XS | The two expected skips; the controller-part assertion and its helpers retire; the viewer-constant mutation deleted and its theme-side twin added | `TemplatePartTests` green; every mutation's verdict as declared |
| 2 `BNXQ1004` on its new guard; `BNXQ1005` declined | S | The scan floor, the live `Skipped` guard with its mutation, the marker off, `scripts/xq1004-skips.*` and the hint line removed | `GridSlotTests` green; the overflow, unevaluable-size and markup-free mutations each tripping its own guard first; `--guards` reporting no marker; no current text naming `xq1004-skips` |
| 3 AssemblyQuality | S | The stock control; `BNAQ1004` with its floor, findings assertion and control; the shadow test retired | Every new guard tripped first by its own committed mutation, among them a compiling planted shadow, a blinded scan and a stock control that stops leaking |
| 4 The record | S | `AGENTS.md`, `PROGRESS.md` (Open items 7 and 8 close), `DECISIONS.md`, `CHANGELOG.md` | A full `scripts/mutate-gates.sh` run: every mutation as declared, every guard in the gate files tripped first; the suite in `en-US` and `de-DE` under `catch-crash --expect auto`; `--guards` clean; the trimmed publish without an `IL` warning; CI green on all five jobs |

## Risks

- **A mutation that plants a shadow can be killed by the compiler instead of the gate** — which the
  harness reports as a failure, not a kill. Phase 3 plants under a namespace nothing else is inside,
  as the probe did.
- **The scan floor counts what the probe counted.** Grid children are the direct element children of
  a `Grid` that are not property elements; phase 2 writes that count in the test, beside the floor, so
  the number and the thing counted cannot drift apart.
- **Windows and macOS resolve the facades on their own runtimes.** The referenced assemblies are the
  same and so is .NET 10's `System.Runtime`, so `BNAQ1004`'s reading should not move; CI's three
  platforms are what confirm it.
- **The branch starts from `main` after plan 00021 lands.** Every figure above was read at
  `c465c65`; if `main` differs from it when the branch is cut, phase 0 re-reads them first.
