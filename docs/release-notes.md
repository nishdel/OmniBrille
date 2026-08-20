## Install on Windows

OmniBrille 1.0.0 provides a self-contained Windows x64 installer. It installs for the current user at `%LOCALAPPDATA%\Programs\OmniBrille`, creates a Start Menu shortcut and uninstall entry, and requires no separately installed .NET runtime or administrator access.

OmniBrille installs no service, startup task, file association, telemetry component, updater, OmniSorSe binary, speech recognizer, or speech model.

## Verify the installer

Download the installer, `.sha256` sidecar, manifest, and these notes from the same GitHub Release. Calculate the hash independently:

```powershell
Get-FileHash .\OmniBrille-1.0.0-win-x64-setup.exe -Algorithm SHA256
```

The generated artifact-specific notes place the expected SHA-256 above this template content. The calculated value must match that value, the `.sha256` file, and the manifest. A checksum identifies the exact bytes; it does not authenticate an unsigned publisher.

## License and source

OmniBrille project code is licensed under the **MIT License**. The release includes the full `LICENSE`, and source is available from the matching `v1.0.0` tag and source archives on the official GitHub Release. Bundled third-party components retain their own installed licenses and notices. The Windows renderer uses a project-built SkiaSharp 3.119.4 native asset with the unused Adobe DNG/RAW codec excluded; its exact upstream pins, build configuration, hash, and notice derivation are recorded in the release manifest and repository provenance guide.

## Supported v1.0 experience

- Standalone selected-folder Structure navigation, bounded aggregation, Search, details, Dark/Light themes, reduced motion/effects, and the synchronized keyboard-friendly list.
- Optional compatibility-dependent OmniSorSe Connected mode for authorized indexed Structure, Search, details, and server-authored Context/Hybrid data. See `COMPATIBILITY.md`; current-host validation is not implied by the installer alone.
- Optional local push-to-talk remains disabled by default and requires separately supplied whisper.cpp runtime/model components.

## Current limitations

- Windows x64 is the only download target. Interactive release qualification is recorded against Windows 10 22H2 x64; other Windows client versions are not separately validated.
- Linux has source build/test coverage only; no package or interactive-runtime support is claimed. macOS runtime is unverified.
- Connected mode depends on a compatible OmniSorSe build and is not the primary v1.0 support contract.
- Automated keyboard, list, text-scaling, and automation coverage is not screen-reader certification.
- Real microphone hardware validation remains outstanding; voice is outside the validated v1.0 contract.
- Destructive file operations, automatic updating, cloud services, always-listening audio, and telemetry are intentionally absent.

## Privacy and support

Standalone reads only the folder explicitly selected by the user. OmniBrille does not persist selected roots, Search queries, audio, transcripts, grants, or connected identities. Uninstall removes installer-owned files and registration; safe visual/voice configuration remains below `%LOCALAPPDATA%\OmniBrille` by policy.

Use **Copy safe diagnostics** and review the text before sharing it. Do not attach private filenames, paths, file contents, queries, audio, tokens, handoff values, or databases unless separately reviewed and requested.
