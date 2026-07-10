---
id: GAME-011
title: Add model-backed static map objects
track: GAME
milestone: M6
priority: medium
dependsOn:
- ASSET-002
- ASSET-003
- PHYS-011
- RENDER-011
- GAME-002
createdAt: 2026-07-10T06:15:48.4364530Z
modifiedAt: 2026-07-10T08:18:57.0321290Z
---

Add map-authored static model instances that reference stable model asset IDs and transforms. For each instance, render the declared GLB asset on clients and create server-authoritative static collision from its build-generated convex hull, triangle mesh, or separately authored collision mesh according to the asset manifest. Use crate.glb as the first end-to-end fixture, remove its hard-coded smoke placement, keep server artifacts free of render materials/textures, and test client/server transform agreement, collision queries, packaging, diagnostics, and failure behavior.

## Notes

- 2026-07-10 08:18 UTC - Validation decision: the end-to-end crate placement must be validated with automated collision/query tests, client/server transform agreement checks, packaging assertions, and a deterministic screenshot inspected through the screenshot tool. Human visual validation is not required.