---
id: RENDER-006
title: Add primitive debug drawing
track: RENDER
milestone: M1
dependsOn:
- RENDER-004
- PHYS-008
- GAME-002
createdAt: 2026-07-04T09:21:32.8303430Z
modifiedAt: 2026-07-06T08:30:56.5933230Z
---

Render lines, boxes, capsules, contact points, raycasts, player bounds, and the safe-zone boundary.

## Implementation Notes

- Added low-level Box3D debug draw bindings for `b3DebugDraw`, `b3DebugShape`, `b3Sphere`, `b3DefaultDebugDraw`, `b3World_Draw`, and debug callback delegates.
- `MapStaticCollisionWorld` now configures Box3D debug-shape create/destroy callbacks at world creation and stores geometry-only managed wire segments behind Box3D `userShape` pointers.
- Added project-owned `DebugPrimitiveList` helpers for lines, wire boxes, capsules, points, and circles.
- Added `Box3DDebugDrawAdapter` to call `b3World_Draw` for the local collision world and convert Box3D shape callbacks into debug lines.
- Added `DebugLineRenderer` with a separate SDL GPU line-list pipeline and `debug_line` shaders. Debug line vertices are uploaded before the main pass and drawn after static solids with depth testing disabled.
- Added game-specific debug scene helpers for the local player capsule, spawn markers, and safe-zone boundary.
- Created follow-up task `RENDER-007` for F5-F8 render view modes; mode switching is intentionally out of scope for this task.

## Verification

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- Captured `/tmp/royale-debug-frame.bmp` with the client screenshot mode and inspected a PNG conversion; debug collision and gameplay lines are visible through solid geometry.
- Updated `architecture/content-and-rendering` and `architecture/physics-and-combat` wiki pages.
