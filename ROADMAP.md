# OmniBrille roadmap

Version 1.2.0 includes the implementation fixes from issues #1–#9; native visual/control/motion, microphone/noise, and physical sound acceptance remain manual follow-up. See [release notes](docs/release-notes.md) for current behavior and downloads and the [issue audit](docs/runs/2026-09-11-github-issue-audit.md) for the preserved investigation.

Checked items record implementation or engineering evidence at their named stage. Older stage descriptions are historical milestones and may be superseded by later stages; [architecture](docs/architecture.md) and the [interaction contract](docs/interaction-state-contract.md) define current behavior.

## Stage 1 — Architecture and working Structure slice (complete)

- [x] Independent standalone shell and repository structure.
- [x] Abstract explorer/search model and bounded graph model.
- [x] Selected-folder privacy boundary, standalone hierarchy, navigation, aggregation, search, details, and Light/Dark themes.
- [x] Custom Avalonia renderer and future protocol boundary without premature IPC.

## Stage 2 — Visual system and Structural Explorer hardening (complete)

- [x] Progressive, cancellable directory batches with honest partial/loading/failure state.
- [x] Request-identity protection against stale navigation and search results.
- [x] Deterministic reversible aggregate pages within the hard scene budget.
- [x] Three-depth stable radial layout and continuous deterministic focus choreography.
- [x] Zoom/depth/density LOD, priority labels, and collision rejection.
- [x] Search emphasis, dismissible details/results, and HUD consistency.
- [x] Mature shared Dark/Light tokens, restrained atmospheric network, and bounded blue data rain.
- [x] Persisted reduced motion, reduced visual effects, and optional local diagnostics.
- [x] Improved automation metadata, keyboard conventions, and Avalonia headless UI tests.

## Stage 3 — Performance, accessibility, cross-platform hardening, and Context readiness (complete)

- [x] Phase-level renderer profiling and bounded caches for the measured text/resource hotspot.
- [x] Explicit local performance targets and 32/48/64 scene-budget review; 48 remains the readability default.
- [x] Complete synchronized keyboard/list navigation alternative and bounded visible-node automation peers.
- [x] Structured contrast review, 100/125/150/200% text-scale tests, and reduced-motion/effects validation.
- [x] Native path-case semantics, opaque case-sensitive IDs, and Windows/Ubuntu CI.
- [x] Profile-backed Context renderer limits, relationship priority, provenance seam, and synthetic density tests.

## Stage 4 — Real OmniSorSe Explorer Protocol v1 integration (complete)

- [x] Consume the actual v1 strict framed named-pipe contract and validate version, capabilities, identity, authorization, client-consumed safety limits, and errors.
- [x] Add a connected provider behind existing explorer/search/details abstractions with cancellation, request generations, opaque IDs, scope enforcement, and clear offline/incompatible states.
- [x] Preserve standalone independence and avoid OmniSorSe SQLite/application/indexing dependencies.
- [x] Validate authorized roots, bounded Structure navigation, Search, details, disconnect, and fresh-session restart against the production OmniSorSe 2.4.0 host in two processes.
- [x] Document that released OmniSorSe 2.4.0 has no companion launcher; the coordinated v2.5 RC closes this historical gap in Stage 5.

## Stage 5 — Companion launch completion and real Context mode (complete)

- [x] Consume the committed OmniSorSe v2.5 RC current-user-only one-time handoff without adding discovery, token persistence, or a second launch contract.
- [x] Consume real `GetNeighborhood(IncludeContext: true)` and `GetRelated` data within the 48-node/36-context-edge rendering contract.
- [x] Distinguish Structure and Context edges and expose provider-authored reason/evidence/provenance on selection and in the accessible alternative.
- [x] Treat Protocol v1's missing relationship ID honestly through session-local immutable-snapshot keys; incremental relationship update/removal remains disabled.
- [x] Revalidate cancellation, stale replacement, density, performance, themes, reduced effects/motion, automation, disconnect, and fresh-grant behavior.

## Stage 6 — Packaging, discovery, and Context maturation (complete)

