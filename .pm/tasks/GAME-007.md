---
id: GAME-007
title: Add spawn points
track: GAME
milestone: M2
dependsOn:
- GAME-001
- GAME-002
- PHYS-008
createdAt: 2026-07-04T09:21:53.3079670Z
modifiedAt: 2026-07-05T20:59:15.1005950Z
---

Define map spawn locations and choose an unoccupied valid spawn through overlap checks.

## Notes

- `MapSpawnPoint.Position` is the player feet anchor.
- `MapCatalog` now requires at least one spawn point, non-empty unique spawn ids, and spawn positions inside `worldBounds`.
- The graybox spawn anchors were moved to valid floor positions with standing clearance from static boundary and cover geometry.
- `Royale.Simulation` now exposes `SpawnSelectionSettings`, `SpawnReservation`, and `MapSpawnSelector`.
- Spawn selection is deterministic in map order. It rejects candidates whose standing clearance AABB overlaps static map collision or caller-provided reservations, and returns `false` when every candidate is blocked.
- AABB touching without positive overlap is allowed. Random spawn selection and match/server integration remain deferred.
- Box3D-dependent simulation tests now share an xUnit collection so static collision and spawn selector tests do not race native Box3D world state.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui package pruning warning.
- `dotnet test Royale.slnx -m:1 --no-restore` passed with the existing ImGui package pruning warning.
- Wiki pages updated: `architecture/content-and-rendering` and `architecture/physics-and-combat`.
