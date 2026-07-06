---
id: PHYS-009
title: Create managed ownership wrappers
track: PHYS
milestone: M1
priority: medium
dependsOn:
- PHYS-008
createdAt: 2026-07-04T09:21:47.1842830Z
modifiedAt: 2026-07-06T05:20:38.5796650Z
---

Add disposable managed world and resource wrappers while retaining access to the low-level native bindings.

## Notes

- 2026-07-06 05:20 UTC - Implemented managed `Box3DWorld`, `Box3DBody`, and `Box3DShape` ownership wrappers in `Royale.Box3D`. Ownership is recursive: worlds track bodies, bodies track shapes, shape disposal leaves the owning body/world valid, body disposal invalidates attached shape wrappers, and world disposal invalidates all child wrappers. Wrapper `Dispose()` methods are idempotent, native IDs remain exposed for low-level binding calls, and wrapper operations throw `ObjectDisposedException` after disposal. Query wrappers remain deferred; `MapStaticCollisionWorld` now owns static collision through the wrappers internally while preserving raw ID/query behavior. Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed; `dotnet test Royale.slnx -m:1 --no-restore` passed. Both commands emitted the existing ImGui.Net `NU1510` warning about `System.Runtime.CompilerServices.Unsafe` not being pruned.