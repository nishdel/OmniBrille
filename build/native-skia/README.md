# OmniBrille DNG-free SkiaSharp native asset

This package contains a project-built Windows x64 `libSkiaSharp.dll` from the exact SkiaSharp 3.119.4 upstream source. The optional Adobe DNG/RAW codec is disabled; the DNG SDK and RAW-only PIEX sources are not fetched or linked.

This is not an official Microsoft-signed SkiaSharp native package. The DLL and OmniBrille installer are currently unsigned. The package embeds the checksum-bound build log, evaluated GN arguments and dependency closure, normalized exports, toolchain details, patches, and verification result. The source pins, build procedure, accepted SHA-256, maintenance policy, and validation requirements are authoritative in `docs/native-skia.md` in the OmniBrille repository.

The package retains SkiaSharp's MIT license and a conservatively derived third-party notice set for the linked build. It must be consumed with the official `SkiaSharp.NativeAssets.Win32` runtime assets explicitly excluded.
