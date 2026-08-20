# OmniSorSe Explorer Protocol v1 integration

> **Authority:** current OmniBrille client, adapter, and failure boundary. The Stage 4–6 host-validation sections are historical evidence against the named external commit, not a guarantee for untested OmniSorSe versions.

## Status and authority

Current OmniBrille consumes the same real read-only protocol introduced in OmniSorSe v2.4.0 and companion workflow committed in the v2.5 release candidate. The external source historically inspected and run was commit `59be07c6cebff12072cbf18701fb16cb11801287`, especially `src/OpenSorSe.Application/Explorer/ExplorerCompanionLaunch.cs`, `ExplorerReadService.cs`, and `docs/OMNIBRILLE_COMPANION_HANDOFF_v2.5.md`. Protocol major was 1 and schema 5 in that evidence; OmniSorSe source was not modified by those runs.

The 2026-08-20 engineering audit also compared the mirrored DTO/enums and launcher/handoff history with the available OmniSorSe checkout at `cc6c331c984a6298f74fbc8ed7fb8e0681974ff2`: the inspected wire DTOs and handoff shape were stable, while host relationship projection had evolved (including pair aggregation, evidence classification, and explicit user-authority provenance). This is source-level boundary evidence, not a new installed two-process validation or a claim that host results are unchanged.

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

OmniBrille uses a three-second connection timeout and an 18-second client request deadline so the server's stable 15-second result normally wins. It validates the grant and the advertised safety limits it consumes (request/response size, nodes, edges, Search/Related results, query presence, and timeout), then independently validates response identity, enum, ID, collection, relationship reference, reason, metadata, and details bounds before adapting them. Several advertised but currently unconsumed limits are not validated; that gap is tracked rather than described as comprehensive negotiation.

## Authorization, discovery, and lifecycle

The server is dormant until the explicit OmniSorSe desktop action creates a session over a bounded set of enabled indexed source IDs. The resulting grant contains an unpredictable endpoint, random session ID, 256-bit bearer secret, absolute expiry, and protocol version. The v2.5 companion session lifetime is 15 minutes. Tokens are retained only in the live connection coordinator and never persisted or logged.

Node IDs are opaque, case-sensitive, HMAC-derived, and session-bound. They are never reconstructed from paths or treated as database keys. A new grant/server session invalidates old IDs, so reconnect with a fresh grant resets scene, selection, Back history, Search, details, and aggregates.

The v2.5 RC supplies the normal companion workflow without introducing a listener. On explicit user action its bounded locator checks, in order, the configured absolute executable, `OMNISORSE_OMNIBRILLE_PATH`, the OmniSorSe application directory, conventional per-user/machine install directories, and `PATH`. It accepts only reviewed `OmniBrille.exe` or `OmniBrille.Desktop.exe` candidates and performs no recursive search, download, probe, or startup scan.

OmniSorSe creates a current-user-only one-connection pipe named `omnibrille-handoff-` plus 32 lowercase hexadecimal characters, starts OmniBrille with only `--omnisorse-handoff <pipe-name>`, and sends a strict length-prefixed JSON `ExplorerSessionGrant` capped at 4 KiB. OmniBrille requires that exact pipe-name shape, connects within 15 seconds, validates the grant, and immediately performs authenticated `GetProtocolInfo`. OmniSorSe observes that first authenticated compatible request as acknowledgement. A bearer token never appears in the argument list, environment, file, preference, UI, or normal diagnostic.

The grant is one-use because the bootstrap server accepts one connection and closes. It is replay-resistant through the unpredictable pipe, current-user isolation, one-connection lifetime, short-lived scoped session, random bearer secret, and revocation. Handoff timeout, early companion exit, incompatible/unauthenticated acknowledgement, launch failure, cancellation, or observed child-process exit revokes the created session. Repeated desktop actions create independent processes and grants; Stage 5 deliberately does not forward a security-sensitive grant into an existing process.

