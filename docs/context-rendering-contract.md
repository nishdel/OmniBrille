# Context rendering contract

## Status

This is the renderer-facing contract implemented through Stage 6 and regression-gated unchanged for the Stage 7 private preview. OmniSorSe remains authoritative for every contextual node, relationship, score, reason, and provenance record; OmniBrille does not infer semantic relationships or read OmniSorSe storage directly. Synthetic pressure fixtures remain test-only and never appear in the production UI.

Stage 5 consumes bounded contextual neighborhoods and Related Files through `GetNeighborhood(IncludeContext: true)` and focus-local `GetRelated`, with edge kind, strength, reason, evidence class, and provenance. The shipped `ExplorerEdge` does **not** contain a stable relationship ID. OmniBrille therefore derives an ephemeral session-local SHA-256 scene key from source, target, kind, reason, and provenance. That key only deduplicates and selects immutable snapshots; it is not persisted or treated as durable across refresh/session restart.

## Hard visible budgets

Stage 3 profiling keeps one conservative combined scene envelope:

| Item | Default limit | Rationale |
|---|---:|---|
| Combined visible nodes | 48 | Existing Structure readability and stable three-depth layout; includes focus, prior context, aggregates, and contextual nodes. |
| Structural edges | 47 | A normal bounded containment tree needs at most `nodes - 1`. |
| Contextual edges | 36 | 0.75 per node globally; enough for focus-local context without many-to-many saturation. |
| Combined edge slots | 84 | Conservative envelope; a full structural tree plus the contextual cap normally uses 83. |
| Contextual edges touching one node | 3 | Prevents hubs from obscuring focus and labels. A selected edge may temporarily displace a lower-priority edge, never grow the global cap. |

These are engineering defaults, not wire constants or universal hardware guarantees. Candidate 32/48/64 Structure scenes were profiled; 48 remains the readability default. A synthetic 48-node scene with 72 contextual edges was rejected because its density and cold-frame cost were disproportionate. The accepted 47-structural/36-context fixture produced 83 combined edges and a comfortable warmed local headless sample. Changing a limit requires new representative profiling, label-pressure review, keyboard/list review, and documentation.

Context does not add 48 nodes beside 48 Structure nodes. Structure and Context share the 48-node cap. Stage 6 retains an immutable provider snapshot separately from its filtered/rendered projection, and renders only endpoints of accepted relationships and accepted structural edges; omitted server nodes remain honestly truncated. Client semantic clustering is forbidden. Presentation filtering/summary may group only exact server fields and never creates a relationship.

## Relationship priority

Relationship selection is deterministic:

1. selected relationship;
2. relationships touching current focus;
3. descending provider-supplied ranking strength;
4. deterministic evidence before derived evidence at equal strength;
5. ephemeral session-local relationship key.

`ContextRenderBudgetPolicy` applies the global and per-node caps after this ordering. Importance is the protocol strength mapped to presentation input, not a semantic calculation by OmniBrille. Duplicate/self relationships are rejected or normalized before rendering. Stable node IDs are case-sensitive opaque strings. Stable relationship identity remains a Protocol v1 gap; incremental relationship updates are not enabled.

Stage 6 presentation filters may constrain relationship kind, minimum numeric ranking strength, and evidence class because those fields are explicitly supplied by Protocol v1. Filters apply only after authorization/acquisition, are reversible against the retained snapshot, reset on provider/session/root replacement, and never modify OmniSorSe Search, index, or intelligence state. Counts distinguish authorized, matching, and visible relationships so a budgeted result is not mistaken for the complete authority set.

Progressive disclosure is focus-local. Activating a related node requests a new independently bounded Context snapshot, and Back restores previous Context focuses before returning to Structure. OmniBrille never calls `GetRelated` for every visible node. An eight-entry session-scoped LRU deduplicates repeated focus reads and a one-request gate bounds Context acquisition; reconnect/new grant replaces the provider and its cache. Since v1 cannot push a disconnect notification, every cache hit first performs an authenticated bounded protocol-info probe, so the next Context action cannot silently reuse cached data after host death.

## Visual edge policy

