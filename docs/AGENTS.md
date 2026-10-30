# AGENTS.md — `docs/`

Documentation for readers outside this repository, and the documents tools write.

| File | What it is | Written by |
|---|---|---|
| `hosting-diffview.md` | The hosting guide, and the `PackageReadmeFile` of both packages, so nuget.org shows it | hand |
| `mutation-proofs.md` | The method `scripts/mutate-behaviour.cs` proves a behaviour test able to fail by | hand |
| `ui-automation-notes.md` | What this repository measured about driving a desktop UI that has not moved to Inspections' documents | hand |
| `theme-audit.md` | The theme audit's report | `theme-audit report`, from `theme-audit.json` |
| `locale-review/<culture>.md` | A review packet per shipped locale, for a native speaker | `scripts/gen-locale-review.cs` |

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| **A generated document is never edited by hand**: a correction goes into what it is generated from — a `.resx`, or the themes and `theme-audit.json` — and the document is regenerated | Two copies of one fact with no rule about which wins | `LocaleReviewTests.Every_committed_review_document_describes_the_strings_it_claims_to`, `ReferenceAuditTests.The_committed_report_equals_a_fresh_run` |
| A gate that disagrees with a document here writes `<name>.received.md` beside it; CI uploads that when a job fails, and `.gitignore` keeps it out of a commit | The failure otherwise names a file nobody outside the runner can read | `.github/workflows/ci.yml`, *Upload what the gates rejected* |
| The hosting guide's `AutomationId` table is the fixture's, both ways | A host scripts against the ids it reads there | `HostingGuideTests.The_guides_id_table_names_exactly_the_ids_the_fixture_pins` |
| The hosting guide states the locale caveat: the translations are machine-generated and unreviewed | A consumer meets it there, and on nuget.org | `LocaleCaveatTests.Every_place_a_consumer_meets_the_caveat_states_it` |
| Every `§N` a document here cites names a section of the root `AGENTS.md` that exists, and every test this file and the hosting guide name exists | A citation is evidence only while it resolves | `DocumentationCitationTests` |
