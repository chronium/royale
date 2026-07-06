---
id: COMBAT-003
title: Implement hitscan raycasts
track: COMBAT
milestone: M3
dependsOn:
- COMBAT-002
createdAt: 2026-07-04T09:21:58.5531910Z
modifiedAt: 2026-07-06T08:28:00.6171560Z
---

Resolve shots against world and player collision using nearest-hit raycasts.

## Implementation Notes

- Added simulation-owned `HitscanRay`, `HitscanTarget`, `HitscanHit`, and `HitscanResolver` types.
- Rifle shots are built from player feet position plus `PlayerViewSettings.EyeHeight`, using the same yaw/pitch direction convention as `RenderCamera.Forward` and the weapon `RangeMeters` as ray length.
- Static collision is resolved through `MapStaticCollisionWorld.CastRayClosest()` and returns source static collider metadata when available.
- Caller-provided targets are feet-anchored vertical capsules using the same radius/height convention as `KinematicCharacterState` plus `KinematicCharacterSettings`.
- Nearest valid hit wins across static world geometry and capsule targets. Static geometry blocks farther targets.
- Local offline player firing now records `LastHitscanResult` only on ticks where rifle cadence actually fires; no damage, ammunition, target ownership, or visual effects were added.
- Updated `architecture/physics-and-combat` with the hitscan ray contract. Damage application remains deferred to `COMBAT-004`.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed. Existing warning: `NU1510` on `System.Runtime.CompilerServices.Unsafe` in the ImGui generator project.
- `dotnet test Royale.slnx -m:1 --no-restore` passed: 269 tests total.
