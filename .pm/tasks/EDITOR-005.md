---
id: EDITOR-005
title: Add viewport selection, transform gizmos, and grid snapping
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-003
- EDITOR-004
createdAt: 2026-07-11T18:46:38.9766390Z
modifiedAt: 2026-07-13T05:29:19.3574220Z
---

Support single selection through viewport picking or hierarchy and provide ImGuizmo translate, rotate, and scale controls with local or world orientation, selection highlighting, and one undo command per completed manipulation. Render a toggleable world-space XZ construction grid at Y=0. Translation snapping uses a configurable grid size; rotation and scale use independently configurable increments. Grid visibility and snapping are independent toggles, with defaults of 1 metre translation, 15 degrees rotation, and 0.1 scale. Persist these authoring preferences per user outside project source. Add deterministic selection, transform, snapping, undo, settings, and grid-generation tests; validate grid density and gizmo interaction through screenshots; and request owner validation of visibility and manipulation feel.

## Notes

- 2026-07-13 05:29 UTC - 2026-07-13 implementation and automated validation: added stable hierarchy/viewport selection for every spatial entity; nearest-hit OBB/model/marker picking with gizmo exclusion; read-only selected Inspector details; ImGuizmo translate/rotate/scale with capability routing, Local/World, W/E/R, transient preview, Escape cancel, validation, no-op suppression, and one identity-resolved undo command; depth-aware grid/axes/major lines with bounded density; markers/highlight; and atomic per-user editor-settings.json persistence with safe defaults. Validation: `dotnet format Royale.slnx --no-restore --verify-no-changes --include src/Royale.Editor src/Royale.Rendering tests/Royale.Editor.Tests tests/Royale.Rendering.Tests` passed (workspace-load warning only); `dotnet build Royale.slnx -m:1 --no-restore` passed with 0 warnings/0 errors; `dotnet test Royale.slnx -m:1 --no-restore --no-build` passed all 1,113 tests; `git diff --check` passed. Runtime capture `/tmp/editor-005-grid-final.png` (1920x1080) confirms macOS native startup, toolbar, visible 1 m depth-aware grid, axes, and spawn/loot/navigation markers. Computer-control tooling could not address the unbundled SDL executable, so automated captures of selected Translate/Rotate/Scale and changed spacing were unavailable. Owner validation is required for viewport/hierarchy picking accuracy, selection highlighting, Translate/Rotate/Scale interaction, Local/World behavior, snapping and changed grid spacing, grid readability, Escape, undo/redo, and W/E/R. Task intentionally remains in doing until that validation.