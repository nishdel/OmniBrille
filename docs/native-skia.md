# DNG-free SkiaSharp native asset

OmniBrille's Windows renderer uses the managed SkiaSharp API selected by Avalonia. The official `SkiaSharp.NativeAssets.Win32` 3.119.4 binary also contains Adobe DNG/RAW decoding code that OmniBrille does not use. This release-remediation recipe builds one Windows x64 `libSkiaSharp.dll` from the unmodified upstream API source with that optional codec disabled.

This is authoritative provenance and upgrade guidance for the replacement native asset. It is not a fork of SkiaSharp and does not change OmniBrille's renderer design.

The accepted Windows x64 DLL has SHA-256 `A5A4C1EECE528A5BED7C98889435BD8214BBA610F963FE80E35256A91508B5DD` and was reproduced byte-for-byte by two independent hosted builds before package integration. It is stored in the distinctly named local package `OmniBrille.SkiaSharp.NativeAssets.Win32.NoDng` 3.119.4.2 (package SHA-256 `880578F572F541A58C47418AABF881D5F0665A4B1A48AD26753549D01EDDC10C`). `NuGet.Config` maps that exact ID to the repository package source; Desktop explicitly excludes every asset from the transitive official Win32 native package. Release gates bind the reviewed hash through restore, publish, manifest, installation, and final-installer qualification.

