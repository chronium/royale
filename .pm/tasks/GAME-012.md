---
id: GAME-012
title: Add a representative Kenney environment asset set
track: GAME
milestone: M6
priority: medium
dependsOn:
- GAME-011
- DEBT-002
createdAt: 2026-07-10T06:15:48.6570130Z
modifiedAt: 2026-07-10T13:41:07.5749970Z
---

After model-backed map objects work end to end, import a small representative set from the Kenney Prototype Kit such as walls, doorways, stairs, columns, slopes, targets, and crates. Assign deliberate collision modes, add focused validation fixtures, preserve attribution, and demonstrate reusable placement without converting the project into a general asset library or coupling this work to the gray-box arena expansion.

## Notes

- 2026-07-10 13:41 UTC - Imported the requested nine Kenney Prototype Kit GLBs beside the existing crate and reused the identical shared colormap. Declared all ten assets with the requested convex/triangleMesh modes and explicit texture resources. Added a focused SimpleMesh patch for valid glTF UNSIGNED_BYTE indices. Per owner decision, triangle-mesh cooking deterministically discards degenerate faces; convex cooking remains strict. Added deterministic client/server package tests, shared-resource and separation checks, all-model StaticMeshAssetCache coverage, and native Box3D shape validation for every artifact. Validation passed: AssetPipeline 18/18, Client 277/277, Simulation 78/78. Updated content/rendering and third-party pin docs; no protocol/schema/default-map changes.