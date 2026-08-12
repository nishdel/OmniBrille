# Future OmniSorSe explorer protocol boundary

This is a design seam, not an implemented server or published package.

The future local integration should be a tiny, versioned contract—conceptually `OmniSorSe.ExplorerProtocol`—shared as source or a deliberately versioned package only when both applications need it. OmniBrille will add a connected provider adapter behind the same explorer/search abstractions used by standalone mode.

## Naming decision

`OmniSorSe.ExplorerProtocol` remains the preferred conceptual package name. It describes the stable role of the contract rather than the current name of a particular client, so future product renames do not force protocol churn. `OmniSorSe.NavProtocol` and `OmniSorSe.OmniBrilleProtocol` were considered but would either be less precise or couple the contract to one application. Domain DTO names such as `ExplorerNode`, `ExplorerEdge`, and `ExplorerNeighborhood` remain semantically strong for the same reason.

No protocol project, transport, package, client, or server exists in Stage 3.

## Responsibilities

The contract may define versioned DTOs such as `ExplorerNode`, `ExplorerEdge`, `ExplorerNeighborhood`, `ExplorerNodeDetails`, `ExplorerCapabilities`, `AccessibleRoots`, `CurrentScope`, `SearchRequest`/`SearchResult`, and `RelatedFilesRequest`/`RelatedFilesResult`.

Likely operations are:

- capability/version negotiation;
- `GetAccessibleRoots()`;
- `GetChildren(nodeId)`;
- `GetNeighborhood(nodeId, depth, maxNodes)`;
- `GetRelated(nodeId, maxNodes)`;
- `GetNodeDetails(nodeId)`;
- `Search(query, mode)`.

Every neighborhood/search operation must be bounded, cancellable, and scoped to roots OmniSorSe declares accessible. Requests should carry a unique request ID, expected scope/focus, explicit node/edge limits, and cancellation semantics. Results should carry case-sensitive stable opaque node and relationship IDs, relationship type/importance, provider-authored reason/provenance for non-structural relationships, truncation/completion information, and user-safe failure codes.

The handshake must negotiate protocol major/minor version and optional capabilities. Incompatible major versions fail closed with a clear user-facing state; optional operations are never assumed. OmniSorSe remains authoritative for accessible roots, current scope, search, contextual relationships, and provenance.

Large or expensive operations may return an internally consistent bounded initial snapshot followed by bounded incremental pages or explicitly versioned replacement snapshots. Every update must identify its request and scene revision so OmniBrille can discard cancelled/obsolete work. A late response for a prior focus must never replace the current scene.

Provider limits and renderer limits are separate. OmniBrille supplies its current renderer envelope with requests and enforces it again on receipt. Stage 3 defaults are 48 combined visible nodes, 36 contextual edges, 84 combined edge slots, and three contextual edges touching one visible node; see [the future Context rendering contract](context-rendering-contract.md). These values may evolve through profiling without changing protocol identity, so the wire format must carry requested/returned limits rather than assuming constants.

Relationship reason/provenance must be available on demand without requiring labels on every edge. A future details request may return a concise human-readable reason plus structured evidence references. Missing, redacted, or unsupported provenance is explicit and must not be fabricated by OmniBrille.

## Forbidden contents

The protocol must not expose or contain:

- SQLite tables, SQL, migrations, or storage implementation;
- indexer/search-engine internals;
- Content Intelligence or OmniSorSe domain implementations;
- renderer/layout/theme types;
- arbitrary unrestricted filesystem paths as an authority bypass.

Transport is deliberately undecided. Before implementation, write an ADR comparing named pipes/local sockets and a loopback authenticated endpoint for lifecycle, security, streaming/cancellation, diagnostics, authentication, and cross-platform behavior.

Standalone mode remains fully usable without this protocol or any OmniSorSe installation.
