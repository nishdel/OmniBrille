<p align="center">
  <img src="assets/branding/OmniBrille-icon-source.png" width="112" alt="OmniBrille">
</p>

# OmniBrille

Explore a folder as a spatial graph—locally, privately, and without indexing your whole computer.

OmniBrille is a Windows desktop file explorer that keeps the folder you are exploring at the center and arranges its immediate contents around it. Back, Up, Root, and Trail provide distinct orientation. Search the selected tree, inspect terminal-style Details, and use either the visual graph or its synchronized accessible list.

## Download

[![Download latest Windows release](https://img.shields.io/badge/Download-latest_Windows_release-0078D4?logo=windows)](https://github.com/nishdel/OmniBrille/releases/latest)

**Latest stable version: v1.2.0.** [Download the Windows x64 installer](https://github.com/nishdel/OmniBrille/releases/download/v1.2.0/OmniBrille-1.2.0-win-x64-setup.exe): **`OmniBrille-1.2.0-win-x64-setup.exe`**. The [v1.2.0 GitHub Release](https://github.com/nishdel/OmniBrille/releases/tag/v1.2.0) contains its SHA-256 checksum, release notes, manifest and validation record. The badge follows GitHub's latest stable release; the versioned link identifies this exact release. [v1.1.0](https://github.com/nishdel/OmniBrille/releases/tag/v1.1.0) remains available as the previous stable version.

1. Download the installer and its `.sha256` file from the same release; compare with `Get-FileHash .\OmniBrille-1.2.0-win-x64-setup.exe -Algorithm SHA256`.
2. Run the installer. It installs for the current user without administrator access or a separate .NET runtime.
3. Open **OmniBrille** from the Start Menu, then choose a folder. Single-click a folder to enter it; right-click goes Back. Use **TRAIL** to return directly to a recent stop.

The app installs at `%LOCALAPPDATA%\Programs\OmniBrille` and can be removed through Windows Installed apps. It has no automatic updater; install a newer official version to upgrade.

> **Signing notice:** v1.2.0 is unsigned. Windows may show **Unknown Publisher** or a SmartScreen reputation warning. Download only from the official GitHub Release, compare its SHA-256 with the attached checksum, and follow your organization’s security policy. A checksum detects corruption; it does not authenticate an unsigned publisher.

Windows x64 is the only download target. Exact package/installer evidence is attached to the release; the hosted lifecycle covers fresh install, launch, relaunch, and uninstall. Native control/transition feel, real microphones and room noise, visual acceptance, motion comfort, physical sound, assistive technology, GPU/DPI and live Connected behavior remain manual acceptance areas. Ubuntu is covered by source build/tests only; interactive Linux and macOS are not validated.

## Current visual evidence

The v1.2.0 implementation has [four inspected software renders](docs/assets/screenshots/issue-audit-2026-09-11/README.md), showing actual subfolder previews in both themes and all 48 names in dense scenes at normal and minimum zoom. These synthetic-data captures were taken during the PR #11 issue audit; they document software composition, not the installed release, native rendering, motion comfort, or DPI validation.

Earlier v1.1 images are retained as [superseded historical evidence](docs/assets/screenshots/README.md). They are not current v1.2.0 screenshots. Publication does not imply that the outstanding visual, hardware, or assistive-technology checks were performed.

## What v1.2.0 does

See the [v1.2.0 changes](CHANGELOG.md#120---2026-09-11) and preserved [issue audit](docs/runs/2026-09-11-github-issue-audit.md) for implementation, regression evidence, and remaining acceptance checks from issues #1–#9.

- Starts empty and reads only a folder you explicitly choose.
- Shows one focused folder and a bounded set of nearby items instead of crawling an entire drive.
- Keeps direct children distinct from optional subfolder previews: at most four visible folders contribute up to three actual subfolders each, using only free slots within the same 48-node scene.
- Streams large directories progressively and groups overflow into reversible pages.
- Enters Structure folders and aggregate pages with one click, uses right-click for Back, and makes recent Trail stops actionable. Files remain single-click selections with explicit double-click or keyboard activation.
- Supports distinct Back/Up/Root navigation, geometric keyboard selection, safe ordinary-file opening, Details, pan, and zoom.
- Retains every on-screen node name, places labels around glyphs with connector lines where possible, and arranges like file types near each other without inventing semantic relationships. Long names use ellipsis; the synchronized list provides full-size reading in crowded scenes.
- Searches names, folders, and paths inside the selected root with bounded foreground work.
- Provides Dark and Light themes, reduced motion/effects, and an obvious persisted master Sound switch.
- Provides a synchronized accessible list plus bounded graph `SelectionItem`/invoke automation over the same session state.
- Stores only safe sensory/voice preferences; selected roots and searches are not persisted.
- Has no telemetry, cloud upload, background indexer, service, auto-start entry, updater, or destructive file operation.

Voice starts from the microphone button without an enablement submenu. Stop submits the utterance; two seconds of quiet after detected speech also submits. Initial silence does not submit a command. The runtime and English model are bundled; no manual download or path configuration is needed. Real microphone and noise behavior remain acceptance checks.

## Standalone first; OmniSorSe optional

OmniBrille works on its own for Structure navigation and structural Search. This is the supported v1.2.0 public experience.

A compatible [OmniSorSe](https://github.com/nishdel/OmniSorSe) build can explicitly launch OmniBrille with a short-lived, authorized Explorer Protocol session. In Connected mode, OmniSorSe remains the authority for roots, Search, metadata, and contextual relationships; OmniBrille presents those results as Structure, Context, or Hybrid without reading OmniSorSe’s database or inventing relationships.

Connected mode is compatibility-dependent and is not a promise of support for every Explorer Protocol v1 host. The exact combinations and limitations that have repository evidence are recorded in the [compatibility matrix](COMPATIBILITY.md). Direct launch never discovers or connects to OmniSorSe in the background.

## Privacy and trust

Standalone access is limited to the selected root. OmniBrille does not recursively follow directory reparse points, modify files, or persist the chosen root. Search is bounded and runs only when requested.

Local Voice uses a pinned installer-owned whisper.cpp v1.9.2 runtime and quantized English base model. Commands and spoken Search use English; selecting Auto does not make the bundled model multilingual. The build/package/application verify exact hashes; the installed app performs no voice-asset download or update. Real microphone hardware remains unvalidated. OmniBrille has no wake word or always-listening mode.

The GitHub Release includes the release manifest, dependency graph, exact installer checksum, and generated artifact notes. The installed application contains the MIT project license and separately applicable third-party license/notice files. The repository records the [security and privacy posture](docs/SECURITY-PRIVACY.md). `Copy safe diagnostics` produces a user-reviewed support snapshot designed to exclude paths, filenames, queries, content, endpoints, grants, tokens, and session/node IDs.

## Current limitations

- The Windows installer is unsigned.
- The supported public contract is Windows x64 Standalone use; exact-artifact automated qualification is broader than the remaining manual visual/hardware evidence.
- Connected mode requires a compatible OmniSorSe host and has narrower validation than Standalone.
- Automated accessibility coverage checks keyboard/list/automation behavior; v1.2.0 is not screen-reader-certified or manually validated with every assistive technology.
- Performance budgets and diagnostics are backed by tests and representative engineering measurements, not a guarantee for every filesystem or machine.
- Voice is optional, Windows-only in implementation, and lacks real-microphone validation.
- The stable installer identity supports in-place upgrades, but the exact v1.2.0 public bytes are qualified by the fresh-install lifecycle rather than a manual upgrade sweep.
- Dense layouts at extreme text scales may crowd names; use the synchronized list for full-size reading.
- Issues [#3](https://github.com/nishdel/OmniBrille/issues/3), [#4](https://github.com/nishdel/OmniBrille/issues/4), [#5](https://github.com/nishdel/OmniBrille/issues/5), [#6](https://github.com/nishdel/OmniBrille/issues/6), and [#7](https://github.com/nishdel/OmniBrille/issues/7) retain manual control/transition, microphone/noise, concept, motion/readability, and sound-character acceptance. Their implementation is included; these checks are not known implementation blockers.

The official [v1.2.0 GitHub Release](https://github.com/nishdel/OmniBrille/releases/tag/v1.2.0) contains exact artifact notes and checksums. See [compatibility](COMPATIBILITY.md) for the support contract.

## Keyboard essentials

| Action | Shortcut |
|---|---|
| Search | `Ctrl+F` |
| Accessible list | `Ctrl+Shift+L` |
| Back | `Backspace` or `Alt+Left` |
| Up / Root | `Alt+Up` / `Alt+Home` |
| Select / activate | Geometric arrow keys / `Enter` |
| Reopen Details | `Ctrl+I` |
| Zoom / reset | `+`, `-`, `0` |
| Structure / Context / Hybrid | `Ctrl+1`, `Ctrl+2`, `Ctrl+3` |
| Cancel or dismiss | `Escape` |

Context and Hybrid explain when a compatible OmniSorSe connection is required; they do not fabricate standalone relationship data.

## Build from source

The SDK version is pinned in [`global.json`](global.json).

See [Contributing](CONTRIBUTING.md) for setup, test and release-validation commands, and the distinction between automated and manual evidence.

```powershell
dotnet restore .\OmniBrille.sln
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniBrille.Desktop\OmniBrille.Desktop.csproj
```

Build the Windows installer with:

```powershell
.\build\Package-Windows.ps1 -BootstrapInnoSetup
```

## License

OmniBrille project code is licensed under the **MIT License**. See [`LICENSE`](LICENSE). Bundled third-party components remain under their own terms, preserved in [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt) and [`THIRD-PARTY-LICENSES`](THIRD-PARTY-LICENSES). The Windows renderer uses a project-built SkiaSharp 3.119.4 native asset with Adobe DNG/RAW support excluded; its [source pins, build procedure, and verification](docs/native-skia.md) are maintained in the repository.

## Engineering documentation

OmniBrille keeps current engineering knowledge in the repository:

- [Engineering start page](docs/engineering/README.md) — task-specific authority and validation router
- [Current architecture](docs/architecture.md) — subsystem ownership, state, data flow, and Mermaid diagrams
- [Windows packaging](docs/PACKAGING.md) — installer, signing, artifact, and lifecycle details
- [Compatibility matrix](COMPATIBILITY.md) — verified and unverified platform/OmniSorSe combinations
- [Security and privacy](docs/SECURITY-PRIVACY.md) — access, handoff, voice, diagnostics, and release boundaries
- [Explorer Protocol boundary](docs/explorer-protocol.md) — Connected-mode contract and evidence
- [Context rendering contract](docs/context-rendering-contract.md) — bounded relationship presentation
- [Release checklist](RELEASE_CHECKLIST.md) and [changelog](CHANGELOG.md)

The compact [`AGENTS.md`](AGENTS.md) routes Codex and specialist agents without duplicating architecture. Historical run reports are evidence, not current authority.