- [x] Add reversible focus-local filtering by actual Protocol v1 relationship kind, ranking strength, and evidence class without client semantic inference.
- [x] Mature relationship hierarchy/provenance inspection and deterministic strength-aware Context depth without permanently labeling edges.
- [x] Build a reproducible self-contained per-user Inno Setup package at an existing v2.5 locator path, with upgrade/uninstall and signing readiness.
- [x] Validate installed standalone use and normal OmniSorSe discovery/handoff without `OMNISORSE_OMNIBRILLE_PATH`.
- [x] Preserve hard Context budgets, session expiry/new-grant safety, multiple-grant isolation, accessibility, reduced motion/effects, and Windows/Ubuntu CI.

## Stage 7 — Private preview and release hardening (complete)

- [x] Coherent preview version/product metadata and release-quality provisional Windows branding.
- [x] Deterministic release check, SHA-256 sidecar, release manifest, and sanitized runtime dependency inventory.
- [x] External-secret Authenticode path with fail-closed signed mode and unsigned development mode.
- [x] Node.js 24+ GitHub Actions modernization plus separate normal CI and manual private-preview workflows.
- [x] Compatibility matrix, changelog, release checklist, security/privacy review, and private-preview support guidance.
- [x] Clean/isolated install, previous-preview upgrade, installed OmniSorSe companion, artifact, performance, and uninstall gates.

## Stage 8 — Private-preview gate and distribution readiness (complete engineering gate)

- [x] Exact-artifact checksum policy, generated tester notes, and 90-day commit-named private artifact retention.
- [x] User-invoked sanitized diagnostics report and privacy-conscious feedback/rollout guidance.
- [x] Artifact-only fresh hosted-Windows hash/install/window/uninstall gate with retained validation metadata.
- [ ] Genuine clean interactive Windows VM validation of Standalone and normal OmniSorSe companion workflow.
- [x] Maintainer selected the MIT License before the stable public release; the earlier GPL choice was superseded before publication.
- [ ] Production Authenticode certificate and signed preview validation.
- [ ] Private tester rollout, support triage, and evidence-driven blocker remediation.
- [ ] Windows VM matrix expansion and Linux/macOS interactive runtime validation.

## Stage 9 — Initial local voice Search and navigation (historical implementation; hardware follow-up retained)

- [x] Optional bounded Windows push-to-talk capture with explicit listening/transcribing/cancel states and no background service.
- [x] Replaceable local speech provider; user-provided whisper.cpp/GGML setup with no mandatory download or bundled model.
- [x] Deterministic English navigation/mode/theme/UI command grammar with safe ambiguity handling and no LLM.
- [x] Standalone voice queries use structural Search; connected voice queries use the existing OmniSorSe Search provider and never create relationships.
- [x] Provider-generation stale-result rejection, temporary-audio cleanup, privacy-safe diagnostics, accessibility, reduced motion/effects, and fake-provider/headless tests.
- [ ] Real Windows microphone + local model command/Search smoke on available hardware.
- [x] Validated Stage 9 installer/private-preview workflow; controlled voice tester rollout remains deferred until real microphone hardware validation.

The Stage 9 push-to-talk and user-provided-model workflow was superseded by the installer-owned English bundle in Stage 14 and the click-to-toggle/quiet-completion refinements in Stage 15. See the [current Voice guide](docs/voice.md); the earlier setup is not required by v1.2.0.

## Stage 10 — Hybrid mode and graph exploration maturation (complete)

- [x] Add the primary `Structure | Context | Hybrid` mode model without changing Explorer Protocol v1 or introducing client semantic inference.
- [x] Compose authorized structural and contextual snapshots into one deduplicated 48-node scene with the existing 47/36/84 edge limits and maximum three contextual edges per node.
- [x] Add deterministic structural/contextual planes, stable mode transitions, focus/refocus, shared Back history, Search emphasis, relationship inspection, and Context-only filtering.
- [x] Extend bounded graph automation, the synchronized accessible list, keyboard navigation, and `Ctrl+3` for Hybrid.
- [x] Validate sparse connected and maximum synthetic Hybrid scenes, themes, reduced motion/effects, renderer diagnostics, packaging, and voice regression without claiming microphone hardware validation.
- [x] Validate the installed normal OmniSorSe RC handoff with a sparse five-node Hybrid scene containing five structural roles and three real Context roles; preserve structural orientation when the Context response omits containment.

