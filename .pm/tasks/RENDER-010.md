---
id: RENDER-010
title: Load and render a SimpleMesh GLB asset
track: RENDER
milestone: M3
priority: medium
dependsOn:
- BUILD-012
createdAt: 2026-07-06T16:50:28.0790170Z
modifiedAt: 2026-07-06T16:50:32.6122420Z
---

Use SimpleMesh to load a first real mesh asset into the client renderer without introducing a general asset pipeline or scene framework. Copy the Kenney Prototype Kit `crate.glb` from `/Users/chronium/Developer/kenney_prototype-kit/Models/GLB format/crate.glb` into the repository under an explicit assets path, include a Kenney CC0 license/credit note, and add client copy rules for mesh assets. Add a client/rendering-owned SimpleMesh adapter that converts supported geometry into the current `StaticMeshGeometry` shape using positions, normals, and triangle indices. Render the crate visibly in the gray-box scene as a smoke mesh while keeping existing map static boxes on the unit-box path. Scope excludes collision mesh generation, map schema changes, server dependencies, materials, textures, animation, skinning, and mesh library management.