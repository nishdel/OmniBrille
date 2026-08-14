# Security and privacy posture

OmniBrille is local-first and has no telemetry, cloud upload, crash-reporting service, microphone, background recorder, auto-start task, daemon, or destructive file operation.

## Data authority

- Standalone reads only the folder explicitly selected for that session. It does not crawl unrelated drives, persist the selected root, follow directory reparse points recursively, or modify files.
- Connected mode reads only opaque nodes and roots authorized by the live OmniSorSe grant. It never treats a displayed path as permission and never falls back to local filesystem access for a connected node.
- Context nodes, relationships, reasons, ranking strength, evidence, and provenance come only from OmniSorSe. OmniBrille retains at most eight bounded Context snapshots in process memory and never persists an intelligence copy.

## Handoff and local transport

The committed OmniSorSe v2.5 RC launches OmniBrille with a random one-time pipe name—not a bearer token. A current-user-only one-connection handoff pipe transfers the strict bounded grant. Session ID, 256-bit bearer secret, endpoint, opaque node IDs, and Context caches stay in process memory, expire with the server-owned grant, and are invalidated on a new session. They are excluded from UI, normal diagnostics, preferences, release metadata, and logs.

Explorer Protocol requests use current-user named pipes, explicit major-version/capability negotiation, strict framing/JSON, response identity checks, size/count limits, timeouts, and stale-generation rejection. There is no LAN/cloud listener. Protocol errors preserve Standalone availability.

## Installation and release

The Windows installer is per-user and requires the lowest privilege level. It installs only into `%LOCALAPPDATA%\Programs\OmniBrille`, creates a Start Menu entry and uninstall registration, and installs no service, file association, startup entry, updater, or OmniSorSe binary.

Development/private-preview packages may be unsigned. A signed release path requires externally supplied GitHub secrets, imports the certificate only into the ephemeral runner user store, signs the application and installer, verifies both, then removes the imported certificate. Private keys and passwords must never enter source control or build artifacts.

The release gate rejects debug symbols, source/test artifacts, databases, logs, key/certificate files, unexpected OmniSorSe binaries, and developer-profile/repository paths from the publish directory. Installer SHA-256 is integrity metadata, not publisher authentication.

## Safe diagnostics

Developer diagnostics are local, disabled by default, and show product/provider/protocol state, bounded counts, cache occupancy, timing, and sanitized error categories. OmniBrille does not automatically export or upload them. A preview tester should copy only version, OS/runtime, connection label, protocol version, renderer timings, sanitized failure category, and reproduction steps. Filenames, full paths, file contents, Search queries, OCR/transcripts, grants, endpoints, tokens, and databases should not be shared by default.
