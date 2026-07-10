---
id: ASSET-003
title: Generate triangle and separate collision mesh artifacts
track: ASSET
milestone: M6
priority: medium
dependsOn:
- ASSET-001
- ASSET-002
createdAt: 2026-07-10T06:15:36.5390460Z
modifiedAt: 2026-07-10T09:21:24.8413960Z
---

Extend the build pipeline with static triangle-mesh collision output and optional separate collision GLB sources. Validate triangle indices, finite geometry, transforms, degenerate triangles, source relationships, and deterministic output. Keep collision-source selection explicit per asset and ensure render-only material or texture data is excluded from server collision artifacts.

## Notes

- 2026-07-10 09:17 UTC - Implementation plan: build on ASSET-002's versioned collision schema. `triangleMesh` will cook transformed triangles from the render GLB; `separateMesh` will cook transformed triangles only from its explicitly declared collision GLB. Canonicalize vertices and triangle ordering while preserving winding, reject invalid/degenerate/indexed geometry, strip build-only collision source paths from generated catalogs, and test deterministic output plus client/server exclusion of collision-source GLBs and render resources.
- 2026-07-10 09:21 UTC - Implemented deterministic indexed triangle collision artifacts. `triangleMesh` cooks from the render GLB; `separateMesh` cooks only from its explicit collision GLB. Both bake hierarchy transforms, canonicalize positions/vertices/triangle order, preserve winding, and reject invalid indices, non-finite data, and triangles that collapse after collision precision snapping. Generated catalogs strip build-only separate source paths. Tests generate GLB fixtures to verify source selection, transforms, winding, deterministic output, client exclusion of the separate collision GLB, and server exclusion of all source GLBs/render data. Asset generation now tracks the cooker assembly as an MSBuild input. Validation: full solution build passed with 0 warnings/errors and all 841 tests passed.