Connection states are `Standalone`, `Discovering`, `Connecting`, `Connected`, `Disconnected`, `Unavailable`, `Incompatible`, `Error`, and `Reconnecting`. Retry reuses an unexpired in-memory grant conservatively. Server restart requires a new grant because the old host/session and opaque IDs are gone.

## Actual operations

| Operation | Shipped behavior | Stage 4 use |
| --- | --- | --- |
| `GetProtocolInfo` | Version, app identity, capabilities, read-only flag, transport, hard limits. | Required negotiation. |
| `GetAccessibleRoots` | Only configured source IDs authorized in the session. Paths appear only with separate path projection. | Connected root picker. |
| `GetChildren` | Stable bounded structural children with total/truncation and opaque offset continuation. | Progressive Structure graph. |
| `GetNeighborhood` | Bounded Structure plus optional retained Context. For a file focus, `IncludeContext: true` uses retained Related evidence; folder focuses remain structural. | Connected Structure navigation and bounded Context acquisition. |
| `Search` | Existing unified deterministic-first Search over the authorized session scope; known indexed IDs only; no AI assistance. | Connected structural Search presentation. |
| `GetRelated` | Existing bounded medium/strong Related Files evidence with kind, strength, reason, evidence class, and provenance. | One focus-local request per uncached file Context focus. |
| `GetNodeDetails` | Bounded metadata, timestamps, summaries, topics/entities, safe media fields, relation summaries, indexed state. | Existing compact details panel. |

Protocol v1 capability bits include Structure, Search, Context, Related Files, Media/Content Intelligence, OCR, Transcripts, Topics, Entities, and Summaries. A bit means retained evidence can be projected; it does not promise that each node has the evidence. OmniBrille currently requires Structure and Search and displays only details actually returned.

Search has no selected-root parameter: it covers every source authorized in the current session. OmniBrille presents that honest coverage string and does not run a second search engine or crawl paths. Result nodes retain their opaque node/parent IDs so a resolvable hit can load its protocol neighborhood and become graph focus.

## Adapter and authority rules

`OmniSorSeConnectedProvider` implements the same `IExplorerProvider`, progressive, Search, details, diagnostics, and provider-independent `IExplorerContextProvider` interfaces as standalone acquisition. `ExplorerSession`, graph layout/renderer, list alternative, automation peers, details, Search presentation, and Back do not know transport details.

Standalone authority begins with a user-selected filesystem path. Connected authority begins with the grant and server-returned roots. The modes never merge:

- no connected failure falls back to direct `System.IO`;
- an authorized path is display text only;
- protocol IDs remain case-sensitive opaque targets;
- switching provider clears provider-specific state;
- visual preferences survive because they do not confer data access.

Protocol child pages stream into the existing 32-item interactive batches. OmniBrille retains at most 512 adapted children per focus so deterministic aggregation remains reversible and bounded; the rendered scene remains 48 nodes. If the server reports truncation or no complete total, the UI preserves that uncertainty rather than fabricating unseen counts.

For an uncached Context focus, the provider requests one depth-1 neighborhood with `IncludeContext: true`, at most 48 nodes, and at most 84 combined edges. If the focus is an issued file, it also requests at most 36 `GetRelated` results. It validates and merges duplicate edges, adapts only server-supplied reason/evidence/provenance, then applies the existing global/per-node renderer policy. One session-scoped acquisition gate and an eight-entry LRU prevent fan-out. A new grant constructs a new provider, invalidating all cached opaque IDs and Context snapshots.

Because v1 has no relationship ID, OmniBrille computes an ephemeral scene key from source ID, target ID, kind, reason, and provenance. It is useful only for deduplication and deterministic budget ordering inside the current immutable session snapshot. It is never persisted or presented as a durable protocol identity.

## Cancellation, errors, and diagnostics

Protocol v1 has no separate cancel request. Client cancellation closes/cancels the active connection; the server's disconnect probe cooperatively cancels provider work. OmniBrille additionally applies monotonically increasing load/search/details generations, so a late response can never replace newer navigation. Disconnect leaves the last valid graph visible as stale context and updates the accessible connection status.

