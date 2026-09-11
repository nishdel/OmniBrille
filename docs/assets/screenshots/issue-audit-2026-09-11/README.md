# Issue audit software renders — 2026-09-11

These images depict the unreleased `codex/github-issue-fixes` source using deterministic synthetic data. They are captured by `RenderedIssueEvidence` in the headless test project with real Skia software drawing, Inter font, 1× scale, Reduced motion enabled, and effects enabled. They are not installed application, GPU, animation, DPI, or accessibility-backend evidence.

| Scene | Image | Conditions |
| --- | --- | --- |
| Actual descendant previews, Dark | [preview-dark.png](preview-dark.png) | 1280×800; focus, six direct children, eight actual subfolder previews |
| Actual descendant previews, Light | [preview-light.png](preview-light.png) | Same data and bounds |
| Dense same-type grouping | [dense-dark.png](dense-dark.png) | 980×600; focus plus 47 files; every admitted name retained |
| Dense zoomed out | [dense-zoomed-out.png](dense-zoomed-out.png) | Same 48 nodes at minimum zoom; labels reposition around glyphs |

Reproduce from the repository root in PowerShell:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:OMNIBRILLE_RENDER_EVIDENCE = Join-Path $PWD 'artifacts/issue-audit/rendered'
dotnet test tests/OmniBrille.HeadlessTests --configuration Release --filter FullyQualifiedName~RenderedIssueEvidence
Remove-Item Env:OMNIBRILLE_RENDER_EVIDENCE
```

The image fixtures also run in the ordinary headless suite, where they assert admission/name counts without producing pixel evidence. Inspection of the software captures found and corrected top-HUD overlap and barely visible preview branches. Dense scenes and small zoom still trade spatial closeness for readable displaced names; larger text and native scaling need manual assessment. See the [owner report](../../../runs/2026-09-11-github-issue-audit.md) for issue status and remaining gates.
