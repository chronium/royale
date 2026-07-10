---
id: PHYS-011
title: Bind generated convex hull and static mesh collision
track: PHYS
milestone: M6
priority: medium
dependsOn:
- PHYS-007
- ASSET-002
- ASSET-003
createdAt: 2026-07-10T06:15:47.9545450Z
modifiedAt: 2026-07-10T09:33:15.4665420Z
---

Extend the focused Box3D C# binding and managed ownership wrappers for arbitrary convex hull creation, hull destruction, mesh definitions, mesh creation and destruction, and static mesh shapes required by generated model collision artifacts. Verify native layouts and ownership lifetimes, reject invalid generated geometry safely, preserve low-level binding access, and add focused hull/mesh creation, query, debug-draw, and disposal tests.

## Notes

- 2026-07-10 09:23 UTC - Implementation plan: add low-level pointer bindings and verified layouts for `b3CreateHull`/`b3DestroyHull`, `b3MeshDef`/`b3MeshData`, `b3CreateMesh`/`b3DestroyMesh`, and `b3CreateMeshShape`. Add disposable managed hull and mesh resources; hull shapes clone native hull data, while mesh shapes retain a SafeHandle reference until shape/body/world disposal because Box3D does not clone mesh data. Require explicit mesh cook settings and scale, reject malformed/non-finite/degenerate generated geometry before native calls, and test layout, low-level access, creation, query, debug draw, and disposal lifetimes. Artifact-to-vector adaptation remains GAME-011 so the physics layer does not depend on `Royale.Content`.
- 2026-07-10 09:33 UTC - Implemented focused low-level bindings for native hull/mesh creation, destruction, mesh shape creation, and mesh diagnostics, plus verified `b3MeshDef`, mesh node/triangle, and `b3MeshData` layouts. Added SafeHandle-backed `Box3DHull` and `Box3DMesh`; hull shapes clone source geometry, while mesh shapes hold a deferred native lease until shape/body/world native teardown completes. Managed validation rejects malformed, non-finite, degenerate, non-static, and invalid-scale usage. Tests cover direct low-level access, ABI layouts, hull and mesh ray queries, debug-draw traversal, shape/body/world disposal order, and invalid inputs. Validation: full solution build passed with 0 warnings/errors and all 849 tests passed on macOS ARM64; OrbStack Linux x64 passed all 54 Box3D tests using isolated `/tmp` artifacts.