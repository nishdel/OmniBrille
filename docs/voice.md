# Local push-to-talk voice

Stage 9 adds optional, one-shot voice input to OmniBrille. It is not a general assistant: each explicit utterance becomes either a deterministic UI/navigation command or an existing Search request. There is no wake word, always-listening mode, conversational loop, destructive voice action, LLM intent parser, cloud transcription requirement, or telemetry.

## Technology decision

OmniBrille uses a replaceable `ISpeechRecognitionProvider`. The initial provider invokes the MIT-licensed [whisper.cpp](https://github.com/ggml-org/whisper.cpp) `whisper-cli` executable with structured process arguments and a user-provided GGML model. Windows microphone capture uses the MIT-licensed NAudio WinMM package and records 16 kHz, mono, 16-bit PCM into a bounded in-memory buffer.

This was selected over platform speech APIs because it gives an explicit offline path and keeps recognition behavior independent of a cloud account. Vosk remains a plausible future provider, but a second recognizer would add native/runtime/model maintenance without improving the initial replaceable boundary. A long-lived whisper server/native model host was rejected for this stage: process-per-utterance has higher cold latency, but it adds no helper service, no background listener, and has a smaller security/lifecycle surface.

The standard installer includes the small NAudio capture assemblies only. It does **not** include whisper.cpp or a speech model and never downloads either. Recommended English models are:

- `ggml-base.en.bin` (about 142 MiB): the default recommendation for better short-query accuracy;
- `ggml-tiny.en.bin` (about 75 MiB): lower disk/memory and usually lower latency, with reduced accuracy.

Model/runtime distribution remains the user's responsibility, which keeps model licensing and several hundred megabytes out of the standard package. OmniBrille searches only explicit configured paths or these bounded conventional locations:

```text
%LOCALAPPDATA%\OmniBrille\Voice\Runtime\whisper-cli.exe
%LOCALAPPDATA%\OmniBrille\Voice\Models\ggml-base.en.bin
%LOCALAPPDATA%\OmniBrille\Voice\Models\ggml-tiny.en.bin
```

It never scans the filesystem and never downloads silently.

## Setup and use

1. Obtain `whisper-cli` and a compatible GGML model from sources whose license and integrity you trust.
2. In **Settings → Local voice**, enable Voice and enter absolute runtime/model paths. Alternatively, use the conventional locations above.
3. Select English, or Auto-detect for free-form transcription. Deterministic commands are English-only in Stage 9.
4. Click **Push to talk** or press `Ctrl+Shift+Space` once to begin.
5. Click/press the same control again to stop and transcribe. `Escape` or **Cancel** stops capture/transcription.

Microphone activation is always visible in the bottom HUD. Capture automatically stops after 45 seconds. Reduced motion replaces the listening pulse with a static high-contrast state; Reduced visual effects removes optional glow while keeping state explicit. The recognized text appears briefly and can be corrected in the normal Search box.

## Deterministic grammar

The parser normalizes casing, spacing, and punctuation, then applies a small explicit registry. Representative phrases:

- navigation: `Go back`, `Open Documents`, `Focus Downloads`, `Zoom in`, `Zoom out`, `Reset view`;
- mode: `Switch to Structure`, `Switch to Context`, `Show what is related to this`;
- theme: `Use dark mode`, `Use light mode`;
- UI: `Open details`, `Close details`, `Show list`, `Hide list`, `Clear search`, `Cancel`.

`Open <name>` and `Focus <name>` act only on one exact visible-node match. A missing or ambiguous visible match becomes a normal Search rather than selecting arbitrarily. All other transcripts are Search queries; common prefixes such as `find`, `show me`, and `search for` are removed. No destructive intents exist.

Standalone voice Search uses the current bounded standalone structural provider. Connected voice Search calls the existing Explorer Protocol v1 OmniSorSe Search provider. Context is still based only on server-authored `GetNeighborhood`/`GetRelated` data; voice does not infer a relationship.

## Privacy and security

- Voice is off by default and cannot prevent application startup.
- Microphone capture begins only after explicit activation and runs only inside OmniBrille.
- The in-memory capture is bounded to 45 seconds. If whisper.cpp requires a file, OmniBrille creates a GUID-named, app-owned temporary WAV, passes its path through `ProcessStartInfo.ArgumentList`, and deletes the entire utterance workspace in `finally` after success, cancellation, or failure.
- Raw audio and transcripts are not persisted or logged. The transcript preview is cleared after 12 seconds. Search receives the recognized query through the same in-memory flow as typed Search.
- Sanitized diagnostics contain only voice state, bounded timing, transcript length, classification, and a safe error category—never audio, transcript text, runtime/model path, query, token, or user content.
- The runtime executable and model path must resolve to explicit absolute files. No shell command is constructed, process output/time are bounded, and cancellation kills the process tree.
- Model/session state is lazy. Missing/corrupt runtime/model, microphone unavailability, permission denial, and provider failure keep typed interaction available.

## Current limits

- Real capture is implemented and validated for Windows WinMM. Linux/macOS microphone runtime support is not claimed; source/build tests remain cross-platform.
- Commands are English-only. Free-form Search transcription may use whisper.cpp auto-detection, subject to the selected model.
- `whisper-cli` loads the model per utterance, so cold transcription latency and memory depend on CPU/model. There is no warm persistent speech service.
- Confidence is not fabricated because `whisper-cli` JSON does not provide a single reliable utterance-confidence value through this adapter.
- Input-device selection and shortcut customization are deferred. The default Windows input device and `Ctrl+Shift+Space` are used.
- OmniBrille never installs or deletes user-provided runtime/model files.
