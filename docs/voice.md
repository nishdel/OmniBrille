# Local click-to-toggle voice

Voice provides optional, one-shot input to OmniBrille. The current source includes Unreleased microphone/Stop and quiet-completion refinements to the Stage 9 boundary. Each explicit utterance becomes either a deterministic UI/navigation command or an existing Search request. There is no wake word, always-listening mode, conversational loop, destructive voice action, LLM intent parser, cloud transcription requirement, or telemetry.

## Technology decision

OmniBrille uses a replaceable `ISpeechRecognitionProvider`. The initial provider invokes the MIT-licensed [whisper.cpp](https://github.com/ggml-org/whisper.cpp) `whisper-cli` executable with structured process arguments and an installer-owned quantized English model. Windows microphone capture uses the MIT-licensed NAudio WinMM package and records 16 kHz, mono, 16-bit PCM into a bounded in-memory buffer.

This was selected over platform speech APIs because it gives an explicit offline path and keeps recognition behavior independent of a cloud account. Vosk remains a plausible future provider, but a second recognizer would add native/runtime/model maintenance without improving the initial replaceable boundary. A long-lived whisper server/native model host was rejected for this stage: process-per-utterance has higher cold latency, but it adds no helper service, no background listener, and has a smaller security/lifecycle surface.

The Windows x64 installer contains one pinned local voice bundle:

- whisper.cpp v1.9.2 Windows x64 CPU runtime at commit `306c88f4d1286aec1bf96e544632897886af5501`;
- `ggml-base.en-q5_1.bin` from `ggerganov/whisper.cpp` commit `c521a4b02f422512d734391fdf08bb08c0862f68`.

The build downloads only those exact upstream assets and verifies the archive/model plus every installed runtime file with SHA-256. Packaging binds `Voice/voice-bundle-manifest.json`; the application independently rechecks the executable, all native dependencies, and model hashes whenever it resolves the bundle. Only the successful bounded `--help` capability probe is cached after that exact validation. The installed application never scans for, downloads, updates, or replaces voice assets.

```text
%LOCALAPPDATA%\Programs\OmniBrille\Voice\Runtime\whisper-cli.exe
%LOCALAPPDATA%\Programs\OmniBrille\Voice\Models\ggml-base.en-q5_1.bin
```

An unavailable or integrity-failing bundle reports a reinstall action and leaves every nonvoice path usable.

## Setup and use

1. Install the Windows x64 package containing the pinned voice bundle.
2. Select English, or Auto-detect for free-form transcription. Deterministic commands are English-only.
3. Click the **microphone** or press `Ctrl+Shift+Space` once to enable Voice and begin. No enablement submenu is required; this action owns the capability check and capture startup.
4. Click **Stop**, press the same shortcut again, or pause for two seconds after detected input to stop and transcribe. During initialization or transcription the same control cancels the active operation. `Escape` or **Cancel** also cancels capture/transcription.

Microphone activation is visible in the bottom HUD through the Stop icon, level meter, and text state. Capture automatically stops after 45 seconds. The quiet detector uses normalized PCM peak samples reported approximately every 100 ms: a level of at least 0.025 marks detected input, and two seconds of lower levels then requests completion. This is a level gate, not a speech-recognition model; noise can trigger it. Initial silence alone never requests quiet-completion, and the duration limit reports no detected speech without submitting an utterance. Reduced motion replaces the listening pulse with a static high-contrast state; Reduced visual effects removes optional glow while keeping state explicit. The recognized text appears briefly and can be corrected in the normal Search box.

## Deterministic grammar

The parser normalizes casing, spacing, and punctuation, then applies a small explicit registry. Representative phrases:

- navigation: `Go back`, `Up`, `Parent`, `Root`, `Home`, `Open Documents`, `Enter Documents`, `Focus Downloads`, `Open selected`, `Zoom in`, `Zoom out`, `Reset view`;
- mode: `Switch to Structure`, `Switch to Context`, `Show what is related to this`;
- theme: `Use dark mode`, `Use light mode`;
- UI: `Open details`, `Close details`, `Show list`, `Hide list`, `Clear search`, `Cancel`.

`Open/Enter/Go into <name>` and `Focus <name>` act only on one exact visible-node match. A missing or ambiguous visible match produces explicit feedback through the existing bounded Search path rather than selecting arbitrarily. All other transcripts are Search queries; common prefixes such as `find`, `show me`, and `search for` are removed. No destructive intents exist.

Standalone voice Search uses the current bounded standalone structural provider. Connected voice Search calls the existing Explorer Protocol v1 OmniSorSe Search provider. Context is still based only on server-authored `GetNeighborhood`/`GetRelated` data; voice does not infer a relationship.

## Privacy and security

- Voice readiness is lazy and cannot prevent application startup.
- Microphone capture begins only after explicit activation and runs only inside OmniBrille.
- Quiet and duration callbacks retain their originating operation identity. Cancellation, provider replacement, and a newer listening operation invalidate deferred callbacks/results before transcript display or command execution. Returned PCM clips and the capture buffer are cleared when their ownership ends.
- The in-memory capture is bounded to 45 seconds. If whisper.cpp requires a file, OmniBrille creates a GUID-named, app-owned workspace on a local, non-network Windows volume, rejects reparse-point ancestors, and passes its WAV path through `ProcessStartInfo.ArgumentList`. Cancellation or timeout kills the process tree and waits for exit before cleanup. Audio/transcript buffers are zeroed, deletion is retried with bounded backoff, and the operation reports cleanup failure instead of claiming that locked sensitive files were removed. Provider startup schedules one background pass over at most sixteen stale, application-shaped workspaces older than one hour; it rejects files larger than OmniBrille's producer bounds before zeroing, and readiness/transcription await the result.
- Raw audio and transcripts are not persisted or logged. The transcript preview is cleared after 12 seconds. Search receives the recognized query through the same in-memory flow as typed Search.
- Sanitized diagnostics contain only voice state, bounded timing, transcript length, classification, and a safe error category—never audio, transcript text, runtime/model path, query, token, or user content.
- The executable, every native dependency, and model must match compiled/packaged hashes below the fixed application directory on every resolution. No shell command is constructed; the child receives a minimal environment; process output/time are bounded; transcription uses at most four worker threads; cancellation kills the process tree and waits for termination before returning.
- Model/session state is lazy. Missing/corrupt runtime/model, microphone unavailability, permission denial, and provider failure keep typed interaction available.

## Current limits

- Real capture is implemented for Windows WinMM, but no live microphone device was available for the retained validation run, so hardware capture remains unvalidated. Linux/macOS microphone runtime support is not claimed; source/build tests remain cross-platform.
- Commands are English-only. Free-form Search transcription may use whisper.cpp auto-detection, subject to the selected model.
- `whisper-cli` loads the model per utterance, so cold transcription latency and memory depend on CPU/model. There is no warm persistent speech service.
- Confidence is not fabricated because `whisper-cli` JSON does not provide a single reliable utterance-confidence value through this adapter.
- Input-device selection and shortcut customization are deferred. The default Windows input device and `Ctrl+Shift+Space` are used.
- The installer owns the runtime/model and uninstall removes those installed assets. Preferences and user content remain outside installer ownership.
