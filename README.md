# OmniBrille

OmniBrille is an optional, standalone-capable spatial navigation application for graph-based filesystem exploration. It presents standalone Structure and real OmniSorSe-backed Structure, Context, and Hybrid. This repository is an independent application: users who never install OmniBrille incur no renderer, asset, package-size, runtime, dependency, or startup cost in OmniSorSe.

Conceptually, OmniSorSe is the brain. OmniBrille is the visual lens and spatial navigation interface.

OmniBrille is currently a **Private Preview / Pre-release**. Stage 11 refines the established Structure, Context, and Hybrid experience for repeated use with a responsive HUD, clearer first-run guidance, and distinct Search and empty states. Optional local push-to-talk remains an independent, disabled-by-default input path; real microphone hardware validation is still outstanding and is not claimed. Explorer Protocol v1 is unchanged. Always-listening audio, destructive file operations, and automatic updating remain intentionally absent.

## What works now

- Explicit operating-system folder selection; no drive-wide startup crawl.
- A concise first-run surface that explains selected-root Standalone access, offers `Choose folder to explore`, and identifies OmniSorSe as the authority for Context and Hybrid.
- A responsive two-row HUD that keeps root, Search, provider, mode, view, accessibility, and settings controls reachable at the supported minimum window size.
- A hard-bounded spatial graph containing the focused folder and immediate children.
- Progressive directory batches: the focus shell appears first, bounded children stream in, obsolete navigation is rejected, and navigating away cancels prior work.
- Deterministic three-depth radial layout with positional continuity, a strong focus, receding context, and restrained focus transitions.
- Reversible aggregate refinement. Large directories open deterministic structural pages with previous, next, and overview controls without exceeding the 48-node scene budget.
- Priority- and collision-aware labels plus zoom/depth/density level of detail.
- Double-click drill-down, Back/Backspace navigation, selection, dismissible details, mouse-wheel/button/keyboard zoom, drag-to-pan, and arrow/Enter graph navigation.
- Bounded name/folder/path search with graph dimming/highlighting, a secondary compact result surface, cancellation, and result-to-graph focus.
- Provider-accurate Search automation/help, a disabled invalid result action, and a distinct no-match state that keeps the recognized query available for editing.
- A shared electric-blue Dark/Light visual system, restrained decorative network, and bounded data-rain loading treatment.
- Persisted `Reduced motion` and `Reduced visual effects` preferences.
- A synchronized accessible navigation list (`Ctrl+Shift+L`) with the same bounded nodes, selection, search emphasis, drill-down, details, aggregates, and Back state as the graph.
- One automation tree item per visible graph node, including name/type, selection/focus/aggregate status, bounds, help, and an invoke action. Invisible source items are not exposed.
- Text-scale-aware label density validated at 100%, 125%, 150%, and 200%, plus documented Dark/Light contrast checks.
- Bounded text-layout and drawing-resource caches; phase-level local diagnostics for background, edges, glyphs, labels, allocations, caches, and data rain. No diagnostics leave the machine.
- Graceful missing/inaccessible/deleted-folder handling, bounded enumeration, root-boundary enforcement, and no recursive reparse-point traversal.
- Domain/infrastructure tests plus Avalonia headless UI tests, and Windows/Ubuntu GitHub Actions CI.
- A strict Explorer Protocol v1 named-pipe client, explicit version/capability validation, opaque session-bound node identity, and defensive payload bounds.
- A connected provider that adapts real OmniSorSe authorized roots, paged Structure children, unified Search, and bounded details into the same graph/session/list model as standalone mode.
- The real v2.5 release-candidate installed-companion flow: an explicit OmniSorSe action discovers and launches OmniBrille, transfers a short-lived scoped grant through a one-time current-user-only pipe, and authenticates the session without putting a bearer token on the command line.
- A primary `Structure | Context | Hybrid` switch. Context and Hybrid use only real `GetNeighborhood(IncludeContext: true)` and focus-local `GetRelated` evidence supplied by OmniSorSe; standalone Context/Hybrid explain that OmniSorSe is required and never fabricate relationships.
- Hybrid composes one provider-independent bounded snapshot: shared nodes are deduplicated, solid Structure remains the navigational skeleton, dashed Context remains server-authored, and one 48-node/84-edge envelope applies to the complete scene.
- A deterministic focus-centered Context layout, solid structural edges, distinct cyan dashed Context edges, 48 combined nodes, at most 36 Context edges, and at most three Context edges touching a node.
- Context refocus and Back, real OmniSorSe Search-to-Context focus, compact reason/evidence/provenance details, Context-aware graph automation peers, and synchronized keyboard/list navigation.
- Reversible, local presentation filters for server-authored relationship kind, minimum ranking strength, and evidence class. The HUD reports visible, matching, and authorized counts and never alters OmniSorSe state.
- Strength-aware deterministic Context depth, a clear no-relationships/no-filter-matches state, and a compact relationship hierarchy (`Related because`, `Strength`, `Evidence`, `Source`).
- A reproducible per-user Windows installer at `%LOCALAPPDATA%\Programs\OmniBrille`, which the committed OmniSorSe v2.5 RC discovers through its existing conventional-location policy without an environment override. Development artifacts are unsigned; the release workflow supports externally supplied Authenticode credentials and fails closed when signing is required.
- A release manifest, SHA-256 sidecar, sanitized runtime dependency manifest, checksum-bound tester notes, release-quality provisional icon, compatibility matrix, and deterministic `verify-release.ps1` gate.
- A user-invoked `Copy safe diagnostics` action that reports version/runtime, provider/protocol state, bounded counts, timings, and safe error categories without paths, filenames, queries, content, endpoints, grants, tokens, or session/node IDs.
- Accessible `Standalone`, connecting, connected, unavailable, incompatible, disconnected, and reconnecting states; a disconnected graph remains visible as stale context rather than crashing.
- Safe provider switching: connected opaque IDs, selection, Search, details, and Back history are cleared before standalone access is established.
- Optional one-shot push-to-talk (`Ctrl+Shift+Space`) with a visible bounded 45-second capture state, local whisper.cpp transcription, a deterministic English command registry, visible-node matching, and conservative Search fallback. No model/runtime is bundled or downloaded; missing voice components never block normal startup.
- Voice Search reuses the current provider: standalone remains filename/folder/path Search and connected mode delegates to real OmniSorSe Search. Voice never adds a semantic engine or client-generated Context relationship.

