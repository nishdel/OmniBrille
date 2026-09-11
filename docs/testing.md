# Testing and evidence

Use the [risk guide](engineering/risk-and-validation.md) to select focused checks and specialists. The commands below are the ordinary source qualification path; the clean [release gate](PACKAGING.md) additionally packages and validates exact artifacts. Current test code and CI are authoritative for counts and outcomes.

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet restore .\OmniBrille.sln
.\build\Test-EngineeringDocs.ps1
dotnet format .\OmniBrille.sln --verify-no-changes --no-restore
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
.\build\Test-NuGetVulnerabilities.ps1
git diff --check
```

Run from the repository root with the SDK in [global.json](../global.json). Preserve failures, platform-specific skips, exact source commit, and commands in the run record. Do not call a stale build final-tree evidence after source changes. Normal [CI](../.github/workflows/ci.yml) runs Windows and Ubuntu qualification and builds the Windows installer on its Windows leg.

## Coverage and limits

| Evidence | What it establishes | What it does not establish |
| --- | --- | --- |
| [Ordinary tests](../tests/OmniBrille.Tests) | Core bounds/layout/semantics, provider authority, protocol/session races, Voice/parser/process behavior, audio samples, preferences, privacy, and release assertions | Live microphone/noise, physical audio character, external host compatibility, or interactive UI quality |
| [Headless tests](../tests/OmniBrille.HeadlessTests) | Shell/input/action results, bounded graph/list/automation parity, label visibility, metadata reveal, and representative allocation/cache behavior | Native GPU/DPI/chrome behavior, motion comfort, or actual screen-reader certification |
| [Software renders](assets/screenshots/issue-audit-2026-09-11/README.md) | Inspectable deterministic Skia composition under recorded fixture conditions | Exact installed Windows composition, animation, OS scaling, or hardware performance |
| [Engineering docs check](../build/Test-EngineeringDocs.ps1) | Required entry points, repository-relative Markdown links, and closed Mermaid fences | Semantic truth, external URL availability, or current GitHub state |
| [Release gate](../build/verify-release.ps1) and hosted artifact-only workflow | Exact package/version/hash/signature policy, allowed contents, licenses, installation, process lifecycle, registration, and uninstall evidence as actually run | Native feature acceptance, real microphone/audio, live Connected host, or unexecuted upgrade paths |

The v1.2.0 implementation audit qualified 377 tests: Windows passed 376 with one platform-specific reparse/symlink fixture skipped; Ubuntu passed all 377. Those are [PR #11 audit results](runs/2026-09-11-github-issue-audit.md), not a substitute for final release-tree test results. Final totals and exact-artifact outcomes belong with the [v1.2.0 release](https://github.com/nishdel/OmniBrille/releases/tag/v1.2.0) and its linked runs.

## Manual acceptance after installation

Use non-private test content and record installer SHA-256, Windows version, display/text scaling, device details where relevant, and the actual steps/results. The [interaction contract](interaction-state-contract.md) gives expected navigation, Details, Voice, sound, reduced settings, and accessibility-projection behavior.

For v1.2.0, implementation is complete for issues #1–#9. Remaining acceptance covers native controls/transitions (#3), real microphone/noise and quiet completion (#4), visual/concept fidelity (#5), motion comfort/native readability (#6), physical sound character (#7), and dense layouts at extreme text/display scales. Narrator/NVDA, high contrast, custom chrome, GPU behavior, live-host compatibility, and wider platform support require their own evidence before stronger claims. These are separate manual validation items; publication for continued testing does not imply they passed.

Record fresh install and upgrade from the exact public predecessor separately. Preserve safe preferences and user data; only installer-owned files should be removed on uninstall. The [release checklist](../RELEASE_CHECKLIST.md) enumerates the evidence without marking unperformed manual checks complete.
