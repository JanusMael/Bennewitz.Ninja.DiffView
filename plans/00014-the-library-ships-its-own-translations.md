# 00014 — The library ships its own translations

Left open since plan 00010 closed and stated in `DECISIONS.md` §"A string that names a side is a
whole sentence, one per direction": DiffView localizes through `DiffViewStrings.Resolver`, which a
host wires, where ClaudeForge ships `.resx` and satellite assemblies that follow `CurrentUICulture`
by themselves. The key structure suits either, so nothing built has to be undone.

**Two things this plan changes that are not the translations.**

The first is the seam. `Resolver` is `Func<string, string?>` and is told nothing about culture, which
is harmless while English is the only fallback and becomes a defect the moment a satellite sits
behind it — see *A partial resolver must not mix languages*. The library has never been released:
`CHANGELOG.md` holds one `[Unreleased]` section, the only tag is a plan checkpoint, and **every
assignment of `Resolver` in the repository is in a test**. The seam will never be cheaper to reshape.

The second is that 143 strings in eight languages get rendered through menus that were **already
narrowed once** — on 2026-09-10, at Brian's word, when a copy entry ran to 41 characters with its
accelerator against it — a status strip that is one line of separator-joined fields, and gutter
tooltips measured against a column. German runs roughly a third longer than English. The
translations are the cheap half; what they do to the layout is the plan.

## Goal

A host that drops `SideBySideDiffView` into a German application gets German with no wiring, and a
host that wires a resolver still outranks it **by construction**. Eight cultures, the same eight
ClaudeForge ships, so a host localised for one is localised for both.

## Non-goals

| Excluded | Note |
|---|---|
| **Replacing the English table with the resx** | `English` stays the compiled final fallback and the single **authored** English surface. A dictionary cannot fail to load, and `StringCatalogueTests` already contracts that every key has English text. The neutral `.resx` is *generated from it* and guarded by a drift test, so there is no second surface to keep in lockstep — the objection ClaudeForge's own library wrote down when it declined to do this |
| `SupportedCultures` | A host building a language picker would want it, nobody has asked, and it is purely additive — it can land later without breaking anyone. Named so its absence reads as a choice |
| `AsyncLocal` resolution | Considered for test isolation and rejected: the suite is serial by design (`xunit.runner.json`, `parallelizeTestCollections: false`), so it buys isolation nothing needs, and it adds a flow-sensitive *"why can't my resolver see it"* failure mode to a public API |
| Localising `DiffViewLog` | Log lines are for an operator with a search box, not a reader. `AGENTS.md` §6 holds `DiffViewLog` as the only place a line is formatted, and that stays true in one language |
| Localising the demo's own chrome | The demo is a harness. It gets one flag, `--culture`, to *drive* the library's locales by hand; its own menus stay English |
| Right-to-left layout | Arabic and Hebrew are not in the eight, and RTL is a layout change to every gutter, margin and connector, not a string change. Named so it is not mistaken for an oversight |
| Translated grammar names, encodings, line-ending names | `UTF-8`, `CRLF` and `C#` are not words in any language |
| Number and date formatting | Already `CurrentCulture` at every call site, which is the correct and separate axis — see *Two cultures, deliberately* |

## Architecture

### One record, not two properties

```csharp
public sealed record DiffViewLocalization
{
    public Func<string, CultureInfo, string?>? Resolver { get; init; }
    public CultureInfo? Culture { get; init; }          // null = CurrentUICulture
    public static DiffViewLocalization Default { get; } = new();
}

public static DiffViewLocalization Localization { get; set; } = DiffViewLocalization.Default;
public static IDisposable Override(DiffViewLocalization localization);   // restores on dispose
public static void ResetForTesting() => Localization = DiffViewLocalization.Default;
```

A resolver and a culture that must agree are **one value or they are a bug waiting on someone's
ordering**. As a record they swap atomically, cannot be half-set, cannot be half-reset, and
`ResetForTesting` — a static seam `AGENTS.md` §5 already names — becomes a single assignment. It is
also the repository's existing idiom: `DiffPaneContext` and `DiffHeaderContext` are both records.

`Override` exists because restoration should be structural rather than disciplinary. All six current
assignment sites use `try`/`finally`; a `using` survives an exception, an early return and a
rewrite that forgets. Hosts get it too — switching language for one screenshot is a real thing to want.

### The fallback chain

```
culture  = Localization.Culture ?? CultureInfo.CurrentUICulture
Get(key) = Localization.Resolver?.Invoke(key, culture)   // host wins, always
        ?? Satellite(key, culture)                        // null when neutral or missing
        ?? English[key]                                   // compiled, cannot fail
        ?? key
```

