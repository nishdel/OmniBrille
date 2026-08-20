# Engineering knowledge foundation — 2026-08-20

Historical snapshot: this report records what this run established and verified. Current architecture documents and source remain authoritative if later evidence disagrees.

## What this run was meant to do

Reconstruct OmniBrille from its source, tests, Git history, CI, and real integration boundary, then leave the smallest durable engineering-memory system that lets humans and Codex begin future work without old conversations or another archaeology prompt of this size. Product behavior was intentionally out of scope.

## What actually changed

The repository now has a compact engineering router, an authority/context-loading map, verified ownership and state-flow documentation, three colocated Mermaid diagrams, a focused glossary, two reconstructed ADRs, an evidence-derived risk/routing guide, representative historical failure chains, controlled lesson promotion/pruning, and a selective owner-report convention.

A lightweight PowerShell check now validates permanent entry points, repository-relative Markdown links, and closed code/Mermaid fences in CI and the release gate. Existing current documents were corrected where source/history disproved them, including progressive-loading timing, Context selection, strict JSON, snapshot ownership, external-host evolution, microphone validation, and connection/provider ownership. Long stage timing narratives were removed from default architecture context. No graph, renderer, navigation, protocol, persistence, or feature implementation was deliberately changed; one user-facing microphone availability sentence now says “implemented,” not falsely “validated.”

## Important technical decisions

- **Use existing current documents as authorities; add one router →** avoids a parallel CURRENT-STATE encyclopedia → future tasks load only relevant headings and subsystem contracts.
- **Keep two ADRs only →** separate provider authority and bounded deterministic scenes are durable, repeatedly rediscovered decisions → fixes and stages remain in tests/history rather than ADR sprawl.
- **Retain only significant run reports →** an earlier performance claim referred to an untracked report, and later fixes sometimes disproved “complete” claims → archaeology gains honest historical snapshots without making reports default context.
- **Use a path/fence check, not generated architecture →** broken references are reliable to automate; semantic truth is not → CI gains useful executable memory without pretending parsed Mermaid proves architecture.
- **Defer the Connected navigation comparer correction →** independent review found a real product-state defect, but this run is infrastructure-scoped → current docs now qualify it and the roadmap requires Architecture/Integration, a Windows regression, and adversarial review.
- **Do not add a Codex Skill or more standing roles →** repository routing already encodes the reusable procedure; no separate repeated tool workflow was evidenced → lower maintenance and context cost.

## Validation and confidence

### Verified

- Baseline and post-change Release builds completed with zero warnings/errors; 191 ordinary tests and 41 Avalonia headless tests passed (232 total). The technical release gate completed `Ready = true` with `-AllowDirty -SkipPackage`, including restore, format, build/tests, documentation validation, vulnerability audit, and informational dependency review.
- `dotnet format --verify-no-changes`, `git diff --check`, the engineering-document check under PowerShell 7 and Windows PowerShell 5.1, and the release-hardening assertions passed after corrections.
- Current source, tests, all 34 commits, CI/release scripts, tags/branches, and relevant documents were inspected. Six diverse historical chains plus the unresolved Connected identity case were counterfactually reviewed.
- The component, state, and stale-result Mermaid sources were manually checked against composition/session/renderer code; the checker confirmed closed fences and paths.
- The mirrored protocol DTO/handoff boundary was compared with the available later OmniSorSe checkout. Wire/handoff shape was stable; host relationship projection had evolved. This was source inspection, not runtime compatibility validation.
- Independent architecture/history/quality investigations, fresh-developer simulation, semantic documentation review, and adversarial review challenged the result. Their material findings were corrected or explicitly deferred.

### Not verified

- No manual visual, GPU, DPI, animation, keyboard, screen-reader/backend, or general UX session was performed.
- No live microphone hardware capture, Linux interactive/connected runtime, macOS runtime, signing, installed-package lifecycle, or fresh real two-process OmniSorSe run was performed.
- No new repeatable performance benchmark was run; production performance code did not change.

### Inferred

- Functional runtime behavior is preserved because executable changes are limited to validation wiring, release-test assertions, project description/comment text, and truthful microphone wording; the full automated suite passed. Automated evidence cannot prove visual or platform runtime equivalence.
- Future small tasks can remain small and risky tasks can discover their required context/review from the router. The fresh-developer simulation demonstrated this at source level, not through a real future feature run.

## Problems found

