# OmniSorSe Explorer Protocol v1 integration

## Status and authority

Stage 4 consumes the real read-only protocol shipped in OmniSorSe v2.4.0. The authoritative source inspected for this integration is tag `v2.4.0`, commit `40552b9b2b18637313354713d66593d04cf0d92f`, especially `src/OmniSorSe.ExplorerProtocol` and `docs/OMNISORSE_TRANSITION_AND_EXPLORER_PROTOCOL_v2.4.md`.

The earlier OmniBrille document was conceptual. Its core boundary was correct—opaque graph DTOs, bounded reads, capability/version negotiation, cancellation, and no SQLite—but transport and exact fields were undecided. This document records the shipped behavior. OmniSorSe is authoritative when the two differ.

`src/OmniSorSe.ExplorerProtocol` mirrors the dependency-free v1 DTO/enums so OmniBrille can compile without an OmniSorSe application reference. It contains no host, SQLite, indexing, Search, intelligence implementation, renderer, or write API. A future published contract package can replace the mirrored source without changing the provider boundary.

## Actual v1 transport and framing

- Transport: on-demand .NET named pipe; Unix .NET implementations are Unix-domain-socket-backed.
- Windows isolation: server and client request `PipeOptions.CurrentUserOnly`.
- Endpoint: `ose-` plus 32 lowercase hexadecimal characters (128 random bits); no TCP/HTTP listener.
- Framing: four-byte little-endian payload length followed by strict UTF-8 JSON.
- Connection model: one request and one response per connection.
- JSON: web/camel-case property names, case-sensitive fields, string enums only, depth 16, no comments/trailing commas/unknown members/runtime types.
- Envelope: protocol major, request ID, session ID, bearer authorization token, operation, and typed JSON payload; the response repeats protocol major/request ID and contains either payload or a stable error.
- Bounds: 64 KiB requests, 1 MiB responses, 256 nodes, 512 edges, 100 Search/Related results, depth 2, four concurrent and sixteen queued requests, and a 15-second server timeout.

OmniBrille uses a three-second connection timeout and an 18-second client request deadline so the server's stable 15-second result normally wins. It validates the grant and negotiated limits, then independently validates every response identity, enum, ID, collection, relationship reference, reason, metadata, and details bound before adapting it.

## Authorization, discovery, and lifecycle

The actual server is dormant until an explicit trusted launcher calls `CreateSessionAsync` with a bounded set of currently configured OmniSorSe source IDs. The resulting grant contains an unpredictable endpoint, random session ID, 256-bit bearer secret, absolute expiry, and protocol version. Default lifetime is five minutes; the accepted range is 15 seconds to 15 minutes. Tokens are not persisted by OmniBrille.

Node IDs are opaque, case-sensitive, HMAC-derived, and session-bound. They are never reconstructed from paths or treated as database keys. A new grant/server session invalidates old IDs, so reconnect with a fresh grant resets scene, selection, Back history, Search, details, and aggregates.

OmniSorSe v2.4.0 intentionally has **no discovery service, public endpoint registry, companion launch action, or finalized grant-handoff message**. Normal startup keeps the protocol host dormant. OmniBrille consequently does not enumerate pipes, processes, ports, drives, or profile files. It exposes an in-memory connection seam and a `--omnisorse-handoff <one-time-pipe-name>` launch option. The bounded current-user-only handoff pipe passes the grant—not the secret on the command line—and was used for the Stage 4 production-host validation. The launcher side of that handoff must be agreed and implemented in a future OmniSorSe release before ordinary installed users can initiate connected mode.

Connection states are `Standalone`, `Discovering`, `Connecting`, `Connected`, `Disconnected`, `Unavailable`, `Incompatible`, `Error`, and `Reconnecting`. Retry reuses an unexpired in-memory grant conservatively. Server restart requires a new grant because the old host/session and opaque IDs are gone.

## Actual operations

| Operation | Shipped behavior | Stage 4 use |
| --- | --- | --- |
| `GetProtocolInfo` | Version, app identity, capabilities, read-only flag, transport, hard limits. | Required negotiation. |
| `GetAccessibleRoots` | Only configured source IDs authorized in the session. Paths appear only with separate path projection. | Connected root picker. |
| `GetChildren` | Stable bounded structural children with total/truncation and opaque offset continuation. | Progressive Structure graph. |
| `GetNeighborhood` | Bounded Structure plus optional retained Context. | Client implemented/validated; production UI deliberately does not request Context yet. |
| `Search` | Existing unified deterministic-first Search over the authorized session scope; known indexed IDs only; no AI assistance. | Connected structural Search presentation. |
| `GetRelated` | Existing bounded medium/strong Related Files evidence. | Deferred to Context mode. |
| `GetNodeDetails` | Bounded metadata, timestamps, summaries, topics/entities, safe media fields, relation summaries, indexed state. | Existing compact details panel. |

Protocol v1 capability bits include Structure, Search, Context, Related Files, Media/Content Intelligence, OCR, Transcripts, Topics, Entities, and Summaries. A bit means retained evidence can be projected; it does not promise that each node has the evidence. OmniBrille currently requires Structure and Search and displays only details actually returned.

