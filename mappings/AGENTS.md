# AGENTS.md — `mappings/`

The reviewed key tables the theme audit generates the compatibility dictionaries from:
`FluentToSemi.json` and `SimpleToSemi.json`, each named by a target's `mapping` in `theme-audit.json`.
`theme-audit` is the tool `Bennewitz.Ninja.XamlQuality.ThemeAudit` ships; the tables are this
repository's.

## Rules

| Rule | Why | Guarded by |
|---|---|---|
| A table is reviewed by hand, and an edit is followed by `theme-audit compat` and then `theme-audit report`, committed together | The compatibility dictionaries and the report are generated from it, in that order | `ReferenceAuditTests.The_committed_compat_dictionaries_equal_a_fresh_generation`, `ReferenceAuditTests.The_committed_report_equals_a_fresh_run` |
