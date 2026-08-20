# v1.0 public-release preparation — 2026-08-20

Historical snapshot: this report records the candidate state and release decision at the end of this run. Current source, release documentation, and GitHub state remain authoritative if later evidence differs.

## What this run was meant to do

Turn the existing OmniBrille application into a truthful, understandable, downloadable v1.0.0 for ordinary Windows users: a stable installer, public landing page, real screenshots, consistent release notes, exact artifact validation, and—only if every gate passed—a tag and GitHub Release.

## What actually changed

OmniBrille now has a public-facing README with a plain-language product explanation, direct future v1 download path, four real installed-candidate screenshots, Standalone/Connected boundaries, privacy posture, platform qualification, unsigned-installer warning, limitations, and routes to deeper engineering documentation. The screenshots show Dark and Light Structure, Search, and the synchronized accessible list using non-private data under `C:\Users\Public`.

The existing Inno Setup path now supports stable `1.0.0` versioning and release-note generation. Packaging emits an installer, checksum, release manifest, dependency graph, and notes with consistent identity. The manual release-candidate workflow binds the exact installer bytes to its manifest and requested signing mode, then validates per-user installation, installed product/version/notices, window launch, normal close/relaunch, and uninstall on a fresh artifact-only Windows runner. Project licensing and the complete indexed third-party-notice set are fail-closed release gates.

A redistribution review expanded packaged notices for Avalonia, ANGLE, Inter, .NET/runtime, MicroCom, SkiaSharp/HarfBuzz, System.IO.Pipelines, Tmds.DBus, and NAudio. Small presentation fixes removed corrupted transcript quote glyphs and replaced a hard-coded OmniSorSe RC promise with compatibility-dependent wording. No renderer, graph, layout, navigation, state, protocol, persistence, or Connected behavior was redesigned.

Publication did not occur. The owner explicitly accepted an unsigned v1 release after the initial audit, with prominent SmartScreen/Unknown Publisher disclosure and code signing deferred to a future release. The owner has not selected a project license, so the final clean commit/artifact/tag/release gates remain deliberately blocked.

## Important technical decisions

- **Keep the established Inno Setup package →** it already implements the intended per-user, self-contained Windows deployment → v1 gains stable-version support without a second packaging system.
- **Support Windows x64 Standalone first →** the real interactive preflight host was Windows 10 22H2 x64, while Linux has source build/test evidence only and macOS has none → public claims remain useful and narrow instead of converting CI runners into false platform promises.
- **Describe Connected mode as compatibility-dependent →** current-host qualification was not run and known boundary gaps remain, although the previously validated host emits canonical lowercase IDs → v1 does not promise arbitrary Explorer Protocol hosts or make the deferred opaque-ID comparer defect a Standalone release blocker.
- **Allow an unsigned path only by explicit owner choice →** checksum validation proves byte integrity but not publisher identity → the gate preserves a viable first release while making SmartScreen/Unknown Publisher consequences impossible to hide.
- **Stop at the license boundary →** no repository license grants redistribution rights for OmniBrille itself → no commit, tag, GitHub metadata mutation, or release publication can make an unapproved legal choice on the owner's behalf.

## Validation and confidence

### Verified

- Stable metadata resolves to semantic/product version `1.0.0` and file/assembly version `1.0.0.0`; installer and generated sidecars use the stable filename.
- Release build succeeded with zero warnings/errors. All 191 ordinary tests and 41 Avalonia headless tests passed (232 total). The focused release/packaging set passed 11 tests. `dotnet format --verify-no-changes`, the NuGet vulnerability audit, and `git diff --check` passed.
- Engineering-document validation passed across 30 Markdown files, 101 relative links, and three Mermaid diagrams. Preview-era rollout/templates are now explicitly historical rather than competing release authority.
- The latest local preflight package build completed with Inno Setup 6.7.3: unsigned installer `OmniBrille-1.0.0-win-x64-setup.exe`, 34,784,886 bytes, SHA-256 `79F4D6778B3770A7C65DA9CB8A07E7446FFF55404231E920A18462D036075A87`, and 105,450,259 published runtime bytes. Its manifest, checksum, dependency-graph wording, neutral local-build provenance, and ten-file third-party-license directory were inspected. It intentionally lacks the blocked project `LICENSE` and is not publishable.
- A prior same-visible-binary preflight was installed on Windows 10 22H2 x64 at 125% display scaling. Manual checks exercised Dark/Light, structural Search (`diagram`, two matches), accessible-list synchronization, drill-in, Back, normal close, and relaunch. Installer registration and later uninstall cleanup were verified; the run-owned demo fixture was removed.
- Four original-resolution PNGs were independently checked for complete window chrome/controls, DPI correctness, non-private content, absence of debug/Connected/voice claims, and misleading state. They are bound to their exact earlier preflight hash in screenshot provenance, not falsely to the final artifact.
- The release gate was executed and failed immediately with the intended message requiring a maintainer-approved non-empty `LICENSE`.

### Not verified

- No final clean-commit artifact exists. The hosted release-candidate workflow, validation JSON, final exact-artifact install/interaction/relaunch/uninstall sweep, asset checksum comparison, tag, push, and published GitHub Release were not run.
- No Authenticode certificate is available. The owner explicitly accepted the prominently disclosed unsigned v1.0.0 path on 2026-08-20; code signing remains future work.
- Connected mode against a current real OmniSorSe host, Connected Search success, real microphone/model capture, manual screen-reader use, Windows versions other than the Windows 10 22H2 interactive host, Linux desktop runtime/package, and macOS runtime remain unverified.
- Performance-sensitive production code did not change, so no new benchmark was run. Existing automated performance contracts are not a universal hardware guarantee.

### Inferred

