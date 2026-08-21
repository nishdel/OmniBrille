# Screenshot provenance

## v1.1 visual-convergence candidate

The `v1.1-*.png` files are real captures of an installed Windows x64 `1.1.0` candidate. They are not mockups, concept art, or AI-generated application imagery.

- Capture date: 2026-08-21.
- Host: Windows 10 build 19045 at 125% display scaling.
- Visible application commit: `04c6f363f0efd880573e0c1481dbda627cc8e37e`.
- Installed candidate SHA-256: `E82E431A0A1FCFD96EDE5AECB5DED6393095CDFA572BDC0FEF86D9635C559E96`.
- DNG-free native renderer SHA-256: `EBE9A21F29D2474129B06FFAB67B3E74474B7F6E0D0442F14B8BAC3CFF870619`.
- Executable metadata: file version `1.1.0.0`, product version `1.1.0`.
- Demo roots: `C:\Users\Public\OmniBrille Visual Demo` and its `09 Research` child.
- Data: purpose-built generic folders; no personal content, user-profile path, diagnostics, OmniSorSe session, or voice data.
- Views: dense Dark Structure, dense Light Structure, structural Search for `Research`, nested focus/details, and the synchronized accessible list.

The captures use a Per-Monitor-V2-aware process and the physical Win32 window bounds so the complete 125%-DPI window is represented. They were inspected at full size for native window chrome; floating navigation, mode, Search, utility, zoom, status, voice, and details/list surfaces; graph nodes, labels, outlined glyphs, edges, focus reticle, Search emphasis, and theme contrast. The same installed candidate exercised arrow/Enter navigation, Alt+Left Back, `Ctrl+F` Search, `Ctrl+Shift+L` list access, normal close/relaunch, a real progressive 5,000-file load, and the supported 820-by-520 logical minimum window. Automated headless coverage, rather than this capture session, exercised 100/125/150/200% text scaling and reduced-motion/effects behavior. No screen reader was used.

The images were captured before this provenance file and the PNGs themselves were committed. No visible application or native-renderer source is permitted to change after commit without recapturing them. The eventual exact release installer must still be rebuilt from the final documentation/screenshot commit and separately pass installed-artifact and public-release qualification before these images can be described as release screenshots.

## v1.0 release

These PNGs are real captures of the installed Windows x64 `1.0.0` DNG-free release candidate, not mockups or AI-generated application imagery.

- Capture date: 2026-08-21.
- Host: Windows 10 build 19045 at 125% display scaling.
- Candidate commit: `fbb1c7fabebca056e750d17869d807ea93b629b9`.
- Installed candidate SHA-256: `1D4A99A03AF73BEA60D34F088DB2A2CAE8CEABE62A3A1DF78EA2222416A60627`.
- DNG-free native renderer SHA-256: `EBE9A21F29D2474129B06FFAB67B3E74474B7F6E0D0442F14B8BAC3CFF870619`.
- Executable metadata: file version `1.0.0.0`, product version `1.0.0`.
- Demo root: `C:\Users\Public\Documents\OmniBrille Demo\Atlas Workspace`.
- Data: purpose-built generic folders/files; no personal content, user profile path, diagnostics, OmniSorSe session, or voice data.
- Views: Dark Structure, Light Structure, structural Search for `diagram`, and the synchronized accessible list.

The captures use Windows `PrintWindow` from a Per-Monitor-V2-aware process so the complete 125%-DPI window is represented. They were visually inspected for full window chrome; the Connection, zoom, Theme, List, and Settings controls; graph nodes, text, icons, and lines; Search emphasis; and the synchronized list. The same installed candidate also completed an accessibility-pattern folder drill-down and Back cycle, multiple normal close/relaunch cycles, and uninstall cleanup. An earlier DPI-unaware capture was rejected as cropped.

This candidate uses the path-independent, independently reproduced DNG-free native renderer package shipped in v1.0.0. No visible application or native-renderer code changed between this capture and release commit `53f787b4489f487fa5dcf86af227510fc4ebc029`. The exact published installer was separately recaptured and visually inspected in Dark, Light, and Search states during final qualification; its installer SHA-256 is `67443C616B82E066199D39EF7EDBD3DCE68B7E66BAEB26E25872E30BE0F347BF`. Future screenshot reuse requires recapture if the visible application binary, native renderer, theme, demo content, version presentation, or supported workflow changes.
