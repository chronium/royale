---
id: RENDER-005
title: Add flat directional lighting
track: RENDER
milestone: M2
dependsOn:
- RENDER-004
createdAt: 2026-07-04T09:21:32.7437360Z
modifiedAt: 2026-07-05T20:58:47.6039420Z
---

Add normals, ambient light, and one directional diffuse light for readable gray-box environments.

## Implementation Notes

- Replaced static mesh vertex color data with per-face outward unit normals for the built-in unit box mesh.
- Updated the SDL GPU vertex layout to position `float3` plus normal `float3`.
- Updated the basic shaders to render a fixed neutral gray albedo with ambient `0.35` plus directional diffuse `0.65` from a normalized down-diagonal light direction.
- Added per-instance shader constants containing world-view-projection and world-inverse matrices so normals shade in world space.
- Kept lighting fixed and renderer-local; no material system, render graph, shadows, or per-instance lighting API were introduced.

## Validation Notes

- `dotnet build Royale.slnx -m:1 --no-restore` passed. The build compiled updated shaders through `shadercross`.
- `dotnet test Royale.slnx -m:1 --no-restore` passed: 213 total tests.
- Screenshot smoke check passed with `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-build -- --screenshot /tmp/royale-render-005.bmp --screenshot-after-frames 5`; the captured startup view uses neutral gray lighting and the sampled visible static face increased above ambient-only after the final down-diagonal direction was selected.
- Updated `architecture/content-and-rendering` with the fixed gray material and flat directional lighting model.
