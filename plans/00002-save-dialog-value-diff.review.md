# 00002 — adversarial review

A review of `00002-save-dialog-value-diff.md` against the ClaudeForge code it targets. The
verdict is that the plan **should not be approved as written**: its central premise is
substantially false, and the strongest form of the idea lives somewhere else in the application.

## 1. The premise is largely false

The plan exists to solve this: *a settings value that is a JSON object or array renders as its
first 80 characters, so you cannot see what changed inside it.*

`ClaudeForge.Sdk.Diagnostics.JsonDiff` **already solves that**, deliberately, and says so:

> recursing into nested objects and computing a multi-set delta for nested [arrays]
>
> **Why recursion matters:** Claude config has "container" keys … large objects. Without
> recursion, a single hook removal emits an entire [container] … With recursion they see a single
> Removed row at path `hooks.Stop`
>
> Both sides are objects → recurse; **surfaces just the leaf changes**.

`PropertyDiff`'s own contract confirms it: `Key` is a dot-separated path such as `hooks.Stop`, and
for arrays the value "carries just the removed element's JSON, **not the whole array**".

So by the time a `PropertyDiff` reaches the dialog, a container has already been decomposed. The
80-character truncation is a backstop for a case the differ mostly prevents.

### What actually survives the recursion

The plan's predicate requires `Kind == Modified` with both full values present. After recursion,
both sides of a `Modified` entry are **leaves** — if both were objects, it recursed instead. The
qualifying population is therefore:

| Survivor | Frequency |
|---|---|
| A long single-line string leaf — a hook command, a long path, a prompt | The realistic case |
| A type change, scalar → object, where one side is still a blob | Rare |
| An array element that is itself a large object | Uncommon; the multi-set delta already isolated it |

The feature would fire far less often than the plan assumes, and on a different shape of data.

## 2. Where it does fire, the granularity is wrong

For two long **single-line** strings, a line-based diff renders one removed line and one added
line. That is informationally identical to what the dialog already prints:

```
~ key:  old  →  new
```

A whole editor pane, virtualised over a two-line document, to restate the existing string. The
thing that would genuinely help — showing *which part* of a long command line changed — is a
word or character-level diff **within** one line. DiffView has word-level highlights, but as a
layer inside a multi-line diff, not as a one-line inline renderer.

The plan reaches for a control whose unit is the line, against data whose unit is the character.

## 3. The dependency cost is far higher than stated

`src/DiffView.Avalonia/DiffView.Avalonia.csproj` makes `AvaloniaEdit.TextMate` an unconditional
`PackageReference`. There is no syntax-free variant. A consumer that wants zero syntax
highlighting still gets all of it:

| Inherited by ClaudeForge | Size |
|---|---|
| `TextMateSharp.Grammars.dll` | **6.7 MB** |
| `libonigwrap.so` — native, per RID | 564 KB |
| `TextMateSharp.dll` | 128 KB |
| `Onigwrap.dll`, `AvaloniaEdit.TextMate.dll` | ~35 KB |
| `AvaloniaEdit`, `DiffPlex` | — |

A **native** library enters a trimmed, cross-platform published application, for a dialog
expander that highlights nothing. This is the cost that grew DiffView's own publish from 51 MB to
57 MB.

## 4. Phase 1's risk is imposed, not inherent — and it was under-scoped

The solution-wide Avalonia bump exists for exactly one reason: DiffView is built against 12.1.2
and ClaudeForge pins 12.1.0. It is a cost this plan creates for its host, not a need the host has.

The plan also got the scope wrong. There is a **third** version in play that Phase 1 never
mentions: `Avalonia.Headless` is pinned at **12.1.1** across `ClaudeForge.Tests`,
`LayeredEditors.Avalonia.Tests` and `LayeredEditors.Avalonia.Diagnostics.Tests`. So "a patch
bump" spans 12.1.0 application pins, 12.1.1 test pins, a dependabot-managed group, and a
pre-existing `Tmds.DBus.Protocol` pin guarding NU1605 — spent on a feature of doubtful value.

## 5. It does not serve the reason it was scheduled

Plan 00002 was chosen over in-pane editing on the argument that a real consumer should exercise
DiffView's public API before editing hardens it. This consumer would use:

| Used | Unused |
|---|---|
| Two `PaneSource`s, read-only | Find, syntax, navigation, minimap, scroll sync, connectors, side-by-side, view options, word diff, state machine, banners, status strip |

It exercises a sliver of the surface, and none of the parts editing will stress. As API
validation it is close to worthless.

## 6. Cheaper things that beat it

| Alternative | Cost | Verdict |
|---|---|---|
| Raise the 80-char cap; the cell already has `TextWrapping="Wrap"` | One constant | Solves most of the real complaint |
| Click-to-expand `Flyout` with `JsonFormatting.TryPrettyPrint`, replacing the flicker-capped tooltip | ~30 lines, no dependency | Solves the rest |
| `DiffPlex` alone rendering into an `ItemsControl` of coloured `TextBlock`s | ~200 KB, no Avalonia control, no native code | If a real diff is wanted, this is the proportionate one |

Each is a fraction of the plan's cost, and in a 260px column probably reads better.

## 7. What survives the review

Not everything here is wrong, and these hold regardless of what happens to the plan:

- **The AvaloniaEdit-under-Semi resolution** — the rejection was correct when written (6 undefined
  keys under bare Semi, 3 of them `StaticResource`, which throw at template load), and the compat
  dictionaries merged as `93065ba` take it to 0. That is worth recording in ClaudeForge on its own
  merits, independent of any integration.
- **The infinite-height analysis** — `SizeToContent="Height"` defeating a `ScrollViewer`'s own
  `MaxHeight` applies to any virtualising control anyone ever puts in that dialog.
- **Reusing `JsonFormatting`** rather than growing JSON opinions in DiffView.
- **Bump-before-feature ordering**, if a bump is ever needed.

## 8. Recommendation

**Retarget to Backup/Restore.**

`claudeforge-81` proposed this and the plan filed it as a non-goal. That was the wrong call:
restoring an archive genuinely *is* old-file versus new-file, the content exists in full on both
sides, and a restore preview surface already exists. There DiffView earns every one of the
dependencies it drags in — side-by-side panes, find, navigation, and syntax highlighting over
real config files, which is exactly what the 6.7 MB of grammars is *for*. It also sits clear of
`feat/agentforge-opencodeforge`, so the rebase risk that dominates the current plan disappears.

The ranked options:

| Option | Assessment |
|---|---|
| **Retarget 00002 to Backup/Restore** | Best. Keeps the "real consumer before editing" logic intact while pointing it at a surface where a diff control is the right answer |
| **Drop 00002; do in-pane editing next** | Also defensible. Editing is the larger, better-designed body of work, and integration can wait for a genuine need |
| **Descope to the flyout + wrap fix** | A real, small improvement to the dialog with no dependency at all. Worth doing whoever does it, and it is not a DiffView plan |
| **Proceed as written** | Not recommended. High cost, imposed risk, dubious benefit, wrong granularity |
