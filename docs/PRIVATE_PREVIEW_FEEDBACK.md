# OmniBrille private-preview feedback

> **Historical preview artifact.** This feedback template describes the earlier controlled preview and is not current public-release authority. Use the [public release checklist](../RELEASE_CHECKLIST.md) for v1.0 release gates.

Thank you for testing OmniBrille. A useful report includes:

- OmniBrille version and commit from the candidate notes or safe diagnostics;
- OmniSorSe version/commit, if Connected mode was involved;
- Windows edition/version;
- Standalone or Connected;
- Structure or Context;
- Voice disabled, unavailable, or active; for voice issues include the state/error category and model family (`tiny.en`/`base.en`) but not the full runtime/model path or spoken text;
- the action attempted;
- expected and actual behavior;
- whether the issue reproduces after a normal restart;
- sanitized diagnostics copied explicitly from the settings HUD, when relevant.

For installer problems, include whether the failure occurred during fresh install, upgrade, launch, or uninstall, plus the installer's exact filename and SHA-256. For connection problems, include the visible connection state and protocol version—not the pipe endpoint or handoff value.

Do not upload microphone audio, spoken/transcribed text, speech runtime/model paths, file contents, private filenames or full paths, Search queries, OCR/transcripts, screenshots containing private data, bearer tokens, handoff values, session grants, logs not reviewed for private data, or OmniSorSe databases. OmniBrille sends nothing automatically; the tester chooses what to share.

Before submitting, reproduce with the smallest safe controlled folder possible and inspect every attachment manually.
