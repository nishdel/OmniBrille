# OmniBrille engineering router

This file applies to the whole repository. Keep it compact: architecture belongs in `docs/architecture.md`, not here.

## Start every task

1. Establish branch, HEAD, tracking, worktree, and active Git-operation state. Preserve pre-existing changes.
2. Use [`docs/engineering/README.md`](docs/engineering/README.md) as the authority map and load only the task-specific rows/sections it routes to.
3. Classify the change with the applicable row in [`docs/engineering/risk-and-validation.md`](docs/engineering/risk-and-validation.md) before choosing specialists or validation. An obvious Low-risk documentation-only edit need not load the whole guide.
4. Prefer current source and tests over prose. When they disagree, investigate and correct the false authority.

## Global boundaries

- OmniBrille is an independent, standalone-capable application. Do not import OmniSorSe architecture or process except at the verified Explorer Protocol boundary.
- Standalone filesystem paths and connected opaque node IDs are different authorities. Never use a projected connected path as filesystem authority or add direct-filesystem fallback to connected mode.
- OmniBrille presents server-authored Context; it does not infer relationships, duplicate OmniSorSe intelligence, or persist connected identities.
- Preserve the established bounded-scene, stale-result, accessibility-projection, privacy, and provider-replacement invariants. Their authoritative locations and tests are identified by the risk guide.
- Do not claim visual, accessibility, performance, hardware, protocol-host, or cross-platform validation that was not actually performed.

## Working method

- Follow: discover → verify → design → implement → validate.
- Keep one implementation owner for overlapping files. Exploratory specialists return conclusions, evidence, risks, unresolved questions, and a recommended next action.
- Use the minimum routing justified by risk. AX is not a standing role; invoke it only if OmniBrille itself gains AI-assisted behavior.
- Prefer executable memory (tests/checks) to prose when a stable failure contract can be enforced reliably.
- For every meaningful change, check current architecture, terminology, Mermaid diagrams, decisions, validation guidance, and historical/report accuracy. Update only affected artifacts.
- A substantial run follows [`docs/engineering/learning-and-reports.md`](docs/engineering/learning-and-reports.md), including an owner report and retrospective with independently reviewed lesson promotion.

## Completion test

Ask: “Could another competent developer understand this change without reading the Codex conversation that produced it?” If not, put the missing knowledge in code, a test, current architecture, a decision record, or a maintained diagram.
