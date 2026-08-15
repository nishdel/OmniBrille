# Changelog

All notable OmniBrille changes are recorded here. The project uses SemVer-compatible pre-release versions; dates identify validated engineering checkpoints, not public availability.

## [Unreleased]

### Planned

- Private-preview feedback and support triage.
- Maintainer license decision before any public distribution.
- Production Authenticode signing when externally managed credentials are available.

## [0.6.0-preview.3] - 2026-08-15

### Added

- Checksum-bound generated tester notes and privacy-conscious feedback/controlled-rollout guidance.
- User-invoked sanitized diagnostics copy action with explicit exclusion of paths, queries, content, endpoints, grants, tokens, and session/node identifiers.
- Fresh hosted-Windows artifact-only install, installed-window, integrity, and uninstall lifecycle gate with a retained validation record.

### Changed

- Private-preview candidate artifacts now retain for 90 days and carry optional GitHub workflow identity in schema-v2 release manifests.
- Exact-artifact policy explicitly treats SHA-256 as identifying one build rather than every rebuild of a commit.

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
