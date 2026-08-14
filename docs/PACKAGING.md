# Windows packaging

## Decision

OmniBrille uses Inno Setup 6.7.3 for its first serious Windows package. The installer is per-user and independent of OmniSorSe. It installs no service, auto-start entry, telemetry component, file association, or OmniSorSe binary.

The deterministic install path is:

```text
%LOCALAPPDATA%\Programs\OmniBrille\OmniBrille.exe
```

This is already one of the bounded conventional locations searched by the committed OmniSorSe v2.5 release-candidate companion locator. `OMNISORSE_OMNIBRILLE_PATH` is not required for an installed workflow.

Inno Setup was selected because it provides reliable current-user installation, one stable application ID, upgrade/uninstall handling, Start Menu registration, close-app coordination, version metadata, and a straightforward signing hook without requiring Store identity or an oversized toolchain. MSIX was deferred because identity/signing and package lifecycle constraints add friction to the current private preview. WiX/MSI was unnecessarily heavy for a per-user optional companion. NSIS remains viable but would require more custom lifecycle scripting for no current benefit.

## Version and deployment model

Stage 6 is `0.6.0-preview.1`; the numeric Windows file version is `0.6.0.0`. Pre-1.0 semantic versions may make breaking product changes. Preview suffixes identify engineering packages that are not public stable releases. `Directory.Build.props` is the source of truth for assembly, product, and installer version inputs.

The package is:

- `win-x64`;
- self-contained, so users do not need to install .NET separately;
- non-trimmed, to preserve reflection/XAML behavior;
- multi-file, to minimize Avalonia/native extraction risk;
- named `OmniBrille-<version>-win-x64-setup.exe`.

The compressed Stage 6 development installer is approximately 35 MB and the published runtime is approximately 210 MB before installer exclusions. PDB files are deliberately excluded from the installed package, and an upgrade removes legacy root-level PDBs from this installer-owned directory, so developer source paths are not shipped.

## Reproducible command

From a clean checkout with the .NET 8 SDK and PowerShell:

```powershell
dotnet restore .\OmniBrille.sln
.\build\Package-Windows.ps1 -BootstrapInnoSetup
```

`Get-InnoSetup.ps1` downloads the official Inno Setup 6.7.3 installer into the ignored `artifacts/tools` directory and verifies its pinned SHA-256 before a local, non-advertised tool installation. Alternatively, pass an existing compiler explicitly:

```powershell
.\build\Package-Windows.ps1 -InnoCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Outputs are written below ignored `artifacts/publish/win-x64` and `artifacts/packages`. CI uses the same script on Windows and retains, but does not publicly release, the unsigned installer.

## Install, upgrade, and uninstall semantics

The fixed Inno application ID makes a newer package an in-place upgrade. The installer coordinates with a running OmniBrille process through standard close-app behavior, updates the one install directory, and preserves one Start Menu and one uninstall entry. It does not force-kill without installer coordination.

Small visual preferences remain in `%LOCALAPPDATA%\OmniBrille\visual-preferences.json` and are intentionally outside the install directory. Upgrade and uninstall preserve these safe theme/effects/diagnostics choices. They contain no bearer tokens, grant, selected root, opaque node ID, search query, or Context cache. Protocol credentials and session state exist only in process memory.

Uninstall removes the application directory, Start Menu entry, and uninstall registration. It does not touch OmniSorSe, its indexes/settings, user files, or retained OmniBrille visual preferences. There is no installed service or background process to remove.

## Signing readiness

Stage 6 installers and executables are unsigned development artifacts. No private key or placeholder production certificate is committed. A future release pipeline should Authenticode-sign the published executable and final installer after build, using an externally supplied certificate/secret, then verify signatures before publication. The current scripts and deterministic artifact naming provide that hook without weakening present builds.

## Privacy audit

The installer includes only self-contained application/runtime files and branded assets. It excludes PDBs and must never include logs, databases, screenshots, test fixtures, user paths/content, bearer tokens, handoff grants, developer configuration, or OmniSorSe state. No telemetry is present. Inspect package contents and the installed directory as a release gate.

The bundled blue/cyan spatial-node icon is a temporary, replaceable branded asset created for packaging consistency; it is not a claim of final logo completion.
