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

## Completion Notes

- Added `assets/meshes/kenney-prototype-kit/crate.glb` and a Kenney Prototype Kit CC0 credit note.
- `Royale.Client` references the vendored SimpleMesh `net8.0` project with `SetTargetFramework="TargetFramework=net8.0"`; SimpleMesh was not patched to .NET 10.
- Client mesh assets under `assets/meshes/**` are copied to runtime output preserving the `assets/meshes/...` path.
- Added `SimpleMeshStaticMeshLoader` in client rendering. It loads GLB geometry through SimpleMesh, ignores external texture bytes for this render-only path, applies node transforms, converts triangle geometry to `StaticMeshGeometry`, and rejects empty, non-triangle, invalid, or non-16-bit-compatible geometry.
- Generalized `StaticMeshRenderer` to upload and draw multiple static mesh batches with the existing basic shader, depth target, and flat lighting. Map static boxes remain on the built-in unit-box batch; the crate is a separate render-only smoke mesh batch.
- SimpleMesh convex hull collision, crate collision, map schema changes, server dependencies, textures/materials, animation/skinning, mesh library management, and SimpleMesh .NET 10 patching were considered and deferred because they are not needed for this render-only smoke asset.
- Updated `architecture/content-and-rendering` with the render-only SimpleMesh GLB behavior and non-goals.

## Validation

- `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` passed.
- `dotnet build Royale.slnx -m:1 --no-restore` passed with existing/vendor warnings only.
- `dotnet test Royale.slnx -m:1 --no-restore` passed: 330 tests.
- Screenshot smoke run passed and wrote `/tmp/royale-render-010.bmp` after 5 frames. The default startup camera currently faces the north boundary wall, so project-owner visual validation in freecam is still requested to confirm the crate presentation.