## Stage 11 — Product polish and daily-use maturity (complete)

- [x] Review the installed first-run, standalone, and normal OmniSorSe companion workflows before changing production code.
- [x] Replace the overflowing single-row header with a bounded two-row HUD that keeps primary controls reachable at the supported minimum window size.
- [x] Clarify first-run selected-root authority and the OmniSorSe requirement for Context/Hybrid without adding an onboarding wizard.
- [x] Distinguish empty Structure, no Search result, no Context relationship, and Context-filtered-to-zero states with honest recovery actions.
- [x] Keep details secondary while Search is active and use provider-accurate Search automation/help in Standalone and connected sessions.
- [x] Preserve Structure/Context/Hybrid budgets, Explorer Protocol v1, handoff security, voice architecture, release automation, and privacy boundaries.
- [ ] Continue controlled preview feedback where useful; real microphone hardware, broader real Context/Hybrid density, interactive Linux/macOS, and future code signing remain separate follow-up work. MIT licensing and the unsigned v1.0.0 decision are resolved.

## Stage 12 — v1.0.0 first stable public release (complete)

- [x] Define a Windows x64 Standalone-first public contract without widening renderer, protocol, persistence, or product behavior.
- [x] Add stable-version packaging support, public release notes, exact-artifact metadata, dependency notices, and stronger installed lifecycle checks.
- [x] Capture and review real installed DNG-free release-candidate screenshots using non-private demo data; recapture is required only if the final visible binary, native renderer, or demo presentation changes.
- [x] Record the MIT License in source and release metadata; the exact installed-artifact copy remains part of final qualification.
- [x] Record explicit owner acceptance of a prominently disclosed unsigned v1.0.0; add Authenticode code signing in a future release.
- [x] Validate the exact release-commit artifact through local interaction and the hosted artifact-only workflow.
- [x] Complete independent release review, push the release commit/tag, publish the GitHub Release and metadata, and verify the public download.
- [x] Replace the DNG-bearing official SkiaSharp Win32 runtime with a pinned, project-built DNG-free native package; preserve build provenance, target-aware notices, and fail-closed upgrade checks.
- [ ] Treat Connected-mode broad compatibility, real voice hardware, interactive Linux/macOS, and screen-reader certification as follow-up unless separately validated.

## Stage 13 — Graph-first visual convergence (superseded candidate)

This candidate established the full-client shell but owner issues #1–#7 later invalidated its hierarchy, compact-control, static-interaction, Voice-setup, and release-readiness conclusions. Its checked items are historical implementation facts, not current acceptance evidence.

- [x] Replace the reserved two-row application header and full-width footer with reusable compact floating surfaces over a full-client graph.
- [x] Add a centered Current Focus chip, segmented mode selector, collapsed/expandable Search, compact navigation/zoom/Voice/status controls, and integrated secondary panels without duplicating session state.
- [x] Implement the earlier eight-node crisp/density-band concept; later superseded because it made direct children appear to occupy false deeper hierarchy.
- [x] Refine the deep navy/cyan and pale ice-blue themes plus the bounded data-rain identity without adding blur, unbounded resources, or a second animation loop.
- [x] Extend minimum-window, Search focus/dismissal, text-scale, hierarchy, keyboard, automation, reduced-effects, and renderer-profile validation.
- [x] Capture and independently review the final corrected v1.1.0 screenshot set from one exact candidate using non-private data.

This candidate was not published. Its open publication steps were superseded by Stage 14, which separately qualified and published v1.1.0 with explicit unsigned authorization. Historical tags and artifacts remain immutable.

## Stage 14 — Focus Plane + Navigation Trail convergence (stable v1.1.0 published; manual follow-up open)

