---
id: EDITOR-007
title: Add face snapping
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-005
- EDITOR-006
createdAt: 2026-07-11T18:46:39.4838400Z
modifiedAt: 2026-07-13T11:42:35.6113750Z
---

Provide previewed bounds-based face snapping against collision surfaces, preserving rotation by default with optional local attachment-axis alignment, excluding the selected object's own collider, and committing through one undoable command.

## Notes

- 2026-07-13 11:42 UTC - Implemented face snapping for static boxes/models, spawns, loot points, and navigation waypoints. Added managed Box3D filtered ray hits with selected-content exclusion and direct asset-root loading; project sessions rebuild generated/server through the asset pipeline (including empty box-only manifests), while standalone maps use packaged assets. Added bounds-support placement, six optional attachment axes, anti-parallel handling, retained preview/session lifecycle, target debug geometry, toolbar/input suppression, one-command commit, diagnostics, and disposal on every cancellation/replacement/shutdown path. Editor now references Simulation/Box3D, and RID-specific asset-pipeline helper resolution was corrected so editor publish succeeds with the native Box3D library.

  Validation evidence (2026-07-13): `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj --no-restore` passed 185/185; focused `MapStaticCollisionWorldTests` passed 22/22; `Royale.AssetPipeline.Tests` passed 18/18; `dotnet build Royale.slnx --no-restore` passed with 0 warnings/errors; `dotnet test Royale.slnx --no-restore --no-build` passed all 1,173 tests; formatter verification completed without changes; `git diff --check` passed; PM project validation passed. `dotnet publish src/Royale.Editor/Royale.Editor.csproj --no-restore -r osx-arm64 --self-contained false -o /tmp/editor-007-publish` succeeded and the artifact contains Royale.Simulation.dll, Royale.Box3D.dll, generated assets, and runtimes/osx-arm64/native/libbox3d.dylib. The editor screenshot smoke `/tmp/editor-007-smoke.png` initialized successfully and visually confirmed the Face Snap toolbar control in the composed viewport UI.

  Updated `architecture/editor` with supported entities, project/standalone collision generation, axis semantics, exact-contact policy, preview/commit/cancellation input lifecycle, diagnostics, dependencies, and coverage. Owner validation remains required for target selection, preview/hit-indicator readability, alignment controls, Escape/right-click/toggle cancellation, and overall snapping feel.