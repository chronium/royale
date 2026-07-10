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
modifiedAt: 2026-07-10T09:54:15.3779910Z
---

Add map-authored static model instances that reference stable model asset IDs and transforms. For each instance, render the declared GLB asset on clients and create server-authoritative static collision from its build-generated convex hull, triangle mesh, or separately authored collision mesh according to the asset manifest. Use crate.glb as the first end-to-end fixture, remove its hard-coded smoke placement, keep server artifacts free of render materials/textures, and test client/server transform agreement, collision queries, packaging, diagnostics, and failure behavior.

## Notes

- 2026-07-10 08:18 UTC - Validation decision: the end-to-end crate placement must be validated with automated collision/query tests, client/server transform agreement checks, packaging assertions, and a deterministic screenshot inspected through the screenshot tool. Human visual validation is not required.
- 2026-07-10 09:40 UTC - Implementation plan: add map `staticModels` entries with instance ID, stable asset ID, position, Euler rotation, and 3D scale; centralize `scale * rotation * translation` in shared content. Replace the crate smoke instance with map-driven render batches. Extend `MapStaticCollisionWorld` to load generated catalogs/artifacts, create convex or indexed static collision, and expose model-aware collider diagnostics without adding SimpleMesh to simulation/server runtime. Add default crate placement, content/failure tests, convex and triangle/separate query tests, render/collision transform agreement, client/server packaging assertions, updated telemetry counts, and deterministic screenshot inspection.
- 2026-07-10 09:54 UTC - Implemented map-authored static models with stable asset IDs and shared position/Euler/scale transforms. Removed the hard-coded crate smoke path; the client now groups map instances by asset ID and renders cached manifest assets. `MapStaticCollisionWorld` loads generated catalogs/artifacts for convex, triangleMesh, and separateMesh modes, creates authoritative static collision, maps shape IDs to model-aware diagnostics, and supports mesh debug wireframes. Graybox now owns `crate-south-east`; client prediction/server collider counts include models. Added validation for map transforms, missing assets/artifacts/kind mismatches, convex and triangle queries, transform agreement, debug geometry, and client/server packaging. Automated capture `/tmp/royale-game011-crate.bmp` at freecam `(8,2.2,8)` looking at `(6,0.5,5)` shows the textured crate clearly with aligned Box3D debug hull. Validation: local solution build passed with 0 warnings/errors and all 858 tests passed; Linux x64 passed all 77 simulation and 164 server tests (fresh SimpleMesh build emitted its two existing unused-local warnings). Server output contains only the generated catalog and collision artifact, with no GLB, texture, SimpleMesh, or pipeline runtime assembly.