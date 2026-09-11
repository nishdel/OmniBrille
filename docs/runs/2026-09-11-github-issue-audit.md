# GitHub issue audit and fixes — 2026-09-11

## What this run was meant to do

Read every open issue and comment, trace the exact findings, fix coherent root causes, add regression evidence, and update GitHub without publishing a release. The audit began on 2026-09-04 and resumed on 2026-09-11 after an interruption. All nine open issue bodies, comments, and eleven attached images were read before implementation; the backlog was refreshed on resumption and was unchanged.

## What actually changed

Structure shows actual parent-linked subfolder previews when the 48-node scene has spare capacity. Direct children keep their meaning regardless of density band. Same-type files occupy nearby sectors with deterministic variation. Every admitted on-screen name remains present, with measured placement around glyphs. The graph reserves space for the HUD, and connectors end outside glyphs.

Folders enter on one click, right-click goes Back, and six named Trail destinations support direct return with failure/stale-state safety. File double-clicks cannot carry over into a replacement scene. Crowded targets resolve to the nearest node center. Details reveal the actual metadata while exposing complete semantic text immediately. Voice starts on the first microphone action, shows Stop while active, and submits after two seconds of quiet following detected speech. Cancellation and device cleanup retain operation identity. Sounds now combine local synthesized air, click, latch, and descending vault textures.

## Important technical decisions

- **Actual preview data → truthful depth → bounded optional acquisition.** At most four admitted parents and three children each use unused scene slots. Standalone counts at most 64 inspected entries; Connected uses one 32-node page and opaque authority. No recursion, continuation chasing, inferred Context, persistence, or filesystem fallback. [ADR 0003](../decisions/0003-bounded-descendant-previews.md) records the decision.
- **Keep every name → measure and relocate → cache reusable text layouts.** The initial all-name implementation exposed repeated draw-time shaping allocations; reusable `TextLayout` objects restored the existing pressure bound. Extreme text/viewport combinations can still overlap; the synchronized list remains the full-size reading surface.
- **Bind completion to its originating operation → reject obsolete writes → preserve cancellation safety.** An old cancellation cannot reset a replacement Voice capture or dispose its recorder.
- **Keep exact activation semantics → preserve session authority.** Trail destinations expose names and modes, with private current-generation bindings; failed jumps preserve the accepted scene/history. Structure single-click follows the explicit later navigation request in #3; files retain double-click/open behavior.

## Issue map and verification

All entries were normal user-facing severity; state/authority changes were classified High engineering risk. #1/#5/#6/#8/#9 shared layout/presentation causes; #3 depended on session/input state; #2/#4 shared Voice setup; #7 shared Details/sensory behavior with #6.