Fixed in this run: contradictory microphone claims, several architecture/Context/protocol ownership and behavior drifts, stale Stage labels, overlong historical default context, a missing documentation freshness gate, and initial PowerShell 5.1 incompatibility in that gate.

Deferred product/engineering work:

- Connected `NavigationState` uses native path equality for opaque targets; on Windows, case-distinct IDs can desynchronize target/history from the applied scene. High risk; fix with Architecture/Integration and an adversarially reviewed regression.
- Optional Context/Related capability absence is detected on activation but not reflected in mode availability and is reported as connection failure. High integration/UX risk.
- Protocol-info validation covers client-consumed safety limits but not every advertised field; the intended compatibility contract needs an explicit decision and malformed-info tests. High integration/validation risk.
- Contrast tests use literals rather than application theme resources. Normal accessibility-maintenance risk pending a reliable seam.
- `MainWindow`, `ExplorerSession`, `GraphSceneControl`, and the large headless fixture concentrate responsibilities. Improve only during touched-area work; no mass refactor.

None blocks using the engineering system. The first two must be considered before related product work or compatibility claims.

## What the agents learned

The initial working directory was an empty assignment workspace, not the canonical checkout; the run established the canonical clean source before making a local no-hardlink working copy. Important rediscoveries were that `ExplorerSession` in Desktop owns application state, `MainWindow` constructs the connected provider and projects session state to renderer/list, request identities are operation-specific, Context builders are stateless, strict JSON rejects additive fields, and persisted state is limited to visual/voice configuration.

Bounded history, architecture, quality, fresh-developer, semantic-documentation, and adversarial specialists were all useful; Product, AX, and a separate Performance implementation specialist were unnecessary because product direction and performance code did not change. Parallel evidence questions reduced duplicate archaeology; editing remained single-owner.

Promoted lessons:

- Significant validation claims need discoverable historical snapshots → selective `docs/runs` policy and this report; supported by the missing Stage 2 task report and later claim/correction chains.
- Repository-relative knowledge paths/fences should be executable memory → `Test-EngineeringDocs.ps1` in CI/release; independently demonstrated by its missing-report detection.
- Risky state/protocol/accessibility/performance work needs targeted evidence and independent challenge → risk routes backed by repeated regressions and this run’s adversarial discovery.

Remain candidates: Connected navigation target comparison, optional-capability UX, protocol-info limit completeness, theme-resource contrast coupling, and a reliable GPU/runtime performance guardrail. Rejected: a universal frame-time threshold, `ToString()` as a global accessibility rule, an ADR per fix, every specialist on every task, a new user-flow document, and a duplicated Codex Skill.

## Documentation and diagrams

Created or materially updated: `AGENTS.md`; engineering authority/risk/learning/history guides; glossary; ADR index and two ADRs; significant-report index/template; current architecture, Context, protocol, voice, compatibility, packaging/release, roadmap, and README claims.

Architecture now includes component/data ownership, view/provider reset state, and operation-generation sequence diagrams. Source-level semantic review corrected their arrows and labels. No important current architecture discovered in this run is known to remain only in the Codex conversation. Manual runtime/visual knowledge remains explicitly unverified rather than undocumented as fact.

## Repository state

- Repository: OmniBrille audit working copy, made as a local no-hardlink clone from the clean canonical checkout.
- Branch / HEAD: `main` at `8766e33d9778afbf262356be57dbbe7152eb2a83` (tagged `v0.8.0-preview.2`); canonical GitHub `main` was verified at the same commit at baseline.
- Working tree: dirty only with this run’s documentation, validation, test-assertion, metadata/comment, and microphone-wording changes; baseline had no pre-existing changes.
- Commits / push: none created; nothing pushed. The audit copy’s transport remote is the local canonical checkout; the canonical checkout retains `https://github.com/nishdel/OmniBrille.git` and was not modified.
- Schema / protocol / persistence format / public interfaces: unchanged.
- Compatibility assumptions: corrected documentation now distinguishes stable inspected wire/handoff shape from evolved external host relationship projection.
- Intentional product behavior: no functional change; one truthful user-facing platform-availability wording correction.

## Bottom line

Status: Complete with follow-up

OmniBrille’s repository is safe to use as the engineering starting point for major feature work, provided future work follows the risk routes and treats the two documented Connected-boundary defects as real. Confidence is high in repository architecture, test/build health, and documentation discoverability; it remains limited for manual visual/accessibility/hardware/cross-platform and current real-host runtime behavior. The owner’s next priority should be the Connected opaque-target comparer regression before further Connected navigation work, followed by the optional-capability UX decision.