GitHub Actions run [32423416314](https://github.com/nishdel/OmniBrille/actions/runs/32423416314) produced the first deterministic candidate and stopped at the then-old accepted-hash gate. Run [32425650654](https://github.com/nishdel/OmniBrille/actions/runs/32425650654) rebuilt the same recipe after only the expected candidate hash and its explanatory test/documentation were updated, reproducing the exact DLL bytes and producing the complete accepted proof bundle embedded in package 3.119.4.2.

## Pinned upstream source

- SkiaSharp version: `3.119.4`
- SkiaSharp commit: `f568ac94dd768ef9a2f593537cfde2dd0d348ef5`
- Pinned `mono/skia` commit: `7dbfc07dd33181f84e0958afb7ee805c6c769f0b`
- Pinned `depot_tools` commit: `8fecc592a290769242d5098666cee8d29b7f0523`
- Native ABI: Skia milestone 119, C increment 0
- Build arguments: `skia_use_dng_sdk=false`, `/Brepro` in the compiler and linker flags, and `/PDBALTPATH:libSkiaSharp.pdb` in the linker flags

Upstream's Windows Cake target exposes additional GN arguments and builds the same `SkiaSharp` native target used by the official package. At the pinned Skia commit, the optional `raw` target is enabled only when `skia_use_dng_sdk`, JPEG decoding, and PIEX are all enabled. Disabling DNG therefore removes the RAW/DNG codec and its DNG/PIEX link dependencies without changing the managed or exported C API. A fail-closed local patch to that exact pinned Windows build file appends `/Brepro` to its existing compile and link flag arrays, because the unpatched target emits wall-clock timestamps in the PE and debug-directory metadata. It also sets the embedded CodeView reference to the literal `libSkiaSharp.pdb`; otherwise the absolute checkout directory becomes part of the DLL and byte reproduction depends on using the same build path. The build rejects any absolute Windows PDB path in the output. The patch is retained in the proof bundle; path-independent reproducible native bytes are required before OmniBrille accepts a replacement hash.

The build also removes the exact pinned DNG and RAW-only PIEX entries from Skia's local `DEPS` file before `git-sync-deps`. This is a fail-closed source-acquisition guard: the build fails if either upstream entry changes, and those unused sources are not downloaded. The generated patch is retained in the proof bundle; no upstream source branch or permanent fork is maintained. A second fail-closed patch changes upstream's `global.json` from `latestFeature` roll-forward to the exact reviewed .NET SDK, preventing an ambient hosted-runner SDK from silently changing the toolchain.

## Reproduce

The build requires Windows, network access, Python 3, Visual Studio 2022 C++ tools with an x64 Spectre library, the upstream-pinned .NET SDK and Cake tool, and LLVM 19.1.1. SkiaSharp v3.119.4's `scripts/install-llvm.ps1` is the authoritative LLVM provisioner.

Supply the official 3.119.4 Windows x64 DLL from the NuGet package as the ABI reference:

```powershell
./build/Build-DngFreeSkia.ps1 `
  -OfficialReferenceDll "$env:USERPROFILE\.nuget\packages\skiasharp.nativeassets.win32\3.119.4\runtimes\win-x64\native\libSkiaSharp.dll"
```

The reference must have SHA-256 `7DEC3BA900AB353491E6446F0083739924C6F8DD668832E2F09D38EBFFDBBE1C`. The script refuses to reuse work/output directories and leaves its source worktree available for inspection. Its fail-closed `global.json` source hash normalizes Git's platform line endings before hashing; native binaries and other artifact hashes remain byte-exact.

The output directory contains at least:

- `libSkiaSharp.dll` — the candidate DNG-free native asset;
- `provenance.json` — immutable sources, toolchain, build configuration, hashes, and `NotSigned` status;
- `evaluated-gn-args.txt` — evaluated GN values, including DNG disabled;
- `gn-dependencies.txt` — the complete native target dependency closure;
- `exports.txt` — normalized official/replacement export sets and equality result;
- `build.log` — checkout, tool restore, and upstream build log;
- `skia-deps-dng-removal.patch`, `sdk-selection.patch`, `windows-reproducibility.patch`, `verification.txt`, supporting tool/dependency output, and `proof-bundle.sha256`.

Create the repository package only from a reviewed proof directory:

```powershell
./build/New-DngFreeSkiaPackage.ps1 -ProofDirectory <proof-directory>
```

The package builder verifies every proof checksum, accepted native and notice hashes, DNG/RAW marker absence, ABI-comparison result, source-acquisition result, and `NotSigned` state before packing. It embeds the complete checksum-bound proof bundle rather than depending on the hosted workflow artifact's retention period. The target-aware notice is derived by `New-DngFreeSkiaNotice.ps1` from the exact official 3.119.4 notice; only the unfetched/unlinked DNG SDK and RAW-only PIEX sections are removed, and all other upstream sections remain conservatively.

The script fails unless all of these are true:

1. all three upstream commits match the pins;
2. exactly the reviewed DNG `DEPS` entry is removed before dependency sync;
3. DNG source is not fetched;
4. GN evaluates `skia_use_dng_sdk=false`, retains `/Brepro` in both compile and link flags, and retains the path-independent `/PDBALTPATH:libSkiaSharp.pdb` linker flag;
5. the generated dependency closure/build files contain no DNG, `SkRawCodec`, or PIEX linkage;
6. strong DNG/RAW markers are absent from the resulting DLL;
7. its normalized C export set equals the official 3.119.4 DLL;
8. the DLL embeds `libSkiaSharp.pdb` without an absolute build-machine path;
9. the project-built native DLL is `NotSigned`;
10. the accepted output hash is exactly the reviewed value above.

These are native-code provenance gates, not renderer qualification. Before adopting an output, OmniBrille must still run the High-risk renderer, visual, performance, build/test, packaging, exact-installer, install/relaunch/uninstall, and adversarial validation routed by [`engineering/risk-and-validation.md`](engineering/risk-and-validation.md).

## v1.0 qualification evidence

Release candidate `e7dc5981c10ae2f9a48f3b98f4d67073566aa1f2` exercised package 3.119.4.2 on Windows 10 build 19045 at 125% display scaling. The exact locally installed candidate completed Dark and Light graph rendering, text/icons/lines, structural Search, the synchronized accessible list, folder drill-down and Back, normal close/relaunch, and uninstall cleanup. The reviewed captures and their exact installer/native hashes are retained beside the [public screenshots](assets/screenshots/README.md). This was a visual/interaction review, not a pixel-equivalence test or manual screen-reader certification.

The existing headless renderer-pressure test was run twice per binary on the same host and current managed application, with each value already the median of five warmed frames. Milliseconds are ranges across the two invocations:

| Scene | DNG-free Full / Reduced / Search | Official DNG-bearing Full / Reduced / Search |
|---|---|---|
| 8-node Hybrid | 2.735–3.020 / 2.627–2.996 / 3.782–3.866 | 3.191–3.245 / 2.786–3.001 / 4.148–4.495 |
| 24-node Hybrid | 3.889–3.980 / 3.760–3.770 / 6.678–6.710 | 3.877–4.013 / 3.784–3.847 / 6.748–6.829 |
| 48-node Hybrid | 5.638–6.172 / 6.216–6.326 / 8.383–8.513 | 5.676–5.918 / 6.468–8.000 / 10.523–13.671 |

All sampled DNG-free paths remained below the repository's 16.7 ms local engineering target, and no meaningful regression was observed. These same-host headless samples are proportionate regression evidence, not a guarantee for every GPU, filesystem, or machine.

Hosted run [32428287100](https://github.com/nishdel/OmniBrille/actions/runs/32428287100) then rebuilt the accepted DLL hash, passed the complete release gate, produced an unsigned 1.0.0 candidate, and passed independent artifact integrity plus fresh-runner install, launch, normal close/relaunch, registration, uninstall, and cleanup. A final release commit must repeat the exact-artifact gate; an earlier candidate is never substituted for the published bytes.

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
