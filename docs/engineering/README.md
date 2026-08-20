# Engineering start here

This is the entry point for human and Codex engineering work. It routes to authoritative knowledge; it is not another architecture manual.

## Authority map

Repository evidence wins in this order: current source and observable behavior, tests, Git history, CI/build configuration, current documentation, retained historical reports, then inference.

| Need | Authority | Classification |
| --- | --- | --- |
| Product boundary and implemented capability overview | [`README.md`](../../README.md) | Authoritative current overview |
| Subsystems, ownership, flows, state, and invariants | [`docs/architecture.md`](../architecture.md) | Authoritative current architecture |
| Terms whose meanings must not blur | [`docs/glossary.md`](../glossary.md) | Authoritative terminology |
| Context/Hybrid presentation limits | [`docs/context-rendering-contract.md`](../context-rendering-contract.md) | Authoritative subsystem contract |
| Explorer Protocol client and connected boundary | [`docs/explorer-protocol.md`](../explorer-protocol.md) | Current client contract plus explicitly historical external validation |
| Voice input boundary | [`docs/voice.md`](../voice.md) | Authoritative subsystem guide |
| Packaging, compatibility, privacy, and release gates | [`docs/PACKAGING.md`](../PACKAGING.md), [`COMPATIBILITY.md`](../../COMPATIBILITY.md), [`RELEASE_CHECKLIST.md`](../../RELEASE_CHECKLIST.md) | Current guidance and tested combinations |
| DNG-free Windows SkiaSharp native asset | [`docs/native-skia.md`](../native-skia.md) | Authoritative native-build provenance and upgrade procedure |
| Architectural reasons | [`docs/decisions/README.md`](../decisions/README.md) | Decision records; not alternate current architecture |
| Risk, specialists, and validation | [`risk-and-validation.md`](risk-and-validation.md) | Engineering guidance |
| Retrospectives, lesson promotion, freshness, and reports | [`learning-and-reports.md`](learning-and-reports.md) | Engineering guidance |
| Failure chains and earned lessons | [`history-and-lessons.md`](history-and-lessons.md) | Historical evidence; not active architecture |
| Milestones and unscheduled work | [`CHANGELOG.md`](../../CHANGELOG.md), [`ROADMAP.md`](../../ROADMAP.md) | Historical/planned |
| Significant run claims | [`docs/runs/README.md`](../runs/README.md) | Historical snapshots; current docs remain authoritative |

If documentation conflicts with implementation, do not silently choose one. Verify the intended contract, correct current documentation or code as appropriate, and preserve uncertainty when the repository cannot resolve it.

## Load context by task

Small tasks should stay small. Use `AGENTS.md`, the authority map above, the target file, and only the applicable task/risk rows. A clear Low-risk documentation correction can stop there after the freshness checklist and documentation check; it need not load full architecture/history/process documents.

| Task | Read | Usually safe to ignore |
| --- | --- | --- |
| Typo, template, or isolated documentation fix | Target document; the eight-point [documentation freshness checklist](learning-and-reports.md#documentation-freshness) | Git history, subsystem internals, old run reports |
| Isolated Core contract or deterministic leaf helper | Relevant [architecture component/state section](../architecture.md#components); exact source and matching test | Packaging, voice, renderer, and protocol history |
| Core builder/layout/budget or renderer-facing policy | Relevant architecture layout/bounds sections; exact Core source/tests; [Context contract](../context-rendering-contract.md) when relationships or budgets are involved | Packaging and voice history; this is High renderer/UX/performance risk, not a Normal Core change |
| Session, Back, modes, Search, selection, or loading | [State ownership](../architecture.md#state-ownership) and [stale-work flow](../architecture.md#progressive-loading-and-stale-work-safety); `ExplorerSession`; session/Context/Hybrid tests; relevant [earned lessons](history-and-lessons.md) | Release reports unrelated to the state transition |
| Renderer, LOD, labels, density, graph input | Architecture layout/accessibility/performance sections; Context contract; renderer/layout/presentation source; headless fixtures; [performance failure case](history-and-lessons.md#2-search-emphasis-regressed-renderer-allocation) | Protocol transport internals unless data semantics change |
| UI, graph/list parity, keyboard, accessibility | Architecture state/accessibility sections; glossary; MainWindow/automation source; headless tests; [accessibility failure case](history-and-lessons.md#4-accessibility-state-was-correct-while-the-projectionaction-was-wrong) | Packaging internals unless installer UX changes |
| Standalone filesystem or persistence | Current architecture; provider/store source; focused tests; platform/root-boundary lessons | Connected host history |
| Protocol, handoff, connected provider/cache | Current architecture; protocol guide; ADR 0001; client/provider/coordinator tests; compatibility matrix | Renderer history unless visible graph semantics change |
| Voice | Current architecture; voice guide; voice source/tests; privacy/release guidance | Historical graph performance samples |
| Packaging/release | Packaging, compatibility, privacy, release checklist, build scripts and release tests | Renderer implementation unless packaged output changed |
| SkiaSharp/native renderer dependency | [Native Skia provenance](../native-skia.md); renderer/performance sections of current architecture; build recipe and release tests | Protocol, voice, and historical run reports |

Do not load every retained run report. Use one only when investigating what a past run claimed, a specific regression, or a validation environment.

## Repository map

- `src/OmniBrille.Core`: visual-agnostic contracts, bounded builders/layouts/policies, navigation, preferences contract, and voice coordination.
- `src/OmniBrille.Infrastructure`: standalone acquisition/persistence, connected protocol adapters, and local voice adapters.
- `src/OmniBrille.Desktop`: Avalonia composition, authoritative `ExplorerSession`, shell, renderer, input, accessibility projection, and diagnostics.
- `src/OmniSorSe.ExplorerProtocol`: dependency-free mirrored v1 wire DTOs/enums; not an OmniSorSe application dependency.
- `tests/OmniBrille.Tests`: domain, provider, protocol, state, persistence, privacy, and release tests.
- `tests/OmniBrille.HeadlessTests`: non-pixel shell, input, automation, accessibility-projection, and renderer-pressure tests.

`ExplorerSession` lives in Desktop and is the application/session authority even though there is no separately named Application project. Future work must not assume all orchestration lives in Core.

## Normal change flow

1. Record baseline and pre-existing work.
2. State the user-visible and architectural contract in plain language.
3. Classify risk and plan the validation that can disprove the implementation.
4. Load targeted context and route only justified specialists.
5. Implement with one owner per overlapping area.
6. Run focused checks, then the proportional repository validation.
7. Perform independent adversarial review for high-risk work.
8. Check documentation/diagram/decision freshness.
9. For a substantial run, record the retrospective and owner report.

A specialist handoff should contain only: conclusion, supporting paths/tests/commits, risks, unresolved uncertainty, and recommended next action.
