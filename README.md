# OmniBrille

OmniBrille is an optional, standalone-capable spatial navigation application for graph-based filesystem exploration. It presents standalone Structure and real OmniSorSe-backed Structure and Context. This repository is an independent application: users who never install OmniBrille incur no renderer, asset, package-size, runtime, dependency, or startup cost in OmniSorSe.

Conceptually, OmniSorSe is the brain. OmniBrille is the visual lens and spatial navigation interface.

Stage 6 packages that connected experience as an independently installable Windows companion and matures bounded Context filtering and inspection over unchanged Explorer Protocol v1. Voice, destructive file operations, Hybrid mode, and automatic updating remain intentionally absent.

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
- A synchronized accessible navigation list (`Ctrl+Shift+L`) with the same bounded nodes, selection, search emphasis, drill-down, details, aggregates, and Back state as the graph.
- One automation tree item per visible graph node, including name/type, selection/focus/aggregate status, bounds, help, and an invoke action. Invisible source items are not exposed.
- Text-scale-aware label density validated at 100%, 125%, 150%, and 200%, plus documented Dark/Light contrast checks.
- Bounded text-layout and drawing-resource caches; phase-level local diagnostics for background, edges, glyphs, labels, allocations, caches, and data rain. No diagnostics leave the machine.
- Graceful missing/inaccessible/deleted-folder handling, bounded enumeration, root-boundary enforcement, and no recursive reparse-point traversal.
- Domain/infrastructure tests plus Avalonia headless UI tests, and Windows/Ubuntu GitHub Actions CI.
- A strict Explorer Protocol v1 named-pipe client, explicit version/capability validation, opaque session-bound node identity, and defensive payload bounds.
- A connected provider that adapts real OmniSorSe authorized roots, paged Structure children, unified Search, and bounded details into the same graph/session/list model as standalone mode.
- The real v2.5 release-candidate installed-companion flow: an explicit OmniSorSe action discovers and launches OmniBrille, transfers a short-lived scoped grant through a one-time current-user-only pipe, and authenticates the session without putting a bearer token on the command line.
- A primary `Structure | Context` switch. Context requests only real `GetNeighborhood(IncludeContext: true)` and focus-local `GetRelated` evidence supplied by OmniSorSe; standalone Context explains that OmniSorSe is required and never fabricates relationships.
- A deterministic focus-centered Context layout, solid structural edges, distinct cyan dashed Context edges, 48 combined nodes, at most 36 Context edges, and at most three Context edges touching a node.
- Context refocus and Back, real OmniSorSe Search-to-Context focus, compact reason/evidence/provenance details, Context-aware graph automation peers, and synchronized keyboard/list navigation.
- Reversible, local presentation filters for server-authored relationship kind, minimum ranking strength, and evidence class. The HUD reports visible, matching, and authorized counts and never alters OmniSorSe state.
- Strength-aware deterministic Context depth, a clear no-relationships/no-filter-matches state, and a compact relationship hierarchy (`Related because`, `Strength`, `Evidence`, `Source`).
- A reproducible unsigned, per-user Windows installer at `%LOCALAPPDATA%\Programs\OmniBrille`, which the committed OmniSorSe v2.5 RC discovers through its existing conventional-location policy without an environment override.
- Accessible `Standalone`, connecting, connected, unavailable, incompatible, disconnected, and reconnecting states; a disconnected graph remains visible as stale context rather than crashing.
- Safe provider switching: connected opaque IDs, selection, Search, details, and Back history are cleared before standalone access is established.

## Privacy and access philosophy

Standalone mode can inspect only the root the user explicitly chooses. It does not enumerate unrelated drives or user folders, follow directory reparse points recursively, modify files, use cloud services, emit telemetry, or run a background indexer. Structural search is an explicit foreground action, stays inside that root, and has result/directory limits.

Visual preferences are stored below the operating system's local application-data directory in `OmniBrille/visual-preferences.json` (`%LOCALAPPDATA%\OmniBrille` on Windows). They contain only theme/effects choices and the developer diagnostics toggle; selected filesystem paths are not persisted.

## Build and run

Prerequisites for source builds: .NET 8 SDK and a Windows, macOS, or Linux desktop supported by Avalonia. Stage 6 validates Windows installed runtime and the normal v2.5 RC two-process handoff plus Windows/Ubuntu build/tests; macOS remains build-compatible by design but is not runtime-validated yet.

```powershell
cd "D:\Own Projects\OmniBrille"
dotnet restore .\OmniBrille.sln
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniBrille.Desktop\OmniBrille.Desktop.csproj
```

Build the pinned, unsigned Windows installer with:

