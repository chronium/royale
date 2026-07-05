---
id: PHYS-005
title: Port the Box3D Hello World test
track: PHYS
milestone: M0
createdAt: 2026-07-04T09:21:46.8129210Z
modifiedAt: 2026-07-04T09:22:33.6219400Z
---

Reproduce the upstream falling-box example in C# and verify comparable simulation results.

## Notes

Ported the upstream Box3D `docs/hello.md` falling-box example into `Box3DHelloWorldTests`.

Bound only the Hello World API surface:

* `b3DefaultBodyDef`
* `b3CreateBody`
* `b3Body_IsValid`
* `b3Body_GetPosition`
* `b3Body_GetRotation`
* `b3DefaultShapeDef`
* `b3MakeBoxHull`
* `b3MakeCubeHull`
* `b3CreateHullShape`

Added the native value structs needed by that subset: `B3MotionLocks`, `B3BodyDef`, `B3SurfaceMaterial`, `B3ShapeDef`, `B3HullData`, `B3HullVertex`, `B3HullHalfEdge`, `B3HullFace`, and `B3BoxHull`. Broader managed body/shape ownership wrappers, lifecycle APIs, mesh/capsule/sphere bindings, and shape query APIs remain deferred.

The test creates a default-gravity world, adds the static ground box at `{0, -10, 0}`, adds a dynamic cube at `{0, 4, 0}` with density `1.0` and friction `0.3`, steps 90 ticks at `1/60` with 4 substeps, and asserts the cube falls and settles near `Y = 1` with approximately identity rotation. Native world tests are serialized because Box3D world counts are process-global.

Native layout tests were extended for the new structs, including native bool packing, pointer fields, `B3BodyDef` and `B3ShapeDef` offsets, hull metadata, and `B3BoxHull` embedded storage offsets.

macOS ARM64 native tests require the local artifact first:

```sh
sh thirdparty/build-box3d-macos.sh
```

Validated with:

```sh
sh thirdparty/build-box3d-macos.sh
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```