## Privacy and access philosophy

Standalone mode can inspect only the root the user explicitly chooses. It does not enumerate unrelated drives or user folders, follow directory reparse points recursively, modify files, use cloud services, emit telemetry, or run a background indexer. Structural search is an explicit foreground action, stays inside that root, and has result/directory limits. Voice has no wake word or background recorder: the microphone activates only after explicit push-to-talk, audio is processed locally, bounded, and discarded after transcription, and transcript text is neither logged nor persisted.

Visual preferences are stored below the operating system's local application-data directory in `OmniBrille/visual-preferences.json` (`%LOCALAPPDATA%\OmniBrille` on Windows). They contain theme/effects choices, the developer diagnostics toggle, and—only when configured—the voice-enabled/language and local runtime/model paths. Selected filesystem roots, audio, transcripts, queries, grants, and session IDs are not persisted.

## Build and run

Prerequisites for source builds: the SDK selected by `global.json` and a Windows, macOS, or Linux desktop supported by Avalonia. Windows is the validated microphone/runtime platform; Windows/Ubuntu build and model-independent test coverage remain in CI. Linux interactive voice runtime and macOS build/runtime remain unvalidated.

```powershell
cd "D:\Own Projects\OmniBrille"
dotnet restore .\OmniBrille.sln
dotnet build .\OmniBrille.sln --configuration Release --no-restore
dotnet test .\OmniBrille.sln --configuration Release --no-build --no-restore
dotnet run --project .\src\OmniBrille.Desktop\OmniBrille.Desktop.csproj
```

Build the pinned, unsigned Windows installer with:

```powershell
.\build\Package-Windows.ps1 -BootstrapInnoSetup
```

The Stage 11 package is `OmniBrille-0.8.0-preview.2-win-x64-setup.exe`. The preview increment reflects daily-use presentation and accessibility refinements without changing the feature or protocol boundary. It remains self-contained, non-trimmed, and multi-file for reliable Avalonia/XAML behavior, and includes only the small Windows capture dependency—not whisper.cpp or a speech model. The build also emits a release manifest, sanitized dependency manifest, SHA-256 sidecar, and tester notes containing that exact artifact's hash. See [PACKAGING.md](docs/PACKAGING.md) for prerequisites, install/upgrade/uninstall semantics, signing, private-preview automation, and alternatives considered.

The application initially shows no filesystem content. Choose a folder to establish the access root. For an explicit command-line launch (useful for local smoke testing), append `-- --root "C:\path\you\chose" --theme Light`. The theme value may be `Light` or `Dark`.

The committed OmniSorSe v2.5 release candidate can start OmniBrille with `--omnisorse-handoff <one-time-pipe-name>`. The pipe name has the fixed product prefix and a 128-bit random suffix; the pipe transfers a strict, bounded, short-lived grant over a current-user-only channel. The secret is never a command-line value, file, preference, UI string, or normal diagnostic. OmniBrille performs no background discovery. Direct launch remains standalone.