| Issue | Root cause and affected components | Evidence and disposition |
| --- | --- | --- |
| [#1 — unclear file level](https://github.com/nishdel/OmniBrille/issues/1) | Symmetric density bands suggested hierarchy; builder/layout/scene semantics/renderer now distinguish direct children and actual previews. | Semantic depth/parent tests plus Dark/Light captures show solid parent branches and `↳` previews. Verified in development source. |
| [#2 — whisper setup](https://github.com/nishdel/OmniBrille/issues/2) | Manual runtime/model setup applied to older source; bundling was already implemented in `c54adac` before this baseline and shipped in v1.1.0. | Rechecked bundled resolution and packaging regression coverage. No duplicate packaging change required; physical microphone use is outside this setup finding. |
| [#3 — navigation/control clarity](https://github.com/nishdel/OmniBrille/issues/3) | Passive Trail, competing history authorities, tiny off-center content; session, MainWindow, renderer. | Trail success/failure/cancellation/provider/mode/aggregate tests; actual button invocation, single/right/double-click, crowded targets and minimum-size scrolling fixtures pass. Keep open for subjective transition and native control acceptance. |
| [#4 — click-toggle Voice](https://github.com/nishdel/OmniBrille/issues/4) | First-click enablement triggered a second capability refresh; no quiet endpoint; stale completion could affect new capture. | Deferred first-click, icon state, virtual-time quiet/max-duration, cancellation/provider/restart tests. Keep open for real microphone, ambient-noise threshold, command recognition, and listening-animation acceptance. |
| [#5 — concept fidelity](https://github.com/nishdel/OmniBrille/issues/5) | Central edge anchors, symmetric positions, no acquired deeper folders; geometry, layout, builder/providers/session. | Periphery/leader geometry, explicit-parent bounds, preview separation, and both-theme renders. Keep open for concept/contrast/native-scale acceptance and navigation motion. |
| [#6 — motion and Details](https://github.com/nishdel/OmniBrille/issues/6) | Wide hover influence, typography tied to motion, duplicate CLI header; renderer and MainWindow. | Local spatial falloff, stable typography, all-name fixtures, actual metadata reveal/atomic text, reduced motion and allocation checks. Keep open for motion comfort and native readability. |
| [#7 — sound character](https://github.com/nishdel/OmniBrille/issues/7) | Existing short chirps lacked requested texture; synthesized audio and Details presentation. | Deterministic bounded PCM, duration/peak/quiet-tail checks and action routing/mute fixtures. Keep open for listening to hover/click/file/vault cues on physical output and subjective acceptance. |
| [#8 — inconsistent names](https://github.com/nishdel/OmniBrille/issues/8) | Zoom/interaction label budgets and collision suppression hid an arbitrary subset. | All 48 labels survive zoom/text-scale/selection fixtures; normal and minimum-zoom captures inspected. Verified in development source; extreme label crowding remains documented. |
| [#9 — same-type grouping](https://github.com/nishdel/OmniBrille/issues/9) | Alphabetical symmetric placement lacked type proximity. | Deterministic case-insensitive extension groups, unknown-type bucket, cross-band alignment, continuity/admission tests and dense capture. Verified in development source. |

## Validation and confidence

**Verified baseline:** clean `main` at `6783a6baf725e81ef39a355bedb804abec795cd9`, tracking `github/main`; one worktree, no active Git operation or pre-existing changes. Restore/Release build succeeded without warnings/errors. Ordinary 230 + headless 57 = **287 passed, zero skipped**. Environment: Windows x64, SDK 9.0.316, .NET 8 targets, Avalonia 12.1.1.

**Verified final development qualification (2026-09-11):** restore, formatting verification, Release build (zero warnings/errors), full ordinary/headless suite, engineering documentation validation, NuGet vulnerability audit (no known vulnerable packages reported), and `git diff --check` passed. Ordinary **304 passed / 1 skipped**, headless **72 passed / 0 skipped**: **376 passed, 1 skipped, 377 total**, up from 287 baseline cases. Four additional executions of the existing software-capture theories passed with real Skia drawing; these are not added to the unique test count. The full suite includes navigation/Search/Details/mode/provider replacement, hostile Connected responses, privacy/package checks, reduced motion/effects, automation action-result, and renderer cache/allocation pressure fixtures. Final local TRX evidence is in ignored `artifacts/issue-audit/final`; reviewed images are retained in the repository.

Independent adversarial review completed after implementation. Its two final findings (file-leader envelope mismatch and crowded wrong-folder activation) were fixed before qualification. GitHub Windows/Ubuntu CI is a separate post-push check; this local result does not assert that CI or installer packaging has run.

**Not verified:** real microphone/capture quality or physical sound output; installed native visual composition, motion comfort, GPU performance, DPI/high contrast, Narrator/NVDA/Windows UIA backend; interactive Linux/macOS; current real OmniSorSe host; fresh installation of this branch. The Windows test suite skips its Unix symlink replacement fixture; native Windows junction replacement remains a manual/platform follow-up. No claim is made that the concept or subjective sound/motion requests are fully accepted.

**Inferred:** existing bounded immutable projections and provider contracts preserve the established product boundary, supported by hostile-response/stale-state regressions. Software captures show composition for the recorded synthetic scenarios only. [Render evidence and reproduction](../assets/screenshots/issue-audit-2026-09-11/README.md) are retained separately from historical installed screenshots.

## Problems found and retrospective

Independent Architecture/UX/Performance, Voice/privacy, session, documentation, and adversarial review were justified by the cross-subsystem scope. Review caught old-cancel/new-listen state overwrite, recorder replacement cleanup, dense preview overlap, cross-scene double-click activation, file leader envelope mismatch, and wrong-folder hit resolution at minimum size. Render inspection caught HUD overlap and faint preview edges that non-pixel count assertions had missed. The first all-name renderer allocated excessively despite cached `FormattedText`; drawing, not construction alone, needed profiling. Four evidence fixtures also incorrectly disposed the MainWindow-owned session twice; fixture ownership was corrected.

The main avoidable cost was treating label counts and layout bounds as sufficient visual evidence. Earlier software capture and minimum-size glyph-center activation would have exposed the problems sooner. Current architecture had stale label budgets and motion terminology; the authority map helped identify the exact prose to replace. Bounded providers and operation identities were rediscovered in source, not inferred from the screenshots.

| Candidate lesson | Independent evidence/review | Outcome |
| --- | --- | --- |
| Count inspected preview work, including rejected/unreadable entries. | Separate provider review; 64-nonfolder inspection and no-continuation tests. | Promoted to executable tests and ADR 0003. |
| Retain operation/device identity through completion and final state writes. | Independent stale-cancel review and delayed-cancel/restarted-listening regression. | Promoted to coordinator/device guards, tests, and Voice contract. |
| Descendant depth requires acquired children with explicit parent authority. | Wrong-parent/orphan/recursive fixtures and separate architecture/documentation review. | Promoted to builder/provider tests, semantics, architecture, ADR 0003. |
| Share glyph envelopes across edges and relocated-label leaders. | Independent file-envelope mismatch reproduction; geometry tests. | Promoted to shared renderer helper and finite-segment tests. |

No global AGENTS rules or new skills were added. Native comfort/readability conclusions remain candidates for manual review, not promoted claims.

## Documentation and diagrams

Updated README, unreleased changelog, architecture, glossary, interaction/Context/Voice contracts, and ADR index/0002 refinement; added ADR 0003 and software captures. The acquisition Mermaid now includes optional preview enrichment after the accepted primary scene. Protocol/packaging/persistence/version files remain applicable; historical reports and public-release evidence were preserved. Important intent now lives in code, regression tests, current contracts, and this report.

## Repository state

Work is on `codex/github-issue-fixes`, based on the clean baseline above. Application-local preview/session APIs and intentional input/presentation behavior changed. Protocol wire schema, persistence, grants, installer version, tags, and releases did not change.

- `c7529d5a4b2429d5e28786c5d6ff35aef3698842` — operation-bound Voice completion and local audio textures (#4, #7).
- `ecc20913e57924d7c286f1083122c2d7aa882805` — graph hierarchy/labels, bounded providers, session/navigation, first Voice click, UI and regression coverage (#1, #3–#6, #8, #9).
- The accompanying documentation/evidence commit records the current contract and this qualification. Its identity is available in branch history; a report cannot embed its own commit hash.

Issue disposition: #1, #2, #8, and #9 are verified for the recorded scope and eligible for closure with the implementation/baseline references above. #3–#7 retain the exact manual acceptance steps in the issue table. No issue is blocked by missing implementation information. Branch delivery, PR URL, and actual GitHub comments/closures are recorded in the task's final report and GitHub history rather than predicted here.

## Outcome

Status: Complete with follow-up

The development source is qualified and safe to build on within the recorded evidence limits. Merge and the listed manual acceptance remain separate steps. Recommend **v1.2.0**, after the remaining native/motion/Voice/audio acceptance gates: actual descendant previews and changed navigation/Voice behavior warrant a feature release rather than v1.1.1. This task does not publish or tag a release.
