# OmniBrille v1.2.0

**[Download the Windows installer](https://github.com/nishdel/OmniBrille/releases/download/v1.2.0/OmniBrille-1.2.0-win-x64-setup.exe)** · [v1.2.0 release and verification files](https://github.com/nishdel/OmniBrille/releases/tag/v1.2.0) · [Latest stable release](https://github.com/nishdel/OmniBrille/releases/latest)

Version 1.2.0 includes the implementation work from issues #1–#9 and is published for continued manual acceptance testing. The release remains a self-contained Windows x64 Standalone explorer with optional local English Voice and compatibility-dependent OmniSorSe integration. Implementation and automated evidence are distinct from the manual checks below.

## Install on Windows

1. Download `OmniBrille-1.2.0-win-x64-setup.exe` and its matching `.sha256` from the official release.
2. Verify the checksum, then run the installer as your normal Windows user. The package is **unsigned**: Windows may show **Unknown Publisher** or a SmartScreen reputation warning. Do not disable Windows security globally. The checksum confirms the bytes, not publisher identity.
3. Open **OmniBrille** from the Start Menu and choose the folder to explore. No filesystem content is preloaded before that choice.

The application installs below `%LOCALAPPDATA%\Programs\OmniBrille`, with a Start Menu shortcut and uninstall entry. It requires no separately installed .NET runtime or administrator access. To upgrade, run the new installer under the same Windows account. Safe UI preferences survive upgrade and uninstall; user content is outside installer ownership.

OmniBrille installs no service, startup task, file association, telemetry component, updater, or OmniSorSe binary. The installer includes its pinned whisper.cpp runtime and English-only model. The installed application never downloads or updates those assets.

## Verify the installer

Keep the installer, `.sha256` sidecar, manifest, generated notes, and hosted validation record from the same release together. Calculate the hash independently:

```powershell
Get-FileHash .\OmniBrille-1.2.0-win-x64-setup.exe -Algorithm SHA256
```

The value must match the sidecar, manifest, and artifact-specific hash near the top of the generated notes. The manifest identifies the release commit and build workflow; the hosted validation JSON identifies the exact installer exercised. A later build from the same source can have a different checksum because build timestamps are not normalized.

## Improvements in v1.2.0

- **Hierarchy and graph geometry (#1, #5):** direct children remain semantically distinct from real subfolder previews. Bounded previews connect to their actual parent, use a `↳` label, and never displace direct children from the 48-node scene. Peripheral connectors, folder silhouettes, and deterministic asymmetric placement improve graph readability.
- **Navigation and controls (#3):** first-click Structure folder entry, graph right-click Back, and clickable named Trail destinations use the same session history. Centered controls, reserved HUD space, and scrolling Trail entries keep actions usable. File double-click activation requires the same node and unchanged scene across both presses.
- **Voice setup and reliability (#2, #4):** the installer retains the bundled local English runtime/model. First activation enables Voice and listens; the microphone becomes Stop. A second activation or two seconds of quiet after detected input stops capture. Initial silence does not submit an utterance. Cancellation and provider changes invalidate obsolete work before it can start, stop a newer capture, show a transcript, or execute an action.
- **Motion and Details (#6):** hover magnification acts locally while the central focus stays steady. The actual metadata fields reveal in sequence, with complete text immediately available to automation and Reduced motion. New selection cancels an earlier reveal.
- **Sound (#7):** bounded, locally synthesized airy hover, selection, file-open, and folder/navigation cues provide optional feedback. Master mute and device failure preserve every navigation outcome.
- **Names and file grouping (#8, #9):** every admitted node whose center is on the graph retains its name at every zoom. Labels move around glyphs and other labels with peripheral leaders where unobstructed. Like file extensions group together, including an unknown-type group, while placement remains deterministic and asymmetric.
- **Regression coverage:** new fixtures cover previews and provider authority, crowded targets, cross-scene double-clicks, Trail history, labels, metadata reveal, Voice cancellation races, and audio bounds. The [issue audit](https://github.com/nishdel/OmniBrille/blob/v1.2.0/docs/runs/2026-09-11-github-issue-audit.md) records the investigation and retained evidence; exact final release validation is recorded with the release artifacts and CI.

## Supported experience and limits

Windows x64 is the download target. Standalone supports selected-root Structure, bounded aggregation, Search, Details, safe ordinary-file activation, themes, sensory preferences, and synchronized keyboard/list navigation. Connected Structure/Context/Hybrid requires a compatible OmniSorSe host and keeps server-authored relationships and opaque IDs; publication does not imply a fresh live-host check. See the [compatibility matrix](https://github.com/nishdel/OmniBrille/blob/v1.2.0/COMPATIBILITY.md).

Voice uses the default Windows input device and an English-only bundled model. Auto-detect does not add multilingual support. Model loading occurs for each utterance, so CPU and model-load latency affect response time. Missing hardware or a failed bundle-integrity check leaves typed and pointer operation available.

Remaining manual acceptance areas are:

- native control usability and transition feel (#3);
- real microphone capture, background noise, quiet completion, and command/Search behavior (#4);
- native visual composition and concept acceptance (#5);
- motion comfort and native readability (#6);
- physical playback, device behavior, and subjective sound character (#7);
- dense graph layouts at extreme text/display scales; names may crowd, and the synchronized list provides full-size text;
- Narrator/NVDA, high contrast, custom-chrome DPI/snap, GPU behavior, and broader platform/runtime qualification.

These are manual validation items, not known implementation blockers. Automated headless/software evidence does not certify them. Linux has source build/test coverage only; macOS runtime remains unverified. Destructive file operations, cloud services, always-listening audio, automatic updating, and telemetry are intentionally absent.

## License, privacy, and support

OmniBrille project code uses the **MIT License**. The installed `LICENSE`, matching `v1.2.0` source tag, third-party notices, and bundled component licenses preserve the distribution terms. The Windows renderer uses a project-built SkiaSharp 3.119.4 native asset with the unused Adobe DNG/RAW codec excluded; its pins and notice derivation are documented in [native renderer provenance](https://github.com/nishdel/OmniBrille/blob/v1.2.0/docs/native-skia.md).

Standalone reads only the explicitly selected root. OmniBrille does not persist selected roots, Search queries, audio, transcripts, grants, or connected identities. Safe preferences remain below `%LOCALAPPDATA%\OmniBrille` by policy.

Use **Copy safe diagnostics** and review the text before sharing an issue. Include version, Windows version, steps, expected behavior, and observed behavior. Do not attach private filenames, paths, file contents, queries, audio, tokens, handoff values, or databases unless separately reviewed and requested.
