# v1.0 MIT release attempt — 2026-08-20

Historical snapshot: this report records the state after the owner superseded the unshipped GPL choice with MIT. Current source and release documentation remain authoritative if a later owner decision clears the remaining Adobe DNG SDK gate.

## What this run was meant to do

Transition the prepared v1.0.0 release from GPL-3.0-only to MIT, reassess the SkiaSharp-bundled Adobe DNG SDK terms under that actual license model, qualify the exact installer, and publish only if every release gate passed.

## What actually changed

The project now carries the standard MIT License using the repository's established `Copyright (c) 2026 OmniBrille Contributors` attribution. Project, package, manifest, dependency-graph, installed-license, README, release-note, architecture, roadmap, and checklist metadata identify OmniBrille project code as MIT while preserving separate third-party terms.

The earlier GPL “further restrictions” conflict no longer applies: MIT does not require bundled third-party components to be relicensed MIT. The actual SkiaSharp 3.119.4 native Windows binary still contains Adobe DNG SDK code. Its agreement expressly grants distribution and sublicensing for any purpose, requires human-readable notice preservation, and states a defense/indemnity condition for distribution in a “commercial product.” Because that term is undefined, repository evidence cannot decide whether it applies to this distribution. Publication stopped pending explicit owner acceptance, qualified review, or a DNG-free asset.

Release metadata was strengthened rather than relaxed. Manifest schema 3 calls the MIT field `projectLicenseExpression`, binds the reviewed Skia/DNG notice path and SHA-256, and the hosted installed-artifact gate verifies the same bytes and material notice clauses.

## Important technical decisions

- **Use standard MIT with the existing project attribution →** the owner changed the project-license decision and repository metadata already names OmniBrille Contributors → no new legal identity was invented.
- **Separate project and dependency licenses →** MIT covers OmniBrille code, not every installed binary → public text and schema 3 avoid calling the whole distribution MIT-only.
- **Remove only the GPL-specific incompatibility claim →** Adobe separately grants distribution/sublicensing but retains its own obligations → full DNG terms remain indexed, packaged, installed, and hash-bound.
- **Keep publication fail-closed →** “commercial product” is undefined and price alone does not resolve it → owner acceptance, qualified review, or DNG-free assets are required before exact-artifact/publication work.

## Validation and confidence

### Verified

- Root `LICENSE` contains the standard MIT grant, notice-preservation condition, warranty disclaimer, and established copyright attribution.
- Current project, release-script, workflow, test, README, and documentation authorities use MIT rather than GPL for OmniBrille code.
- The actual win-x64 Skia native binary contains DNG code; the 139,775-byte vendor notice is byte-identical to the resolved package and includes the applicable agreement.
- Manifest schema 3 distinguishes `projectLicenseExpression` and binds the reviewed DNG notice path/hash.
- Release build passed with zero warnings/errors after rerunning outside the workspace sandbox required by Avalonia's telemetry-log path.
- All 232 automated tests passed: 191 ordinary and 41 Avalonia headless/UI tests. Focused packaging/release tests passed 11/11.
- Formatting, `git diff --check`, engineering-document validation, and NuGet vulnerability audit passed. Documentation validation covered 32 Markdown files, 105 relative links, and 3 Mermaid diagrams.
- The release command fails at the intended explicit DNG owner/qualified-review gate.

### Not verified

- The owner has not explicitly accepted the Adobe DNG SDK agreement's conditional commercial-product indemnity, and no qualified legal classification was obtained.
- No final exact committed MIT installer was generated, installed, interactively exercised, relaunched, uninstalled, or frozen for release.
- Hosted candidate validation, final checksums, tag, push, GitHub Release/assets, metadata updates, and public download verification were not performed.
- No new manual visual, assistive-technology, performance, Connected-host, microphone, Linux-runtime, or macOS validation was claimed.

### Inferred

- No renderer rebuild is technically required merely to combine MIT-licensed OmniBrille code with the separately licensed DNG-bearing native asset, provided all applicable DNG obligations are accepted and preserved.
- Existing screenshots remain representative because no runtime visual or interaction behavior changed; they are not final-artifact evidence.

## Problems found

Fixed: the GPL project license and GPL-specific release assumptions were replaced consistently with MIT. The manifest now distinguishes project licensing from the distribution's third-party terms. Notice validation compares packaged bytes with reviewed sources, and the installed DNG notice is manifest-bound.

Blocker: repository evidence cannot determine whether Adobe's undefined “commercial product” indemnity condition applies. The owner must explicitly accept the DNG agreement for the intended distribution, obtain qualified advice, or authorize a DNG-free native dependency path.

Deferred: all exact-artifact and publication gates; the historical public `v0.8.0-preview.2` binary remains unchanged and still deserves separate owner/legal review.

## What the agents learned

The initial assumption that MIT alone would fully clear the dependency question was too broad. Release-artifact review correctly separated GPL compatibility from continuing DNG obligations; adversarial review caught the unsupported claim that a zero-price GitHub download is necessarily noncommercial.

**Promoted:** project-license metadata must be explicitly scoped when the binary carries separately licensed native code. Evidence is the misleading ambiguity of schema 2 plus the compiled DNG component. Durable form: schema 3 `projectLicenseExpression` and manifest-bound notice evidence.

**Remain candidate:** a reusable legal-acceptance data model. One conditional native license does not yet justify a general policy framework; the manual checklist and focused fail-closed gate are sufficient.

**Rejected:** deleting DNG checks because MIT is permissive, treating NuGet's top-level MIT expression as complete native-component evidence, or inferring “noncommercial” from zero price.

## Documentation and diagrams

Updated: README, MIT license/current metadata, changelog, architecture release status, packaging, release notes, roadmap/checklist, historical-preview qualification, notice index, and this report. The three Mermaid diagrams remain current because runtime architecture did not change. The missing owner/qualified determination is explicitly outside repository knowledge rather than hidden in this conversation.

## Repository state

- Repository: OmniBrille audit worktree.
- Branch: `main`, advanced locally from `7fe84736dea30498d633ea6e7365aed725cab684` by the commit containing this report.
- Worktree: clean after the MIT transition commit; no pre-existing work was discarded.
- Push / tag / release: none; `v1.0.0` was not created or published.
- GitHub metadata: not updated.
- Product schema, Explorer Protocol, persistence format, and public APIs: unchanged. Release-manifest schema advanced from 2 to 3 before v1 publication.
- Intentional product behavior: none. Licensing, packaging metadata, validation, and public wording only.

## Bottom line

Status: Blocked

The MIT transition is technically complete and the former GPL-specific DNG conflict is resolved, but OmniBrille v1.0.0 is not published. The owner must explicitly accept the separately applicable Adobe DNG SDK agreement and its conditional commercial-product indemnity, obtain qualified clearance, or select a DNG-free path before the exact committed installer and publication gates can run.
