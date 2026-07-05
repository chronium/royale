---
id: PHYS-004
title: Bind world lifecycle
track: PHYS
milestone: M0
createdAt: 2026-07-04T09:21:46.7197140Z
modifiedAt: 2026-07-05T17:55:16.3763060Z
---

Bind world definition, creation, fixed stepping, and destruction.

## Notes

Implemented the low-level Box3D world lifecycle binding surface for macOS ARM64:

* Added `B3Capacity` and `B3WorldDef`, with callback and user pointers represented as `nint` and native `bool` fields marshaled as one-byte booleans.
* Bound `b3DefaultWorldDef`, `b3CreateWorld`, `b3DestroyWorld`, `b3GetWorldCount`, `b3GetMaxWorldCount`, `b3World_IsValid`, and `b3World_Step` through `Box3DBindingSurface.NativeLibraryName`.
* Added test-local copying of `thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.0.1.0.dylib` to the `Royale.Box3D.Tests` output as `libbox3d.dylib`.
* Added layout tests for the new world definition structs and native lifecycle tests for default definition values, create/count/validity, fixed stepping, destroy, and invalidation.

Native lifecycle tests require the macOS ARM64 Box3D artifact to be built first:

```sh
sh thirdparty/build-box3d-macos.sh
```

Validated with:

```sh
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

Managed ownership wrappers remain deferred to `PHYS-009`; final bundled native-library resolution remains owned by `BUILD-004`.
