# OmniExplorer roadmap

Status language is intentionally conservative: only checked items are present in the current vertical slice.

## Stage 1 — Architecture (initial slice complete)

- [x] Independent standalone shell and repository structure.
- [x] Abstract explorer/search data model and bounded graph model.
- [x] Custom renderer with centralized Light/Dark theme foundations.
- [x] Future protocol boundary documented without premature IPC.

## Stage 2 — Structural Explorer (working vertical slice)

- [x] Explicit selected-folder access root.
- [x] Immediate hierarchy graph with folders and files.
- [x] Bounded neighborhoods, aggregation, stable focus navigation, Back, and transitions.
- [ ] Multi-depth contextual neighborhoods and richer reversible aggregate refinement.
- [ ] Persisted opt-in recent roots and session restoration.

## Stage 3 — Visual System

- [x] Coherent blue Dark/Light foundations and representative loading data rain.
- [ ] Mature depth/fog, background network, transition choreography, and visual polish.
- [ ] Profiled effects tiers and reduced-effects settings.

## Stage 4 — Search

- [x] Bounded standalone filename/folder/path search, graph highlighting, and focus.
- [ ] Filter chips, richer structural metadata, ranking, and large-root progress reporting.
- [ ] Complete keyboard/list navigation alternative.

## Stage 5 — OmniSorSe Integration

- [ ] Versioned local protocol and capability negotiation.
- [ ] Accessible roots/current scope, neighborhoods, search, and node details.
- [ ] Connected provider adapter with clear offline/failure states.

## Stage 6 — Context Mode

- [ ] OmniSorSe-supplied Related Files, topics/entities, and cross-media relationships.
- [ ] Explanations and provenance. No fabricated semantic edges.

## Stage 7 — Voice

- [ ] Push-to-talk local speech recognition.
- [ ] Deterministic navigation commands and OmniSorSe-backed search queries.
- [ ] No always-listening microphone.

## Stage 8 — Performance + Accessibility

- [ ] Renderer/frame-time and text-cost profiling on representative hardware.
- [ ] Reduced effects/motion, per-node automation peers, full keyboard/list alternative.
- [ ] Cross-platform hardening and packaging validation.
