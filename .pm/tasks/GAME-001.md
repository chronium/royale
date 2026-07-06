---
id: GAME-001
title: Create a gray-box map
track: GAME
milestone: M1
priority: high
dependsOn:
- RENDER-004
createdAt: 2026-07-04T09:21:52.7524290Z
modifiedAt: 2026-07-05T20:59:55.7385910Z
---

Build a small test environment with ground, walls, ramps, steps, platforms, cover, and boundaries.

## Implementation Notes

Implemented shared copied map content in `Royale.Content`:

- Added `src/Royale.Content/Maps/graybox.json`, copied to runtime output as `maps/graybox.json`.
- Added `GameMap`, static box, spawn, loot, bounds, safe-zone, and vector DTOs.
- Added `MapCatalog` loading by map id with clear missing-file, invalid-id, malformed-JSON, and invalid-map-shape failures.
- Kept spawn points, loot points, bounds, and safe-zone fields as schema placeholders only.
- Replaced the client-only procedural preview scene with map-loaded static box rendering through `MapStaticMeshScene`.
- Revised the first gray-box formation after manual review so the ramp/platform area reads more clearly.
- Left Box3D static collision creation to `GAME-002`; this task only defines shared map data and client visualization.

Updated wiki page: `architecture/content-and-rendering`.

## Validation

Automated validation completed on 2026-07-06:

```text
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 --no-restore -- --map graybox --screenshot /tmp/royale-graybox-map.bmp --screenshot-after-frames 5
```

Results:

- Build passed with the existing ImGui `NU1510` package-pruning warning only.
- Tests passed: 34 Box3D, 71 client, 4 diagnostics, 4 native, 2 protocol, 10 server, 1 simulation.
- Screenshot command loaded map `graybox` with 12 static boxes and exited successfully.
- Visual screenshot inspection confirmed a multi-object gray-box environment with boundaries, cover, platform/step geometry, and a rotated ramp.

After the layout revision, validation was repeated:

```text
dotnet test Royale.slnx -m:1 --no-restore
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 --no-restore -- --map graybox --screenshot /tmp/royale-graybox-map-v2.bmp --screenshot-after-frames 5
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 --no-restore -- --map graybox
```

Results:

- Tests still passed with the same counts.
- Screenshot command loaded map `graybox` with 12 static boxes and exited successfully.
- Interactive client run loaded the revised map and exited cleanly.
- Project owner manually reviewed the revised formation and accepted it as good enough to complete GAME-001.
