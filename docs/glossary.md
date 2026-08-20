# OmniBrille glossary

This file defines terms that repository history shows are easy to conflate. Current source and [`architecture.md`](architecture.md) remain authoritative for behavior.

| Term | Meaning |
| --- | --- |
| **Structure** | Filesystem or indexed containment presented as a bounded graph. Standalone and connected providers can both supply it. |
| **Context** | A connected-only view of OmniSorSe-authored relationships. OmniBrille filters and presents this evidence but does not create it. |
| **Hybrid** | One client-composed bounded scene combining authorized Structure and Context snapshots. It is not a protocol operation or a semantic engine. |
| **Graph focus** | The central node whose bounded neighborhood is being shown. This is distinct from selected node and keyboard focus. |
| **Selected node** | The visible node whose details/actions are active. Selection may change without acquiring a new graph focus. |
| **Keyboard focus** | The Avalonia control currently receiving keyboard input. It is UI state, not graph focus. |
| **Previous-focus node** | In Structure scenes, a receding orientation node represented by `ExplorerNodeKind.Context`. This enum value is historical structural context, not semantic Context mode. |
| **Standalone access root** | The filesystem path explicitly chosen by the user. It is the lexical security/navigation boundary for standalone enumeration and Search. |
| **Connected access root** | An opaque OmniSorSe-issued source node ID. It is not a filesystem path and grants no direct filesystem authority. |
| **Navigation target** | A provider-specific target: normally a path in Standalone and an opaque node ID in Connected. Code using it must not assume one representation. |
| **Display path** | Human-readable provider text. In Connected mode it is a label only, never an access token or fallback path. |
| **Core graph model** | `OmniBrille.Core` application-local entries, nodes, edges, snapshots, and provider interfaces. |
| **Protocol wire model** | `OmniSorSe.ExplorerProtocol` DTOs/enums carried over Explorer Protocol v1. Similar type names do not make the two models interchangeable. |
| **Authoritative snapshot** | Immutable authorized provider data, or a bounded client composition of such data for Hybrid, retained before local presentation filters/render budgets. `ExplorerSession` owns the active projection source; the connected provider has a separate short-lived Context cache. |
| **Presentation projection** | The visible bounded `ExplorerNeighborhood` built from authoritative provider data. Filters and budgets can omit data without changing authority. |
| **Aggregate** | A deterministic structural paging control representing items that do not fit the 48-node scene. It is not semantic clustering. |
| **Stale context** | The last valid connected graph retained for orientation after a provider failure. It must be visibly non-live and cannot authorize new work. |
| **Operation generation** | A monotonically increasing load, Search, details, or Context/Hybrid identity used with cancellation so late work cannot replace newer state of the same kind. |
| **Provider generation** | The authority identity changed on provider replacement; it additionally invalidates a deferred voice transcript whose targets may contain obsolete paths or opaque IDs. |
