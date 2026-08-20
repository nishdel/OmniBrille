# ADR 0002: Bounded deterministic graph scenes

Status: Accepted

Reconstructed from repository evidence.

## Context

OmniBrille needs readable focus-centered navigation, stable input/automation targets, responsive rendering, and explainable Context without recursive preload or continuous physics. Historical profiling found that label shaping and drawing-resource allocation—not primitive glyph geometry—were the expensive renderer path. A 48-node/72-Context-edge candidate was rejected as too dense and costly.

## Decision

- Build provider-independent immutable snapshots into one bounded visible scene.
- Keep the default scene at 48 combined nodes. Context/Hybrid use the limits owned by `ContextRenderBudgetPolicy`; [`context-rendering-contract.md`](../context-rendering-contract.md) is authoritative for current numeric limits.
- Use deterministic Structure rings, Context rings, and Hybrid planes with stable IDs/positions rather than continuous force simulation.
- Draw with an Avalonia custom `Control`/`DrawingContext`; keep layout and presentation policy in Core and input/transient drawing state in Desktop.
- Bound text/brush/pen caches and retain local phase/allocation diagnostics.
- Preserve structural correctness, accessibility, and user-controlled reduced motion/effects before adding density or decoration.
- Context edges and nodes must come from the provider. Client filtering/bounding may omit them but cannot infer replacements.

## Reasoning

Determinism makes navigation, Back, tests, accessibility projection, and performance reasoning tractable. Hard bounds keep layout, labels, hit testing, automation, and rendering predictable across small and very large source directories. The custom renderer gives control without adopting a browser/runtime or owning a lower-level text/input/accessibility stack.

## Consequences

- Large Structure sources use reversible aggregates; Context filters cannot reveal relationships omitted by the authoritative bounded snapshot.
- Changing a scene/edge/label/cache limit requires representative density, label, keyboard/list, and performance review—not only unit tests.
- Warm headless samples are engineering evidence, not GPU/runtime guarantees; an absolute CI frame-time gate is intentionally absent.
- Relationship interaction remains node-centric because Protocol v1 lacks durable relationship identity.

## Rejected alternatives

- Unbounded or recursively preloaded scenes.
- Continuous force-directed simulation.
- Raising density because a synthetic frame happens to render quickly.
- WebView/WebGL or direct Skia/Win2D without measured need.
- Client-created semantic clustering or inferred Context.

## Evidence

- Progressive bounded Structure and aggregate paging: commit `4904f85`.
- Visual policy and renderer: `ee7a94e`.
- Search-allocation correction, cache bounds, Context pressure review, and accessibility/platform hardening: `17f3004`, `0bce6b9`.
- Context/Hybrid implementation: `6dd9ae5`, `9731dc4`, `7753741`, `2e7466b`.
- Current implementation: [`GraphNeighborhoodBuilder.cs`](../../src/OmniBrille.Core/GraphNeighborhoodBuilder.cs), [`ContextRenderBudgetPolicy.cs`](../../src/OmniBrille.Core/ContextRenderBudgetPolicy.cs), layout/presentation classes in [`OmniBrille.Core`](../../src/OmniBrille.Core), and [`GraphSceneControl.cs`](../../src/OmniBrille.Desktop/Rendering/GraphSceneControl.cs).
