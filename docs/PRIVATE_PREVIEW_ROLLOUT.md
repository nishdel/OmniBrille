# Controlled private-preview rollout

## Initial boundary

- Start with one to three trusted Windows x64 testers.
- Give every tester the same exact retained installer, its matching `.sha256`, manifest, generated preview notes, and feedback guidance.
- Ask each tester to exercise install, Standalone, normal OmniSorSe handoff, Structure, Search, Context, and uninstall.
- Collect only tester-initiated, reviewed, sanitized diagnostics. There is no telemetry.
- Expand only after reproducible blockers from the first group are resolved.

## Private-preview blockers

- installer cannot install, upgrade safely, or uninstall without affecting unrelated data;
- installed OmniBrille cannot launch or crashes during basic Structure/Context use;
- normal compatible OmniSorSe handoff cannot establish its authorized session;
- package, diagnostics, or logs expose secrets or private user content;
- checksum/manifest/independent hash disagree;
- connected mode broadens authority or bypasses the protocol.

Visual imperfections, absent voice/Hybrid, unsigned status consciously accepted by the maintainer, sparse Context on low-intelligence data, and missing Linux/macOS packages are documented limitations rather than automatic blockers.

## Distribution boundary

Keep the repository and artifact private. Do not create a public tag, GitHub Release, package-feed publication, or announcement. A maintainer license decision is required before public distribution.