Open the HUD settings control to select `Reduced motion`, `Reduced visual effects`, local Voice configuration, the diagnostics overlay, or `Copy safe diagnostics` for a user-reviewed support snapshot. Open the accessible list from the `List` HUD control or `Ctrl+Shift+L`. Keyboard essentials are `Ctrl+1` for Structure, `Ctrl+2` for Context, `Ctrl+3` for Hybrid, `Ctrl+Shift+F` for Context/Hybrid relationship filters, `Ctrl+F` for Search, `Ctrl+Shift+Space` for push-to-talk, `Backspace` or `Alt+Left` for Back, arrows to change selection, `Enter` to activate/refocus, `Escape` to dismiss/cancel, `+`/`-` to zoom, and `0` to reset the graph view. See [local push-to-talk voice](docs/voice.md) for setup, commands, privacy, and limitations.

## Repository structure

```text
src/
  OmniBrille.Core/            explorer/Context/Hybrid/voice contracts, bounded builders, layouts, caches, command coordinator
  OmniBrille.Infrastructure/  standalone/Protocol adapters, preferences, bounded local audio/whisper.cpp provider
  OmniBrille.Desktop/         Avalonia shell/session, Structure/Context/Hybrid renderer, accessible push-to-talk HUD, themes
  OmniSorSe.ExplorerProtocol/ exact dependency-free v1 wire contracts mirrored from OmniSorSe
tests/
  OmniBrille.Tests/           domain, session, filesystem, failure, and persistence tests
  OmniBrille.HeadlessTests/   non-pixel Avalonia shell/interaction tests
docs/
  architecture.md             system boundaries and Stage 6 provider/rendering/packaging decisions
  context-rendering-contract.md implemented Context limits, semantics, accessibility, and gaps
  explorer-protocol.md        actual Protocol v1 and v2.5 RC handoff behavior
  PACKAGING.md                reproducible Windows installer and lifecycle policy
  voice.md                    local runtime/model setup, deterministic grammar, privacy, and limitations
  private-preview.md          generated-note template for installation, checksum, voice, and feature guidance
  PRIVATE_PREVIEW_FEEDBACK.md privacy-conscious tester report template
  PRIVATE_PREVIEW_ROLLOUT.md  controlled rollout boundary and blocker policy
  SECURITY-PRIVACY.md         release security, privacy, handoff, and safe diagnostics posture
COMPATIBILITY.md              tested OmniBrille/OmniSorSe/protocol/platform combinations
CHANGELOG.md                  sustainable preview milestone history
RELEASE_CHECKLIST.md          automated and manual private-preview gates
ROADMAP.md                    staged implementation plan
```

## Renderer profiling

Enable `Developer diagnostics` from the settings HUD. The overlay reports node/edge/label counts; scene budget and zoom; layout, preparation, total render, background, edge, glyph, and label time; per-render allocations; bounded cache occupancy; data-rain cost; and the latest directory-load duration. These are local sampling aids, not product guarantees or telemetry. Representative measurements and the rationale for retaining the 48-node budget are recorded in [the architecture](docs/architecture.md).

CI is defined in `.github/workflows/ci.yml`. It restores, verifies formatting, builds Release with analyzers-as-errors, runs all tests, and audits NuGet vulnerabilities on `windows-latest` and `ubuntu-latest`. The Windows leg also builds the unsigned installer and retains it privately for 14 days. `.github/workflows/private-preview.yml` is a manual unsigned/signed release-validation workflow; a second fresh hosted-Windows job receives only the exact candidate artifact and verifies its hash, per-user install, Start Menu/uninstall registration, installed window startup, and cleanup. It never creates a tag, release, or feed publication.

## OmniSorSe relationship

OmniSorSe is the primary local-first file intelligence application. It is responsible for scanning, indexing, Search, Content Intelligence, Media Intelligence, OCR, transcripts, Related Files, organization, safe file operations, and persistent intelligence/index state.

OmniBrille is the optional spatial navigation companion. It is responsible for graph-based filesystem navigation, Structure/Context/Hybrid presentation, spatial Search presentation, visual navigation, and optional local voice input. Context relationships are supplied exclusively by OmniSorSe; Hybrid only composes existing bounded Structure and Context snapshots, and voice Search routes through the same existing standalone/connected providers, so OmniBrille does not duplicate intelligence.

OmniBrille consumes OmniSorSe Explorer Protocol v1 through a narrow named-pipe client and will never read OmniSorSe's SQLite schema or reuse its application/indexing implementations. Only the small dependency-free wire contract is mirrored locally. Standalone and connected providers remain separate authorities: connected mode uses only roots and opaque nodes authorized by OmniSorSe, with no direct-filesystem fallback.

The private GitHub repository is `nishdel/OmniBrille`.

See [the architecture](docs/architecture.md), [voice guide](docs/voice.md), [packaging guide](docs/PACKAGING.md), [compatibility matrix](COMPATIBILITY.md), [release checklist](RELEASE_CHECKLIST.md), [security/privacy posture](docs/SECURITY-PRIVACY.md), [Context rendering contract](docs/context-rendering-contract.md), [Protocol v1 and handoff integration record](docs/explorer-protocol.md), and [roadmap](ROADMAP.md).
