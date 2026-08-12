# OmniBrille

OmniBrille is an optional, standalone-capable spatial navigation application for graph-based filesystem exploration. It presents Structure now and is designed to present OmniSorSe-backed Context later. This repository is an independent application: users who never install OmniBrille incur no renderer, asset, package-size, runtime, dependency, or startup cost in OmniSorSe.

Conceptually, OmniSorSe is the brain. OmniBrille is the visual lens and spatial navigation interface.

This initial engineering pass delivers a runnable standalone Structure explorer. It is not the finished product and does not fabricate or implement semantic Context intelligence.

## What works now

- Explicit operating-system folder selection; no drive-wide startup crawl.
- A bounded spatial graph containing the focused folder and its immediate children.
- Stable radial layout, thin luminous edges, simple folder/file glyphs, subdued previous context, and restrained focus transitions.
- Double-click drill-down, Back/Backspace navigation, selection, compact details, mouse-wheel/button zoom, drag-to-pan, and arrow/Enter graph navigation.
- On-demand name/folder/path search within the selected root, with bounded traversal, compact results, visible-node highlighting, and result-to-graph focus.
- Understandable aggregate nodes when a folder exceeds the initial 48-node scene budget.
- Dark and Light themes sharing one blue visual system.
- Cancellation, loading feedback, bounded enumeration, and graceful deleted/inaccessible-folder handling.
- Automated coverage for graph budgets, aggregation, deterministic layout, navigation boundaries, filesystem behavior, search limits, failures, and cancellation.

## Privacy and access philosophy

Standalone mode can inspect only the root the user explicitly chooses. It does not enumerate unrelated drives or user folders, follow directory reparse points, modify files, use cloud services, emit telemetry, or run a background indexer. Structural search is an explicit foreground action, stays inside that root, and has result/directory limits.

## Build and run

Prerequisites: .NET 8 SDK and a Windows, macOS, or Linux desktop supported by Avalonia. Windows is the validated platform for this pass.

```powershell
cd "D:\Own Projects\OmniBrille"
dotnet restore .\OmniBrille.sln
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniBrille.Desktop\OmniBrille.Desktop.csproj
```

The application initially shows no filesystem content. Choose a folder to establish the access root. For an explicit command-line launch (useful for local smoke testing), append `-- --root "C:\path\you\chose" --theme Light`. The theme value may be `Light` or `Dark`.

## Repository structure

```text
src/
  OmniBrille.Core/            explorer contracts, graph model/budget, layout, navigation
  OmniBrille.Infrastructure/  bounded standalone filesystem and structural search adapter
  OmniBrille.Desktop/         Avalonia shell, presentation session, custom graph renderer, themes
tests/
  OmniBrille.Tests/           non-rendering behavior and filesystem integration tests
docs/
  architecture.md             system boundaries and rendering decision
  explorer-protocol.md        future OmniSorSe local-contract direction
ROADMAP.md                    staged implementation plan
```

## OmniSorSe relationship

OmniSorSe is the primary local-first file intelligence application. It is responsible for scanning, indexing, Search, Content Intelligence, Media Intelligence, OCR, transcripts, Related Files, organization, safe file operations, and persistent intelligence/index state.

OmniBrille is the optional spatial navigation companion. It is responsible for graph-based filesystem navigation, Structure mode, spatial Search presentation, visual navigation, and future voice navigation/search. Future Context mode will display relationships supplied by OmniSorSe rather than duplicating its intelligence.

OmniBrille will consume a narrow, versioned local protocol and will never read OmniSorSe's SQLite schema or reuse internal indexing/domain types. The protocol and connected adapter are intentionally design-only in this pass.

The intended future GitHub repository is `nishdel/OmniBrille`; it has not been created or configured as a remote yet.

See [the architecture](docs/architecture.md), [the future protocol boundary](docs/explorer-protocol.md), and [the roadmap](ROADMAP.md).
