# DNG-free SkiaSharp native asset

OmniBrille's Windows renderer uses the managed SkiaSharp API selected by Avalonia. The official `SkiaSharp.NativeAssets.Win32` 3.119.4 binary also contains Adobe DNG/RAW decoding code that OmniBrille does not use. This release-remediation recipe builds one Windows x64 `libSkiaSharp.dll` from the unmodified upstream API source with that optional codec disabled.

This is authoritative provenance and upgrade guidance for the replacement native asset. It is not a fork of SkiaSharp and does not change OmniBrille's renderer design.

Until the proof bundle has passed review and the separately named replacement package is selected by restore, this recipe is a candidate-build procedure rather than evidence that a packaged OmniBrille binary is DNG-free. The release gates must bind the reviewed native hash through restore, publish, installation, and the final installer.

## Pinned upstream source

- SkiaSharp version: `3.119.4`
- SkiaSharp commit: `f568ac94dd768ef9a2f593537cfde2dd0d348ef5`
- Pinned `mono/skia` commit: `7dbfc07dd33181f84e0958afb7ee805c6c769f0b`
- Pinned `depot_tools` commit: `8fecc592a290769242d5098666cee8d29b7f0523`
- Native ABI: Skia milestone 119, C increment 0
- Build argument: `skia_use_dng_sdk=false`

Upstream's Windows Cake target exposes additional GN arguments and builds the same `SkiaSharp` native target used by the official package. At the pinned Skia commit, the optional `raw` target is enabled only when `skia_use_dng_sdk`, JPEG decoding, and PIEX are all enabled. Disabling DNG therefore removes the RAW/DNG codec and its DNG/PIEX link dependencies without changing the managed or exported C API.

The build also removes the exact pinned DNG and RAW-only PIEX entries from Skia's local `DEPS` file before `git-sync-deps`. This is a fail-closed source-acquisition guard: the build fails if either upstream entry changes, and those unused sources are not downloaded. The generated patch is retained in the proof bundle; no upstream source branch or permanent fork is maintained.

## Reproduce

The build requires Windows, network access, Python 3, Visual Studio 2022 C++ tools with an x64 Spectre library, the upstream-pinned .NET SDK and Cake tool, and LLVM 19.1.1. SkiaSharp v3.119.4's `scripts/install-llvm.ps1` is the authoritative LLVM provisioner.

Supply the official 3.119.4 Windows x64 DLL from the NuGet package as the ABI reference:

```powershell
./build/Build-DngFreeSkia.ps1 `
  -OfficialReferenceDll "$env:USERPROFILE\.nuget\packages\skiasharp.nativeassets.win32\3.119.4\runtimes\win-x64\native\libSkiaSharp.dll"
```

The reference must have SHA-256 `7DEC3BA900AB353491E6446F0083739924C6F8DD668832E2F09D38EBFFDBBE1C`. The script refuses to reuse work/output directories and leaves its source worktree available for inspection.

The output directory contains at least:

- `libSkiaSharp.dll` — the candidate DNG-free native asset;
- `provenance.json` — immutable sources, toolchain, build configuration, hashes, and `NotSigned` status;
- `evaluated-gn-args.txt` — evaluated GN values, including DNG disabled;
- `gn-dependencies.txt` — the complete native target dependency closure;
- `exports.txt` — normalized official/replacement export sets and equality result;
- `build.log` — checkout, tool restore, and upstream build log;
- `skia-deps-dng-removal.patch`, `verification.txt`, supporting tool/dependency output, and `proof-bundle.sha256`.

The script fails unless all of these are true:

1. both upstream commits match the pins;
2. exactly the reviewed DNG `DEPS` entry is removed before dependency sync;
3. DNG source is not fetched;
4. GN evaluates `skia_use_dng_sdk=false`;
5. the generated dependency closure/build files contain no DNG, `SkRawCodec`, or PIEX linkage;
6. strong DNG/RAW markers are absent from the resulting DLL;
7. its normalized C export set equals the official 3.119.4 DLL;
8. the project-built native DLL is `NotSigned`.

These are native-code provenance gates, not renderer qualification. Before adopting an output, OmniBrille must still run the High-risk renderer, visual, performance, build/test, packaging, exact-installer, install/relaunch/uninstall, and adversarial validation routed by [`engineering/risk-and-validation.md`](engineering/risk-and-validation.md).

## Upgrade procedure

Do not reuse this binary with another managed SkiaSharp version.

For an upgrade:

1. inspect the new official Windows package and upstream build files before changing pins;
2. verify whether an official DNG-free package now exists and prefer it if suitable;
3. update the exact SkiaSharp and Skia commits, ABI metadata, official reference hash, DNG dependency revision, and pinned toolchain together;
4. confirm that disabling DNG still removes only an unused optional codec and review all notice changes;
5. generate a fresh proof bundle and independently review it;
6. integrate only the reviewed DLL hash and rerun the full renderer/release qualification.

The installed third-party notices must describe the code actually linked into the adopted binary. Adobe DNG terms and PIEX notices are not applicable only after the proof bundle and final packaged-byte scan establish that those components are absent.
