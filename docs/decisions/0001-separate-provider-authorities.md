# ADR 0001: Separate standalone and connected provider authorities

Status: Accepted

Reconstructed from repository evidence.

## Context

OmniBrille must work without OmniSorSe while also presenting authorized indexed Structure, Search, details, and server-authored Context when launched by OmniSorSe. Filesystem paths and session-bound protocol IDs have different security, identity, lifetime, and failure semantics. History also showed that letting provider-specific exceptions or path assumptions leak into the session makes the boundary fragile.

## Decision

- Keep OmniBrille an independently packaged, standalone-capable application.
- Put visual-agnostic application contracts in `OmniBrille.Core` and acquisition adapters in `OmniBrille.Infrastructure`.
- Standalone authority begins only at a user-selected filesystem root and is enforced with native path semantics.
- Connected authority begins only with a short-lived OmniSorSe grant and issued opaque IDs. Projected paths are display text, not access tokens.
- Connected failure never falls back to `System.IO`, and OmniBrille never reads OmniSorSe storage or imports its application/indexing implementation.
- Provider replacement makes `ExplorerSession` clear provider-specific graph, histories, selection, Search, details, and filters; its generations invalidate outstanding/deferred work. The discarded connected provider separately owns and loses its Context LRU.
- Mirror only the dependency-free Explorer Protocol v1 wire contract locally until a stable shared package exists; adapt wire DTOs into the application-local model.

## Reasoning

This keeps Standalone useful and private, prevents a connected authorization gap from becoming filesystem authority, avoids runtime/package coupling to OmniSorSe, and lets one session/renderer present both sources without knowing transport details.

## Consequences

- Similar Core and wire types must remain explicitly adapted; they are not interchangeable.
- Connected Search follows the authorized session scope supplied by OmniSorSe, not a client-invented root filter.
- Opaque IDs are contractually case-sensitive and session-bound; reconnect/new grant invalidates prior state. Most code uses ordinal `ExplorerIdentity`, but Connected `NavigationState` equality currently has a documented Windows case-folding defect and must not be treated as fully enforced.
- Cross-repository compatibility still requires real-host validation when the external protocol/launcher changes.
- Optional capabilities must be represented truthfully. Current code has a known follow-up: missing Context/Related capability is detected only when requested rather than disabling the mode in advance.

## Rejected alternatives

- Reading OmniSorSe SQLite or referencing its application projects.
- Treating projected connected paths as direct filesystem targets.
- Falling back to a standalone crawl when connected data is missing.
- Background discovery/listener or persisting bearer grants.
- A single merged provider authority whose state survives provider replacement.

## Evidence

- Initial boundary and Core/provider split: commits `def16ea`, `775789a`.
- Connected implementation and hardening: `7338245`, `f6f3afd`, `9e877d0`, `e556f14`.
- Secure handoff and Context: `6dd9ae5`, `fb6d4f0`.
- Current implementation: [`ExplorerModels.cs`](../../src/OmniBrille.Core/ExplorerModels.cs), [`ExplorerSession.cs`](../../src/OmniBrille.Desktop/Presentation/ExplorerSession.cs), [`OmniSorSeConnectedProvider.cs`](../../src/OmniBrille.Infrastructure/OmniSorSe/OmniSorSeConnectedProvider.cs), and [`explorer-protocol.md`](../explorer-protocol.md).
- Regression coverage: `NavigationStateTests`, `ExplorerSessionTests`, `NamedPipeExplorerProtocolClientTests`, `OmniSorSeConnectedProviderTests`, and `OmniSorSeConnectionCoordinatorTests`.
