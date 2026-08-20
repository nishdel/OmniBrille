# v1.0.0 public-release checklist

Checkboxes are maintainer gates. Automation does not mark manual checks complete, and a source build is not an installed-artifact validation.

## Release authority

- [ ] The release commit is identified, reviewed, and clean; `main` equals GitHub `main`.
- [ ] `Directory.Build.props`, installer fallback, executable metadata, changelog, compatibility matrix, README, release notes, and artifact names all say `1.0.0`.
- [x] The project owner selected MIT and the authoritative root `LICENSE` plus package metadata record that exact expression.
- [ ] The exact installed application contains the same MIT license text and its manifest records `MIT` plus the release-commit source URL.
- [x] Redistributed runtime licenses/notices were reviewed. The project-built SkiaSharp native asset excludes DNG/RAW; its derived notice omits only the unfetched/unlinked DNG SDK and RAW-only PIEX sections while retaining all other upstream notices conservatively.
- [ ] The exact tracked DNG-free package, native DLL hash, provenance, unsigned signature state, and exclusion checks remain bound through final restore, publish, manifest, install, launch, and uninstall qualification.
- [ ] Exact v1.0 screenshots were captured from the installed release candidate using non-private demo data and independently reviewed.
- [ ] Any release blocker found by the checklist is fixed and the exact artifact is rebuilt; the gate is never lowered to fit an artifact.

## Automated release gate

- [ ] Run `.\build\verify-release.ps1` from a clean Windows checkout.
- [ ] Engineering-document validation, restore, format, Release build/analyzers, all tests, and NuGet vulnerability audit pass.
- [ ] The manual release-candidate workflow succeeds for the intended signing mode on the release commit.
- [ ] Installer, `.sha256`, manifest, dependency inventory, generated release notes, and hosted validation JSON are retained together.
- [ ] Independent, sidecar, manifest, and published-asset SHA-256 values all agree.
- [ ] The hosted artifact-only gate passes version metadata, signature policy, install, first launch, normal close, relaunch, registration, uninstall, and cleanup.

## Signing and download trust

- [ ] If signed, both installer and installed executable report a valid expected publisher and timestamp.
- [x] The owner explicitly accepted an unsigned v1.0.0 on 2026-08-20; README and release notes prominently disclose Unknown Publisher / SmartScreen risk. Code signing remains future work.
- [ ] The GitHub Release is the only advertised download location; no unrelated mirror is presented as authoritative.
- [ ] The published installer downloads successfully and its checksum matches the independently retained value.

## Exact installed Windows candidate

- [ ] Record the Windows version and whether validation used a genuine clean VM, hosted runner, or development host.
- [ ] Fresh install succeeds as a normal current user without a separate .NET installation.
- [ ] Start Menu launch opens OmniBrille in Standalone with no filesystem content preloaded.
- [ ] Selecting the non-private demo root loads Structure and permits drill-down and Back.
- [ ] Name/path Search, result focus, file/folder details, Light/Dark, reduced motion/effects, and the accessible list are exercised.
- [ ] Close and relaunch work; safe visual preferences behave according to policy.
- [ ] OmniBrille starts and non-voice Standalone works with voice disabled and without whisper.cpp/model files.
- [ ] Uninstall removes installer-owned files, shortcut, and registration without deleting demo/user content or retained preferences.

## Optional capabilities (do not block the Standalone contract)

- [ ] If Connected mode is advertised beyond compatibility-dependent preview status, validate the exact claimed OmniSorSe host: discovery/handoff, roots, Structure, Search, details, Context, Hybrid, filters, refocus, disconnect, and Back.
- [ ] If voice is advertised as validated, exercise a real Windows microphone and user-supplied local model, cancellation, permission/hardware failure, and cleanup. Otherwise keep it outside the supported v1.0 contract.
- [ ] Any accessibility claim beyond automated keyboard/list/automation coverage has matching manual assistive-technology evidence.
- [ ] Any Linux/macOS runtime claim has matching packaging and interactive evidence.

## Artifact, privacy, and public presentation

- [ ] The installed app contains the project license and required third-party notices but no PDB/source/test files, logs, databases, screenshots, user content, development paths, keys, tokens, unexpected OmniSorSe binary, raw/test audio, whisper runtime, or GGML model.
- [ ] `Copy safe diagnostics` is reviewed and contains no paths, filenames, queries, content, endpoints, grants, tokens, audio/transcript text, or session/node IDs.
- [ ] No telemetry, cloud upload, background recorder/indexer/service, auto-start, file mutation, or auto-update behavior is introduced.
- [ ] README, screenshots, generated notes, GitHub release body, platform statement, Connected/voice status, signing state, and downloadable files describe the same product.
- [ ] GitHub description, homepage, topics, and security-reporting route are reviewed.

## Publication and post-publication verification

- [ ] Independent adversarial review finds no remaining blocker and distinguishes verified, inferred, and unverified claims.
- [ ] Create a release commit on `main`; push normally and wait for required CI.
- [ ] Create annotated tag `v1.0.0` on that exact commit and push it normally.
- [ ] Create a non-prerelease GitHub Release titled `OmniBrille 1.0.0`, using the reviewed notes.
- [ ] Attach the exact validated installer, checksum, manifest, dependency inventory, and generated notes; do not rebuild after validation.
- [ ] Verify the public page, assets, download, checksum, repository metadata, and README links from GitHub.
- [ ] Retain the owner report and retrospective as historical evidence; record rather than rewrite any later-discovered discrepancy.
