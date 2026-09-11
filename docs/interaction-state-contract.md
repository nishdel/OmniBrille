# Interaction-state contract

This contract keeps OmniBrille's spatial presentation, keyboard/UIA behavior, sensory feedback, and provider authority aligned. [`architecture.md`](architecture.md) remains the ownership authority; this document is the concise behavior matrix for the graph-first shell.

## Independent state planes

| State | Owner | Meaning | Must not be inferred from |
| --- | --- | --- | --- |
| Graph focus | `ExplorerSession.Neighborhood.FocusNodeId` | Center of the acquired bounded scene | Selection, keyboard focus, or density |
| Selected node | `ExplorerSession.SelectedNode` | Item whose Details/actions are active | Graph focus or pointer hover |
| Keyboard focus | Avalonia focus manager | Control receiving keys | Graph focus/selection |
| Scene relation | `ExplorerSceneSemantics` | Current focus, direct child, descendant preview, previous focus, Context, both, or aggregate | `PresentationBand`, radius, opacity, or size |
| Presentation band | layout/presentation policy | Bounded visual density/label priority | Filesystem depth or provider authority |
| Provider authority | active `IExplorerProvider` | Standalone selected-root paths or Connected opaque IDs | Display path, Details text, or motion |

The graph, synchronized list, Details, and automation tree project the same bounded scene and selection. No projection may acquire, crawl, persist, or authorize data independently.

A newly acquired or navigated scene begins without a selected node; centering graph focus never opens Details. Pointer, keyboard, list, UIA, or Voice selection is the only way to create selection. Search, Trail, Connection, and Details share one overlay authority so opening one cannot leave another actionable surface hidden behind it.

## Navigation and activation

| User intent | Pointer/control | Keyboard | Voice | Session/result contract |
| --- | --- | --- | --- | --- |
| Select | single click on file or Context/Hybrid node | geometric arrows, list selection | `focus <visible name>` | Changes selection and Details only |
| Enter Structure folder or aggregate | first click on navigable folder, previous-focus node, or aggregate | select then `Enter` | `open selected`, `enter selected` | Navigates through the same session action; centered focus does not navigate to itself |
| Activate selected | double click / Open | `Enter` | `open selected`, `enter selected` | Context/Hybrid explicitly refocus; ordinary Standalone file uses safe OS activation. Pointer double-click requires the same node and unchanged scene across both presses |
| Back | `BACK` or graph right-click | `Backspace`, `Alt+Left` | `back`, `previous` | Unwinds chronological mode/refocus/aggregate/folder history |
| Up | `UP` | `Alt+Up` | `up`, `parent` | Uses the provider-authored parent target; never display-path inference |
| Root | `ROOT` | `Alt+Home` | `root`, `home` | Returns to the active Standalone path root or Connected opaque root |
| Jump to recent history | `TRAIL`, then named destination | normal Tab activation | no separate voice intent | Up to six named, mode-aware destinations in a scrolling surface. Targets remain private; stale or loading actions are rejected. Crossed history is removed only after successful acquisition |
| Reopen Details | selection or Details action | `Ctrl+I` | `show details` | Reopens the same selected-node projection |

Standalone file activation requires an explicit user action, an ordinary non-reparse file, a path inside the selected root, no reparse-point ancestor below that root, and a non-executable/script/shortcut/URL-like extension. Connected files cannot use projected paths; until the protocol provides an authorized host operation, the UI states that opening is unavailable.

## Sensory and timing behavior

| Event/state | Visual | Announcement/UIA | Sound | Reduced motion / Sound off |
| --- | --- | --- | --- | --- |
| Keyboard enters graph | distinct canvas/selected target cue | selected `TreeItem` remains queryable | none | same static cue |
| Hover node | eased local magnification across target and nearby nodes; central focus remains steady | no focus/selection change | debounced airy hover | no displacement under Reduced motion; no playback when muted |
| Select node | selection halo and Details update | `SelectionItem.IsSelected`; full Details semantics atomic | short select cue | no Details typing under Reduced motion |
| Navigate | destination moves to focus; prior scene recedes | one polite completion status | folder/navigation cue | immediate base layout under Reduced motion |
| Loading/Search/status | visible local surface as applicable | one independent polite status authority | none | unchanged |
| Voice listening | text state plus input-level meter | state-specific button/help and polite status | none | static high-contrast state |
| File opened | status confirms Windows handoff | polite status | file-open cue | outcome identical when muted |

