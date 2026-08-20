# Windows packaging and public releases

## Supported package

OmniBrille uses pinned Inno Setup 6.7.3 for one per-user Windows x64 installer. It is a standalone-capable application and installs no OmniSorSe binary, service, startup entry, telemetry component, updater, file association, speech recognizer, or speech model.

```text
%LOCALAPPDATA%\Programs\OmniBrille\OmniBrille.exe
```

The fixed application ID provides in-place upgrade, one Start Menu entry, one uninstall registration, and standard running-application coordination without administrator rights. This path is also among the bounded locations historically used by the compatible OmniSorSe companion launcher.

MSIX was not selected because its identity/signing lifecycle would raise the first-release gate without improving this current-user application. WiX/MSI is disproportionate; NSIS would duplicate lifecycle scripting already supplied by Inno Setup. Do not introduce a second package format without a new platform or lifecycle requirement.

## Version and artifacts

`Directory.Build.props` is the version authority for v1.0.0:

- semantic/informational version: `1.0.0`;
- assembly version: `1.0.0.0`;
- Windows file and installer version: `1.0.0.0`.

The package is self-contained, non-trimmed, multi-file, and `win-x64`. Users do not need a separate .NET runtime. The retained multi-file deployment avoids first-release risk around Avalonia XAML, reflection, and native-library loading.

```text
OmniBrille-1.0.0-win-x64-setup.exe
OmniBrille-1.0.0-win-x64-setup.exe.sha256
OmniBrille-1.0.0-win-x64-setup-manifest.json
OmniBrille-1.0.0-win-x64-setup-dependencies.json
OmniBrille-1.0.0-win-x64-setup-release-notes.md
```

The manifest binds the installer to version, release commit, UTC build time, runtime/deployment, Explorer Protocol version, size, checksum, signing state, and—when built in Actions—the workflow run. The dependency document is a sanitized project dependency graph; it is neither an exact packaged-file inventory nor a formal SPDX/CycloneDX SBOM. The generated notes bind the same exact artifact to its install, support, and limitation guidance.

OmniBrille project code is licensed `MIT`, recorded by `PackageLicenseExpression` and the authoritative root `LICENSE`. The release manifest records that exact expression and the source URL for its release commit. The installed application includes the project `LICENSE`, [`THIRD-PARTY-NOTICES.txt`](../THIRD-PARTY-NOTICES.txt), and complete redistributed runtime notices below `THIRD-PARTY-LICENSES`. Public release verification fails if the MIT text/metadata is absent, if a reviewed notice is missing, or if a packaged notice differs from its repository source.

The current SkiaSharp 3.119.4 Windows native asset contains Adobe DNG SDK code. Adobe's included agreement expressly grants use, distribution, and sublicensing for any purpose, requires its notices to remain human-readable, and adds a defense/indemnity obligation when the SDK is distributed in a “commercial product.” MIT does not require separately licensed bundled code to be relicensed MIT, so the former GPL-specific compatibility conflict does not apply. However, the DNG agreement does not define “commercial product,” and price alone cannot resolve that legal category. Publication remains blocked until the owner explicitly accepts the applicable agreement and conditional obligation, qualified review clears the intended distribution, or a DNG-free native asset is adopted. The complete agreement remains installed whenever the current asset is used.

## Reproducible commands

From a clean Windows checkout with the SDK pinned by `global.json`:

```powershell
dotnet restore .\OmniBrille.sln
.\build\Package-Windows.ps1 -BootstrapInnoSetup
```

`Get-InnoSetup.ps1` downloads the official Inno Setup 6.7.3 installer below ignored `artifacts/tools`, validates its pinned SHA-256, and extracts the compiler. An existing compiler may instead be passed through `-InnoCompiler`.

The complete local release gate is:

```powershell
.\build\verify-release.ps1
```

