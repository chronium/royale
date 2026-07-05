---
id: PHYS-003
title: Bind foundational value types
track: PHYS
milestone: M0
createdAt: 2026-07-04T09:21:46.6311920Z
modifiedAt: 2026-07-04T09:22:33.6216820Z
---

Bind vectors, quaternions, transforms, opaque IDs, enums, and query result structures with verified memory layouts.

## Notes

- Added the M0-minimum Box3D value binding surface in `Royale.Box3D.Bindings`.
- Bound math/value types: `B3Vec2`, `B3Vec3`, `B3CosSin`, `B3Quat`, `B3Transform`, `B3Pos`, `B3WorldTransform`, `B3Matrix3`, `B3Aabb`, `B3Plane`.
- Bound opaque IDs: `B3WorldId`, `B3BodyId`, `B3ShapeId`, `B3JointId`, `B3ContactId`.
- Bound enums: `B3BodyType`, `B3ShapeType`.
- Bound core/support values: `B3Version`, `B3Filter`, `B3QueryFilter`, `B3Profile`, `B3Counters`.
- Bound query/result values: `B3RayCastInput`, `B3RayResult`, `B3ShapeProxy`, `B3ShapeCastInput`, `B3BoxCastInput`, `B3CastOutput`, `B3WorldCastOutput`, `B3BodyCastResult`, `B3TreeStats`, `B3PlaneResult`, `B3CollisionPlane`, `B3PlaneSolverResult`, `B3BodyPlaneResult`.
- Enabled unsafe code only for binding/test projects that need fixed inline arrays and `sizeof` layout assertions.
- Single-precision assumption: the pinned Box3D checkout is built without `BOX3D_DOUBLE_PRECISION`, so `B3Pos` is three `float` values and `B3WorldTransform` has the same size/field offsets as `B3Transform`.
- Native layout expectations were derived with a temporary C probe against `thirdparty/repos/box3d/include/box3d`; no probe artifacts were committed.
- Intentionally deferred: P/Invoke methods, world definition/lifecycle APIs, body/shape creation definitions, joint APIs, and higher-level gameplay/physics wrappers.
- Updated `architecture/physics-and-combat` with the initial binding scope and the rule that layout-sensitive Box3D bindings require size/offset tests.

## Validation

```sh
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

Both commands passed locally on macOS ARM64 with the current pinned single-precision Box3D headers.