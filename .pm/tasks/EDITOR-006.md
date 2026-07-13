---
id: EDITOR-006
title: Edit the complete map schema
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
- EDITOR-005
createdAt: 2026-07-11T18:46:39.2320810Z
modifiedAt: 2026-07-13T09:12:05.6260210Z
---

Create, duplicate, delete, inspect, and modify static boxes, static models, spawn points, loot points, navigation nodes and links, world bounds, and safe-zone settings, including model placement from the existing asset manifest.

## Notes

- 2026-07-13 09:12 UTC - Implemented complete GameMap schema editing. Added undoable structural commands with stable GUID reindexing; add/duplicate/delete and waypoint rename/delete link cascades; validated entity/root property commands; hierarchy link selection; Inspector editing for every existing map field; render-capable model Place Selected and viewport drag/drop placement using snapped/clamped Y=0 ray resolution; and lightweight MapCatalog validation after commands while preserving authoritative save validation and temporary invalid states. Updated architecture/editor with the resulting contract.

  Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed with 0 warnings/errors; requested parallel `dotnet build Royale.slnx --no-restore` passed with 0 warnings/errors in 2.60s; `dotnet test Royale.slnx --no-restore` passed all 1,158 tests (171 editor tests); `dotnet format Royale.slnx --no-restore --verify-no-changes --include src/Royale.Editor tests/Royale.Editor.Tests` passed (workspace-load warning only); `git diff --check` passed. Native macOS smoke `dotnet run --project src/Royale.Editor/Royale.Editor.csproj --no-restore --no-build -- --reset-layout --screenshot /tmp/editor-006.png --screenshot-after-frames 60` exited successfully and visually confirmed the expanded hierarchy/root Inspector/asset placement controls.

  Owner validation requested for hierarchy and Inspector ergonomics, model drag-and-drop and Place Selected positioning, immediate scene/picking updates, deletion confirmation and waypoint incident-link wording, and undo/redo interaction behavior.