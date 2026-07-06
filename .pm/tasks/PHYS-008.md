---
id: PHYS-008
title: Bind spatial queries
track: PHYS
milestone: M1
priority: high
dependsOn:
- PHYS-007
createdAt: 2026-07-04T09:21:47.0737620Z
modifiedAt: 2026-07-05T20:57:09.0882780Z
---

Bind raycasts, shape or capsule casts, and overlap queries needed by movement, shooting, and spawn validation.

## Implementation Notes

2026-07-06:

* Added low-level world query P/Invoke bindings for `b3DefaultQueryFilter`, `b3World_CastRayClosest`, `b3World_CastRay`, `b3World_OverlapAABB`, `b3World_OverlapShape`, `b3World_CastShape`, `b3World_CastMover`, and `b3World_CollideMover`.
* Added unmanaged callback delegate types for overlap results, cast results, mover filtering, and mover collision plane batches.
* Kept the scope binding-level only. Managed query wrappers, body-specific query APIs, shape mutation APIs, mesh/height-field queries, and gameplay movement/combat helpers remain deferred.
* Updated `architecture/physics-and-combat` to document the PHYS-008 query surface and the low-level P/Invoke boundary.

Validation:

* `dotnet build Royale.slnx -m:1 --no-restore` passed.
* `dotnet test Royale.slnx -m:1 --no-restore` passed.
