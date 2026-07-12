---
id: EDITOR-022
title: Build an icon-based asset browser
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-003
- ASSET-001
createdAt: 2026-07-12T09:08:09.4215100Z
modifiedAt: 2026-07-12T09:17:29.2831580Z
---

Replace the editor's name-only asset list with a responsive icon grid file browser that shows asset names, selection, folders or categories, and stable tile sizing. Model assets use preview textures when available; all unsupported or not-yet-previewed files use a neutral placeholder tile without inventing final icon artwork.

## Notes

- 2026-07-12 09:17 UTC - Implemented the read-only icon-grid asset browser with deterministic manifest ordering, render/collision classification, case-insensitive ID filtering, stable-ID single selection, fixed responsive tiles, clipped labels/tooltips, neutral placeholders, disabled collision-only entries, and an SDL GPU texture preview-provider boundary for EDITOR-023. Added deterministic editor tests for ordering/classification/filtering/selection, narrow-to-wide column calculation, and preview fallback/handle resolution. Validation: `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj -m:1 --no-restore` passed 56/56 (rerun outside sandbox because vstest local socket binding was denied); `dotnet format Royale.slnx --no-restore --verify-no-changes --include src/Royale.Editor tests/Royale.Editor.Tests` passed with only workspace-load warnings; `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings and 0 errors. Native screenshot launch loaded graybox and 11 assets; `/tmp/editor-022-grid.bmp` visually confirmed populated placeholder tiles, clipped labels, stable spacing, and no overlap in the normal docked layout. Updated `architecture/editor`. Owner validation remains requested for visual density, selection clarity, filtering interaction, and normal docked-layout behavior.