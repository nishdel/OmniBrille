# Risk, specialist routing, and validation

This guide turns OmniBrille's actual architecture and regression history into proportional review. Risk follows the behavior being changed, not the apparent size of the diff.

## Risk map

| Risk | Change areas | Evidence for the classification |
| --- | --- | --- |
| **High — session/state/concurrency** | `ExplorerSession`, provider replacement, request generations, cancellation, Back/mode history, snapshot/cache replacement, Search/details races | Early loads could overwrite newer navigation; later Context/Hybrid work required explicit generation, rollback, and cache-liveness corrections. |
| **High — protocol/authority/security** | Mirrored contracts, strict serialization/validation, named pipes, grants, coordinator, connected provider, opaque IDs, Standalone/Connected boundary | Initial integration needed wrong-parent, continuation-progress, root-projection, aggregate, and failure-boundary hardening. |
| **High — renderer/UX/accessibility/performance** | `GraphSceneControl`, layouts, LOD/labels, density/budgets, graph/list/automation parity, HUD geometry, reduced motion/effects | Search emphasis caused a measured allocation regression; several accessibility projections worked in backing state but not through real automation semantics. |
| **High when affected — release/privacy** | Installer/signing, diagnostics, artifact contents, handoff secrets, persistence schema, audio/process cleanup | The public-release boundary depends on license/notices, fail-closed signing policy, exact-artifact checks, sanitized output, truthful claims, and explicit manual gates. |
| **Normal — voice; High on concurrency/privacy/latency or hardware claims** | Parser or isolated availability text is Normal; capture/transcription concurrency, temporary files, provider-generation invalidation, privacy/accessibility surfaces, or performance/hardware claims are High | The path is bounded and tested, but real microphone hardware remains unvalidated and process/privacy failure boundaries are user-sensitive. |
| **Normal — standalone/platform** | Filesystem enumeration/Search, root confinement, reparse handling, native path semantics, preferences | The design is bounded with focused tests, but path/opaque-ID case semantics previously required a cross-platform correction. |
| **Low** | Typos, history/template-only docs, isolated comments, non-behavioral metadata, deterministic leaf helpers | Low only when no current capability, authority, persistence, compatibility, release, or accessibility claim changes. |

Escalate one level when a change crosses subsystem ownership, changes a limit or persisted/public/wire contract, affects failure recovery, lacks focused regression coverage, or changes a claim that cannot be automatically verified.

## Specialist roles

Specialists advise with repository evidence; the Lead integrates and remains accountable.

- **Lead / Orchestrator** — baseline, scope, decomposition, routing, file ownership, validation completeness, documentation freshness, integration, and owner report.
- **Product / Strategy** — only for product scope, workflows, feature tradeoffs, or Standalone/Connected product boundaries.
- **Architecture / Integration** — subsystem boundaries, state ownership, provider authority, major interfaces, protocol/handoff, persistence/public contracts, invariants, and significant refactors.
- **UX / Accessibility** — graph interaction, navigation, hierarchy, modes, keyboard/input, automation projection, reduced motion/effects, and user-visible failure states.
- **DX / Maintainability** — naming, organization, test comprehensibility, developer workflow, and context cost. It does not justify broad cleanup by itself.
- **Performance** — renderer/layout/labels/density, allocation/cache behavior, large bounded sources, latency, responsiveness, and reliable measurement design.
- **Implementation** — targeted code changes after constraints and file ownership are established.
- **Documentation** — current architecture, terminology, diagrams, decisions, source/test links, validation claims, and ensuring important reasoning is not chat-only.
- **Adversarial Review / Validation** — independently challenges assumptions, boundaries, state transitions, incomplete tests, accidental behavior change, and lying documentation.
- **AX** — not a standing OmniBrille role. Current voice is deterministic input and Context is server-authored. Use AX only if OmniBrille itself gains AI-assisted behavior.

## Routing by risk

| Risk | Minimum routing | Additional routing triggers |
| --- | --- | --- |
| Low | Lead/implementer; Documentation for current claims | Add a relevant specialist only if the edit reveals architectural uncertainty. |
| Normal | Lead + one relevant specialist + Implementation + documentation freshness check | Add UX for user-visible workflows, Architecture for ownership/contracts, or platform review for path semantics. |
| High | Lead + relevant Architecture/Product/UX/Performance specialists + Implementation + Documentation + independent Adversarial Review | Add security/privacy perspective for grants, packaging, persistence, diagnostics, or audio/process work. |

Do not have several agents rediscover the same architecture. The Lead records established findings, assigns bounded questions, and gives one agent ownership of each overlapping implementation area.

## Evidence-derived task routes

