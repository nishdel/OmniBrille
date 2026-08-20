# Historical failure chains and earned lessons

Classification: **Historical evidence and reusable lessons**. This document does not describe current architecture; follow its links to current source/tests. It reconstructs a representative, deliberately mixed sample from the repository's 34-commit linear history (`def16ea` through `8766e33`).

## Architecture transitions

- `def16ea`/`775789a`: Core/provider split, standalone vertical slice, Avalonia shell/session/custom renderer.
- `4904f85`: progressive batches, explicit load states, request generations, navigation rollback, and reversible aggregate pages.
- `ee7a94e`/`17f3004`: visual policy, bounded caches/diagnostics, accessibility projection, platform identity semantics, and Context budgets.
- `7338245`: Explorer Protocol v1 client/provider/coordinator.
- `6dd9ae5`/`9731dc4`: server-authored Context acquisition and presentation.
- `cada4d2` onward: packaging and private-preview gates.
- `c176c74`/`7f1956c`: optional deterministic push-to-talk over existing actions.
- `7753741`/`2e7466b`: Hybrid client composition without a protocol change.

The recurring sequence was implementation → hardening/tests → documentation/package. That produced good regression memory but also temporary and sometimes surviving claim/implementation drift.

## Representative cases

### 1. Cancellation existed, stale-result safety did not

**What happened.** The initial vertical slice (`775789a`) claimed cancellation, loading feedback, bounds, aggregation, and cancellation coverage. The session cancelled tokens but had no request generation, so a provider losing the cancellation race could overwrite newer navigation. Aggregates were bounded markers rather than reversible pages. `4904f85` added progressive shells/states, monotonically increasing identities, navigation commit/rollback, aggregate paging, and focused regression tests.

**Failure categories.** Requirement interpretation, implementation, test, and reporting precision.

**What a future run reads.** [`architecture.md`](../architecture.md), [`risk-and-validation.md`](risk-and-validation.md), `ExplorerSession`, and `ExplorerSessionTests`.

**Routing and detection.** Architecture/state review plus focused late-result, cancellation, rollback, and aggregate tests; adversarial review for connected/Hybrid state.

**Counterfactual.** Likely detected earlier: yes. A test using a provider that completes late asks a stronger question than a pre-cancelled token test. Context cost is lower because the invariant and tests are now discoverable without reconstructing the commits.

**Remaining gap.** `ExplorerSession` remains a large high-risk authority; future touched-area refactoring should be incremental.

### 2. Search emphasis regressed renderer allocation

**What happened.** `ee7a94e` added richer Search emphasis and label work. Stage 2 documentation referenced exact measurements in an untracked “task report.” Later profiling recorded a 25.835 ms sample caused by repeated `FormattedText`, brush, and pen construction. `17f3004` added bounded text/resource caches, phase/allocation diagnostics, invalidation rules, and pressure fixtures; `0bce6b9` preserved the cause in repository documentation.

**Failure categories.** Implementation/performance, validation depth, and historical evidence retention.

**What a future run reads.** ADR 0002, the renderer/performance sections of current architecture, `GraphSceneControl`, `BoundedLruCacheTests`, and relevant headless pressure fixtures.

**Routing and detection.** UX + Performance + Implementation + independent review; representative small/dense/Search/reduced-effects samples and cache/allocation inspection.

**Counterfactual.** Probably detected earlier with Performance routing and an emphasized-scene sample. Ordinary build/unit success would not. Retained current evidence avoids searching for the missing task report.

**Remaining gap.** There is no reliable GPU/runtime benchmark gate. That is deliberate until repeatable evidence supports one.

### 3. Identity hardening did not cover later Connected navigation reuse

