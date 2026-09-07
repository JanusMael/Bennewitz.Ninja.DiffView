# Third-party notices

DiffView is built on, and in places adapts code from, the following projects. Package
dependencies carry their own licenses through NuGet; this file records the ones whose source
was read or adapted, and the assets bundled into the repository.

| Project | License | Use |
|---|---|---|
| [ClaudeForge](https://github.com/JanusMael/ClaudeForge) | MIT | Design reference and lifted code: the status controller, status and kind tokens, the accessibility coverage guard, the demo's crash-handling shape; `LayeredEditors.Avalonia.Diagnostics` consumed as a package |
| [SourceGit](https://github.com/sourcegit-scm/sourcegit) | MIT | Design reference for the diff rendering approach (background renderers, margins, scroll binding, minimap) |
| [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) | MIT | The text editor the panes are built on; source read for the padding and priming design; its `TextEditor` and `TextArea` control templates adapted in `src/DiffView.Avalonia/Themes/DiffPanePresenter.axaml` |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | MIT | The UI framework; Fluent and Simple theme sources read by the theme audit |
| [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) | MIT | The demo's primary theme; source read by the theme audit |
| [DiffPlex](https://github.com/mmanela/diffplex) | Apache-2.0 | Line diff engine and word chunkers |
| [TextMateSharp](https://github.com/danipen/TextMateSharp) | MIT | Grammar and theme engine behind `AvaloniaEdit.TextMate`; `TextMateSharp.Grammars` carries the grammars and the Dark+ / Light+ themes, which come from [VS Code](https://github.com/microsoft/vscode) (MIT) and the upstream grammars each grammar's own header names |
| [Onigwrap](https://github.com/danipen/TextMateSharp) / [Oniguruma](https://github.com/kkos/oniguruma) | MIT / BSD-2-Clause | The regular-expression engine TextMateSharp tokenizes with, and its native binding |
| [DejaVu fonts](https://dejavu-fonts.github.io/) | Bitstream Vera license; DejaVu changes public domain | `fixtures/fonts/DejaVuSansMono.ttf`, bundled so rendered snapshots do not depend on installed fonts. License text beside the font |
| Inter (via `Avalonia.Fonts.Inter`) | SIL OFL 1.1 | The demo's UI font |

Code adapted close to verbatim is marked at the point of use with the project it came from.
