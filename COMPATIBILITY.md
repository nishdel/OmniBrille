# Compatibility

OmniBrille uses capability negotiation and Explorer Protocol major-version validation rather than requiring lockstep application versions. This matrix records tested combinations; it is not a claim about untested releases.

| OmniBrille | OmniSorSe | Explorer Protocol | Platform | Status |
|---|---|---|---|---|
| 1.0.0 candidate | not installed | n/a | Windows 10 22H2 x64 | Intended public support contract: Standalone selected-root Structure, Search, details, themes, preferences, and synchronized accessible list. Other Windows client versions have not been separately qualified; v1.0.0 is not yet published |
| 1.0.0 candidate | committed v2.5 RC `59be07c6cebff12072cbf18701fb16cb11801287` | v1.0 | Windows 10 22H2 x64 | Compatibility is strongly supported by unchanged client implementation and earlier installed validation, but the 1.0.0 artifact was not revalidated with this host. Connected remains compatibility-dependent rather than the primary v1 support contract |
| 0.8.0-preview.2 | committed v2.5 RC `59be07c6cebff12072cbf18701fb16cb11801287` | v1.0 | Windows x64 | Connected Structure/details/Context/Hybrid compatibility retained; Search uses session scope and fails safely when the companion response is unavailable |
| 0.8.0-preview.2 | not installed | n/a | Windows x64 | Standalone Structure/Search supported with selected-root authority; Context and Hybrid fail closed because no authoritative relationship provider exists |
| 0.7.0-preview.1 | committed v2.5 RC `59be07c6cebff12072cbf18701fb16cb11801287` | v1.0 | Windows x64 | Connected Structure/Search/details/Context compatibility retained; connected voice queries use the same Search operation. Live voice additionally requires a user-provided local whisper.cpp runtime/model |
| 0.7.0-preview.1 | not installed | n/a | Windows x64 | Standalone Structure/Search supported; optional voice commands and structural voice Search use the same selected-root authority |
| 0.6.0-preview.3 | committed v2.5 RC `59be07c6cebff12072cbf18701fb16cb11801287` | v1.0 | Windows x64 | Stage 8 exact private-preview candidate validated for installed discovery/handoff, Structure, Search, details, and Context; no voice |
| 0.6.x preview | 2.4.0 | v1.0 | Windows x64 | Protocol data path validated in Stage 4; normal installed companion launch is unavailable because 2.4.0 has no launcher handoff |
| 0.6.x preview | not installed | n/a | Windows x64 | Standalone supported and installed-runtime validated |
| 0.6.x preview | compatible future 2.5.x build | v1.x | Windows x64 | Expected to negotiate by protocol/capability; not claimed until tested |
| 0.6.x preview | any | incompatible protocol major | any | Connected mode fails closed; standalone remains available |
| current source | not required | n/a | Ubuntu | Restore/build and model/microphone-independent tests validated in CI; interactive runtime, microphone capture, and packaging are not validated |
| current source | not required | n/a | macOS | Architecture intended to remain portable; build/runtime are not validated |

## Compatibility policy

- OmniSorSe absent: OmniBrille starts in Standalone and accesses only an explicitly selected root.
- Protocol v1-compatible OmniSorSe: the client validates the grant, major version, read-only server identity, the advertised safety limits it consumes, and required `Structure` and `Search` capabilities before accepting the session. Several currently unconsumed advertised limits are not yet validated comprehensively.
- Missing optional capability: Context and Hybrid are never simulated locally. Current code detects absent Context/Related capability when a mode request is made, but the shell does not yet disable those controls in advance and reports the resulting provider failure as a connection failure. This is a known compatibility UX gap.
- Incompatible major version, malformed consumed limits, or missing required capability: connected mode is rejected without undefined parsing and Standalone remains available. Only a mismatched major is classified as `Incompatible`; malformed limits or missing required capabilities currently surface as a connection failure. Detailed failure categories remain in local diagnostics.
- Minor-version additions: the current strict serializer rejects unknown JSON members and numeric enums. Additive compatibility therefore requires an explicit client change and compatibility tests; protocol-major equality alone is not a tolerance promise. Server bounds still apply.
- Application version labels are informational. Explorer Protocol v1 and capabilities are the compatibility authority.
- Voice does not change Explorer Protocol compatibility. Deterministic commands are local UI actions; connected voice Search calls the already-negotiated v1 Search capability. A missing speech runtime/model affects Voice only and never blocks standalone or connected startup.
- Hybrid does not change Explorer Protocol compatibility. It composes the same authorized `GetNeighborhood(IncludeContext: true)` and focus-local `GetRelated` snapshot already used by Context, with one client-side budget and no `Hybrid` wire request.

OmniBrille never reads OmniSorSe SQLite, broadens an authorized root, or falls back to direct filesystem access for connected nodes.