Search has no selected-root parameter: it covers every source authorized in the current session. OmniBrille presents that honest coverage string and does not run a second search engine or crawl paths. Result nodes retain their opaque node/parent IDs so a resolvable hit can load its protocol neighborhood and become graph focus.

## Adapter and authority rules

`OmniSorSeConnectedProvider` implements the same `IExplorerProvider`, progressive, Search, details, and diagnostics interfaces as standalone acquisition. `ExplorerSession`, graph layout/renderer, list alternative, automation peers, details, Search presentation, and Back do not know transport details.

Standalone authority begins with a user-selected filesystem path. Connected authority begins with the grant and server-returned roots. The modes never merge:

- no connected failure falls back to direct `System.IO`;
- an authorized path is display text only;
- protocol IDs remain case-sensitive opaque targets;
- switching provider clears provider-specific state;
- visual preferences survive because they do not confer data access.

Protocol child pages stream into the existing 32-item interactive batches. OmniBrille retains at most 512 adapted children per focus so deterministic aggregation remains reversible and bounded; the rendered scene remains 48 nodes. If the server reports truncation or no complete total, the UI preserves that uncertainty rather than fabricating unseen counts.

## Cancellation, errors, and diagnostics

Protocol v1 has no separate cancel request. Client cancellation closes/cancels the active connection; the server's disconnect probe cooperatively cancels provider work. OmniBrille additionally applies monotonically increasing load/search/details generations, so a late response can never replace newer navigation. Disconnect leaves the last valid graph visible as stale context and updates the accessible connection status.

Stable server errors are unauthorized, expired, unsupported protocol, capability unavailable, node not found, out of scope, request too large, limit exceeded, malformed request, cancelled, temporarily unavailable, and internal failure. OmniBrille separately distinguishes handoff timeout, request timeout, malformed response, version mismatch, disconnect, and caller cancellation. Standalone remains available in every failure state.

Local diagnostics report provider/connection state, protocol/transport, last request duration and response node count, Search count, timeout/reconnect count, and stale-response rejection count. They never log tokens, payloads, queries, snippets, OCR/transcripts, file contents, or normal-level full paths. No telemetry is added.

## Stage 4 production-host validation

A disposable Windows harness loaded the actual `OpenSorSe.Application, Version=2.4.0.0` production `NamedPipeExplorerProtocolHost` in one process and OmniBrille's real coordinator/provider/session/MainWindow/headless renderer in a second process. It used one controlled authorized indexed source and a current-user-only one-time grant channel. Two independent passes proved fresh-session restart behavior.

Validated sequence: negotiate v1; load an authorized root; render its bounded graph; drill into an indexed folder; Back; run real host Search; focus the result; load real host details; terminate the host; retain the graph and transition to disconnected; start a new production host/session; repeat successfully. Representative second-pass samples on this machine were 761.4 ms for handoff/connect/root/UI readiness, 9.6 ms neighborhood, 44.6 ms Search, 11.9 ms details, and 1.3 ms headless graph render. These are engineering samples, not guarantees.

This proves the actual host/client contract and failure boundary. It does not claim that the released OmniSorSe desktop can launch OmniBrille; the missing launcher is listed below.

## Protocol v1 gap analysis

### Required for ordinary connected Structure use

- **Companion discovery/launch and grant handoff:** absent from the released OmniSorSe desktop. The reads themselves work, but there is no user path that creates a session and starts OmniBrille. This is the only principal product-integration blocker found.

### Required or important for future Context

- `ExplorerEdge` has no stable relationship ID, although OmniBrille's Context replacement/accessibility contract expects one for deterministic selection/update/removal.
- v1 responses are request/response snapshots/pages, not server-pushed incremental updates; future Context streaming would need additive revision/update semantics or bounded replacement requests.
- Search and Context scope are session-wide rather than selected-root-specific. A client must explain this or request narrower grants; it cannot add a root filter the server does not support.

### Nice to have

- A standard launcher-owned handoff schema/acknowledgement and immediate revoke-on-client-exit contract.
- Explicit server/session instance identity to make reconnect invalidation more self-describing (v1 already fails safely because IDs are session-bound).
- Coarse operation progress for expensive projections; exact progress must remain absent when the server cannot know it.

### Unsupported by design

- Remote/LAN/cloud transport, arbitrary filesystem enumeration, writes/moves/deletes, SQLite access, full OCR/transcript/content payloads, and client-created semantic relationships.

## Next boundary

Stage 5 should be coordinated across repositories: first add the smallest reviewed OmniSorSe companion launch/handoff action without changing Protocol v1 read DTOs, then implement OmniBrille Context mode over `GetNeighborhood(IncludeContext: true)` and `GetRelated`. Context must obey the existing 48-node/36-context-edge renderer budget, distinguish structural/context edges, and show server-authored reason/provenance on demand. If stable edge selection/replacement is required, define the minimal additive relationship-ID extension before exposing Context as product functionality.