Stable server errors are unauthorized, expired, unsupported protocol, capability unavailable, node not found, out of scope, request too large, limit exceeded, malformed request, cancelled, temporarily unavailable, and internal failure. OmniBrille separately distinguishes handoff timeout, request timeout, malformed response, version mismatch, disconnect, and caller cancellation. Standalone remains available in every failure state.

Local diagnostics report provider/connection state, protocol/transport, last request duration and response node count, Search count, timeout/reconnect count, and stale-response rejection count. They never log tokens, payloads, queries, snippets, OCR/transcripts, file contents, or normal-level full paths. No telemetry is added.

## Stage 4 production-host validation

A disposable Windows harness loaded the actual `OpenSorSe.Application, Version=2.4.0.0` production `NamedPipeExplorerProtocolHost` in one process and OmniBrille's real coordinator/provider/session/MainWindow/headless renderer in a second process. It used one controlled authorized indexed source and a current-user-only one-time grant channel. Two independent passes proved fresh-session restart behavior.

Validated sequence: negotiate v1; load an authorized root; render its bounded graph; drill into an indexed folder; Back; run real host Search; focus the result; load real host details; terminate the host; retain the graph and transition to disconnected; start a new production host/session; repeat successfully. Representative second-pass samples on this machine were 761.4 ms for handoff/connect/root/UI readiness, 9.6 ms neighborhood, 44.6 ms Search, 11.9 ms details, and 1.3 ms headless graph render. These are engineering samples, not guarantees.

This proved the original host/client contract and failure boundary. The later committed v2.5 RC supplies the launcher described above; the Stage 4 result remains useful historical latency evidence.

## Stage 5 installed-workflow validation

Stage 5 passed the Windows manual integration gate with the committed v2.5 RC desktop implementation—not a fabricated discovery path. A controlled indexed source was added through OmniSorSe's normal UI; its `Open in OmniBrille` action located the reviewed Stage 5 executable, transferred and acknowledged the one-time grant, and produced `Connected · OmniSorSe` without developer harness intervention. The connected application loaded authorized roots, rendered Structure, drilled and returned with Back, performed real Search, loaded real details, switched to Context, displayed a retained server-authored relationship and provenance, refocused, returned with Back, and restored Structure.

Terminating OmniSorSe while Context was visible left OmniBrille alive with the last two-node scene visibly retained. Invoking a cached Context relation performed the authenticated liveness probe, failed closed in about 3.1 seconds, preserved the focus, and changed the accessible provider state to disconnected. Restarting the unchanged RC and using the desktop action again created a fresh independently connected process/grant; no old opaque identity was reused. Representative local UI observations were about 2.9 seconds from desktop action to connected window readiness, 215 ms for connected Structure drill-down, 244 ms for the fresh two-node Context switch, and 142 ms for Context refocus. These include UI scheduling and are engineering samples, not guarantees. Automated CI remains isolated through strict pipe/fake-client fixtures.

## Stage 6 packaged-workflow validation

Stage 6 places the self-contained executable at `%LOCALAPPDATA%\Programs\OmniBrille\OmniBrille.exe`, one of the unchanged RC launcher's conventional candidates. With `OMNISORSE_OMNIBRILLE_PATH` absent at process, user, and machine scope, the exact committed RC desktop's normal `Open in OmniBrille` action discovered the installed executable, transferred the one-time grant, acknowledged it through the authenticated protocol request, and reached `Connected · OmniSorSe`. No discovery listener, registry protocol registration, token file, or alternate handoff was added.

A controlled 12-file source was indexed through ordinary OmniSorSe UI. The installed application loaded its authorized root, ran real Search, and produced a real three-node/two-relationship Context scene for deterministic duplicate-content evidence. It displayed the server reason, ranking strength, evidence class, and provenance; local Topic filtering yielded an honest no-match state, reset restored the immutable snapshot, refocus loaded the related node, and Back returned to the original focus. A separate Knowledge Graph projection attempt failed safely inside the unchanged RC at manifest capture, so no broader semantic scene is claimed. Existing deterministic Related Files evidence was sufficient to validate Protocol v1 and OmniBrille's multi-node Context presentation.

