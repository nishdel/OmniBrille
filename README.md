# OmniExplorer

OmniExplorer is a futuristic spatial interface for exploring files and, later, the relationships supplied by OmniSorSe. This repository is an independent application: OmniExplorer is optional, and OmniSorSe incurs no renderer, asset, package-size, runtime, or startup dependency from it.

This first engineering pass delivers a runnable standalone **Structure** explorer. It is not the finished product and it does not implement semantic **Context** intelligence.

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
dotnet restore .\OmniExplorer.sln
dotnet build .\OmniExplorer.sln --configuration Release --no-restore
dotnet test .\OmniExplorer.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniExplorer.Desktop\OmniExplorer.Desktop.csproj
```

The application initially shows no filesystem content. Choose a folder to establish the access root.
For an explicit command-line launch (useful for local smoke testing), append `-- --root "C:\path\you\chose" --theme Light`. The theme value may be `Light` or `Dark`.

## Repository structure

```text
src/
  OmniExplorer.Core/            explorer contracts, graph model/budget, layout, navigation
  OmniExplorer.Infrastructure/  bounded standalone filesystem and structural search adapter
  OmniExplorer.Desktop/         Avalonia shell, presentation session, custom graph renderer, themes
tests/
  OmniExplorer.Tests/           non-rendering behavior and filesystem integration tests
docs/
  architecture.md               system boundaries and rendering decision
  explorer-protocol.md          future OmniSorSe local-contract direction
ROADMAP.md                      staged implementation plan
```

## OmniSorSe relationship

OmniSorSe is the intended future intelligence/file-management companion (historically/currently named OpenSorSe during its transition). OmniExplorer will consume a narrow, versioned local protocol. It will never read OmniSorSe's SQLite schema or reuse internal indexing/domain types. The protocol and connected adapter are intentionally design-only in this pass.

See [the architecture](docs/architecture.md), [the future protocol boundary](docs/explorer-protocol.md), and [the roadmap](ROADMAP.md).