One culture resolved per lookup and used by **both** layers. A library that decides culture policy
takes a choice away from an application that has already made one — an IDE-embedded diff follows the
IDE's language, not the OS's — so the host override winning is the whole reason this shape was
chosen over shipping a resx that follows the culture on its own.

`Satellite` holds one `ResourceManager` over `DiffView.Avalonia.Localization.Strings`, with
`[assembly: NeutralResourcesLanguage("en")]` so no `en` satellite is ever probed for.

### A partial resolver must not mix languages

This is why the resolver is told the culture, and it is the argument the first draft of this plan
got wrong.

A host wires a resolver for its own UI language — an IDE running Japanese — and covers 100 of the
143 keys, returning `null` for the rest. The machine's `CurrentUICulture` is German. With the
satellite's culture chosen from ambient thread state, independently of what the host meant, those 43
keys resolve **from the German satellite**: a Japanese UI with German strings in it. Today they fall
to English, which is at least the fallback everyone expects.

`Margins_carry_automation_names_through_the_string_resolver` is that scenario already written down —
one key covered, 142 returning `null`, and an assertion that one of the 142 reads English.

Resolving one culture and handing it to the resolver does not stop a host under-covering the
catalogue; it makes the fallback **the culture the host was told about** rather than whatever the
thread happened to carry. A host that sets `Culture` gets agreement across both layers; a host that
sets nothing gets `CurrentUICulture` in both, which is the out-of-the-box behaviour this plan is for.

### The neutral resx is generated, never authored

`scripts/gen-strings` writes `Localization/Strings.resx` from `DiffViewStrings.English`, and a drift
test asserts the committed file equals a fresh generation — the shape of `ReferenceAuditTests.The_committed_report_equals_a_fresh_run`,
which this repository already runs and already trusts. A translator needs a real neutral file to
translate *from*; an empty one would satisfy `ResourceManager` and be useless to a person.

### Two cultures, deliberately

`Format` stays on `CurrentCulture` while text resolution moves to `CurrentUICulture`. .NET separates
them on purpose and so does this: a host pinning German *text* should not thereby get German decimal
separators, and a German machine displaying an English build should still group its thousands the way
its user expects. No code changes; it goes in `DECISIONS.md` so the next reader does not "fix" it.

### What a translator is given

486 words: 106 labels under five words, 37 phrases of five or more, the longest 14. Thirty-nine
entries carry a placeholder. Plan 00010 already did the work that makes this translatable at all —
**a string naming a side is a whole sentence per direction**, fourteen keys and six selectors, so no
translator is handed a word to drop into someone else's sentence.
`No_string_of_the_library_is_built_by_pasting_a_side_word_into_it` is the sentinel that keeps it
that way, and it extends to each locale.

### The eight

`de-DE`, `es-ES`, `fr-FR`, `ja-JP`, `ko-KR`, `pt-BR`, `ru-RU`, `zh-CN`. ClaudeForge's set, verbatim.

**Provenance is recorded, not implied.** These are machine-generated pending native review; each
file carries that in a header comment and `DECISIONS.md` carries it as a decision. The parity gate
proves structure — every key present, every placeholder preserved, no pasted side word — and proves
nothing about fluency. `CHANGELOG.md` holds only `[Unreleased]`, so review gates the first release
rather than this plan.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — The seam** | M | `DiffViewLocalization`, `Localization`, `Override`, the culture-carrying resolver, and the six test sites migrated to `using`. The generated neutral resx and its drift test, the `Satellite` step, `NeutralResourcesLanguage`, the packing and `SatelliteResourceLanguages` wiring, and the trim canary re-run. `HeadlessTestApp` pins `DefaultThreadCurrentUICulture`. The audit of which of the 179 call sites are reachable from the diff worker. **Ships as a no-op**: with no satellite on disk every existing test stays green, and that is the phase's evidence |
| **2 — The parity gate** | S | Every locale has every key and no others; placeholder sets match English per key; the side-word sentinel per locale; the trimmed publish carries the satellites. Written **before** any translation exists, against a deliberately broken fixture locale, so each test is seen red rather than passing vacuously |
| **3 — The eight locales** | M | 486 words × 8, provenance in each header, and the CI leg that runs the suite pinned to `de-DE` — any test failing under it was asserting English without saying so, which is the audit that makes the locales trustworthy |
| **4 — Evidence, and the layout it breaks** | M | `de-DE` and `ja-JP` rendered at real size — the pane menu, the header detail line, the status strip and the gutter tooltips — **driven by hand under `AGENTS.md` §9**, because a 600 px capture window already lied once about this exact menu. Then `DECISIONS.md`, `AGENTS.md` §6, `PROGRESS.md`, the changelog |

