# v1.0.0 public-release checklist

Checkboxes are maintainer gates. Automation does not mark manual checks complete, and a source build is not an installed-artifact validation.

Completed for release commit `53f787b4489f487fa5dcf86af227510fc4ebc029` and published installer SHA-256 `67443C616B82E066199D39EF7EDBD3DCE68B7E66BAEB26E25872E30BE0F347BF`. Evidence and intentionally unverified boundaries are retained in the [v1.0.0 owner report](docs/runs/2026-08-21-v1.0.0-public-release.md).

## Release authority

- [x] The release commit is identified, reviewed, and clean; `main` equals GitHub `main`.
- [x] `Directory.Build.props`, installer fallback, executable metadata, changelog, compatibility matrix, README, release notes, and artifact names all say `1.0.0`.
- [x] The project owner selected MIT and the authoritative root `LICENSE` plus package metadata record that exact expression.
- [x] The exact installed application contains the same MIT license text and its manifest records `MIT` plus the release-commit source URL.
- [x] Redistributed runtime licenses/notices were reviewed. The project-built SkiaSharp native asset excludes DNG/RAW; its derived notice omits only the unfetched/unlinked DNG SDK and RAW-only PIEX sections while retaining all other upstream notices conservatively.
- [x] The exact tracked DNG-free package, native DLL hash, provenance, unsigned signature state, and exclusion checks remain bound through final restore, publish, manifest, install, launch, and uninstall qualification.
- [x] Exact v1.0 screenshots were captured from the installed release candidate using non-private demo data and independently reviewed.
- [x] Any release blocker found by the checklist is fixed and the exact artifact is rebuilt; the gate is never lowered to fit an artifact.

## Automated release gate

- [x] Run `.\build\verify-release.ps1` from a clean Windows checkout.
- [x] Engineering-document validation, restore, format, Release build/analyzers, all tests, and NuGet vulnerability audit pass.
- [x] The manual release-candidate workflow succeeds for the intended signing mode on the release commit.
- [x] Installer, `.sha256`, manifest, dependency inventory, generated release notes, and hosted validation JSON are retained together.
- [x] Independent, sidecar, manifest, and published-asset SHA-256 values all agree.
- [x] The hosted artifact-only gate passes version metadata, signature policy, install, first launch, normal close, relaunch, registration, uninstall, and cleanup.

## Signing and download trust

- [x] Signed-mode publisher/timestamp validation is not applicable to the authorized unsigned v1.0.0; the exact installer and installed executable report `NotSigned`.
- [x] The owner explicitly accepted an unsigned v1.0.0 on 2026-08-20; README and release notes prominently disclose Unknown Publisher / SmartScreen risk. Code signing remains future work.
- [x] The GitHub Release is the only advertised download location; no unrelated mirror is presented as authoritative.
- [x] The published installer downloads successfully and its checksum matches the independently retained value.

## Exact installed Windows candidate

- [x] Record the Windows version and whether validation used a genuine clean VM, hosted runner, or development host.
- [x] Fresh install succeeds as a normal current user without a separate .NET installation.
- [x] Start Menu launch opens OmniBrille in Standalone with no filesystem content preloaded.
- [x] Selecting the non-private demo root loads Structure and permits drill-down and Back.
- [x] Name/path Search, result focus, file/folder details, Light/Dark, reduced motion/effects, and the accessible list are exercised.
- [x] Close and relaunch work; safe visual preferences behave according to policy.
- [x] OmniBrille starts and non-voice Standalone works with voice disabled and without whisper.cpp/model files.
- [x] Uninstall removes installer-owned files, shortcut, and registration without deleting demo/user content or retained preferences.

## Optional capabilities (do not block the Standalone contract)

- [ ] If Connected mode is advertised beyond compatibility-dependent preview status, validate the exact claimed OmniSorSe host: discovery/handoff, roots, Structure, Search, details, Context, Hybrid, filters, refocus, disconnect, and Back.
- [ ] If voice is advertised as validated, exercise a real Windows microphone and user-supplied local model, cancellation, permission/hardware failure, and cleanup. Otherwise keep it outside the supported v1.0 contract.
- [ ] Any accessibility claim beyond automated keyboard/list/automation coverage has matching manual assistive-technology evidence.
- [ ] Any Linux/macOS runtime claim has matching packaging and interactive evidence.

## Artifact, privacy, and public presentation

- [x] The installed app contains the project license and required third-party notices but no PDB/source/test files, logs, databases, screenshots, user content, development paths, keys, tokens, unexpected OmniSorSe binary, raw/test audio, whisper runtime, or GGML model.
- [x] Automated unit/headless review verifies that `Copy safe diagnostics` omits representative paths, filenames, queries, endpoints, grants, opaque IDs, tokens, and voice-model/transcript secrets; the clipboard action was not manually repeated on the final installer.
- [x] No telemetry, cloud upload, background recorder/indexer/service, auto-start, file mutation, or auto-update behavior is introduced.
- [x] README, screenshots, generated notes, GitHub release body, platform statement, Connected/voice status, signing state, and downloadable files describe the same product.
- [x] GitHub description, homepage, topics, and security-reporting route are reviewed.

## Publication and post-publication verification

- [x] Independent adversarial review finds no remaining blocker and distinguishes verified, inferred, and unverified claims.
- [x] Create a release commit on `main`; push normally and wait for required CI.
- [x] Create annotated tag `v1.0.0` on that exact commit and push it normally.
- [x] Create a non-prerelease GitHub Release titled `OmniBrille 1.0.0`, using the reviewed notes.
- [x] Attach the exact validated installer, checksum, manifest, dependency inventory, and generated notes; do not rebuild after validation.
- [x] Verify the public page, assets, download, checksum, repository metadata, and README links from GitHub.
- [x] Retain the owner report and retrospective as historical evidence; record rather than rewrite any later-discovered discrepancy.
