# 00003 — In-pane editing

Turning the side-by-side control from a viewer into an editor. Plan 00001 called this "a flip,
not a rewrite" and paid up front for the nine design choices that make it so; this plan spends
that credit.

## Goal

A pane whose `IsReadOnly` is false accepts typing, re-diffs as the user types, tracks whether it
has unsaved changes, saves back to the file it was read from byte-for-byte faithfully, and can
take a change block wholesale from the other side. Undo and redo are AvaloniaEdit's own, per side,
and survive every rebuild.

## Non-goals

| Excluded | Note |
|---|---|
| Editing the unified view | `InlineDiffView`'s document is composed from both sides; half its lines belong to one file and half to the other. Read-only "full stop" is a locked decision in `DECISIONS.md` and this plan does not reopen it |
| 3-way merge / an editable result pane | Still a later plan; DiffPlex 1.9's merge APIs are untouched here |
| Creating files | `Save` writes back to a `PaneSource` that came from a file. A pane built from a string has no path and cannot be saved |
| Conflict resolution UI | A file changed on disk since load is *reported*, not merged |
| Auto-save | Saving is explicit |

## What plan 00001 already paid for

This is not aspiration — it is in the code today.

| Choice | Where it is now |
|---|---|
| `IsReadOnly` is a real property every layer honours, not an assumption | `LeftReadOnly` / `RightReadOnly` are `StyledProperty` on the composite, pushed to the panes at `SideBySideDiffView.cs:1083`, `:1087` and `:1681`. **Step one of this plan is genuinely a property flip** |
| The editor's document is the source text; padding is rendered, never inserted | `PaddingElementGenerator` and `PaddingRun`. Typing edits the real file — there is no projection layer, no offset mapping, no second undo stack |
| Source-indexed model with a separate alignment table | `DiffPane.Lines` by line, `SideBySideDocument.Rows` by row. After an edit only the metadata changes |
| A rebuild swaps metadata, never replaces a `TextDocument` | The composite's rebuild path already works this way; only a *source assignment* replaces a document |
| Metadata lookups bounds-checked and version-stamped; an unknown line renders `Unchanged`, never throws | `PaneMetadata`. This is what holds between a keystroke and the re-diff that has not landed yet |
| `PaneSource` carries the encoding; `TextInfo` carries the line ending | `PaneSource.Encoding`, `PaneSource.Path`; `TextInfo.LineEnding`, `TextInfo.HasIrregularLineEndings`. Save writes back what was read |
| `ChangeBlock` carries both sides' line ranges | `ChangeBlock(Index, Kind, FirstRow, LastRow, LeftLines, RightLines, …)`. Copy-to-side is a `Replace` over `LeftLines` or `RightLines` |
| The connector gutter owns its column with per-block hit-testing | `ChangeConnectorGutter`. The copy arrows live there |
| Latest-wins build worker, previous result held until the new one lands | `_generation` at `SideBySideDiffView.cs:1186`. Live re-diff is the same pipeline on a debounce |

There is no save path, no dirty tracking and no copy arrow anywhere in the tree today. All three
are additions.

## Architecture

### The editing loop

```
TextChanged ──debounce──▶ capture text (UI thread) ──▶ build (worker, latest-wins)
                                                            │
   metadata swap ◀── re-prime ◀── redraw ◀───────────────────┘
```

The one hard rule that separates this from a source assignment: **an edit-triggered rebuild must
not replace the `TextDocument`**. The user's caret, selection, scroll position and undo stack all
live in it. The composite already distinguishes the two paths; this plan makes the distinction
explicit and tested rather than incidental.

During the debounce window the panes are briefly misaligned — the padding belongs to the previous
build. That is accepted, as plan 00001 anticipated, and the padding catches up when the build
lands.

**Invalidation an edit forces, which a source change never had to think about:**

| Cache | Why an edit breaks it | Action |
|---|---|---|
| `WordDiffLookup` | Bound to a build and filled from rendered rows over the *live* documents. An edited line's cached pieces describe text that no longer exists | Dropped with the build that owned it |
| Find results | Match offsets are into a `DocumentPaneText` snapshot taken before the edit | Invalidated on edit; re-run when the build lands |
| Height priming | Padding moves, so primed heights move | Re-primed after every rebuild — see *Risks* |

### Dirty state and save

| Member | Behaviour |
|---|---|
| `IsDirty(DiffSide)` | True when the pane's document has changed since load or since the last save. AvaloniaEdit's `UndoStack` provides the signal |
| `Save(DiffSide)` | Writes the pane's document to `PaneSource.Path` using `PaneSource.Encoding` and `TextInfo.LineEnding`, preserving the BOM exactly as `PaneSource.FromBytes` detected it |
| Disk changed since load | Detected before writing and reported through the existing error boundary — the banner, with the state moving to `Degraded`. Never silently overwritten |
| No path | A pane built from a string cannot be saved; `Save` reports rather than throws |