```powershell
.\build\Package-Windows.ps1 -BootstrapInnoSetup
```

The current package is `OmniBrille-0.6.0-preview.1-win-x64-setup.exe`. It is self-contained, non-trimmed, and multi-file for reliable Avalonia/XAML behavior. See [PACKAGING.md](docs/PACKAGING.md) for prerequisites, install/upgrade/uninstall semantics, signing readiness, and alternatives considered.

The application initially shows no filesystem content. Choose a folder to establish the access root. For an explicit command-line launch (useful for local smoke testing), append `-- --root "C:\path\you\chose" --theme Light`. The theme value may be `Light` or `Dark`.

The committed OmniSorSe v2.5 release candidate can start OmniBrille with `--omnisorse-handoff <one-time-pipe-name>`. The pipe name has the fixed product prefix and a 128-bit random suffix; the pipe transfers a strict, bounded, short-lived grant over a current-user-only channel. The secret is never a command-line value, file, preference, UI string, or normal diagnostic. OmniBrille performs no background discovery. Direct launch remains standalone.

Open the HUD settings control to select `Reduced motion`, `Reduced visual effects`, or the local diagnostics overlay. Open the accessible list from the `List` HUD control or `Ctrl+Shift+L`. Keyboard essentials are `Ctrl+1` for Structure, `Ctrl+2` for Context, `Ctrl+Shift+F` for Context filters, `Ctrl+F` for Search, `Backspace` or `Alt+Left` for Back, arrows to change selection, `Enter` to activate/refocus, `Escape` to dismiss/cancel, `+`/`-` to zoom, and `0` to reset the graph view.

## Repository structure

```text
src/
  OmniBrille.Core/            explorer/Context contracts, bounded builders, layouts, caches, presentation policy
  OmniBrille.Infrastructure/  standalone adapter, strict Protocol v1 client/connected Context adapter, preferences
  OmniBrille.Desktop/         Avalonia shell/session, Structure/Context renderer, accessibility, themes
  OmniSorSe.ExplorerProtocol/ exact dependency-free v1 wire contracts mirrored from OmniSorSe
tests/
  OmniBrille.Tests/           domain, session, filesystem, failure, and persistence tests
  OmniBrille.HeadlessTests/   non-pixel Avalonia shell/interaction tests
docs/
  architecture.md             system boundaries and Stage 6 provider/rendering/packaging decisions
  context-rendering-contract.md implemented Context limits, semantics, accessibility, and gaps
  explorer-protocol.md        actual Protocol v1 and v2.5 RC handoff behavior
  PACKAGING.md                reproducible Windows installer and lifecycle policy
ROADMAP.md                    staged implementation plan
```

## Renderer profiling

Enable `Developer diagnostics` from the settings HUD. The overlay reports node/edge/label counts; scene budget and zoom; layout, preparation, total render, background, edge, glyph, and label time; per-render allocations; bounded cache occupancy; data-rain cost; and the latest directory-load duration. These are local sampling aids, not product guarantees or telemetry. Representative measurements and the rationale for retaining the 48-node budget are recorded in [the architecture](docs/architecture.md).

CI is defined in `.github/workflows/ci.yml`. It restores, verifies formatting, builds Release with analyzers-as-errors, and runs all tests on `windows-latest` and `ubuntu-latest`. The Windows leg also builds the unsigned installer and retains it as a private workflow artifact for 14 days; it does not publish a release.

## OmniSorSe relationship

OmniSorSe is the primary local-first file intelligence application. It is responsible for scanning, indexing, Search, Content Intelligence, Media Intelligence, OCR, transcripts, Related Files, organization, safe file operations, and persistent intelligence/index state.

OmniBrille is the optional spatial navigation companion. It is responsible for graph-based filesystem navigation, Structure mode, spatial Search presentation, visual navigation, real server-authored Context presentation, and future voice navigation/search. Context relationships are supplied exclusively by OmniSorSe; OmniBrille does not duplicate its intelligence.

OmniBrille consumes OmniSorSe Explorer Protocol v1 through a narrow named-pipe client and will never read OmniSorSe's SQLite schema or reuse its application/indexing implementations. Only the small dependency-free wire contract is mirrored locally. Standalone and connected providers remain separate authorities: connected mode uses only roots and opaque nodes authorized by OmniSorSe, with no direct-filesystem fallback.

The private GitHub repository is `nishdel/OmniBrille`.

See [the architecture](docs/architecture.md), [the packaging guide](docs/PACKAGING.md), [the Context rendering contract](docs/context-rendering-contract.md), [the Protocol v1 and handoff integration record](docs/explorer-protocol.md), and [the roadmap](ROADMAP.md).
