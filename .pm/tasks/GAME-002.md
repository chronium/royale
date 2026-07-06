---
id: GAME-002
title: Add static map collision
track: GAME
milestone: M1
priority: medium
dependsOn:
- GAME-001
- PHYS-007
createdAt: 2026-07-04T09:21:52.8417760Z
modifiedAt: 2026-07-05T20:57:33.9852680Z
---

Create Box3D collision geometry for the gray-box environment, initially using simple boxes and primitives.

## Completion Notes

Implemented a narrow `Royale.Simulation` static map collision world for `GameMap.StaticBoxes`. It owns one Box3D world, creates one static body and one box hull shape per static box, and keeps shape ids associated with source static-box ids for tests and debugging.

Collision transforms use the shared `Royale.Content.MapStaticBoxTransforms` helper so collision and client rendering share the same `position`, `size`, and yaw/pitch/roll `rotationEuler` convention. Box hull half-extents are `size / 2`, matching the centered unit-box render mesh.

This intentionally does not add player controller movement, dynamic bodies, spawn validation, loot collision, safe-zone behavior, mesh collision, height fields, debug rendering, or general managed Box3D wrappers. General wrappers remain deferred to `PHYS-009`.

Updated wiki pages:

* `architecture/physics-and-combat`
* `architecture/content-and-rendering`

Validation run:

```text
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

Both commands passed locally. The build and test output still includes the existing ImGui `NU1510` package-pruning warning.