**What happened.** Before Connected mode, `17f3004` introduced ordinal `ExplorerIdentity` for graph IDs and changed standalone `NavigationState` to native `PathBoundary` comparison, with Windows/Ubuntu CI. `7338245` later reused `NavigationState` for Connected opaque targets without specializing its equality. On Windows, IDs such as `node-A` and `node-a` compare equal there: the new scene can be applied while current target/history remain old. The existing test proves `ExplorerIdentity` is ordinal but never exercises Connected `NavigationState`.

**Failure categories.** Context/architecture reuse, implementation, test, review, and overbroad documentation promotion.

**What a future run reads.** [`glossary.md`](../glossary.md), the qualified cross-platform architecture section, [`risk-and-validation.md`](risk-and-validation.md), `NavigationState`, and its tests.

**Routing and detection.** Architecture/Integration + Implementation + Adversarial; a Windows regression must navigate between case-distinct opaque targets and verify current target, scene, and Back history remain coherent.

**Counterfactual.** The new system did not retroactively prevent this case; independent adversarial review of this foundation discovered it. The updated risk route would likely expose it earlier in future Connected/navigation work, but prevention remains incomplete until code and regression coverage are added.

**Remaining gap.** The product correction is deliberately deferred from this infrastructure run and tracked in `ROADMAP.md`. Connected `AccessRoot`/`CurrentPath` naming also needs care when those files are touched.

### 4. Accessibility state was correct while the projection/action was wrong

**What happened.** The accessible list initially had shared state but not reliable announced item text; `f073db0` added and tested it. Context UI later exposed state but left a hard-coded Structure footer and weak relationship names; `fb6d4f0` corrected them. `5a7557d` found that programmatic/automation radio selection did not invoke the same mode transition and that default Search-result text exposed opaque IDs; it added checked-state handling and exact safe-text/action-result tests.

**Failure categories.** Implementation, test model, accessibility/privacy review, and documentation timing.

**What a future run reads.** Accessibility sections of current architecture, the UX route in the risk guide, `GraphSceneAutomationPeer`, MainWindow list projection, and headless tests.

**Routing and detection.** UX/Accessibility + Adversarial; assert what automation announces, how it activates, and the resulting session state—not only control existence or a simulated pointer event.

**Counterfactual.** Likely for the repeated projection bugs. Headless evidence still cannot prove real assistive-technology behavior.

**Remaining gap.** Manual screen-reader/backend validation and macOS automation runtime remain unverified.

### 5. The first connected boundary was framed correctly but semantically under-validated

**What happened.** `7338245` added the strict Protocol v1 client/provider/coordinator while docs still described a design-only adapter. `f6f3afd` moved provider exception knowledge behind provider diagnostics and added wrong-parent, non-progressing continuation, and invalid-root rejection. `9e877d0` added the missing 5,000-server/512-client/48-scene aggregate-heavy case; `e556f14` made async tests observe events instead of relying on a short poll.

**Failure categories.** Architecture boundary, hostile-response implementation, test/adversarial review, and documentation freshness.

**What a future run reads.** ADR 0001, [`explorer-protocol.md`](../explorer-protocol.md), strict validation/provider source, and protocol/provider/session suites.

**Routing and detection.** Architecture/Integration + Implementation + Adversarial; malformed-but-well-framed responses, scope, pagination progress, bounds, disconnect, and fresh-session tests.

**Counterfactual.** Likely. The new route explicitly asks what semantic assumptions a hostile or buggy peer can violate. Focused docs avoid reconstructing the initial integration commits.

**Remaining gap.** Real-host compatibility must still be revalidated when the external host changes; fake tests cannot prove it.

### 6. A valid Context cache became misleading after host death

**What happened.** `6dd9ae5` cached Context snapshots. A cache hit could return without touching the server even though Protocol v1 has no disconnect push, presenting old data as a successful live read. `fb6d4f0` added an authenticated protocol-info probe before cache return plus a regression test.

**Failure categories.** State/lifecycle design, implementation, review, and test.

**What a future run reads.** Current architecture, Context contract, ADR 0001, provider cache source, and `OmniSorSeConnectedProviderTests`.

