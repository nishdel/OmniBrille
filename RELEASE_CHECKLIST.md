# Private-preview release checklist

Checkboxes are maintainer gates. Automated verification does not mark manual checks complete.

## Source and compatibility

- [ ] Release commit and branch are identified; working tree is clean.
- [ ] `main` equals `origin/main` and normal CI is green.
- [ ] Version, changelog, preview notes, and [compatibility matrix](COMPATIBILITY.md) agree.
- [ ] The maintainer has reviewed whether the repository license permits the intended distribution. No license is currently selected.
- [ ] No tag or GitHub Release is created without separate approval.

## Automated release gate

- [ ] Run `./build/verify-release.ps1` from a clean Windows checkout.
- [ ] Restore, format, Release build, analyzers, all tests, and NuGet vulnerability audit pass.
- [ ] The private-preview GitHub Actions workflow succeeds for the intended signing mode.
- [ ] Installer, release manifest, dependency manifest, and `.sha256` sidecar are retained together.
- [ ] Generated tester notes name the exact installer, commit, signing state, and SHA-256.
- [ ] SHA-256 verification succeeds after artifact download.
- [ ] The artifact-only fresh hosted-Windows install/window/uninstall job succeeds and its validation record is retained.

## Signing

- [ ] For a signed preview, approved secrets are configured and the workflow was invoked with `signed`.
- [ ] `Get-AuthenticodeSignature` reports `Valid` for both installed `OmniBrille.exe` and the installer.
- [ ] Publisher identity and timestamp are reviewed.
- [ ] If the preview is unsigned, testers are explicitly told it is unsigned and may trigger Windows reputation warnings.

## Clean Windows environment

- [ ] Test environment contains no OmniBrille checkout or developer locator override.
- [ ] Record whether the environment is a genuine clean VM, hosted artifact-only runner, or an isolated development host; do not conflate them.
- [ ] Fresh install succeeds as a normal current user.
- [ ] Start Menu launch opens Standalone.
- [ ] Folder selection, Structure, Search, Light/Dark, reduced motion/effects, and accessible list are smoke-tested.
- [ ] Application starts and all non-voice features work with Voice disabled and with no whisper.cpp runtime/model installed.
- [ ] On Windows hardware, push-to-talk clearly announces listening/transcribing, stops on `Escape`, and runs representative deterministic commands.
- [ ] Standalone spoken Search uses only the selected-root structural provider; connected spoken Search reaches the existing OmniSorSe Search capability.
- [ ] Missing runtime/model, microphone unavailable, and permission denial remain non-fatal and leave typed Search available.
- [ ] Compatible OmniSorSe discovers the installed executable without `OMNISORSE_OMNIBRILLE_PATH`.
- [ ] Handoff reaches `Connected · OmniSorSe`; authorized roots, Structure, Search, details, Context, Hybrid, filters, refocus, and Back work.
- [ ] Hybrid deduplicates shared nodes, preserves structural orientation while filtering Context, and remains bounded in sparse and maximum synthetic scenes.
- [ ] In-place upgrade from the previous preview preserves safe preferences and creates no duplicate registration or stale PDBs.
- [ ] Uninstall removes installer-owned files, shortcut, and registration without touching user content or OmniSorSe.

## Artifact, privacy, and support

- [ ] Installer name, version metadata, size, installed footprint, and hash are recorded in release notes.
- [ ] Artifact contains no PDB/source/test files, logs, databases, screenshots, user content, local paths, keys, tokens, or OmniSorSe application binaries.
- [ ] Handoff grants remain memory-only; no secret appears in arguments, preferences, manifests, hashes, or diagnostics.
- [ ] No telemetry, cloud upload, always-listening microphone, voice helper/background service, or auto-start behavior is present.
- [ ] Artifact contains no raw/test audio, whisper runtime, GGML model, or temporary utterance; configured external models remain untouched by uninstall.
- [ ] Voice diagnostics contain timing/length/classification only—no audio, transcript, runtime/model path, or spoken query text.
- [ ] Runtime/model licenses and distribution boundary are documented; only distributable NAudio capture assemblies are packaged.
- [ ] Runtime performance regression samples are recorded.
- [ ] Known limitations and safe support-data instructions are included in preview notes.
- [ ] `Copy safe diagnostics` output is reviewed and contains no paths, queries, content, endpoint, grant, token, or session/node identity.
- [ ] The controlled-rollout boundary and private-preview blocker policy are reviewed.