Graph motion is analytic and bounded around immutable deterministic coordinates. It never changes membership, edge topology, semantic relation, selection, or provider work. One approximately 24-fps foreground timer serves transition/float/lens invalidation and stops for Reduced motion, hidden/minimized windows, teardown, or no scene. There is no background simulation or ambient sound.

Details assigns complete text and automation names to the actual Type-through-Index, summary, and relationship fields before starting an optional sequential visual clip reveal. A new selection or metadata replacement cancels the prior reveal; Reduced motion immediately removes the clips. The projection never substitutes a duplicate command header or emits per-character live announcements.

Every admitted node whose center is on the graph retains its name at every zoom/text scale. Labels use measured widths and ellipsis for long text, move around glyphs and prior labels, and receive peripheral leader lines when displaced and unobstructed. The top/bottom HUD reserves graph-coordinate and label-placement space. Exceptional space pressure may still cause overlapping names; the synchronized list provides full-size reading without changing scene membership.

Descendant previews are actual acquired subfolders, visibly marked with `↳`, connected to their direct-folder parent, and announced as two levels below focus. Their subdued size does not redefine the depth of nearby direct children. Like-type placement and deterministic asymmetry convey organization without adding a relationship.

## Accessibility and target contract

- Primary shell actions and every visible graph node have at least a 44 by 44 DIP effective target. Dense visual glyphs may be smaller only inside that target rectangle.
- Keyboard focus, graph focus, selection, Search match, relationship kind, and semantic scene relation each have a non-color text/shape/automation channel.
- The graph exposes one bounded `Tree` with single-selection semantics; each visible node exposes `TreeItem`, `SelectionItem`, and `Invoke`. Search match and semantic relation are present in queryable state.
- One independent polite status authority announces accepted current work only; its queryable automation name changes with the message. Visible status text is not a second live region. Stale provider, Search, Details, and Voice results are rejected before visual or assistive output.
- Contrast gates parse both application resources and the renderer's actual `ScenePalette`, alpha-composite effective interactive glyph colors, and enforce 4.5:1 text plus 3:1 non-text floors. Release inspection still covers both themes, high contrast, keyboard focus, and actual Windows scaling.

## Voice and audio failure independence

Voice is a click-to-toggle convenience over existing actions: first activation enables Voice and listens without an enablement submenu; second activation stops and transcribes locally. The microphone becomes Stop while active, and the same control cancels during initialization/transcription. Two seconds of quiet after detected input also stops capture; initial silence does not submit an utterance. Quiet/duration callbacks and cancellation completion remain bound to their original operation, so older work cannot stop or reset a newer capture. Grammar has no destructive action, arbitrary shell/file command, or client-created Context. Missing microphone/runtime/model, recognition failure, or cancellation cannot affect typed/pointer navigation or replace provider state.

The installed voice runtime/model are pinned, installer-owned, hash-bound, local-only assets whose hashes are rechecked whenever the bundle is resolved. The installed application does not download or update them. The process receives a minimal environment and bounded arguments/output/time. Its GUID workspace must be local and non-reparse; termination is awaited before zero/retry cleanup, and a cleanup failure is surfaced rather than silently ignored. No transcript or audio enters preferences, diagnostics, release metadata, or logs.

Sound is optional redundant feedback generated locally in memory from bounded deterministic layered sweeps, clicks, and latch cues. The persisted `SOUND OFF` control prevents playback work; device failure is swallowed at the sound boundary and never changes `ExplorerSession` outcomes. Waveform tests can check bounds and silence tails; physical playback and subjective sound character require separate listening evidence.