It requires a clean checkout and checks the distribution license, version consistency, stale branding, tracked-artifact hygiene, engineering-document paths/fences, restore, formatting, analyzer-enabled Release build, all tests, NuGet vulnerabilities, direct dependency updates, the Windows package, runtime contents, exact hash/manifest/notes agreement, and `git diff --check`. `-AllowDirty` exists only while developing the release machinery; it is not evidence for a publishable artifact. The script never tags, pushes, or publishes.

Outputs are below ignored `artifacts/publish/win-x64` and `artifacts/packages`. The publish directory is safely recreated each time. Installer bytes are not reproducible across machines because executable, installer, and optional signing timestamps are not normalized. The checksum therefore identifies one retained artifact; never mix a sidecar, manifest, validation record, or notes file from another build.

## Signing

The packaging scripts support Authenticode but no production certificate is stored in the repository. When an approved certificate with a private key is present in the Windows certificate store:

```powershell
$env:OMNIBRILLE_SIGNING_CERTIFICATE_THUMBPRINT = '<thumbprint>'
.\build\verify-release.ps1 -RequireSigning
```

The application is signed and validated before installer compilation; the installer is then signed and validated with SHA-256 and a timestamp server. Required signing fails closed for a missing, expired, private-key-less, or invalid certificate.

The manual release-candidate workflow can import an externally supplied certificate from GitHub Actions secrets, exposes only the thumbprint to packaging, and removes the temporary PFX and imported certificate. Secrets never enter source, command-line arguments, logs, or artifacts.

If the maintainer explicitly approves an unsigned release, its README, GitHub Release, manifest, and generated notes must all disclose **Unknown Publisher / SmartScreen** risk. SHA-256 proves byte integrity after the user obtains the checksum from a trusted channel; it does not authenticate the publisher.

## Automation and artifact-only validation

Normal CI restores, formats, builds, tests, validates engineering-document paths/fences, audits NuGet packages on Windows and Ubuntu, and builds an unsigned Windows installer on the Windows leg.

`.github/workflows/private-preview.yml` retains its historical filename but is the manual **release candidate artifact** workflow. It accepts unsigned or fail-closed signed mode. A dependent fresh hosted-Windows job receives only the candidate artifacts, independently matches the installer, sidecar, manifest, version metadata, and required signature state, performs a per-user install, launches and normally closes the installed application, relaunches it with the opposite theme, verifies Start Menu/uninstall registration, uninstalls, and retains a validation JSON record.

That workflow deliberately does not create a tag or GitHub Release. Its non-interactive window check does not prove visual quality, feature interactions, assistive-technology behavior, Connected mode, upgrade, or real microphone behavior. Those claims require the applicable manual gates in [`RELEASE_CHECKLIST.md`](../RELEASE_CHECKLIST.md).

## Install, upgrade, and uninstall boundaries

The installer owns its application directory, Start Menu shortcut, and uninstall registration. It excludes PDB/source/test/database/key/audio/model material, development paths, unexpected OmniSorSe binaries, whisper.cpp, and GGML models.

Safe UI preferences remain at `%LOCALAPPDATA%\OmniBrille\visual-preferences.json` and intentionally survive upgrade/uninstall. They may include theme, effects, diagnostics, and optional voice configuration paths. Selected roots, queries, audio, transcripts, grants, bearer tokens, endpoints, connected node IDs, and Context caches are not persisted. User content, OmniSorSe state, and external voice components are never removed.

Forward in-place upgrade is supported through the stable application ID. Downgrade is neither blocked nor promised. A public release must validate the exact installer’s fresh install, representative Standalone interaction, normal close/relaunch, and uninstall. Prior preview lifecycle measurements are historical evidence, not proof for v1.0.0.

## Publication boundary

The owner selects and records the project license. Independent release review then checks the exact artifact, validation record, screenshots, public claims, release notes, signing disclosure, compatibility language, and repository state. Only after all mandatory gates pass may a normal `v1.0.0` tag and GitHub Release be created. Never retag a different commit or replace an attached installer without changing the release/version and rerunning validation.
