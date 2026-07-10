---
id: RENDER-011
title: Render map model assets with Kenney materials
track: RENDER
milestone: M6
priority: medium
dependsOn:
- ASSET-001
- RENDER-010
createdAt: 2026-07-10T06:15:48.1957580Z
modifiedAt: 2026-07-10T09:02:51.9518750Z
---

Replace the hard-coded crate smoke draw with reusable loading and caching of manifest-addressed GLB render assets. Preserve SimpleMesh node transforms and support the basic Kenney material data needed by the prototype kit, including base color and embedded or referenced textures where present, through the existing SDL GPU renderer without introducing a general material graph or scene framework. Validate the crate with deterministic freecam screenshot capture and automated image inspection; human visual validation is not required.

## Notes

- 2026-07-10 08:18 UTC - Validation decision: use the existing deterministic freecam and SDL GPU screenshot path to frame the crate, capture a BMP, inspect the image automatically, and retain the capture as validation evidence. Human visual validation is not required.
- 2026-07-10 09:02 UTC - Implemented manifest-addressed model rendering and caching for `kenney-crate`. SimpleMesh now preserves node transforms, UVs, primitive material groups, base-color factors, and referenced or embedded image bytes. SDL3 `SDL_LoadSurface_IO` successfully decodes the source PNG, so no ImageSharp dependency was added; decoded RGBA data is uploaded to SDL GPU and sampled with a nearest/repeat sampler, with a white fallback texture for untextured geometry. Automated screenshot validation used freecam position `8,2.2,8`, look-at `6,0.5,5`, and `/tmp/royale-render011-crate-v2.bmp`; inspection confirmed an unobstructed textured Kenney crate. Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed with 0 warnings/errors; `dotnet test Royale.slnx -m:1 --no-restore --no-build` passed all 833 tests.