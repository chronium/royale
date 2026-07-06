---
id: RENDER-004
title: Draw multiple static meshes
track: RENDER
milestone: M1
priority: medium
dependsOn:
- RENDER-003
createdAt: 2026-07-04T09:21:32.6563870Z
modifiedAt: 2026-07-05T20:57:26.3903280Z
---

Render multiple independently transformed static meshes without introducing a general-purpose scene framework.

## Implementation Notes

Implemented a procedural client-only static mesh renderer for the current gray-box preview scene.

- Replaced the single animated cube renderer with `StaticMeshRenderer`.
- Kept one SDL GPU graphics pipeline, the existing basic vertex/fragment shader pair, and one reusable unit-box vertex/index buffer pair.
- Added renderer-facing data types for `StaticMeshVertex`, `StaticMeshGeometry`, `StaticMeshInstance`, deterministic preview scene generation, and WVP creation.
- Draws multiple independently transformed box instances by pushing one transposed world-view-projection matrix per draw.
- The preview scene includes a broad floor, wall strips, several cover blocks, and one rotated/sloped-looking box for ramp visualization.
- Scope remains procedural and client-only. No asset loading, map JSON loading, server/physics/gameplay/protocol changes, scene graph, ECS, culling, batching, or instancing API were added.
- Updated `architecture/content-and-rendering` to document the renderer scope and that committed static mesh asset/map loading remains deferred to `GAME-001`.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` succeeded. Existing ImGui dependency pruning warning `NU1510` remains.
- `dotnet test Royale.slnx -m:1 --no-restore` succeeded.
- `dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 -- --screenshot /tmp/royale-static-meshes.bmp --screenshot-after-frames 5` succeeded.
- Converted the BMP to `/tmp/royale-static-meshes.png` for inspection; the captured frame shows multiple visible static mesh boxes and the debug overlay.
- Launched the client interactively for fly-camera inspection.