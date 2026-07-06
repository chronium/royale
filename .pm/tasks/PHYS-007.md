---
id: PHYS-007
title: Bind the required shapes
track: PHYS
milestone: M1
priority: high
dependsOn:
- PHYS-006
createdAt: 2026-07-04T09:21:46.9883920Z
modifiedAt: 2026-07-05T20:57:05.3929340Z
---

Bind boxes, capsules, map collision geometry, shape definitions, creation, and collision filtering.

## Implementation Notes

2026-07-06:

* Added low-level Box3D bindings for capsule shape creation, shape destruction, shape validity/type/body/world/sensor accessors, and shape filter get/set.
* Added `B3Capsule` with pinned native layout coverage.
* Kept scope to primitive gray-box collision support: static box hulls and capsules. Mesh, height-field, compound, sphere, material mutation, event toggles, names, user data, and query/cast APIs remain deferred.
* Updated `architecture/physics-and-combat` to document PHYS-007 shape lifecycle, capsule, box hull, and filter coverage.

Validation:

* `dotnet build Royale.slnx -m:1 --no-restore` passed.
* `dotnet test Royale.slnx -m:1 --no-restore` passed.
