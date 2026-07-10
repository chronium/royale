---
id: DEBT-004
title: Clean up partial SDL GPU renderer construction failures
track: DEBT
priority: low
dependsOn:
- RENDER-011
createdAt: 2026-07-10T12:51:22.0804570Z
modifiedAt: 2026-07-10T12:51:27.9276920Z
---

Make StaticMeshRenderer and texture/geometry upload startup paths release already-created shaders, pipelines, samplers, textures, buffers, and command buffers when a later construction or upload step fails. Add focused coverage where practical.