- [x] Separate explicit current-focus/direct-child/previous-focus/Context/aggregate semantics from 12/16/remainder presentation density and keep every admitted direct child a recognizable glyph.
- [x] Add visible Back, Up, Root, and Trail actions, 44-DIP shell/graph targets, geometric arrow navigation, and shared pointer/keyboard/voice session operations without deriving Connected authority from display paths.
- [x] Add bounded analytic float/hover lens, terminal-style Details reveal, safe Standalone ordinary-file activation, local interaction cues, master mute, and complete Reduced motion/Sound-off parity.
- [x] Replace user-supplied Voice setup with a pinned installer-owned whisper.cpp v1.9.2/base.en q5_1 bundle, full per-file hash/provenance/license/manifest gates, minimal process environment, and no installed-app downloads.
- [x] Add actual-resource contrast, graph selection pattern, target size, semantic relation, connected case-sensitive identity, Up/Root, motion, mute, Voice grammar, and file-activation regression contracts.
- [ ] Capture and independently review a fresh exact-candidate Dark/Light/dense/nested/Search/list/listening/minimum-window set; earlier v1.1 screenshots are superseded.
- [ ] Validate real Narrator or NVDA behavior, actual Windows text/high-contrast settings, borderless chrome DPI/snap, GPU-backed motion, physical sound output, real microphone capture/transcription, and exact installed/uninstalled voice bytes.
- [x] Run clean exact-artifact/hosted qualification, record the explicit unsigned decision, and publish stable v1.1.0 from the exact qualified commit with deliberately limited public claims.
- [ ] Revalidate the compatibility-dependent Connected combination with a live host and complete the outstanding manual visual, assistive-technology, hardware, and broader-platform follow-up before making any stronger claims.

## Stage 15 — v1.2.0 issue fixes (implementation complete; manual acceptance open)

- [x] Audit issues #1–#9, including comments and supplied visual evidence, and preserve the root causes, tests, independent review, and retrospective in the issue audit.
- [x] Keep direct-child hierarchy truthful, add bounded actual descendant previews through the active provider, refine connectors/folder silhouettes, and organize like file types with deterministic asymmetric placement.
- [x] Keep every admitted on-graph node named at every zoom, position labels around glyphs, reserve HUD space, and preserve full-size synchronized-list reading under extreme density.
- [x] Add first-click Structure entry, right-click Back, bounded clickable Trail history, centered controls, and same-scene/same-node file double-click protection.
- [x] Refine local hover motion, reveal actual Details metadata with atomic accessible text, and synthesize bounded optional interaction sounds with mute/failure parity.
- [x] Make Voice first-click startup reliable, expose microphone/Stop state, complete after two seconds of quiet following detected input, and reject stale capture/cancel/transcription operations.
- [x] Expand deterministic regression coverage for provider work/authority bounds, stale results, crowded input, labels, navigation, metadata, Voice races, and sound bounds.
- [ ] Record native controls/transitions (#3), real microphone/noise (#4), visual/concept acceptance (#5), motion/native readability (#6), physical sound-character listening (#7), and extreme text-scale dense-layout results from the downloadable installer.

The owner authorized v1.2.0 publication for these manual checks. The [release evidence checklist](RELEASE_CHECKLIST.md) defines qualification and the [GitHub Release](https://github.com/nishdel/OmniBrille/releases/tag/v1.2.0) retains the exact released commit, installer hash, CI, and artifact results. Unverified manual behavior is not claimed as tested.

## Unscheduled engineering follow-up

- [x] Correct `NavigationState` to compare Connected opaque targets with ordinal semantics and add a Windows regression covering IDs that differ only by case, including Back/history coherence.
- [ ] Decide whether every advertised Protocol v1 limit must be validated or whether unconsumed fields should be explicitly outside client negotiation, then add focused malformed-info tests. Current validation covers the safety limits OmniBrille consumes but not `MaximumDepth`, snippet/topic/entity/reason bounds, or maximum concurrency. Route through Architecture/Integration and Adversarial Review.
- [ ] Make Context/Hybrid availability reflect negotiated optional capabilities before activation, and add a capability-negative connected regression test. Current code fails closed on request but reports the expected capability absence as a generic connection failure. Route through Architecture/Integration, UX/Accessibility, and independent adversarial review.
- [x] Couple contrast validation to actual application theme resources, including alpha-composited HUD/loading states and text/focus floors.
- [ ] Split `MainWindow`, `ExplorerSession`, and the headless fixture by concern only during future touched-area work. Preserve the single state authority and avoid a standalone refactor campaign.
