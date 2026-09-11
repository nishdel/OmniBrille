# ADR 0003: Bounded actual descendant previews

Status: Accepted

Date: 2026-09-11

## Context

Structure scenes need to distinguish immediate children from a small glimpse of deeper folders. Presentation bands alone cannot supply that hierarchy: making a direct child smaller or pushing it outward does not make it a descendant. A preview therefore needs real provider data and an explicit containment parent. Acquisition must preserve the separate Standalone filesystem and Connected opaque-ID authorities in [ADR 0001](0001-separate-provider-authorities.md) and the 48-node scene in [ADR 0002](0002-bounded-deterministic-scenes.md).

A progressive batch size bounds returned data, but may not bound the entries inspected before producing that batch. Reusing ordinary directory acquisition for each visible folder could turn a visual preview into expensive traversal or continuation paging.

## Decision

- Add the optional application-local `IExplorerDirectoryPreviewProvider` contract. Each provider returns a bounded sample of actual immediate folders and owns the work limit and authority checks.
- Start preview acquisition only after a successful primary Structure overview is visible. Consider at most four already-admitted navigable direct folders, request them sequentially, and admit at most three children per parent into unused slots of the existing 48-node scene. Stop when capacity is exhausted. Do not recursively preview returned descendants.
- In Standalone, inspect at most 64 immediate filesystem entries per parent, counting unreadable entries as inspected work, and return at most three ordinary navigable subfolders. Revalidate the requested directory and ancestors through the selected root for reparse points before enumeration.
- In Connected mode, use the issued opaque target for focus details and one requested child page of at most 32 nodes. Retain at most three folders and do not follow a continuation to find more. Validate focus ID, requested page size, and each child's parent ID before mapping. Use existing Protocol v1 operations without direct-filesystem fallback.
- Before admission, verify that each preview's focus matches the requested admitted parent, every child has that exact parent target, and all admitted identities are distinct. Reject files, non-navigable/reparse folders, orphans, and attempts to preview another preview.
- Apply enrichment only while the provider, load generation, primary snapshot, neighborhood, Structure mode, and aggregate-overview state remain current. Cancellation, preview failure, or a stale completion leaves the accepted primary scene intact.
- Keep preview entries separate from primary children in the snapshot. They do not change direct-child totals, hidden counts, or aggregate paging and never displace admitted children or controls. The Structure renderer marks them as descendant previews at semantic depth 2 with a smaller folder, `↳` name, and an edge to the actual parent. Context/Hybrid gain no per-node acquisition or inferred relationships.

## Reasoning

Separate provider work limits protect foreground responsiveness independently of the renderer budget. An optional contract allows providers without a safe sample operation to retain the ordinary Structure experience. Explicit parent data makes the distinction testable and accessible without turning spatial density into false hierarchy. Loading the primary scene first keeps an optional preview failure from becoming a navigation failure.

## Consequences

- Preview absence is not evidence of an empty folder. The finite sample can miss subfolders after the inspection/page limit, and a full scene shows no previews.
- The worst case is four sequential bounded preview operations, with at most twelve additional admitted nodes inside the original 48-node cap. Latency still depends on the filesystem or protocol host; the UI does not wait for previews to declare the primary scene ready.
- New application-local snapshot/provider types and scene semantics require provider, session, layout, list, and automation coverage. There is no wire-schema, persistence-schema, permission, or public installer-version change.
- Real filesystem replacement races, native rendering/hit behavior, and changed external hosts still need their relevant platform/integration qualification; deterministic fixtures do not replace those gates.

## Rejected alternatives

- Inferring descendants from radius, size, names, or layout density.
- Recursively crawling subtrees or asking every visible node for another neighborhood.
- Treating a normal progressive batch count as a filesystem inspection cap.
- Following Connected continuation pages until enough folders are found.
- Reserving preview slots by evicting direct children or aggregate controls.
- Treating a projected Connected path as filesystem authority.

## Evidence

- Contract and admission: [`ExplorerModels.cs`](../../src/OmniBrille.Core/ExplorerModels.cs), [`GraphNeighborhoodBuilder.cs`](../../src/OmniBrille.Core/GraphNeighborhoodBuilder.cs), and [`GraphNeighborhoodBuilderTests.cs`](../../tests/OmniBrille.Tests/GraphNeighborhoodBuilderTests.cs).
- Work and authority limits: [`FileSystemExplorerProvider.cs`](../../src/OmniBrille.Infrastructure/FileSystemExplorerProvider.cs), [`FileSystemExplorerProviderTests.cs`](../../tests/OmniBrille.Tests/FileSystemExplorerProviderTests.cs), [`OmniSorSeConnectedProvider.cs`](../../src/OmniBrille.Infrastructure/OmniSorSe/OmniSorSeConnectedProvider.cs), and [`OmniSorSeConnectedProviderTests.cs`](../../tests/OmniBrille.Tests/OmniSorSeConnectedProviderTests.cs).
- Replacement safety and current-scene admission: [`ExplorerSession.cs`](../../src/OmniBrille.Desktop/Presentation/ExplorerSession.cs) and the bounded, full-scene, cancellation, provider-replacement, and late-preview fixtures in [`ExplorerSessionTests.cs`](../../tests/OmniBrille.Tests/ExplorerSessionTests.cs).
- Depth, placement, and projection: [`RadialGraphLayout.cs`](../../src/OmniBrille.Core/RadialGraphLayout.cs), [`RadialGraphLayoutTests.cs`](../../tests/OmniBrille.Tests/RadialGraphLayoutTests.cs), [`ExplorerSceneSemanticsTests.cs`](../../tests/OmniBrille.Tests/ExplorerSceneSemanticsTests.cs), and [`MainWindowHeadlessTests.cs`](../../tests/OmniBrille.HeadlessTests/MainWindowHeadlessTests.cs).
