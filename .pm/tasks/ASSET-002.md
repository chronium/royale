---
id: ASSET-002
title: Generate convex collision artifacts
track: ASSET
milestone: M6
priority: medium
dependsOn:
- ASSET-001
createdAt: 2026-07-10T06:15:36.3132440Z
modifiedAt: 2026-07-10T09:17:02.1269060Z
---

Extend the model asset build pipeline to read transformed GLB triangle geometry through the pinned SimpleMesh dependency, generate and validate convex hulls using SimpleMesh convex/Quickhull support, and serialize them into a project-owned deterministic collision artifact. Reject degenerate or invalid geometry clearly and test transform, scale, winding, reproducibility, and crate hull output.

## Notes

- 2026-07-10 09:03 UTC - Implementation plan: extend the existing asset pipeline rather than creating a second tool; load transformed GLB triangle geometry through pinned SimpleMesh, generate a convex hull through its Quickhull support, canonicalize and validate the result, and emit a versioned deterministic project-owned JSON collision artifact under generated output. Add focused tests for transforms, scale, winding independence, degenerate rejection, reproducibility, and the Kenney crate. Runtime loading remains PHYS-011 scope.
- 2026-07-10 09:17 UTC - Implemented build-time convex collision cooking through pinned SimpleMesh. The pipeline bakes GLB node hierarchy transforms, validates finite indexed triangles, rejects degenerate/coplanar geometry, snaps positions to a one-micrometer grid, runs Quickhull, and writes deterministic version-1 `collision/<asset-id>.json` support-point artifacts. Convex artifacts intentionally contain no triangle indices because Box3D will create native topology from the support points in PHYS-011. `kenney-crate` now declares convex collision and produces a clean eight-corner hull. Tests cover hierarchy transforms, scale, winding independence, invalid geometry, real crate output, and byte reproducibility. Client output contains catalog, GLB, texture, and collision artifact; server output contains only catalog and collision artifact, with no SimpleMesh or pipeline runtime assembly. Validation: full solution build passed with 0 warnings/errors and all 837 tests passed.