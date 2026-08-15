## What OmniBrille is

OmniBrille is an optional spatial navigation companion for exploring files as a bounded graph. It can run by itself in Structure mode or connect to a compatible OmniSorSe session for authorized Structure, Search, details, and server-authored Context relationships. This is a private pre-release, not a stable production release.

## Windows installation

This preview supports Windows x64. Install the `.exe` as the current user; administrator access is not normally required. The expected application path is:

```text
%LOCALAPPDATA%\Programs\OmniBrille\OmniBrille.exe
```

The installer creates an `OmniBrille` Start Menu entry and a normal uninstall entry. It installs no service, updater, startup task, file association, telemetry component, or OmniSorSe binary.

For Standalone use, open OmniBrille from the Start Menu and explicitly choose the folder you want it to access. For Connected use, start a compatible OmniSorSe and choose its normal **Open in OmniBrille** action; no developer environment variable is required.

## Verify the exact installer

Obtain the installer, `.sha256` sidecar, manifest, and these notes from the same controlled private artifact. In PowerShell, calculate the installer hash independently:

```powershell
Get-FileHash .\OmniBrille-*-win-x64-setup.exe -Algorithm SHA256
```

The calculated value must equal both the SHA-256 near the top of the generated notes and the value in the matching `.sha256` file and release manifest. A checksum identifies this one built artifact; a later rebuild from the same source may have a different checksum because executable, installer, and signing timestamps are not normalized.

If the header identifies an unsigned preview, Windows may show **Unknown Publisher** or a SmartScreen reputation warning. Verify the checksum and obtain the package only from the maintainer-controlled private location. Do not disable Windows security globally.

## Preview coverage

- Standalone selected-folder Structure navigation, bounded aggregation, Search, details, Light/Dark, reduced motion/effects, and accessible list.
- Compatible OmniSorSe discovery and one-time handoff, authorized Structure, Search, details, and bounded server-authored Context.
- Context filters, relationship reason/evidence/provenance, refocus, Back, keyboard navigation, and disconnect-safe stale orientation.
- Optional local push-to-talk commands and Search on Windows. Voice is off by default and requires a separately obtained `whisper-cli` plus compatible GGML model configured in Settings; neither is downloaded or bundled.
- Per-user install, in-place preview upgrade, and uninstall that retains only safe UI preferences by policy. User-provided models outside the install directory are untouched.

## Known limitations

- Windows x64 is the only installed/runtime-validated platform. Ubuntu is build/test-only; macOS runtime is unvalidated.
- Context density depends on OmniSorSe's indexed intelligence. OmniBrille does not invent relationships.
- Voice commands are English-only, use the default Windows input device, and have process-per-utterance model-load latency. Linux/macOS microphone capture is not validated.
- Hybrid mode, destructive file operations, automatic updating, cloud services, always-listening audio, and telemetry are absent.
- The repository has no selected public-distribution license.

## Safe feedback

Use the settings HUD's **Copy safe diagnostics** action and review the text before sharing. It excludes audio, transcript text, runtime/model paths, filesystem paths, filenames, queries, content, endpoints, grants, tokens, and session/node identifiers. Include the OmniBrille/OmniSorSe versions, Windows version, Standalone or Connected, Structure or Context, voice state/model family where relevant, the action attempted, actual behavior, and whether it reproduces. Do not attach microphone audio, spoken text, private content, or databases unless separately reviewed and requested.
