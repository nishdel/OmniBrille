# Changelog

All notable OmniBrille changes are recorded here. The project uses SemVer-compatible pre-release versions; dates identify validated engineering checkpoints, not public availability.

## [Unreleased]

- No changes yet.

## [1.0.0] - 2026-08-20

Prepared first stable public release; not yet published. A public `0.8.0-preview.2` prerelease already existed on GitHub. The v1.0.0 candidate establishes the intended Windows x64 Standalone contract, MIT project license, and a pinned project-built SkiaSharp native dependency with unused Adobe DNG/RAW support excluded. Exact committed-installer, visual, hosted lifecycle, adversarial, and publication qualification remain.

### Added

- Public project landing page with a direct Windows x64 download path, honest support contract, real installed-candidate screenshots, privacy summary, and developer-documentation routes.
- Stable-version release notes plus exact installer manifest, SHA-256, dependency inventory, and artifact-only validation record support.
- Complete packaged notices for Avalonia, Inter, ANGLE, SkiaSharp/HarfBuzzSharp, MicroCom, Tmds.DBus, NAudio, .NET, and System.IO.Pipelines redistributed by the self-contained Windows runtime.
- Release validation for installed executable version metadata, normal close/relaunch, required project/distribution licenses, and signed-artifact status when signing is requested.
- MIT project licensing in source, package metadata, release metadata, and the installed application, with separate bundled third-party terms preserved.

### Changed

- Version/product/installer metadata now identify stable `1.0.0` and describe OmniBrille as a standalone-first local spatial file explorer.
- The established Inno Setup workflow is documented and gated for a public release candidate rather than only private previews.
- Public claims limit downloadable/interactive support to Windows x64 Standalone. Connected mode is compatibility-dependent; voice lacks real-microphone validation; automated accessibility coverage is not described as certification.
- Avalonia's managed SkiaSharp API is unchanged, but the DNG-bearing official Win32 native runtime is excluded in favor of a distinctly named, hash-bound DNG-free package built from pinned upstream source.

### Fixed

- Corrected corrupted smart-quote glyphs around the visible voice transcript.
- Removed a user-facing hard-coded OmniSorSe v2.5 RC requirement in favor of the compatibility matrix.

## [0.8.0-preview.2] - 2026-08-17

### Changed

- Replaced the overflowing single-row desktop header with a responsive two-row HUD that preserves practical access to provider, mode, graph, accessibility, and settings controls at the supported minimum window size.
- Refined first run into a concise selected-root Standalone explanation with a clear `Choose folder to explore` action and an honest OmniSorSe requirement for Context and Hybrid.
- Added distinct Structure-empty and Search-no-match states. Search no longer offers an invalid focus action, and item details recede while the compact Search result surface is active.
- Moved sparse-scene guidance away from the focused node so empty Structure, Context, and Hybrid scenes retain a deliberate graph-first composition.

### Accessibility

- Search names and help now describe the active authority accurately: selected-root Standalone Search or session-scoped OmniSorSe Search.
- First-run guidance, empty states, recovery controls, and the revised HUD remain keyboard reachable and have concise automation metadata.

### Security

- Explorer Protocol v1, provider authority, handoff security, session identity, voice architecture, graph budgets, and privacy behavior are unchanged.

## [0.8.0-preview.1] - 2026-08-16

### Added

- A deliberate connected `Structure | Context | Hybrid` exploration surface that shows structural location and server-authored relationships in one bounded scene.
- Provider-independent Hybrid composition with shared-node deduplication, explicit structural/contextual/both roles, deterministic budget allocation, and a dedicated stable layout.
- Hybrid-aware Back/refocus history, Context filters that leave Structure intact, `Ctrl+3`, accessible graph/list role descriptions, and compact combined details.
- Maximum-density Hybrid domain/headless fixtures plus full/reduced/search renderer diagnostics.

### Changed

- The primary connected navigation model now shares one mode/history/search/details authority across Structure, Context, and Hybrid.
- Hybrid retains one bounded same-session Structure snapshot and performs at most one parent read for an external related-node refocus, preserving real structural orientation when a file-centered Context response omits containment edges.
- Relationship reason, ranking strength, evidence, and provenance are exposed together on the top-level accessible details surface.
- Preview metadata advances to `0.8.0-preview.1` because Hybrid is a material user-facing capability.

