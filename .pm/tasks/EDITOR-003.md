---
id: EDITOR-003
title: Create the docked editor shell
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-001
- EDITOR-002
createdAt: 2026-07-11T18:46:38.4743390Z
modifiedAt: 2026-07-12T07:56:37.9280720Z
---

Create a standalone macOS ARM64 Royale.Editor application with ImGui docking, a central viewport, hierarchy, inspector, asset browser, validation and log panel, and persistent editor layout.

## Notes

- 2026-07-12 07:50 UTC - 2026-07-12 implementation evidence: added standalone macOS ARM64 Royale.Editor with Platform/Rendering/Content/Diagnostics-only boundary; packaged SDL, ImGui, Blurg, rendering shaders, maps, and generated model assets; implemented persistent/resettable docking, read-only panels, offscreen high-DPI viewport, framed free camera, viewport-owned navigation, and composed screenshots. Validation: `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` succeeded; `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings/errors; `dotnet test Royale.slnx -m:1 --no-restore` passed all projects, including 18 Editor tests; macOS ARM64 launch/screenshot succeeded at frame 8. Screenshot inspection found a nonblank, correctly oriented graybox viewport and coherent default docking; inspector formatting was compacted after inspection. Wiki `architecture/editor` updated. Owner validation remains required for docking/resizing, layout persistence/reset, viewport navigation, high-DPI behavior, and overall usability, so task remains doing.
- 2026-07-12 07:56 UTC - 2026-07-12 owner validation passed: ImGui docking and panels work well, and the docked editor shell is accepted as complete. Follow-up observations were split into EDITOR-017 (suppress ImGui interaction during viewport capture), EDITOR-018 (view-relative flight camera with Shift boost and faster wheel dolly), and EDITOR-019 (evaluate a native macOS font such as San Francisco).