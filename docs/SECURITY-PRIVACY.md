# Security and privacy posture

OmniBrille is local-first and has no telemetry, cloud upload, crash-reporting service, always-listening microphone, background recorder, auto-start task, daemon, or destructive file operation. Microphone access is optional and begins only through an explicit click/shortcut; a second activation or two seconds of quiet after detected input stops and transcribes.

## Data authority

- Standalone reads only the folder explicitly selected for that session. It does not crawl unrelated drives, persist the selected root, follow directory reparse points recursively, or modify files.
- Connected mode reads only opaque nodes and roots authorized by the live OmniSorSe grant. It never treats a displayed path as permission and never falls back to local filesystem access for a connected node.
- Context nodes, relationships, reasons, ranking strength, evidence, and provenance come only from OmniSorSe. OmniBrille retains at most eight bounded Context snapshots in process memory and never persists an intelligence copy.

## Handoff and local transport

The committed OmniSorSe v2.5 RC launches OmniBrille with a random one-time pipe name—not a bearer token. A current-user-only one-connection handoff pipe transfers the strict bounded grant. Session ID, 256-bit bearer secret, endpoint, opaque node IDs, and Context caches stay in process memory, expire with the server-owned grant, and are invalidated on a new session. They are excluded from UI, normal diagnostics, preferences, release metadata, and logs.

Explorer Protocol requests use current-user named pipes, explicit major-version/capability negotiation, strict framing/JSON, response identity checks, size/count limits, timeouts, and stale-generation rejection. There is no LAN/cloud listener. Protocol errors preserve Standalone availability.

## Installation and release

The Windows installer is per-user and requires the lowest privilege level. It installs only into `%LOCALAPPDATA%\Programs\OmniBrille`, creates a Start Menu entry and uninstall registration, and installs no service, file association, startup entry, updater, or OmniSorSe binary.

Development/release-candidate packages may be unsigned. A signed release path requires externally supplied GitHub secrets, imports the certificate only into the ephemeral runner user store, signs the application and installer, verifies both, then removes the imported certificate. Private keys and passwords must never enter source control or build artifacts. A public unsigned release requires explicit owner acceptance and prominent Unknown Publisher/SmartScreen disclosure in every download surface; a checksum is integrity evidence, not publisher authentication.

The release gate rejects debug symbols, source/test artifacts, databases, logs, key/certificate files, raw/test audio, unexpected voice files, unexpected OmniSorSe binaries, and developer-profile/repository paths. It requires the exact allowlisted whisper.cpp runtime/model, their installed licenses, per-file hashes, and release-manifest binding. Installer SHA-256 is integrity metadata, not publisher authentication.

## Local voice

- Voice readiness is lazy. There is no wake word, passive monitoring, helper service, auto-start, speaker identification, voiceprint, or emotion analysis.
- The microphone starts only after the button or `Ctrl+Shift+Space` is activated, remains visibly announced, and stops on a second activation, two seconds of quiet after detected input, `Escape`, cancellation, error, or the 45-second bound. Initial silence does not submit an utterance. Delayed completion and cancellation remain bound to their originating operation so they cannot stop a newer capture.
- Windows capture is 16 kHz mono PCM in a bounded in-memory buffer. Raw audio is never logged or persisted as a preference.
- The optional whisper.cpp provider creates only an unpredictable app-owned temporary workspace on a local, non-network Windows volume when its CLI requires a WAV, and rejects reparse-point ancestors. Cancellation/timeout terminates the process tree and waits for exit before audio cleanup. Buffers are zeroed, deletion is retried with bounded backoff, and cleanup failure is reported. Provider startup schedules a background inspection of at most sixteen stale application-shaped workspaces older than one hour; oversized artifacts are rejected before zeroing and readiness/transcription await the recovery result. Audio is never included in diagnostics or packages.
- `whisper-cli`, every native dependency, and the quantized English model are pinned installer-owned files under the application directory. Build/package/application gates bind their exact hashes and reject omissions or extras; the application rechecks every hash whenever it resolves the bundle and never downloads or updates it. Invocation uses `UseShellExecute=false`, structured `ArgumentList`, at most four worker threads, and a minimal allowlisted environment; model/transcript text never becomes shell syntax. Process time/output and parsed JSON are bounded, and cancellation terminates the process tree before returning.
- Transcript text exists only in memory, appears for about 12 seconds, and is sent to the same existing Search path as typed text. It is not written to normal logs, preferences, diagnostics, or release artifacts.
- Provider/session generation is captured before recording. A transcript that completes after authority changes is rejected, so an utterance from one OmniSorSe/standalone session cannot execute against another.
- Microphone permission denial, missing hardware, missing/corrupt runtime/model, timeout, and malformed output fail closed while typed navigation/Search remains available.

## File activation and sound

- Only an explicit double-click, Enter, accessible-list Open action, or equivalent bounded voice command may open a file.
- Standalone activation revalidates the fully qualified selected-root boundary immediately before launch, rejects a reparse-point file or ancestor below the root, and blocks executable, script, installer, control-panel, management-console, shortcut, registry, application-reference, and URL-like extensions. It passes one file path to the Windows shell API without constructing a shell command and immediately releases the returned process handle.
- Connected display paths are labels only. Connected file opening reports unavailable unless a future protocol supplies an explicit host-authorized operation; there is no filesystem fallback.
- Interaction sounds are generated locally in memory, contain no user data, do not communicate a unique state, and are governed by one obvious persisted master switch. Muting releases playback resources and device failure cannot change session outcomes.

## Safe diagnostics

Developer diagnostics are local, disabled by default, and show product/provider/protocol/voice state, bounded counts, cache occupancy, timing, transcript length/classification, and sanitized error categories. The user-invoked `Copy safe diagnostics` action builds a fixed-field report containing version/OS/runtime, provider/connection/view, protocol/capabilities, bounded counts/timings/counters, visual settings, and allowlisted error categories. It cannot receive audio, transcript text, runtime/model paths, filesystem paths, filenames, queries, content, endpoints, grants, tokens, or session/node identifiers; unexpected provider/model/transport/error strings are reduced to safe categories. OmniBrille never sends the report automatically, and the user is told to review it before sharing.
