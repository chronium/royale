---
id: EDITOR-010
title: Add MCP map inspection and mutation tools
track: EDITOR
milestone: M8
priority: low
dependsOn:
- EDITOR-009
createdAt: 2026-07-11T18:46:40.2454480Z
modifiedAt: 2026-07-13T19:09:29.3900560Z
---

Expose revision-guarded tools for editor state, maps, assets, entities, validation, creation, duplication, property and transform updates, deletion, face snapping, undo, redo, and save while protecting unsaved documents.

## Notes

- 2026-07-13 19:09 UTC - Implemented the active-document MCP map API with 18 attributed official-SDK tools, structured schemas/annotations, a main-thread EditorMcpWorkspace facade, revision and interactive-preview guards, stable-GUID entity inspection/mutation, manifest asset checks, command-backed undo/redo, collision-backed face snapping, validation publication, and in-place project/standalone save conflict protection. Added official-client schema/annotation coverage and workspace tests for busy/stale rejection, no-ops, unsupported transforms, creation/duplication, missing render assets, waypoint rename/delete link integrity, selection preservation, face-snap hit/miss, Save-As-required, unchanged save revision, and external conflicts. Validation on 2026-07-13: `dotnet format Royale.slnx --no-restore --verify-no-changes --include src/Royale.Editor tests/Royale.Editor.Tests` passed (workspace-load warning only); `dotnet build Royale.slnx --no-restore` passed with 0 warnings/errors; `dotnet test Royale.slnx --no-restore --no-build` passed all 1,241 tests; native finite editor smoke with `--mcp --mcp-port 51239` successfully hosted the endpoint, loaded the project, rendered the scene, and displayed listening MCP status. Updated `development/editor-mcp` and `architecture/editor`. Owner validation remains requested for live remote edit scene refresh, remote face snapping, and remote undo/redo interaction with concurrent human selection.