| Change | Specialists | Required focused evidence |
| --- | --- | --- |
| Session/navigation/loading/Search/modes | Architecture; UX if semantics change; Adversarial at connected/Hybrid boundaries | Late-result, cancellation, rollback, Back history, filter/search/selection, and provider-replacement tests |
| Renderer/layout/LOD/labels/density | UX + Performance + Adversarial | Core budget/layout tests; representative small/dense/search scenes; allocations/caches; keyboard/list/text-scale/reduced-effects checks |
| Graph/list/mode/input accessibility | UX/Accessibility + Adversarial | Automation name and activation path, resulting session state, keyboard parity, bounded visible-node projection; manual assistive technology reported separately |
| Protocol/provider/cache/handoff | Architecture/Integration + Adversarial | Strict serialization, hostile semantic responses, bounds, scope, disconnect, cache liveness, session expiry/fresh grant, and no filesystem fallback |
| Standalone filesystem or provider-specific navigation target | Architecture or platform specialist when boundary/identity semantics change; Adversarial for connected target history | Root escape, native case behavior, opaque-ID case behavior, reparse avoidance, truncation, cancellation, partial failures, and bounded Search. A known Windows connected-target comparison gap remains in `NavigationState`; do not treat the separate identity test as coverage. |
| Voice | Architecture/privacy + UX/Accessibility for High triggers; Performance only for latency changes; Adversarial for High | Disabled/unavailable behavior, cancellation, process cleanup, stale provider generation, safe diagnostics; real hardware separately |
| Packaging/release | Architecture/security + Documentation + Adversarial | Release tests/scripts, package audit, hash/manifest/signing mode, installed lifecycle, manual gates |

## Validation depth

### Low

- Run `./build/Test-EngineeringDocs.ps1` for documentation changes.
- Inspect the diff and run `git diff --check`.
- Run focused tests only if an executable or release assertion changed.

### Normal

- Run focused tests for the changed contract.
- Run formatting and Release build.
- Run the affected ordinary/headless test project.
- Use both CI platforms when filesystem or platform semantics matter.
- Check affected current docs, terminology, diagrams, decisions, and validation claims.

### High

- Run all Normal checks plus the full automated suite.
- Add or run boundary/failure/race fixtures that could falsify the change.
- Use representative renderer/performance/accessibility/protocol/release validation appropriate to the risk.
- Obtain independent adversarial review after implementation.
- Report every material manual/environment-specific check not performed.

The standard source commands are:

```powershell
dotnet restore .\OmniBrille.sln
./build/Test-EngineeringDocs.ps1
dotnet format .\OmniBrille.sln --verify-no-changes --no-restore
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
```

Use [`build/verify-release.ps1`](../../build/verify-release.ps1) only for release-relevant work; it is intentionally more expensive and may package/sign.

## What automated checks do not prove

- Compilation or headless rendering does not prove visual composition, DPI behavior, animation quality, or GPU performance.
- Headless automation checks do not certify a real screen reader or platform accessibility backend.
- Windows/Ubuntu CI does not prove macOS runtime behavior or Linux interactive behavior.
- Fake protocol tests do not prove compatibility with a changed real OmniSorSe host.
- Recognizer/process tests do not prove real microphone capture.
- Link/fence checks do not prove that prose or Mermaid diagrams are semantically true.

Reports must separate **Verified**, **Not verified**, and **Inferred** accordingly.

## Existing executable memory to preserve

- Request-generation and provider-replacement tests in [`ExplorerSessionTests`](../../tests/OmniBrille.Tests/ExplorerSessionTests.cs), [`ContextSessionTests`](../../tests/OmniBrille.Tests/ContextSessionTests.cs), and [`HybridSessionTests`](../../tests/OmniBrille.Tests/HybridSessionTests.cs).
- Root/native-case tests and a separate `ExplorerIdentity` case test in [`NavigationStateTests`](../../tests/OmniBrille.Tests/NavigationStateTests.cs) and [`FileSystemExplorerProviderTests`](../../tests/OmniBrille.Tests/FileSystemExplorerProviderTests.cs). These do **not** yet cover Connected-mode `NavigationState` targets that differ only by case on Windows.
- Scene/edge budget and layout tests in [`tests/OmniBrille.Tests`](../../tests/OmniBrille.Tests).
- Protocol framing, semantic validation, provider/coordinator, and cache-liveness tests in [`tests/OmniBrille.Tests`](../../tests/OmniBrille.Tests).
- Graph/list/automation action-result tests in [`MainWindowHeadlessTests`](../../tests/OmniBrille.HeadlessTests/MainWindowHeadlessTests.cs).
- Cache bounds and representative renderer diagnostics.
- Release/privacy/package assertions and the artifact-only hosted Windows lifecycle.

Do not add an absolute CI frame-time threshold, blanket coverage target, broad source-string architecture suite, or new platform job without evidence that it will be reliable and worth maintaining.
