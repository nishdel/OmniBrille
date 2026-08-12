# Future OmniSorSe explorer protocol boundary

This is a design seam, not an implemented server or published package.

The future local integration should be a tiny, versioned contract—conceptually `OmniSorSe.ExplorerProtocol`—shared as source or a deliberately versioned package only when both applications need it. OmniBrille will add a connected provider adapter behind the same explorer/search abstractions used by standalone mode.

## Naming decision

`OmniSorSe.ExplorerProtocol` remains the preferred conceptual package name. It describes the stable role of the contract rather than the current name of a particular client, so future product renames do not force protocol churn. `OmniSorSe.NavProtocol` and `OmniSorSe.OmniBrilleProtocol` were considered but would either be less precise or couple the contract to one application. Domain DTO names such as `ExplorerNode`, `ExplorerEdge`, and `ExplorerNeighborhood` remain semantically strong for the same reason.

No protocol project, transport, package, or server is created by this rename.

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

Every neighborhood/search operation must be bounded, cancellable, and scoped to roots OmniSorSe declares accessible. Results should carry stable opaque node IDs, provenance for non-structural relationships, truncation information, and user-safe failure codes.

## Forbidden contents

The protocol must not expose or contain:

- SQLite tables, SQL, migrations, or storage implementation;
- indexer/search-engine internals;
- Content Intelligence or OmniSorSe domain implementations;
- renderer/layout/theme types;
- arbitrary unrestricted filesystem paths as an authority bypass.

Transport is deliberately undecided. Before implementation, write an ADR comparing named pipes/local sockets and a loopback authenticated endpoint for lifecycle, security, streaming/cancellation, diagnostics, and cross-platform behavior. The handshake must reject incompatible major versions and negotiate optional capabilities rather than assuming them.

Standalone mode remains fully usable without this protocol or any OmniSorSe installation.
