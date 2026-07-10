---
id: ASSET-001
title: Define model asset manifest and build pipeline
track: ASSET
milestone: M6
priority: high
dependsOn:
- BUILD-012
- RENDER-010
createdAt: 2026-07-10T06:15:36.0869950Z
modifiedAt: 2026-07-10T06:16:06.1542620Z
---

Define a project-owned model asset manifest with stable asset IDs, source GLB paths, render metadata, collision mode, and optional separate collision-source path. Add deterministic build-time processing with explicit generated-output locations, incremental rebuild behavior, actionable validation errors, and packaging boundaries so clients receive render assets plus collision data while servers receive only gameplay-relevant collision artifacts. Use the existing Kenney crate as the first fixture and preserve source attribution.