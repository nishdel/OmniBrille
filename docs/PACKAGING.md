# Windows packaging and private-preview releases

## Decision

OmniBrille uses pinned Inno Setup 6.7.3 for its Windows private-preview installer. It is a separate per-user application and installs no OmniSorSe binary, service, startup entry, telemetry component, updater, file association, or background companion.

The deterministic install path is:

```text
%LOCALAPPDATA%\Programs\OmniBrille\OmniBrille.exe
```

That path is already one of the bounded conventional locations searched by the committed OmniSorSe v2.5 RC locator. A normal installed handoff does not need `OMNISORSE_OMNIBRILLE_PATH`.

Inno Setup supplies reliable current-user install, one stable application ID, in-place upgrade/uninstall, Start Menu registration, running-app coordination, version metadata, and a modest open-source toolchain. MSIX was deferred because signing/identity and package-lifecycle constraints add friction to a private preview. WiX/MSI is unnecessarily heavy for this optional per-user app. NSIS is viable, but would require more lifecycle script of our own.

## Version and deployment model

Stage 8 is `0.6.0-preview.3`. `Directory.Build.props` is the source of truth:

- SemVer/informational version: `0.6.0-preview.3`;
- assembly compatibility version: `0.6.0.0`;
- Windows file/installer version: `0.6.0.3`.

Patch-like preview increments advance the final numeric file-version component. Pre-release labels progress through `preview.N`, `beta.N`, and `rc.N` before a separately approved stable version. No tag or release is created automatically.

The package remains `win-x64`, self-contained, non-trimmed, and multi-file. This costs disk space but avoids requiring a separate .NET runtime and avoids trimming/single-file risks in Avalonia XAML, reflection, and native library loading. Publish explicitly disables debug symbols; the installer also excludes PDBs and removes legacy root-level PDBs during an upgrade.

Artifact names are:

```text
OmniBrille-0.6.0-preview.3-win-x64-setup.exe
OmniBrille-0.6.0-preview.3-win-x64-setup.exe.sha256
OmniBrille-0.6.0-preview.3-win-x64-setup-manifest.json
OmniBrille-0.6.0-preview.3-win-x64-setup-dependencies.json
OmniBrille-0.6.0-preview.3-win-x64-setup-private-preview-notes.md
```

The manifest contains product/version, commit SHA, UTC build time, runtime/deployment, Explorer Protocol compatibility, signing status, installer size/hash, published-runtime size, and optional private workflow identity. It contains no username, developer path, token, or user content. Generated tester notes bind the exact filename, commit, hash, signing state, and workflow to concise installation/support guidance. The dependency document is a sanitized runtime package inventory, not a formal SPDX/CycloneDX SBOM; a formal SBOM was deferred to avoid adding a release-critical tool dependency before it provides a clear private-preview benefit.

## Reproducible commands

From a clean Windows checkout with the SDK pinned by `global.json` and PowerShell:

```powershell
dotnet restore .\OmniBrille.sln
.\build\Package-Windows.ps1 -BootstrapInnoSetup
```

`Get-InnoSetup.ps1` downloads the official Inno Setup 6.7.3 installer into ignored `artifacts/tools` and verifies its pinned SHA-256 before installing that build tool locally. An existing compiler may be supplied with `-InnoCompiler`.

The complete technical preview gate is:

```powershell
.\build\verify-release.ps1
```

It requires a clean checkout by default and performs version/stale-brand/tracked-artifact checks, restore, formatting, analyzer-enabled Release build, all tests, NuGet vulnerability audit, an informational outdated-package review, package build, publish-content audit, checksum/manifest validation, and `git diff --check`. `-AllowDirty` exists only for developing the release machinery; it is not a release gate. The script never publishes, tags, or creates a GitHub Release.

Outputs are below ignored `artifacts/publish/win-x64` and `artifacts/packages`. The publish directory is safely recreated for each build so removed runtime files cannot accumulate. Exact installer bytes can differ across machines because executable/installer timestamps and signing timestamps are not normalized; inputs, process, naming, version, content policy, and tool version are reproducible. A checksum therefore identifies one exact retained installer. Its `.sha256`, manifest, generated tester notes, and independent verification must agree; a hash from another rebuild is invalid for it.

## Signing architecture

Unsigned development packages remain the default. To sign, an Authenticode certificate with a private key must first be present in the current-user or local-machine `My` store. Supply only its thumbprint:

```powershell
$env:OMNIBRILLE_SIGNING_CERTIFICATE_THUMBPRINT = '<thumbprint>'
.\build\verify-release.ps1 -RequireSigning
```

