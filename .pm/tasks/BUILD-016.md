---
id: BUILD-016
title: Reorganize project source layout by responsibility
track: BUILD
milestone: M1
priority: urgent
createdAt: 2026-07-06T17:33:37.0307090Z
modifiedAt: 2026-07-06T17:33:42.1177730Z
---

Perform a mechanical source-layout pass outside the already cleaned client platform folder. Split oversized folders such as client rendering and flat simulation code into responsibility-focused folders/namespaces, keeping behavior unchanged. Update tests, wiki structure notes, and validation.

## Implementation Notes

- Split `Royale.Client.Rendering` into focused subfolders and namespaces:
  - `Cameras` for render/free/gameplay camera types.
  - `Debug` for debug primitive lists, scene building, Box3D debug adapter, and line rendering.
  - `Meshes` for static mesh geometry, instances, draw constants, and rendering.
  - `Screenshots` for BMP screenshot writing.
  - `Shaders` for shader asset selection helpers.
  - `Text` for Blurg text rendering, screen text, and world text billboards.
- Split `Royale.Simulation` into focused gameplay-domain subfolders and namespaces:
  - `Combat` for health, damage, weapon fire, hitscan rays, and hit resolution.
  - `Movement` for player input samples, look state/settings, view settings, and kinematic character control.
  - `World` for simulation settings, map collision, static world queries, and spawn selection.
  - `Debug` for simulation-side debug geometry descriptions.
- Updated client, server, and tests to use the new namespaces.
- Updated `architecture/overview` so the wiki documents the current source layout.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- `validate_project` passed.
- Existing warning remains: ImGui.Net emits `NU1510` for `System.Runtime.CompilerServices.Unsafe`; this was not introduced by the restructure.