- The v1 Standalone product is technically close to release because the existing application binary passed the full automated suite, real installed preflight use, screenshot review, and uninstall cleanup. This does not make the dirty, unlicensed, unsigned preflight a final release artifact.
- Final screenshots need recapture only if the final visible binary, theme, demo content, version presentation, or supported workflow changes. Packaging a later license alone would not make the current real images misleading, but provenance must continue to distinguish their preflight artifact.

## Problems found

Fixed: stable SemVer was previously rejected by release tooling; generated notes were private-preview-specific; package descriptions presented Standalone as merely an OmniSorSe companion; the public README had no screenshots or real download path; release metadata could overclaim local validation; artifact-only validation did not bind enough manifest fields; redistributed third-party notices were incomplete; and preview documents contradicted the public-release process.

Blocking publication:

1. The project owner must add an approved non-empty root `LICENSE` (MIT was recommended if the intent is open source; otherwise an owner-supplied proprietary license/EULA is required).
2. After that choice, create a clean release commit, push it through the explicit GitHub remote/canonical checkout, pass normal CI and the manual unsigned release-candidate workflow, manually qualify the exact retained artifact, then create and verify tag/release assets.

The GitHub repository is already public and has a public unsigned `v0.8.0-preview.2` prerelease despite the previous no-public-distribution wording and absent project license. The owner should promptly decide, with legal advice if needed, whether to retain, relicense, or withdraw that historical binary. It was not deleted or rewritten during this run.

Recommended GitHub metadata once publication is authorized:

- Description: `A local-first spatial file explorer for Windows—browse a folder as a bounded graph, standalone or with OmniSorSe context.`
- Homepage: `https://github.com/nishdel/OmniBrille/releases/latest`
- Topics: `accessibility`, `avalonia`, `csharp`, `desktop-app`, `dotnet`, `file-explorer`, `filesystem`, `graph-visualization`, `local-first`, `privacy`, `spatial-navigation`, `windows`

Non-blocking follow-up remains documented for the Connected opaque-ID comparer, optional Context-capability UX, protocol-info limit completeness, current OmniSorSe qualification, real microphone validation, assistive-technology validation, broader Windows/client-platform qualification, and production code signing.

## What the agents learned

The false starting assumption was that the project was still private: independent GitHub/API inspection found the repository and an older prerelease already public. Release-artifact, public-presentation, and adversarial specialists were all useful; they found the license/distribution contradiction, incomplete notices, DPI-unaware screenshot cropping, manifest-binding gaps, and platform wording that ordinary tests would not catch. Architecture, Performance, AX, and major Implementation specialists were unnecessary because product architecture and performance code did not change.

Promoted lessons:

- **External release state can contradict repository prose →** independently verified from GitHub and the current docs → promoted into the release checklist's publication/repository-state checks and corrected public/current documentation.
- **Redistribution memory should be executable →** the artifact and adversarial reviews independently found missing runtime notices → promoted into the packaged notice index, release gate, installed-artifact workflow, and focused tests.
- **A package build is not release validation →** local notes previously implied a successful release check → promoted into neutral provenance wording plus exact manifest/hash/signing bindings.
- **Stable-version support needs a real packaging path →** previous scripts/tests accepted only previews → promoted into stable version composition and focused release tests.

Remain candidate: a reusable Per-Monitor-V2 screenshot helper. It prevented a real cropped capture, but independent visual review plus documented provenance/checklist is sufficient evidence for this release; a permanent tool is not yet justified. Rejected: making every known Connected defect a Standalone release blocker, treating hosted `windows-latest` as Windows 11 manual qualification, adding a second packaging format, or creating a broad release-process framework beyond the existing checklist and workflow.

## Documentation and diagrams

Created or materially updated: the public README and screenshots/provenance, stable release notes, packaging guide, release checklist/workflow, compatibility matrix, changelog/roadmap, security/privacy and architecture release posture, historical-preview labels, third-party notices, and this report. Existing architecture Mermaid diagrams remain semantically current because runtime subsystem ownership did not change; no release-specific diagram was warranted. No important v1 claim is known to exist only in the Codex conversation. The unsigned-release decision is now recorded in the checklist, roadmap, and this report; only the owner-controlled project-license decision remains external.

## Repository state

- Repository: OmniBrille audit working copy; its `origin` is the local canonical checkout at `D:\Own Projects\OmniBrille`, not GitHub.
- Branch / HEAD: `main` at `8766e33d9778afbf262356be57dbbe7152eb2a83`.
- Worktree: dirty with the inherited, uncommitted engineering-foundation changes plus this run's v1 release preparation. No pre-existing work was discarded or silently absorbed.
- Commits / tags / push / release: none created; nothing pushed; `v1.0.0` does not exist or resolve to a downloadable application.
- GitHub auth was available by final audit. The owner subsequently authorized the unsigned policy, but authority to choose a project license remains absent. Repository description, homepage, topics, and detected license were not mutated before the release gate.
- Schema / Explorer Protocol / persistence format / public programming interfaces: unchanged.
- Compatibility assumptions: narrowed and made explicit; no protocol-version claim was widened.
- Intentional product behavior: only presentation copy/quote rendering changed. Version and package metadata changed to the v1 candidate. Graph, layout, renderer, navigation, state, accessibility behavior, and filesystem/Connected semantics were preserved.

## Bottom line

Status: Blocked

OmniBrille is not yet ready for ordinary people to download as v1.0.0, and no v1 application is published. The Standalone application, public presentation, stable packaging path, screenshots, unsigned-release authorization, and validation gates are safe to build on, but the owner must first choose the project license. The next run should record that license, rebuild from a clean commit, complete exact-artifact and hosted lifecycle qualification, then tag, publish, attach all verified assets, set the recommended GitHub metadata, and verify the public download.
