# OmniBrille

OmniBrille is an optional, standalone-capable spatial navigation application for graph-based filesystem exploration. It presents Structure now and is designed to present OmniSorSe-backed Context later. This repository is an independent application: users who never install OmniBrille incur no renderer, asset, package-size, runtime, dependency, or startup cost in OmniSorSe.

Conceptually, OmniSorSe is the brain. OmniBrille is the visual lens and spatial navigation interface.

Stage 4 adds the first real read-only connected provider for OmniSorSe 2.4.0 Explorer Protocol v1 while preserving the standalone application. Context presentation, voice, destructive file operations, and packaging remain intentionally absent.

> **Connection availability:** OmniBrille's Protocol v1 client is implemented and has been validated against OmniSorSe's production v2.4.0 host in a real two-process test. The released OmniSorSe 2.4.0 desktop deliberately has no companion discovery, launch action, or finalized grant-handoff contract, so an ordinary installed user cannot initiate connected mode yet. OmniBrille never scans for hidden endpoints or accepts a bearer token on the command line to work around that product gap.

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
- Accessible `Standalone`, connecting, connected, unavailable, incompatible, disconnected, and reconnecting states; a disconnected graph remains visible as stale context rather than crashing.
- Safe provider switching: connected opaque IDs, selection, Search, details, and Back history are cleared before standalone access is established.

## Privacy and access philosophy

Standalone mode can inspect only the root the user explicitly chooses. It does not enumerate unrelated drives or user folders, follow directory reparse points recursively, modify files, use cloud services, emit telemetry, or run a background indexer. Structural search is an explicit foreground action, stays inside that root, and has result/directory limits.

Visual preferences are stored below the operating system's local application-data directory in `OmniBrille/visual-preferences.json` (`%LOCALAPPDATA%\OmniBrille` on Windows). They contain only theme/effects choices and the developer diagnostics toggle; selected filesystem paths are not persisted.

## Build and run

Prerequisites: .NET 8 SDK and a Windows, macOS, or Linux desktop supported by Avalonia. Stage 4 validates Windows build/runtime and two-process Protocol v1 behavior plus Windows/Ubuntu build/tests; macOS remains build-compatible by design but is not runtime-validated yet.

```powershell
cd "D:\Own Projects\OmniBrille"
dotnet restore .\OmniBrille.sln
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniBrille.Desktop\OmniBrille.Desktop.csproj
```

The application initially shows no filesystem content. Choose a folder to establish the access root. For an explicit command-line launch (useful for local smoke testing), append `-- --root "C:\path\you\chose" --theme Light`. The theme value may be `Light` or `Dark`.

A future OmniSorSe launcher may start OmniBrille with `--omnisorse-handoff <one-time-pipe-name>`. That pipe transfers the short-lived grant over a current-user-only channel; the secret itself is never a command-line argument or preference. This is an OmniBrille launch seam, not a discovery service, and OmniSorSe 2.4.0 does not yet expose it from its desktop UI.

Open the HUD settings control to select `Reduced motion`, `Reduced visual effects`, or the local diagnostics overlay. Open the accessible list from the `List` HUD control or `Ctrl+Shift+L`. Keyboard essentials are `Ctrl+F` for search, `Backspace` or `Alt+Left` for Back, arrows to change selection, `Enter` to activate, `Escape` to dismiss/cancel, `+`/`-` to zoom, and `0` to reset the graph view.

## Repository structure

```text
src/
  OmniBrille.Core/            explorer contracts, graph/context budgets, caches, layout, presentation policy
  OmniBrille.Infrastructure/  standalone adapter, strict Protocol v1 client/connected adapter, preferences
  OmniBrille.Desktop/         Avalonia shell, session, custom renderer, visual system, themes
  OmniSorSe.ExplorerProtocol/ exact dependency-free v1 wire contracts mirrored from OmniSorSe v2.4.0
tests/
  OmniBrille.Tests/           domain, session, filesystem, failure, and persistence tests
  OmniBrille.HeadlessTests/   non-pixel Avalonia shell/interaction tests
docs/
  architecture.md             system boundaries and Stage 4 provider/rendering decisions
  context-rendering-contract.md renderer limits and semantics for future Context data
  explorer-protocol.md        actual Explorer Protocol v1 behavior, integration, and shipped gaps
ROADMAP.md                    staged implementation plan
```

## Renderer profiling

Enable `Developer diagnostics` from the settings HUD. The overlay reports node/edge/label counts; scene budget and zoom; layout, preparation, total render, background, edge, glyph, and label time; per-render allocations; bounded cache occupancy; data-rain cost; and the latest directory-load duration. These are local sampling aids, not product guarantees or telemetry. Representative measurements and the rationale for retaining the 48-node budget are recorded in [the architecture](docs/architecture.md).

CI is defined in `.github/workflows/ci.yml`. It restores, verifies formatting, builds Release with analyzers-as-errors, and runs all tests on `windows-latest` and `ubuntu-latest`.

## OmniSorSe relationship

OmniSorSe is the primary local-first file intelligence application. It is responsible for scanning, indexing, Search, Content Intelligence, Media Intelligence, OCR, transcripts, Related Files, organization, safe file operations, and persistent intelligence/index state.

OmniBrille is the optional spatial navigation companion. It is responsible for graph-based filesystem navigation, Structure mode, spatial Search presentation, visual navigation, and future voice navigation/search. Future Context mode will display relationships supplied by OmniSorSe rather than duplicating its intelligence.

OmniBrille consumes OmniSorSe Explorer Protocol v1 through a narrow named-pipe client and will never read OmniSorSe's SQLite schema or reuse its application/indexing implementations. Only the small dependency-free wire contract is mirrored locally. Standalone and connected providers remain separate authorities: connected mode uses only roots and opaque nodes authorized by OmniSorSe, with no direct-filesystem fallback.

The private GitHub repository is `nishdel/OmniBrille`.

See [the architecture](docs/architecture.md), [the future Context rendering contract](docs/context-rendering-contract.md), [the actual Protocol v1 integration record](docs/explorer-protocol.md), and [the roadmap](ROADMAP.md).