The 15-minute grant remains server-owned and is never extended by OmniBrille. `SessionExpired` clears the in-memory grant/client/root state, preserves the last graph only as stale orientation, and requires a new normal OmniSorSe launch for a fresh grant. Independent coordinators/grants are covered without token sharing; Stage 6 continues the deliberate multi-process design.

## Protocol v1 gap analysis

### Compatibility behavior

OmniBrille validates protocol major 1, the read-only `OmniSorSe` server identity, the advertised safety bounds it consumes, and required Structure/Search capabilities. An absent OmniSorSe instance simply leaves Standalone active. All invalid cases are rejected before provider creation, but their current UI classification differs: a mismatched major is `Incompatible`; malformed consumed limits or missing required capabilities fail closed as a connection failure. Detailed failure categories remain local diagnostics.

Current strict JSON rejects unknown members and numeric enums. Minor/additive compatibility is therefore not implied merely by keeping protocol major 1; new fields require an explicit client decision and compatibility tests. A missing optional Context/Related capability is rejected when Context is requested, but the current shell does not disable Context/Hybrid before that attempt and reports the provider failure as a connection failure. This is a known product follow-up, not the previously documented graceful-disable behavior. Standalone remains available and no local relationship substitute is created. See [`COMPATIBILITY.md`](../COMPATIBILITY.md).

### Required for ordinary connected Structure use

- No blocker remains in the committed v2.5 RC contract. Packaging must place the reviewed executable in a configured/adjacent/conventional/PATH location; the RC is not described here as a released product.

### Required or important for future Context evolution

- `ExplorerEdge` has no stable relationship ID. Stage 5 supports node-centric relationship inspection and immutable bounded replacement with an ephemeral key, but not durable edge bookmarks or incremental edge update/removal.
- v1 responses are request/response snapshots/pages, not server-pushed incremental updates; future Context streaming would need additive revision/update semantics or bounded replacement requests.
- Search and Context scope are session-wide rather than selected-root-specific. A client must explain this or request narrower grants; it cannot add a root filter the server does not support.

### Nice to have

- Explicit server/session instance identity to make reconnect invalidation more self-describing (v1 already fails safely because IDs are session-bound).
- Coarse operation progress for expensive projections; exact progress must remain absent when the server cannot know it.

### Unsupported by design

- Remote/LAN/cloud transport, arbitrary filesystem enumeration, writes/moves/deletes, SQLite access, full OCR/transcript/content payloads, and client-created semantic relationships.

## Next boundary

Stage 9 voice introduces no wire operation and found no compatibility reason for protocol churn. Local transcription is classified into deterministic UI commands or the existing `Search` request. A connected spoken query therefore uses the same capability negotiation, scope, cancellation, request identity, and graph presentation as typed Search; `Show what is related to this` only switches the existing focus into Context and still obtains relationships through real `GetNeighborhood`/`GetRelated`. Relationship IDs, immutable snapshots, session-wide Search/Context scope, presentation-string provenance, lack of pushed disconnect, and lack of exact progress remain known limitations, but all fail safely or have honest bounded UX. Protocol v1 should remain unchanged until durable edge actions, incremental updates, selected-root server filtering, or structured provenance becomes a demonstrated requirement.

Stage 10 Hybrid likewise adds no operation, DTO, capability, or identity promise. `ExplorerSession` requests the same bounded Context snapshot already supplied by `GetNeighborhood(IncludeContext: true)` and focus-local `GetRelated`; the client then composes authorized containment and relationship edges into one deduplicated scene. Context-to-Hybrid switching at the same focus reuses the retained bounded Structure snapshot when it contains the focus. A related-node focus outside that snapshot uses at most one ordinary bounded parent/directory read, because real file-centered Context responses do not always carry parent containment edges. No blocker was found: the existing operations, node/edge limits, opaque session-bound node IDs, cancellation, and stale-response generation are sufficient for the first Hybrid experience. Protocol v1 remains frozen.