The dirty marker appears in the pane header and in the status strip, both of which already exist
and already have a place for it.

**Round-tripping is the acceptance bar, not a nicety.** A CRLF file with a UTF-8 BOM that is
opened, edited on one line and saved must differ from the original in exactly that line's bytes.

### Copy to side

Arrows in `ChangeConnectorGutter`, per block and — at the row level — per line. Copying block *i*
left-to-right is `Replace` over `RightLines` with the text of `LeftLines`; the re-diff that
follows collapses the block. Undo is the editor's own, so a copy is undoable like any edit.

An arrow is offered only when the *target* side is editable.

### Key bindings

F7 / Shift+F7 / F6 / Ctrl+F / F3 keep their meanings. Editing adds copy-left and copy-right on the
current block, and Ctrl+S for the focused pane.

## Phases

| Phase | Size | Content |
|---|---|---|
| **1 — Typing** | S | Flip `IsReadOnly`; prove the presenter, renderers, margins and scroll sync all honour it with no hidden dependency on immutability. No re-diff yet: the metadata goes stale and renders `Unchanged`, which is the bounds-check doing its job |
| **2 — Live re-diff** | M | Debounced rebuild on `TextChanged` through the existing worker; the document-preserving rebuild path made explicit; caret, selection, scroll and undo asserted to survive; the three caches invalidated |
| **3 — Dirty and save** | M | `IsDirty`, `Save`, the encoding and line-ending round-trip, the changed-on-disk report, the header and strip markers |
| **4 — Copy to side** | M | Block and line arrows in the connector gutter, hit-testing, the `Replace`, the collapse-on-rebuild, the key bindings |
| **5 — Feedback and polish** | S | Modified-since-load marks in the marker margin, the unsaved-changes state, tooltips, automation names on the new decorators |
| **6 — Scale and hardening** | M | The re-diff loop under the 200k-line pair; the priming cost per keystroke; a `Perf` measurement for the edit→redraw latency |

## Testing

Per the house rule, every new test is proven able to fail before it is committed.

| Test | Asserts |
|---|---|
| Typing re-diffs | A keystroke in an editable pane produces a new model and a changed block count once the debounce elapses |
| The document survives | Caret offset, selection, scroll offset and undo-stack depth are identical across an edit-triggered rebuild |
| Undo restores | Undo after an edit returns the document *and* the model to their prior state |
| Read-only still holds | A read-only pane rejects typing and pasting while its neighbour is editable |
| Copy to side collapses the block | Copying block *i* and letting the re-diff land leaves no block covering those rows |
| Save round-trips | CRLF + BOM in, one line edited, bytes out identical except that line. Repeated for LF and for no-BOM |
| Changed on disk | A file touched behind the control's back is reported, the state degrades, and nothing is overwritten |
| Stale metadata never throws | Between the keystroke and the build, every visible row renders — as `Unchanged` where the metadata cannot answer |
| Caches invalidated | Word pieces and find matches from before the edit are not rendered after it |
| Scale (`Perf`) | Edit→redraw latency on the 200k pair, recorded in *Measurements* |

## Risks

| Risk | Assessment |
|---|---|
| **Re-priming cost per keystroke.** Priming the 200k pair took 1,243 ms to be up. If every debounced rebuild re-primes the union of padded sets, editing a large file stalls | The sharpest risk in this plan. Phase 6 measures it before Phase 2's debounce is tuned. Mitigations, in order of preference: prime only the padded ranges the new build actually changed; keep the previous priming while the build runs; a scale threshold above which re-diff becomes explicit rather than live. **The plan does not assume live re-diff is affordable at every size** |
| Live re-diff makes DiffPlex the bottleneck | The 200k build was 297 ms — fine for save-triggered rebuilds, too slow for a 300 ms debounce on a large file. Same mitigation path as above; the vendoring decision closed in plan 00001 may need reopening, and that is a `DECISIONS.md` entry if so |
| Scroll sync during the debounce | Rows are stale, so the panes drift for the debounce window. Accepted and documented; the alternative is freezing the diff during typing, which is worse |
| Word-diff cache correctness | A stale piece rectangle drawn over edited text is a visible wrong answer, not a crash. The invalidation test is the guard |
| `IsReadOnly` has a hidden dependency somewhere | Phase 1 exists precisely to find out before anything is built on top |

## Conventions

Conventional Commits, dense bodies, no AI attribution trailer. Every user-visible string through
`DiffViewStrings`, every log line through `DiffViewLog`, which never carries document text. The
theme audit regenerates after any change under `src/DiffView.Avalonia/Themes`. New tests are
proven able to fail before they are committed.
