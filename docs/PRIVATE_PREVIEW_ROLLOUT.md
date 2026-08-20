# Controlled private-preview rollout

> **Historical preview artifact.** This rollout is superseded for v1.0 release work by the [public release checklist](../RELEASE_CHECKLIST.md) and [current release-notes template](release-notes.md). Do not use it as current release authority.

## Initial boundary

- Start with one to three trusted Windows x64 testers.
- Give every tester the same exact retained installer, its matching `.sha256`, manifest, generated preview notes, and feedback guidance.
- Ask each tester to exercise install, Standalone, normal OmniSorSe handoff, Structure, Search, Context, Hybrid, and uninstall.
- Collect only tester-initiated, reviewed, sanitized diagnostics. There is no telemetry.
- Expand only after reproducible blockers from the first group are resolved.

## Private-preview blockers

- installer cannot install, upgrade safely, or uninstall without affecting unrelated data;
- installed OmniBrille cannot launch or crashes during basic Structure/Context/Hybrid use;
- normal compatible OmniSorSe handoff cannot establish its authorized session;
- package, diagnostics, or logs expose secrets or private user content;
- checksum/manifest/independent hash disagree;
- connected mode broadens authority or bypasses the protocol.

Visual imperfections, optional voice requiring a separately supplied local runtime/model, unsigned status consciously accepted by the maintainer, sparse real Context/Hybrid on low-intelligence data, and missing Linux/macOS packages are documented limitations rather than automatic blockers.

## Distribution boundary

At the time of this historical rollout, the repository and artifact were intended to remain private and a maintainer license decision was required before public distribution. GPL-3.0-only was selected later for v1.0.