Phase 1 is worth shipping alone: it is the seam, it changes no behaviour, and it is where both a
trim failure and a culture-dependent test would surface — separably from any translation.

## Testing

Every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| **A partial resolver never mixes languages** | Resolver covering one key, `Culture` German, satellite present: the uncovered keys read German, and with `Culture` Japanese and no Japanese satellite they read English — **never the machine's language**. The load-bearing test of the seam change, and the one the old signature could not express |
| A host resolver outranks a satellite | Culture German *and* a resolver, and the resolver's text wins. The difference between this plan and shipping a resx that decides for the host |
| A satellite is used when no resolver is set | The same culture without a resolver returns German, not English |
| English is used when the culture has no satellite | An unlisted culture — `fi-FI` — falls through cleanly rather than throwing or returning a key |
| The localisation state cannot be half-reset | One assignment restores both fields. Mutated to reset only the resolver, it must fail — which is the shape the two-property version would have shipped |
| `Override` restores on an exception | A `using` scope whose body throws leaves `Localization` at `Default`. The failure the six `try`/`finally` sites are one refactor away from |
| The existing resolver-swap contract still holds | `AGENTS.md` §6: every string is computed per instance, never at type initialisation, so `Swapping_the_string_resolver_before_load_changes_the_rendered_strings` must stay green. A `ResourceManager` cached in a static initialiser is exactly how an inserted step breaks it |
| The committed neutral resx equals a fresh generation | The drift gate. Mutated by one entry, it must fail |
| Every locale has every key, and no extra ones | Against the neutral file, per locale. The fixture that proves it: a locale missing one key and carrying one invented key must fail on both counts |
| Placeholders match English per key | Same count, same indices, same set. A translation that drops `{1}` or renumbers it renders as literal braces to a user, and no other test would see it |
| No locale string pastes a side word | The existing sentinel, per locale |
| A trimmed publish carries the satellites | The CI trim canary, extended. A satellite trimmed away is a silent fall back to English |
| The suite passes pinned to `de-DE` | Phase 3's CI leg. A test asserting English without pinning fails here and nowhere else |
| The pane menu fits in `de-DE` at real size | Phase 4, by hand, against the running app. Not a headless frame — the 600 px capture window already reported this menu as overflowing when it fits |

## Risks

| Risk | Assessment |
|---|---|
| **German breaks the menu that was already narrowed for length** | The most likely real defect here. *"Show differences with context"* is 29 characters before its accelerator, and the menu was shortened on 2026-09-10 when an entry reached 41. German at +30% puts several entries back over it. Phase 4 measures it by hand; the answer may be shorter German rather than a wider menu, which is a translator's call and a reason the strings are not generated and forgotten |
| **The suite is culture-dependent today and nothing says so** | `HeadlessTestApp` pins `SemiTheme { Locale = en-US }` but not `DefaultThreadCurrentUICulture`, and no test pins a UI culture. Every assertion of library text is therefore at the mercy of the machine the moment a satellite exists — passing on an English CI box, failing on a German desk. Phase 1 pins it; phase 3 flips the pin in CI to prove the pinning is real |
| **Satellite assemblies under trimming** | CI publishes trimmed and treats a new `IL2xxx` as a failure (`ci.yml` `trim-check`). `ResourceManager` is a known trim hazard and `SatelliteResourceLanguages` governs what survives. Phase 1 re-runs the canary **before a single locale exists**, so a trim failure is never entangled with a translation |
| **Machine translation shipped unreviewed** | Named rather than hidden: provenance in every file header and in `DECISIONS.md`, structure gated by tests, fluency gated by a human before the first release. The alternative — shipping nothing — was weighed and rejected |
| Migrating the six resolver sites changes assertions that were culture-dependent | Mechanical, but not blind: each site gains a parameter *and* an explicit culture. Phase 1, where nothing else is moving |
| The status strip is one line of joined fields | Same measurement, same phase. It carries theme, variant, palette, view, editable and a find lane; the longest locale is where it wraps or clips |
| `Culture = null` still means `CurrentUICulture`, which is per-thread | Not designed away. Builds and searches run latest-wins on a worker, so any string formatted there reads that thread's culture. Phase 1 audits which of the 179 call sites are worker-reachable and pins at the seam if any are |
| A parity gate that passes vacuously | This repository's own recurring trap — a test that passes on its first run has not been tested. Phase 2 lands **before** the locales and is written against a broken fixture, so every assertion is seen red first |
| Scope creep into RTL | A non-goal with its reason. Nothing here makes it harder later |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`. New tests are proven able to fail before
they are committed. An approved plan is committed before implementation and never edited; drift
goes to `DECISIONS.md`.