The package script signs and validates `OmniBrille.exe` before installer compilation, then signs and validates the final installer using SHA-256 and a timestamp server. `-RequireSigning` fails immediately when no thumbprint is provided and fails if the certificate is missing, expired, lacks a private key, or produces a non-valid signature. An unsigned file is never described as signed in the manifest.

The manual private-preview workflow accepts `unsigned` or `signed`. In signed mode it requires repository secrets `OMNIBRILLE_SIGNING_PFX_BASE64` and `OMNIBRILLE_SIGNING_PFX_PASSWORD`, imports the certificate into the ephemeral runner user's certificate store, supplies only the thumbprint to the build, and removes both temporary PFX and imported certificate. Private keys/passwords are never source, command-line inputs, logs, or artifacts. A future signing service can replace certificate import without changing package semantics.

No production signing certificate is currently available. Stage 8 candidates are therefore unsigned unless externally managed credentials are supplied. Private testers should expect Windows reputation/unknown-publisher warnings and should verify the SHA-256 from the separately retained sidecar. Hash verification detects corruption; it does not authenticate an unsigned publisher. Testers should follow their organization's security policy rather than casually bypass protections.

## CI and private-preview workflow

Normal CI restores, formats, builds, tests, and audits NuGet packages on Windows and Ubuntu. The Windows leg also creates an unsigned installer plus its manifests/checksum and retains them privately for 14 days.

`.github/workflows/private-preview.yml` is `workflow_dispatch` only. It runs the full release check on Windows and retains one commit-named private candidate for 90 days. A second fresh hosted-Windows job has no source checkout: it downloads only that candidate, independently verifies installer/sidecar/manifest hashes, performs a per-user silent install, verifies registration and an installed OmniBrille window, then uninstalls and records the exact gate result. This hosted gate does not replace manual interaction, upgrade, or OmniSorSe companion testing. The workflow creates no tag, GitHub Release, or feed publication. Selecting signed mode without valid secrets is an intentional hard failure.

## Install, upgrade, and uninstall

The fixed Inno application ID makes a newer package an in-place upgrade. Standard close-app handling asks OmniBrille to close; the installer does not add a custom force-kill path. Upgrade refreshes the owned install directory and preserves exactly one Start Menu/uninstall registration.

Safe visual preferences remain at `%LOCALAPPDATA%\OmniBrille\visual-preferences.json`, outside the install directory, and intentionally survive upgrade/uninstall. They contain only theme, reduced-motion/effects, and the diagnostics toggle. Grants, bearer tokens, endpoints, selected roots, queries, opaque IDs, and Context caches are never persisted.

Uninstall removes application files, Start Menu entry, and uninstall registration. It leaves preferences, user files, and every OmniSorSe index/setting untouched. No service or daemon exists. Downgrade is not supported or promised; preview upgrade testing is forward-only.

The Stage 7 isolated-host lifecycle sample upgraded `0.6.0-preview.1` to `0.6.0-preview.2` in place while OmniBrille was running. Standard installer coordination closed the process, retained one Start Menu shortcut and one uninstall registration, preserved visual preferences byte-for-byte, and installed no PDBs. The local sample took 8.599 seconds to install preview.1, 10.093 seconds to upgrade, and 2.237 seconds to uninstall preview.2. The installed footprint was 222 files / 109,315,321 bytes (about 104.25 MiB). The final uninstall removed the install directory, shortcut, and product registration and retained `%LOCALAPPDATA%\OmniBrille\visual-preferences.json` by policy.

This validation used an isolated install lifecycle on the Windows 10 development host, not a fresh VM: Windows Sandbox and a suitable clean Windows VM were unavailable. The installed executable was launched from the deterministic install path and did not need the source checkout or a separately installed .NET runtime. A genuinely clean Windows VM remains a private-preview distribution gate in `RELEASE_CHECKLIST.md`.

The maintainer checklist in [`RELEASE_CHECKLIST.md`](../RELEASE_CHECKLIST.md) requires a clean Windows environment for fresh install, installed standalone, normal OmniSorSe handoff, in-place upgrade, uninstall, signature state, checksum, artifact/privacy audit, and release notes. Source-tree execution does not satisfy that manual gate.

## Branding and licensing

The executable, window, Start Menu entry, installer, and uninstall metadata use `OmniBrille`. The Stage 7 mark is a release-quality provisional blue/cyan spatial-navigation asset with transparent PNG source and complete Windows icon sizes; see [`assets/branding/README.md`](../assets/branding/README.md). It can later be professionally refined without changing resource names.

This private repository currently has no selected license. Stage 8 does not choose one. Public distribution should not proceed until the maintainer makes and records that decision.
