---
id: ASSET-003
title: Generate triangle and separate collision mesh artifacts
track: ASSET
milestone: M6
priority: medium
dependsOn:
- ASSET-001
createdAt: 2026-07-10T06:15:36.5390460Z
modifiedAt: 2026-07-10T06:16:06.2114290Z
---

Extend the build pipeline with static triangle-mesh collision output and optional separate collision GLB sources. Validate triangle indices, finite geometry, transforms, degenerate triangles, source relationships, and deterministic output. Keep collision-source selection explicit per asset and ensure render-only material or texture data is excluded from server collision artifacts.