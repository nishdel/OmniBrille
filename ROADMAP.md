# OmniBrille roadmap

Status language is intentionally conservative: checked items exist in the application or its validated engineering foundation.

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

## Stage 9 — Local push-to-talk voice Search and navigation (architecture complete; hardware gate outstanding)

- [x] Optional bounded Windows push-to-talk capture with explicit listening/transcribing/cancel states and no background service.
- [x] Replaceable local speech provider; user-provided whisper.cpp/GGML setup with no mandatory download or bundled model.
- [x] Deterministic English navigation/mode/theme/UI command grammar with safe ambiguity handling and no LLM.
- [x] Standalone voice queries use structural Search; connected voice queries use the existing OmniSorSe Search provider and never create relationships.
- [x] Provider-generation stale-result rejection, temporary-audio cleanup, privacy-safe diagnostics, accessibility, reduced motion/effects, and fake-provider/headless tests.
- [ ] Real Windows microphone + local model command/Search smoke on available hardware.
- [x] Validated Stage 9 installer/private-preview workflow; controlled voice tester rollout remains deferred until real microphone hardware validation.

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

## Stage 12 — v1.0.0 first stable public release (release candidate in progress)

- [x] Define a Windows x64 Standalone-first public contract without widening renderer, protocol, persistence, or product behavior.
- [x] Add stable-version packaging support, public release notes, exact-artifact metadata, dependency notices, and stronger installed lifecycle checks.
- [x] Capture and independently review real installed preflight-candidate screenshots using non-private demo data; recapture is required only if the final visible binary or demo presentation changes.
- [x] Record the MIT License in source and release metadata; the exact installed-artifact copy remains part of final qualification.
- [x] Record explicit owner acceptance of a prominently disclosed unsigned v1.0.0; add Authenticode code signing in a future release.
- [ ] Validate the exact release-commit artifact through local interaction and the hosted artifact-only workflow.
- [ ] Complete independent release review, push the release commit/tag, publish the GitHub Release and metadata, and verify the public download.
- [ ] Record explicit owner acceptance or qualified review of the SkiaSharp-bundled Adobe DNG SDK agreement, including its undefined “commercial product” indemnity condition, or use a DNG-free native asset. Preserve the complete human-readable agreement in every case where DNG remains compiled in.
- [ ] Treat Connected-mode broad compatibility, real voice hardware, interactive Linux/macOS, and screen-reader certification as follow-up unless separately validated.

## Unscheduled engineering follow-up

- [ ] Correct `NavigationState` to compare Connected opaque targets with ordinal semantics and add a Windows regression covering IDs that differ only by case, including Back/history coherence. Current provider/session behavior can apply the new scene while retaining the old target. Route through Architecture/Integration, Implementation, and independent adversarial review.
- [ ] Decide whether every advertised Protocol v1 limit must be validated or whether unconsumed fields should be explicitly outside client negotiation, then add focused malformed-info tests. Current validation covers the safety limits OmniBrille consumes but not `MaximumDepth`, snippet/topic/entity/reason bounds, or maximum concurrency. Route through Architecture/Integration and Adversarial Review.
- [ ] Make Context/Hybrid availability reflect negotiated optional capabilities before activation, and add a capability-negative connected regression test. Current code fails closed on request but reports the expected capability absence as a generic connection failure. Route through Architecture/Integration, UX/Accessibility, and independent adversarial review.
- [ ] Couple contrast validation to actual application theme resources if a reliable low-maintenance test seam can be established; current contrast math tests use separately declared literals.
- [ ] Split `MainWindow`, `ExplorerSession`, and the headless fixture by concern only during future touched-area work. Preserve the single state authority and avoid a standalone refactor campaign.
