---
id: COMBAT-001
title: Add one rifle definition
track: COMBAT
milestone: M3
createdAt: 2026-07-04T09:21:58.3717550Z
modifiedAt: 2026-07-04T09:22:44.3580670Z
---

Define a single automatic hitscan rifle with fixed damage, magazine size, fire rate, reload time, and range.

## Implementation Notes

- Added `WeaponDefinition` and `WeaponCatalog` in `Royale.Content`.
- Kept the weapon catalog code-backed for COMBAT-001; no JSON loading was introduced.
- Stable default weapon id is `rifle` through `ContentCatalog.DefaultWeaponId`, `WeaponCatalog.DefaultWeaponId`, and `WeaponCatalog.DefaultRifle`.
- Unknown well-formed weapon ids fail with `KeyNotFoundException`; malformed weapon ids fail with `ArgumentException` using the existing ASCII id validation style.

## Canonical Rifle Stats

- Fire model: hitscan
- Automatic: true
- Damage: `25`
- Magazine size: `30`
- Fire rate: `10 shots/sec`
- Fire interval: `0.1 seconds`
- Reload time: `2.0 seconds`
- Range: `120 meters`

## Scope Notes

COMBAT-001 defines shared weapon data and lookup behavior only. Fire cadence enforcement, ammunition mutation, raycasts, damage application, reload state, pickups, inventory, UI, and networking remain deferred to later combat tasks.

## Validation

- `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1`
- `dotnet build Royale.slnx -m:1 --no-restore`
- `dotnet test Royale.slnx -m:1 --no-restore`
