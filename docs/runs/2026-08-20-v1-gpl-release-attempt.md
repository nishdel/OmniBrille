# v1.0 GPL release attempt — 2026-08-20

Historical snapshot: this report records why publication stopped after the owner selected GPL-3.0-only and authorized an unsigned installer. Current source and release documentation remain authoritative if later review resolves the blocker.

## What this run was meant to do

Record the project license and unsigned-release decision, integrate the prepared engineering/release work, qualify the exact committed Windows installer, and publish OmniBrille v1.0.0 only if every release gate passed.

## What actually changed

The standard GPL version 3 text was added as the root `LICENSE`, and `GPL-3.0-only` was propagated to project, manifest, dependency-graph, installer-content, release-note, README, and verification metadata. The owner-approved unsigned policy remains explicit: Windows may show SmartScreen or Unknown Publisher, and SHA-256 identifies exact bytes without authenticating the publisher.

Publication stopped during the required third-party compatibility review. The exact SkiaSharp 3.119.4 Windows native binary used by the candidate contains Adobe DNG SDK code, and its byte-matched upstream notices include the Adobe DNG SDK License Agreement. The repository cannot establish that conveying that combined binary is compatible with the owner's GPL-3.0-only choice. A fail-closed release check and current documentation now preserve this boundary.

No release commit, final artifact, tag, GitHub Release, or GitHub metadata mutation was made.

## Important technical decisions

- **Keep GPL-3.0-only as the project decision →** the owner explicitly selected it → source and release metadata now record the exact SPDX expression, but that choice does not relicense third-party components.
- **Block rather than infer compatibility →** the executable contains the separately licensed DNG SDK and its notice imposes additional terms → qualified review or a DNG-free Skia native asset is required before distribution.
- **Make the known blocker executable →** a future release run could otherwise pass presence-only notice checks → `verify-release.ps1` now fails with the concrete remediation boundary.
- **Do not build, tag, push, or publish a substitute candidate →** exact-artifact qualification must follow a clean intended release commit → the older preflight installer and checksum remain non-release evidence only.

## Validation and confidence

### Verified

- Root `LICENSE` is the complete GNU GPL version 3 text; central package metadata says `GPL-3.0-only`.
- The checked-in SkiaSharp/HarfBuzz upstream notice is byte-identical to the notice from the resolved `SkiaSharp.NativeAssets.Win32` package and contains the DNG SDK License Agreement.
- The candidate `libSkiaSharp.dll` contains numerous compiled `dng_*` and DNG class symbols; the notice is not merely unrelated inventory.
- Independent release-artifact review reproduced the dependency and terms finding.
- Engineering-document links/fences, focused packaging/release tests, formatting, and `git diff --check` passed before the blocker was recorded; affected focused validation was rerun afterward.

### Not verified

- No qualified legal opinion established GPL compatibility.
- No DNG-free SkiaSharp Windows native package/build was produced or renderer-qualified.
- No exact committed v1 installer was built, installed, interactively exercised, relaunched, uninstalled, or frozen for publication.
- Hosted release-candidate validation, final checksum comparison, tag/release publication, public download verification, and GitHub metadata updates were not performed.

### Inferred

- The prepared Standalone behavior and reviewed screenshots remain representative because the GPL metadata/check work did not change runtime rendering or interactions. They do not substitute for final exact-artifact qualification.
- Replacing or rebuilding the Skia native asset may be feasible, but it is renderer/dependency work outside this run and requires its own High-risk validation.

## Problems found

Fixed: the project had no owner-selected source license; the unsigned decision and checksum limitation are now durable and consistent. The release gate now refuses the known unresolved dependency-license state.

Blocker: current Windows packaging combines OmniBrille with an Adobe DNG SDK-bearing Skia native binary whose terms have not been cleared for GPL-3.0-only conveyance.

Deferred: obtain qualified license review or produce a DNG-free SkiaSharp native asset; then rerun the complete clean-commit, hosted, exact-artifact, manual Standalone, checksum, adversarial, and publication gates. The historical public `v0.8.0-preview.2` binary still deserves owner/legal review and was not altered.

## What the agents learned

The initial assumption that complete notice collection was enough for license readiness was wrong. Release-artifact and adversarial review were necessary; Architecture, UX, Performance, and product Implementation specialists were unnecessary until a native dependency replacement is proposed.

**Promoted:** inspect known bundled native-code terms, not only notice presence, for the exact distribution. Evidence is the byte-matched notice plus compiled DNG symbols and independent reproduction. Durable form: a narrowly scoped fail-closed release check and packaging/checklist documentation.

**Remain candidate:** general automated license-policy classification. License compatibility is contextual and a broad scanner could produce false assurance or noisy failures; the repository needs qualified review, not a generic allowlist.

**Rejected:** treating an unresolved legal question as compatible because the package is commonly distributed, or publishing first and correcting later. Neither satisfies the owner-directed gate.

## Documentation and diagrams

Updated: project-license metadata, public download status, packaging/release checklist, roadmap, compatibility/current architecture status, and this retained report. Existing Mermaid diagrams remain current because architecture and runtime behavior did not change. The external legal determination required to clear the blocker does not exist in this conversation or repository.

## Repository state

- Repository: OmniBrille audit worktree.
- Branch / HEAD: `main` advanced locally from baseline `8766e33d9778afbf262356be57dbbe7152eb2a83`; final HEAD is the follow-up commit containing this finalized report and byte-preserved vendor notices.
- Worktree: the inherited engineering-foundation and v1 preparation changes plus this run's GPL/blocker changes are integrated in local commit `c692425` and the final follow-up; no pre-existing work was discarded.
- Tags / push / release: no tag, push, or release; `v1.0.0` was not created or published.
- GitHub metadata: not updated.
- Schema, protocol, persistence format, and public APIs: unchanged.
- Intentional product behavior: none. Presentation copy/version/package metadata from the preparation work remain candidate changes.

## Bottom line

Status: Blocked

OmniBrille v1.0.0 was not published, and no final installer or trustworthy final SHA-256 exists. The GPL and unsigned decisions are recorded, and the preparation work remains useful, but ordinary users should not be directed to a v1 download until the Skia/DNG distribution question is resolved and the exact committed artifact completes every release gate.
