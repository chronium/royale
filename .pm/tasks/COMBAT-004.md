---
id: COMBAT-004
title: Add health and damage
track: COMBAT
milestone: M3
dependsOn:
- COMBAT-003
createdAt: 2026-07-04T09:21:58.6410810Z
modifiedAt: 2026-07-06T08:28:00.6261450Z
---

Add basic health, damage application, and death without armour, healing, or limb multipliers.

## Implementation Notes

- Added simulation-owned `HealthState`, `DamageRequest`, `DamageResult`, and `DamageController` types.
- Default player health is `100/100` and alive.
- Rifle target hits apply the weapon damage value (`25`) to the matching target health entry.
- Static hits, no-hit results, missing target ids, missing health entries, non-positive damage, and already-dead targets apply no damage.
- Health clamps at `0`; reaching `0` marks the target dead.
- Local offline player exposes default health only. Movement, firing restrictions, spectating, respawn, target ownership, dummy targets, combat UI, and damage history remain deferred.
- Updated `architecture/physics-and-combat` with the health and damage contract.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed. Existing third-party ImGui NU1510 warning remains.
- `dotnet test Royale.slnx -m:1 --no-restore` passed: 277 tests.