### Security

- Hybrid composes existing Explorer Protocol v1 snapshots only. It adds no protocol operation, filesystem fallback, semantic inference, persistent identity, bearer-token storage, or telemetry.
- Standalone Hybrid fails closed with an OmniSorSe-required explanation. Session replacement invalidates Hybrid nodes, relationships, and Back entries.

## [0.7.0-preview.1] - 2026-08-15

### Added

- Optional Windows push-to-talk capture with an explicit 45-second bound, visible microphone state, `Ctrl+Shift+Space`, cancellation, and reduced-motion/effects presentation.
- Replaceable local speech contracts plus a user-provided whisper.cpp CLI/GGML provider with lazy validation, bounded process/output handling, and guaranteed temporary-audio cleanup.
- Deterministic English navigation/mode/theme/UI command registry with exact visible-node matching and conservative Search fallback.
- Voice-specific automation/live regions, sanitized timing/classification diagnostics, provider-generation stale-result protection, and model/microphone-independent unit/headless coverage.

### Changed

- Standalone voice queries now use the existing bounded structural Search; connected voice queries use the existing OmniSorSe Explorer Protocol v1 Search path. No semantic or LLM pipeline was added.
- Windows packaging includes only small NAudio WinMM capture assemblies. whisper.cpp, models, and audio are neither bundled nor downloaded.

### Security

- Voice is disabled by default, never listens in the background, never logs/persists transcript text, and discards in-memory/temporary audio after each utterance.
- Speech process invocation uses structured arguments with explicit paths, bounded output/time, cancellation process-tree termination, and release gates that reject audio/model/runtime artifacts.

## [0.6.0-preview.3] - 2026-08-15

### Added

- Checksum-bound generated tester notes and privacy-conscious feedback/controlled-rollout guidance.
- User-invoked sanitized diagnostics copy action with explicit exclusion of paths, queries, content, endpoints, grants, tokens, and session/node identifiers.
- Fresh hosted-Windows artifact-only install, installed-window, integrity, and uninstall lifecycle gate with a retained validation record.

### Changed

- Private-preview candidate artifacts now retain for 90 days and carry optional GitHub workflow identity in schema-v2 release manifests.
- Exact-artifact policy explicitly treats SHA-256 as identifying one build rather than every rebuild of a commit.
- Structure/Context radio automation now performs the same mode transition as pointer and keyboard input, and Search result automation labels no longer expose opaque session identifiers.

### Security

- Unexpected failure/transport values are reduced to bounded categories before diagnostics export.
- The hosted lifecycle job starts without a source checkout and independently compares installer, sidecar, and manifest hashes.

## [0.6.0-preview.2] - 2026-08-14

### Added

- Release-quality provisional blue/cyan spatial-navigation icon with complete Windows ICO sizes.
- Compatibility matrix, release checklist, private-preview notes template, and release-oriented security/privacy guidance.
- Deterministic release verification, SHA-256 sidecar, release manifest, and sanitized runtime dependency manifest.
- Manual private-preview workflow with explicit unsigned or fail-closed signed paths.

### Changed

- Modernized GitHub Actions away from Node.js 20 action runtimes.
- Updated Avalonia runtime/headless packages from 12.1.0 to the compatible 12.1.1 patch.
- Normalized assembly, executable, installer, and informational version metadata.

### Security

- Signing credentials remain outside Git; a signed workflow imports a short-lived certificate into the runner user store and removes it afterward.
- Release checks reject packaged debug/source/test/database/key artifacts and developer-path leakage.

## [0.6.0-preview.1] - 2026-08-14

- Added the reproducible per-user Windows installer and locator-compatible installed companion workflow.
- Added Context relationship filters, clearer provenance inspection, empty states, and Stage 6 lifecycle hardening.

## [0.5.0-preview] - 2026-08-14

- Added real OmniSorSe v2.5 RC handoff consumption and server-authored Structure/Context navigation over Explorer Protocol v1.

## [0.3.0-preview] - 2026-08-13

- Added renderer performance budgets, accessibility automation peers, synchronized list navigation, Windows/Ubuntu CI, and Context-rendering limits.

## [0.1.0-preview] - 2026-08-12

- Established the independent application architecture and working standalone Structure vertical slice.
