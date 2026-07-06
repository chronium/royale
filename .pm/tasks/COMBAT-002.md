---
id: COMBAT-002
title: Implement fire input and cadence
track: COMBAT
milestone: M3
dependsOn:
- COMBAT-001
createdAt: 2026-07-04T09:21:58.4633870Z
modifiedAt: 2026-07-06T13:28:02.6106490Z
---

Process firing through fixed simulation ticks and enforce the weapon fire interval.

## Notes

- 2026-07-06 13:28 UTC - Implemented fire intent and fixed-tick rifle cadence. `PlayerInputSample` now carries `Fire`; the client maps held left mouse to fire through existing input ownership rules. `Royale.Simulation` owns `WeaponFireController`, `WeaponFireState`, and `WeaponFireStepResult`; the default rifle resolves to a 6-tick interval at 60 Hz, fires immediately on the first eligible held-fire tick, and does not reset cooldown on release. The local offline player owns default-rifle cadence state and exposes last fire result plus total shots fired for tests. Added simulation and client tests. Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed; `dotnet test Royale.slnx -m:1 --no-restore` passed. Updated `architecture/physics-and-combat`.