**Routing and detection.** Architecture/Integration + Adversarial; cache-after-disconnect, session expiry, new grant, and stale-scene behavior.

**Counterfactual.** Likely. The risk map now treats protocol cache/liveness as high risk, and the regression test is executable memory.

**Remaining gap.** Protocol v1 still has no pushed disconnect; the probe is the current bounded compromise.

### Control cases

Not every later commit proves an earlier failure:

- Stage 2 explicitly documented missing per-node automation before the later accessibility work.
- Stage 10's installed smoke explicitly stated which Search/list actions its host could not automate and did not claim them.
- The final HUD/empty-state polish was a planned maturity stage, not proof every previous package was invalid.
- The real voice recognizer/process test accurately stated that the host had no WinMM input; the failure was contradictory wording elsewhere.

## Lesson status

| Lesson | Outcome | Durable form / reason |
| --- | --- | --- |
| Late work must not replace newer provider/session state | **Promoted** | Request/provider generations and regression tests |
| Graph identity comparison and filesystem path comparison are different | **Promoted** | `ExplorerIdentity`, `PathBoundary`, focused tests, glossary, two-OS CI |
| Provider-specific navigation targets must choose their comparer | **Remain candidate** | Current Connected/Windows defect; requires code correction plus regression before promotion |
| Renderer Search/labels require bounded resources and representative pressure review | **Promoted** | Bounded caches, diagnostics, tests, ADR 0002, risk routing |
| Context density must remain bounded | **Promoted** | `ContextRenderBudgetPolicy`, tests, authoritative contract |
| Correct framing is insufficient protocol validation | **Promoted** | Strict semantic validation, focused tests, ADR 0001, high-risk routing |
| Cached connected data needs liveness semantics | **Promoted** | Authenticated probe, regression test, lifecycle documentation |
| Accessibility checks must exercise projection and action result | **Promoted** | Headless regression tests and UX/Accessibility routing |
| Significant validation claims should remain discoverable | **Promoted** | Selective `docs/runs` retention; reports remain historical |
| Documentation paths should be mechanically checked | **Promoted** | `build/Test-EngineeringDocs.ps1`; semantic truth remains review-owned |
| Add a universal hard renderer time threshold | **Rejected** | Font shaping, host load, headless/GPU differences would create noisy false failures |
| Every list item must override `ToString()` | **Rejected** | The earned contract is accessible output/action testing, not one implementation trick |
| Every hardening commit deserves an ADR/global rule | **Rejected** | Focused tests and subsystem documentation are cheaper and more precise |
| Invoke every specialist for every task | **Rejected** | Unsupported cost; risk-based routing keeps small work small |

## Current candidates

- **Connected navigation target comparison.** Evidence: `NavigationState` uses `PathBoundary.Comparer` for Connected opaque targets, with no case-distinct Connected test. Proposed form: provider-aware equality plus a Windows regression; remain candidate until implemented and independently reviewed.
- **Protocol-info limit completeness.** Evidence: `ValidateProtocolInfo` checks client-consumed limits but leaves several advertised fields unchecked while historical prose claimed comprehensive negotiation. Proposed form: an explicit compatibility decision plus malformed-info regression tests; remain candidate.
- **Optional Context capability behavior.** Evidence: the audit corrected prior documentation drift, but current mode availability/failure behavior has no capability-negative test. Recurrence matters at compatibility boundaries. Proposed form: product/architecture decision plus regression test; remain candidate until intended UX is confirmed and implemented.
- **Theme-token contrast coupling.** Evidence: contrast tests use independent literals rather than application resources. Proposed form: resource-driven accessibility test if a reliable low-maintenance seam is designed; remain candidate.
- **Stable renderer performance guardrail.** Evidence: historical regression but noisy environment. Proposed form: repeatable GPU/runtime benchmark only after sufficient samples; remain candidate.
