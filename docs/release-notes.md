> **Stable release:** v1.1.0 is the current public release. Download only from the official [v1.1.0 GitHub Release](https://github.com/nishdel/OmniBrille/releases/tag/v1.1.0); v1.0.0 remains available as the previous release.

## Install on Windows

The OmniBrille 1.1.0 release provides a self-contained Windows x64 installer. It installs for the current user at `%LOCALAPPDATA%\Programs\OmniBrille`, creates a Start Menu shortcut and uninstall entry, and requires no separately installed .NET runtime or administrator access.

OmniBrille installs no service, startup task, file association, telemetry component, updater, or OmniSorSe binary. It includes an installer-owned, hash-bound local whisper.cpp runtime and English model; the installed app never downloads or updates those assets.

## Verify the installer

After publication, download the installer, `.sha256` sidecar, manifest, and these notes from the same GitHub Release. Calculate the hash independently:

```powershell
Get-FileHash .\OmniBrille-1.1.0-win-x64-setup.exe -Algorithm SHA256
```

The generated artifact-specific notes place the expected SHA-256 above this template content. The calculated value must match that value, the `.sha256` file, and the manifest. A checksum identifies the exact bytes; it does not authenticate an unsigned publisher.

## License and source

OmniBrille project code is licensed under the **MIT License**. The release includes the full `LICENSE`, and its source is available from the matching `v1.1.0` tag and source archives on the official GitHub Release. Bundled third-party components retain their own installed licenses and notices. The Windows renderer uses a project-built SkiaSharp 3.119.4 native asset with the unused Adobe DNG/RAW codec excluded; its exact upstream pins, build configuration, hash, and notice derivation are recorded in the release manifest and repository provenance guide.

## What changed in v1.1

- The graph fills a borderless application client area with angular Back/Up/Root/Trail, mode, Search, utility, Sound, zoom, Details, Voice, status, and custom window-control surfaces.
- Current Focus, direct children, previous focus, and Context are explicit semantic roles. Every admitted direct child remains on one truthful plane as a recognizable outlined glyph; separate density bands no longer imply deeper folders.
- Geometric arrow selection, 44-DIP targets, a distinct graph keyboard-focus cue, and graph `SelectionItem` semantics keep keyboard/list/automation behavior aligned.
- Bounded analytic float and hover lens preserve immutable topology; Reduced motion removes float, lens, transition, and Details typing movement.
- Details has a fast cancel-safe terminal reveal while complete semantic/automation text is available immediately. Short local interaction cues are redundant and governed by a persisted `SOUND OFF` switch.
- Ordinary Standalone files may open after explicit activation and immediate selected-root/reparse/type checks. Connected display paths are never filesystem authority.
- Voice is first-click Listen, second-click Stop/Transcribe, with Back/Up/Root/Enter/selected-node variants over the same existing session actions.
- Cancelling Voice or replacing the active provider during its capability check prevents obsolete work from starting microphone capture.
- First-run guidance and the bottom status surface yield when a secondary panel claims their space, keeping focused controls unobscured at the supported minimum size.
- Dark and Light themes use a deeper navy/cyan and pale ice-blue visual system; the bounded loading data rain has a restrained focal aperture.
- Connected server-authored Context, Explorer Protocol v1, 48-node scene admission, stale-result rejection, and no-destructive-operation policy remain unchanged. Connected opaque target comparison is now correctly ordinal on Windows.

## Supported v1.1 experience

- Standalone selected-folder Structure navigation, bounded aggregation, Search, details, Dark/Light themes, reduced motion/effects, and the synchronized keyboard-friendly list.
- Optional compatibility-dependent OmniSorSe Connected mode for authorized indexed Structure, Search, details, and server-authored Context/Hybrid data. See `COMPATIBILITY.md`; current-host validation is not implied by the installer alone.
- Optional local Voice uses the exact pinned runtime/model in the Windows package; missing microphone or integrity failure leaves typed/pointer operation available.

## Current limitations

- Windows x64 is the only download target. Exact local build/install/upgrade qualification is recorded against Windows 10 22H2 x64, and the public artifact passed a fresh hosted Windows lifecycle; manual visual, physical-hardware, and assistive-technology qualification remains unverified. Other Windows client versions are not separately validated.
- Linux has source build/test coverage only; no package or interactive-runtime support is claimed. macOS runtime is unverified.
- Connected mode depends on a compatible OmniSorSe build and is not the primary v1.1 support contract.
- Automated keyboard, list, text-scaling, and automation coverage is not screen-reader certification.
- Real microphone, physical sound output, Narrator/NVDA, custom-chrome DPI/snap, and GPU-backed continuous-motion validation remain outstanding and block their respective v1.1 release claims.
- Destructive file operations, automatic updating, cloud services, always-listening audio, and telemetry are intentionally absent.

The exact public v1.0.0 installer was exercised as the predecessor for an in-place upgrade to an exact-release-commit local package: installer-owned files advanced to v1.1.0, the obsolete v1.0 runtime-diagnostics file was removed, preferences were preserved, and uninstall still removed only installer-owned state. The separately timestamped public artifact passed the hosted fresh-install workflow rather than that upgrade path.

## Privacy and support

Standalone reads only the folder explicitly selected by the user. OmniBrille does not persist selected roots, Search queries, audio, transcripts, grants, or connected identities. Uninstall removes installer-owned files and registration; safe visual/voice configuration remains below `%LOCALAPPDATA%\OmniBrille` by policy.

Use **Copy safe diagnostics** and review the text before sharing it. Do not attach private filenames, paths, file contents, queries, audio, tokens, handoff values, or databases unless separately reviewed and requested.
