# 00027 — The locale caveat reaches the consumer

> Status: **approved 2026-10-13**. Supersedes nothing.

Open item 6, reserved as plan 00027 on 2026-09-25 and unstarted since. The caveat is that the eight
shipped locales are machine-generated and unread by a native speaker; what is owed is that a
consumer meets it, and that it stops drifting.

## What is actually missing, measured 2026-10-13

The survey matters more than the task, because four of the five places were already right and the
two defects are not the ones item 6 names.

| Where a consumer or a maintainer meets it | State |
|---|---|
| `README.md` | ✅ correct — the lifted release gate was removed 2026-10-12 |
| `docs/hosting-diffview.md` § *Localization happens whether you ask or not* — **this is the `PackageReadmeFile` of both packages**, so it is the text nuget.org renders on the package page | ✅ correct, and the strongest statement of the three: machine-generated, unreviewed, structure gated but wording not, and how to override |
| `src/DiffView.Avalonia/DiffView.Avalonia.csproj` `<Description>` | ⛔ **no caveat at all** — *"Side-by-side diff control for Avalonia 12, built on AvaloniaEdit."* |
| `src/DiffView.Avalonia/Localization/Strings.<culture>.resx` × 8, header comment | ⚠ caveat present in all eight, but each also orders *"Do not ship a release on these without a speaker of the language reading them"* — **the gate lifted on 2026-09-18**, restated as an imperative, eight times |
| `src/DiffView.Core/DiffView.Core.csproj` `<Description>` | ✅ nothing owed — Core ships no strings and no satellites |

⛔ **Item 6 says the README and the package description. The README was one of two defects and the
package readme was never one at all.** The second defect is the eight `.resx` headers, which no one
had looked at, and which contradict a decision that is three and a half weeks old.

## Decisions

All five settled with Brian on **2026-10-13**, the plan having been drafted far enough to put them.

| Decision | Why |
|---|---|
| The `<Description>` gains a **short clause**, not the caveat — *"Ships eight machine-generated locales, which apply without configuration."* | A `Description` is the one-line blurb in nuget search results and the package sidebar. The full caveat belongs in the readme and is already there; what the blurb owes is the *surprise* — that installing this localizes the control with no configuration — which the guide itself calls out as arriving in bug reports |
| Only `DiffView.Avalonia`'s | Core ships no strings, no satellites and no locale files. A caveat there would be false |
| The eight `.resx` headers lose the **imperative**, keep the **caveat** | Machine-generated and pending native review is true and load-bearing for anyone editing them. *Do not ship a release on these* is a lifted decision, and a stale instruction in eight files is worse than in one: it reads as policy |
| The gate asserts **agreement on two facts and the absence of one claim** | Wording cannot be compared across a blurb, a guide and an XML comment. What can be held is that each place states machine-generated and unreviewed, and that **no** shipped document or locale header reasserts that native review gates the release |
| The gate covers the locale headers too | They are the files the drift was found in, and they are authored rather than generated (`AGENTS.md` §6), so nothing else protects them |

### Alternatives dismissed

| Rejected | Why |
|---|---|
| Put the full caveat in `<Description>` | Crowds out what the package *is*, and duplicates the readme that nuget.org renders directly beneath it |
| Leave the `<Description>` alone, the readme being enough | Search results and the sidebar show the blurb alone. A consumer can choose this package without ever opening the readme |
| Compare the three texts for identical wording | They are three registers — a blurb, a guide, an XML comment. An identical-wording gate would force one voice on all three and fail on the first legitimate rewrite |
| Regenerate the `.resx` headers from one source | The locale files are authored by design, precisely so no one regenerates over a correction (`DECISIONS.md`, plan 00014). A generator here would invite exactly that |
| Also fix plans 00014, 00015 and 00021, which carry the same stale claim | ⛔ An approved plan is never edited. They are history and stay as written |

## Scope

**In:** `DiffView.Avalonia`'s `<Description>`; the eight `Strings.<culture>.resx` header comments; one
new gate; the `AGENTS.md` contract row and `PROGRESS.md` item 6 closing.

**Out:** the hosting guide and `README.md`, both already correct and touched only if the gate proves
otherwise. The neutral `Strings.resx`, which is generated. `DiffView.Core`. Any approved plan. The
first publish, which is item 3 and gated on a Trusted Publishing policy that is Brian's. The spike
shelving, which is its own change — PR #27 — so this branch carries one subject.

## Steps

### 1 — The blurb says the control localizes itself

`src/DiffView.Avalonia/DiffView.Avalonia.csproj`:

```xml
<Description>Side-by-side diff control for Avalonia 12, built on AvaloniaEdit. Ships eight
machine-generated locales, which apply without configuration.</Description>
```

*Verification:* `PackagingTests` already requires every packable project to carry a `Description`;
the new gate in step 3 is what holds this one's content.

### 2 — The eight locale headers stop ordering a lifted policy

Each `Strings.<culture>.resx` header keeps *MACHINE-GENERATED, PENDING NATIVE REVIEW* and the parity
note, and loses *"Do not ship a release on these without a speaker of the language reading them."*
In its place: that review is owed to the wording and does not gate the release, per 2026-09-18.

*Verification:* all eight edited; `LocaleParityTests` and `LocaleReviewTests` stay green, proving the
headers are comments and the gated structure is untouched.

### 3 — One gate holds them in agreement

A test over the consumer-facing set — the `<Description>`, `README.md`, `docs/hosting-diffview.md`
and the eight locale headers — asserting:

- each of the README, the guide and the eight headers states **machine-generated** and
  **not reviewed by a native speaker**;
- the `<Description>` names the shipped locales;
- **none of them** claims that native review gates, blocks or precedes the first release.

⛔ **`DECISIONS.md` and `PROGRESS.md` are excluded by name, with a comment saying why**: the
superseded-pointer blockquotes added on 2026-10-12 necessarily **quote the claim they supersede**, so
an absence assertion over them fails on the very markers that record the correction. The gate reads
the shipped and authored set — the two `.csproj`-adjacent texts, the guide, and the eight locale
files — and nothing that is history.

*Verification:* proven able to fail, three ways, each the first and only assertion to break —
reinstate the imperative in one locale header; strip the caveat from the guide; empty the
`<Description>`'s clause. ⚠ The third-party trap: the phrase that went stale was **wrapped across a
line break** in `DECISIONS.md` and a one-line grep found nothing, so the gate normalizes whitespace
before matching or it will have the blind spot that caused this plan.

### 4 — The record

`AGENTS.md` §6 gains the contract row. `PROGRESS.md` item 6 closes, naming the two defects found and
that the package readme was never one. `CHANGELOG.md` notes the `Description`.

*Verification:* `DocumentationCitationTests` resolves every test this plan's prose names; the full
suite green and judged by `catch-crash --expect auto`; `mutate-gates --guards` unchanged at 81, the
new test not being in a gate file.

## Done when

The `<Description>` carries the clause; no locale header orders a lifted policy; one gate fails if
any of the eleven places drops the caveat or reasserts the release gate, proven able to fail three
ways; item 6 closed; the suite green.
