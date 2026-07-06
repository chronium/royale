---
id: PHYS-006
title: Bind bodies and transforms
track: PHYS
milestone: M1
priority: urgent
dependsOn:
- PHYS-004
- PHYS-005
createdAt: 2026-07-04T09:21:46.9007300Z
modifiedAt: 2026-07-05T20:57:02.2160610Z
---

Bind body creation, destruction, types, transforms, positions, rotations, and linear velocities.

## Implementation Notes

Added low-level Box3D P/Invoke coverage for body destruction, body type get/set, transform get/set, and linear velocity get/set. This keeps the existing creation, validation, position, and rotation bindings and does not add managed ownership wrappers or broader body APIs.

Added native tests for default static type, body type round-tripping, transform updates, linear velocity round-tripping, velocity-driven dynamic body movement after stepping, and body destruction invalidation while the world remains valid.

Validation passed:

```text
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

Updated wiki page: `architecture/physics-and-combat`.
