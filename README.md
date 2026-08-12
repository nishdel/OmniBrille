# OmniBrille

OmniBrille is an optional, standalone-capable spatial navigation application for graph-based filesystem exploration. It presents Structure now and is designed to present OmniSorSe-backed Context later. This repository is an independent application: users who never install OmniBrille incur no renderer, asset, package-size, runtime, dependency, or startup cost in OmniSorSe.

Conceptually, OmniSorSe is the brain. OmniBrille is the visual lens and spatial navigation interface.

Stage 2 delivers a hardened standalone Structure explorer and a materially richer visual foundation. It remains an engineering-stage application: Context intelligence, OmniSorSe integration, voice, destructive file operations, and packaging are intentionally absent.

## What works now

- Explicit operating-system folder selection; no drive-wide startup crawl.
- A hard-bounded spatial graph containing the focused folder and immediate children.
- Progressive directory batches: the focus shell appears first, bounded children stream in, obsolete navigation is rejected, and navigating away cancels prior work.
- Deterministic three-depth radial layout with positional continuity, a strong focus, receding context, and restrained focus transitions.
- Reversible aggregate refinement. Large directories open deterministic structural pages with previous, next, and overview controls without exceeding the 48-node scene budget.
- Priority- and collision-aware labels plus zoom/depth/density level of detail.
- Double-click drill-down, Back/Backspace navigation, selection, dismissible details, mouse-wheel/button/keyboard zoom, drag-to-pan, and arrow/Enter graph navigation.
- Bounded name/folder/path search with graph dimming/highlighting, a secondary compact result surface, cancellation, and result-to-graph focus.
- A shared electric-blue Dark/Light visual system, restrained decorative network, and bounded data-rain loading treatment.
- Persisted `Reduced motion` and `Reduced visual effects` preferences.
- Optional local diagnostics for scene nodes, edges, labels, zoom, layout, preparation, render, and latest load timings. No diagnostics leave the machine.
- Graceful missing/inaccessible/deleted-folder handling, bounded enumeration, root-boundary enforcement, and no recursive reparse-point traversal.
- Domain/infrastructure tests plus Avalonia headless UI tests for the important shell and settings states.

## Privacy and access philosophy

Standalone mode can inspect only the root the user explicitly chooses. It does not enumerate unrelated drives or user folders, follow directory reparse points recursively, modify files, use cloud services, emit telemetry, or run a background indexer. Structural search is an explicit foreground action, stays inside that root, and has result/directory limits.

Visual preferences are stored locally in `%LOCALAPPDATA%\OmniBrille\visual-preferences.json`. They contain only theme/effects choices and the developer diagnostics toggle; selected filesystem paths are not persisted.

## Build and run

Prerequisites: .NET 8 SDK and a Windows, macOS, or Linux desktop supported by Avalonia. Windows is the validated platform for this stage.

```powershell
cd "D:\Own Projects\OmniBrille"
dotnet restore .\OmniBrille.sln
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniBrille.Desktop\OmniBrille.Desktop.csproj
```

The application initially shows no filesystem content. Choose a folder to establish the access root. For an explicit command-line launch (useful for local smoke testing), append `-- --root "C:\path\you\chose" --theme Light`. The theme value may be `Light` or `Dark`.

Open the HUD settings control to select `Reduced motion`, `Reduced visual effects`, or the local diagnostics overlay. Keyboard essentials are `Ctrl+F` for search, `Backspace` or `Alt+Left` for Back, arrows to change graph selection, `Enter` to activate, `Escape` to dismiss/cancel, `+`/`-` to zoom, and `0` to reset the view.

## Repository structure

```text
src/
  OmniBrille.Core/            explorer contracts, graph budget/refinement, layout, presentation policy
  OmniBrille.Infrastructure/  bounded filesystem/search adapter and local preference store
  OmniBrille.Desktop/         Avalonia shell, session, custom renderer, visual system, themes
tests/
  OmniBrille.Tests/           domain, session, filesystem, failure, and persistence tests
  OmniBrille.HeadlessTests/   non-pixel Avalonia shell/interaction tests
docs/
  architecture.md             system boundaries and Stage 2 rendering/loading decisions
  explorer-protocol.md        future OmniSorSe local-contract direction
ROADMAP.md                    staged implementation plan
```

## Renderer profiling

Enable `Developer diagnostics` from the settings HUD. The overlay reports the current node/edge/label counts, scene budget, zoom, layout and scene-preparation time, last render time, and most recent directory-load duration. These are local sampling aids, not product guarantees or telemetry. Representative performance measurements and the rationale for retaining the 48-node budget are recorded in [the architecture](docs/architecture.md).

## OmniSorSe relationship

OmniSorSe is the primary local-first file intelligence application. It is responsible for scanning, indexing, Search, Content Intelligence, Media Intelligence, OCR, transcripts, Related Files, organization, safe file operations, and persistent intelligence/index state.

OmniBrille is the optional spatial navigation companion. It is responsible for graph-based filesystem navigation, Structure mode, spatial Search presentation, visual navigation, and future voice navigation/search. Future Context mode will display relationships supplied by OmniSorSe rather than duplicating its intelligence.

OmniBrille will consume a narrow, versioned local protocol and will never read OmniSorSe's SQLite schema or reuse internal indexing/domain types. The protocol and connected adapter remain design-only.

The private GitHub repository is `nishdel/OmniBrille`.

See [the architecture](docs/architecture.md), [the future protocol boundary](docs/explorer-protocol.md), and [the roadmap](ROADMAP.md).
