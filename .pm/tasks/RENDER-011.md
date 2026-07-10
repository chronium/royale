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
modifiedAt: 2026-07-10T08:18:57.0033950Z
---

Replace the hard-coded crate smoke draw with reusable loading and caching of manifest-addressed GLB render assets. Preserve SimpleMesh node transforms and support the basic Kenney material data needed by the prototype kit, including base color and embedded or referenced textures where present, through the existing SDL GPU renderer without introducing a general material graph or scene framework. Validate the crate with deterministic freecam screenshot capture and automated image inspection; human visual validation is not required.

## Notes

- 2026-07-10 08:18 UTC - Validation decision: use the existing deterministic freecam and SDL GPU screenshot path to frame the crate, capture a BMP, inspect the image automatically, and retain the capture as validation evidence. Human visual validation is not required.