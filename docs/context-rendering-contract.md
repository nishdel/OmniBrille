# Future Context rendering contract

## Status

This is a renderer-facing readiness contract, not an OmniSorSe transport, data contract, or implemented Context mode. Synthetic relationships used by tests never appear in the production UI. OmniSorSe remains authoritative for every future contextual node, relationship, score, reason, and provenance record; OmniBrille must not infer semantic relationships or read OmniSorSe storage directly.

## Hard visible budgets

Stage 3 profiling keeps one conservative combined scene envelope:

| Item | Default limit | Rationale |
|---|---:|---|
| Combined visible nodes | 48 | Existing Structure readability and stable three-depth layout; includes focus, prior context, aggregates, and any future contextual nodes. |
| Structural edges | 47 | A normal bounded containment tree needs at most `nodes - 1`. |
| Contextual edges | 36 | 0.75 per node globally; enough for focus-local context without many-to-many saturation. |
| Combined edge slots | 84 | Conservative envelope; a full structural tree plus the contextual cap normally uses 83. |
| Contextual edges touching one node | 3 | Prevents hubs from obscuring focus and labels. A selected edge may temporarily displace a lower-priority edge, never grow the global cap. |

These are engineering defaults, not wire constants or universal hardware guarantees. Candidate 32/48/64 Structure scenes were profiled; 48 remains the readability default. A synthetic 48-node scene with 72 contextual edges was rejected because its density and cold-frame cost were disproportionate. The accepted 47-structural/36-context fixture produced 83 combined edges and a comfortable warmed local headless sample. Changing a limit requires new representative profiling, label-pressure review, keyboard/list review, and documentation.

Context does not add 48 nodes beside 48 Structure nodes. Structure and Context share the 48-node cap. When necessary, lower-priority distant structural or contextual items become an explicit aggregate/cluster while focus, selection, active search results, and the containment path remain visible.

## Relationship priority

Future relationship selection is deterministic:

1. selected relationship;
2. relationships touching current focus;
3. descending provider-supplied importance;
4. stable relationship ID.

`ContextRenderBudgetPolicy` applies the global and per-node caps after this ordering. Importance is presentation input, not a semantic calculation by OmniBrille. Duplicate/self relationships must be rejected or normalized before rendering. Stable node and relationship IDs are case-sensitive opaque strings.

Progressive disclosure is focus-local. A user may select a contextual aggregate/cluster or request a deeper neighborhood, but every replacement scene is independently bounded and reversible. Context clustering is distinct from the deterministic structural aggregate paging already used for large folders.

## Visual edge policy

The future renderer must distinguish five layers without relying on color alone:

- structural containment: primary solid electric-blue edge and junction;
- contextual relationship: thinner/lower-opacity line with a distinct dash or equivalent shape treatment, normally without broad glow;
- selected contextual relationship: stronger width/contrast plus details/provenance state;
- search emphasis: temporary node/path emphasis, not a new semantic edge type;
- decorative background: faint, non-interactive atmosphere with no node IDs, automation peers, or filesystem meaning.

Context edges must sit below focus glyphs and labels, must not permanently label every relationship, and must obey Reduced visual effects by removing nonessential glow/animation while keeping selection and line-style distinctions. Reduced motion makes relationship replacement immediate and understandable.

## Reason and provenance seam

A relationship may carry a provider-authored short reason and structured provenance references, for example same topic, shared entity, OCR evidence, transcript evidence, temporal proximity, Related Files score, or Media Intelligence evidence. OmniBrille does not generate these claims.

Reason/provenance appears only on demand through the compact details surface, a selected-edge flyout/tooltip, or the accessible alternative. The presentation must include relationship type, human-readable reason, confidence/importance when meaningful, source/provenance summary, and an unavailable/redacted state. Edge-wide permanent text is forbidden because it destroys density and screen-reader clarity.

## Replacement, streaming, and stale work

Every Context request needs a request ID, focus/scope identity, node/edge limits, cancellation token, and negotiated capability/version. A response is applied only when its request ID and focus/scope still match the authoritative session. Late pages from Folder A can never overwrite Folder B.

Preferred update semantics are a bounded initial snapshot followed by bounded incremental pages or explicitly versioned replacements. Each update declares completion, truncation, and failure state. Removing/replacing a relationship must use its stable relationship ID. Partial data is interactive once internally consistent; progress is coarse/indeterminate unless the provider knows an honest total.

Cancellation, unavailable provenance, permission changes, disconnected OmniSorSe, incompatible protocol versions, and malformed metadata are normal failure states. The prior valid scene remains usable where safe. Context data never expands the standalone access root or grants filesystem authority.

## Accessibility contract

Only the current bounded visible nodes and selectable relationships belong in automation/list projections. Context nodes require name, type, selected/focused state, openability, aggregate status, and action. A selected relationship requires source, target, relationship type, reason/provenance summary, and an accessible action to inspect it.

The synchronized list/tree remains a view of the one session, not a second provider/browser. Graph selection, list selection, relationship details, Back, search, progressive replacement, and cancellation must remain synchronized. Color, glow, animation, and spatial position may reinforce Context state but may not be the sole carrier of meaning.

## Performance and test gate

Before product Context mode is enabled, validate at least:

- 48 combined nodes with 47 structural and 36 contextual edges;
- focus/selected relationship emphasis;
- search plus Context label pressure;
- progressive replacement and stale-update rejection;
- Full and Reduced visual effects;
- 100/125/150/200% text scale;
- graph automation and accessible-list parity;
- GPU-backed Windows runtime plus Windows/Ubuntu build/tests.

If representative sustained render cost exceeds the local 16.7 ms engineering target, reduce contextual/decorative edge work before reducing structural correctness or accessibility. Node counts must never adapt continuously during navigation.
