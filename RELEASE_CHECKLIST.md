# v1.2.0 release evidence checklist

This is the reusable evidence checklist for the v1.2.0 release. Checkboxes describe what a release record must establish; they are not a claim that every item was performed when this source document was written. The final [GitHub Release](https://github.com/nishdel/OmniBrille/releases/tag/v1.2.0), exact-artifact manifest, hosted validation JSON, and linked CI runs record actual outcomes without requiring a post-tag source edit. The [issue audit](docs/runs/2026-09-11-github-issue-audit.md) remains an unchanged historical snapshot of PR #11 qualification.

The owner authorized v1.2.0 integration and unsigned publication to continue manual testing. Implementation for issues #1–#9 is complete. The manual acceptance items below remain separate and do not imply an implementation blocker or a requirement for another publication approval.

## Source and integration evidence

- [ ] Record previous `main`, PR #11 candidate, final release commit, and normal integration method without rewriting published history.
- [ ] Source/package/assembly/installer metadata, current documentation, download links, artifact names, and release notes consistently identify `1.2.0` / `1.2.0.0` as appropriate.
- [ ] Preserve historical v1.0/v1.1 releases, reports, screenshots, dependency pins, and the issue audit; distinguish them from current claims.
- [ ] Confirm Explorer Protocol v1, server-authored Context, provider replacement, stale-result rejection, the 48-node cap, privacy, and installer ownership remain intact.
- [ ] Independently review release scripts, licensing/native provenance, artifact notes, signing disclosure, source changes, and unresolved manual claims.
- [ ] Run the clean exact-commit release gate: restore, documentation links/fences, formatting, analyzer-enabled Release build, full tests, vulnerability/dependency audits, package generation, artifact checks, and `git diff --check`.
- [ ] Confirm Windows and Ubuntu CI outcomes for final `main`, with test totals and platform-specific skips recorded accurately.
- [ ] Confirm PR #11 is merged, remote `main` equals the validated commit, and the local working tree is clean.

## Tagged artifact and publication evidence

- [ ] Create and push annotated tag `v1.2.0` on the exact validated `main` commit. Never move an existing release tag.
- [ ] Build the public assets from that tag with the existing release artifact workflow; record source commit and workflow identity.
- [ ] Validate exact installer/application versions and unsigned status, payload contents, pinned English Voice bundle, DNG-free native asset, and required licenses/notices.
- [ ] Pass the fresh hosted artifact-only install, launch, normal close/relaunch, Start Menu/uninstall registration, uninstall, and cleanup checks.
- [ ] Match installer SHA-256 independently against sidecar, manifest, generated notes, hosted validation JSON, and downloaded public bytes.
- [ ] Retain one matching artifact set: `OmniBrille-1.2.0-win-x64-setup.exe`, checksum, manifest, dependency graph, generated notes, and hosted validation JSON. Do not rebuild between validation and upload.
- [ ] Publish a stable GitHub Release with the exact tagged assets, prominent Windows download, unsigned/Unknown Publisher/SmartScreen disclosure, implementation highlights, and manual limits.
- [ ] Verify release/tag/main identity, asset filenames, direct installer download, latest stable link, checksum, README discoverability, and issue #1–#9 comments/status.
- [ ] Record the final outcome in the release evidence ledger while keeping the tagged source tree unchanged.

## Manual acceptance record

Record each check as performed, not performed, or failed, with the exact installer hash, Windows version, scaling, host type, and result. Hosted lifecycle checks and software renders do not substitute for interactive checks.

- [ ] Fresh interactive current-user install, Start Menu launch, explicit non-private root selection, and representative Structure/Search/Details/list use.
- [ ] Upgrade from the exact public v1.1.0 predecessor with preserved safe preferences, normal close/relaunch, and uninstall that leaves user content untouched.
- [ ] Native controls, first-click folder entry, right-click Back, clickable Trail, file activation, keyboard targets, and transition feel (#3).
- [ ] Default real microphone, input noise, initial silence, quiet completion, explicit stop/cancel, replacement races, and command/Search behavior using the installed English model (#4).
- [ ] Dark/Light, hierarchy/previews, connector geometry, grouping, names, minimum-window overlays, and visual/concept acceptance using non-private data (#5).
- [ ] Local hover/float comfort, native metadata readability, Reduced motion/effects, foreground/minimized behavior, and GPU/performance observations (#6).
- [ ] Physical audio output, cue character, master mute, debounce, and unavailable-device behavior (#7).
- [ ] Dense layouts at extreme text/display scales, synchronized-list fallback, actual UIA, Narrator/NVDA, high contrast, and custom-chrome DPI/snap behavior.
- [ ] Fresh live Connected-host compatibility or broader Linux/macOS runtime checks before expanding those support claims.

No unchecked manual item may be described as tested. Prior release and software-render evidence retains its original scope. The [testing guide](docs/testing.md), [packaging guide](docs/PACKAGING.md), and [interaction contract](docs/interaction-state-contract.md) define the relevant automated and behavioral boundaries.