The renderer distinguishes five layers without relying on color alone:

- structural containment: primary solid electric-blue edge and junction;
- contextual relationship: thinner/lower-opacity cyan dashed line, normally without broad glow;
- selected contextual relationship: stronger width/contrast plus details/provenance state;
- search emphasis: temporary node/path emphasis, not a new semantic edge type;
- decorative background: faint, non-interactive atmosphere with no node IDs, automation peers, or filesystem meaning.

Context edges must sit below focus glyphs and labels, must not permanently label every relationship, and must obey Reduced visual effects by removing nonessential glow/animation while keeping selection and line-style distinctions. Reduced motion makes relationship replacement immediate and understandable.

## Reason and provenance seam

A relationship may carry the v1 provider-authored kind, reason, evidence class, strength, and provenance string. OmniBrille does not generate, reinterpret, or enrich these claims.

Reason/provenance appears only on demand through the compact details surface and concise accessible node description. The presentation hierarchy is `Related because`, `Strength`, `Evidence`, and `Source`. Numeric protocol strength is described as a ranking band (Confirmed/Strong/Moderate/Limited), not statistical confidence. Known technical provenance may receive a friendly prefix while retaining the original string; unknown strings remain unchanged. A missing reason/provenance is reported honestly. Edge-wide permanent text is forbidden because it destroys density and screen-reader clarity.

## Replacement, streaming, and stale work

Every Context request has a monotonically increasing session generation, focus identity, node/edge limits, cancellation token, and negotiated capability/version. A response is applied only when its generation still matches the authoritative session. Late Context A can never overwrite Context B.

Protocol v1 supplies completed bounded snapshots/pages rather than pushed incremental updates. Stage 5 applies only an internally consistent bounded replacement whose request generation and focus still match. It shows an honest indeterminate/coarse loading state and does not fabricate partial relationships. Future incremental semantics would need completion, revision, and stable relationship identity.

Cancellation, unavailable provenance, permission changes, disconnected OmniSorSe, incompatible protocol versions, and malformed metadata are normal failure states. The prior valid scene remains usable where safe. Context data never expands the standalone access root or grants filesystem authority.

## Accessibility contract

Only the current bounded visible nodes and selectable relationships belong in automation/list projections. Context nodes require name, type, selected/focused state, openability, aggregate status, and action. A selected relationship requires source, target, relationship type, reason/provenance summary, and an accessible action to inspect it.

The synchronized list/tree remains a view of the one session, not a second provider/browser. Graph selection, list selection, relationship details, Back, search, progressive replacement, and cancellation must remain synchronized. Color, glow, animation, and spatial position may reinforce Context state but may not be the sole carrier of meaning.

## Performance and test gate

Stage 7 retains the Stage 6 validation coverage:

- 48 combined nodes with 47 structural and 36 contextual edges;
- focus/selected relationship emphasis;
- search plus Context label pressure;
- replacement, cancellation, stale-update rejection, and new-session invalidation;
- Full and Reduced visual effects;
- 100/125/150/200% text scale;
- graph automation and accessible-list parity;
- filter automation, keyboard opening/reset, and shared graph/list state;
- GPU-backed Windows runtime plus Windows/Ubuntu build/tests.

If representative sustained render cost exceeds the local 16.7 ms engineering target, contextual/decorative effects must be reduced before structural correctness or accessibility. Node counts never adapt continuously during navigation. Normal CI uses fake protocol fixtures; the real installed desktop handoff is a documented Windows integration gate.

## Current limitations

- Protocol v1 relationship identities are ephemeral; no durable edge bookmark, incremental edge removal, or cross-refresh edge selection is claimed.
- Search and Context authority are session-wide, not selected-root-specific. The UI labels connected Search as the authorized indexed scope.
- Protocol v1 supplies completed response snapshots rather than push progress.
- Relationship detail interaction is node-centric; keyboard/mouse users select a related node and inspect its focus relationship.
- Context is unavailable in standalone mode and no filename/content heuristic is substituted.
- Filters do not request additional results. A relationship omitted by the authoritative bounded snapshot cannot be revealed client-side.
