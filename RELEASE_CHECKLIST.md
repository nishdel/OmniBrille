# v1.1.0 public-release checklist

Checkboxes are maintainer gates. Automation does not mark manual checks complete, and a source build is not installed-artifact validation. The immutable v1.0.0 outcome remains in the [v1.0.0 owner report](docs/runs/2026-08-21-v1.0.0-public-release.md).

## Release authority

- [x] Use a new `v1.1.0` release; do not move `v1.0.0` or replace its checksum-bound assets.
- [x] Limit this release to visual shell/presentation changes. Provider, filesystem, Search, protocol, persistence, privacy, and scene-admission contracts remain unchanged.
- [ ] The release commit is identified, independently reviewed, clean, and equals GitHub `main`.
- [x] Source/package version authorities, executable, installer, changelog, compatibility, release notes, screenshot provenance, and candidate artifact names say `1.1.0`; README keeps the published `1.0.0` download explicit until v1.1 publication.
- [x] MIT project licensing and the separate installed third-party terms remain unchanged.
- [x] The pinned DNG-free native renderer package/provenance gate remains unchanged and fail-closed.
- [ ] The owner has explicitly selected signed or unsigned publication for v1.1.0. If unsigned, the exact installer/application report `NotSigned` and public notes retain the SmartScreen/Unknown Publisher disclosure.

## Visual-convergence gate

- [x] The graph owns the full client area; no reserved application toolbar/footer remains.
- [x] Floating Root/Back, Current Focus, modes, Search, provider/theme/list/settings, zoom, Voice, status, and secondary panels use one reusable visual system.
- [x] Search is collapsed initially, expands/focuses on click or `Ctrl+F`, and dismisses without duplicating session query/result state.
- [x] Focus, selected/immediate, secondary, and ambient hierarchy matches the retained original concepts while using only admitted nodes/edges and deterministic presentation bands.
- [x] Dark, Light, dense, nested/details, Search-expanded, loading, and minimum-size source-candidate states were inspected on the interactive Windows host.
- [ ] Final corrected screenshots bind one exact installed candidate and non-private data and are independently reviewed at full size.
- [x] Actual Windows UIA exposure, keyboard traversal, visible focus, reduced motion/effects, and 125% display scaling are recorded separately from automated 100/125/150/200% text-scale coverage; no screen-reader use is claimed.
- [x] Same-host representative renderer timings, allocations, accepted-label changes, and bounded cache capacities were compared with the pre-change baseline; no material unexplained regression remains.

## Automated release gate

- [ ] Run `.\build\verify-release.ps1` from the exact clean Windows release commit.
- [ ] Engineering-document validation, restore, format, Release build/analyzers, all tests, and NuGet vulnerability audit pass.
- [ ] The release-candidate workflow succeeds for the selected signing mode on the exact release commit.
- [ ] Installer, `.sha256`, manifest, dependency graph, generated notes, native proof, and hosted validation JSON are retained together.
- [ ] Independent, sidecar, manifest, and uploaded SHA-256 values all agree.
- [ ] The artifact-only gate passes version/signature policy, install, first launch, normal close/relaunch, registration, uninstall, and cleanup.

## Exact installed Windows candidate

- [ ] Record the Windows version, display scaling, and whether validation used an interactive host/VM or hosted runner.
- [ ] Fresh current-user install succeeds without a separate .NET installation or administrator access.
- [ ] Start Menu launch opens Standalone with no filesystem content preloaded.
- [ ] Selecting the non-private demo root exercises Structure, drill-down, Back, Search/result focus, details, Dark/Light, reduced settings, and accessible list.
- [ ] The final DNG-free renderer draws graph lines, outlined glyphs, labels, floating controls, loading aperture/data rain, and both themes without an obvious regression.
- [ ] Close/relaunch work and uninstall removes installer-owned files, shortcuts, and registration without deleting demo/user data.
- [ ] Exact installer/application signature status matches the recorded owner decision.

## Optional capabilities (do not block the Standalone contract)

- [ ] Connected mode is revalidated against the exact claimed OmniSorSe host before any claim beyond compatibility-dependent status.
- [ ] Real microphone hardware plus user-provided runtime/model is exercised before any validated-Voice claim.
- [ ] Real screen-reader evidence exists before any accessibility certification claim.
- [ ] Linux/macOS package and interactive evidence exists before any runtime-support claim.

## Privacy and public truthfulness

- [ ] Installed files contain project/dependency licenses but no PDB/source/test/database/key/audio/model material, private content, or developer paths.
- [ ] README, screenshots, generated notes, release body, platform/Connected/voice/accessibility/signing status, and downloadable files describe the same artifact.
- [ ] No telemetry, cloud upload, recorder/indexer service, auto-start, file mutation, or updater was introduced.
- [ ] Independent adversarial review finds no blocker and distinguishes verified, inferred, and unverified claims.

## Publication

- [ ] Push the exact release commit to `main` normally and wait for required CI.
- [ ] Create annotated tag `v1.1.0` on that exact commit and push normally.
- [ ] Create non-prerelease GitHub Release `OmniBrille 1.1.0` with reviewed generated notes.
- [ ] Attach only the exact validated installer, checksum, manifest, dependency graph, and notes; never rebuild after validation.
- [ ] Verify the public page, tag, assets, direct download, checksum, repository metadata, and README links.
- [ ] Retain the owner report/retrospective as historical evidence without rewriting the v1.0.0 record.
