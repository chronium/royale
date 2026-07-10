---
id: ASSET-002
title: Generate convex collision artifacts
track: ASSET
milestone: M6
priority: medium
dependsOn:
- ASSET-001
createdAt: 2026-07-10T06:15:36.3132440Z
modifiedAt: 2026-07-10T06:16:06.1968900Z
---

Extend the model asset build pipeline to read transformed GLB triangle geometry through the pinned SimpleMesh dependency, generate and validate convex hulls using SimpleMesh convex/Quickhull support, and serialize them into a project-owned deterministic collision artifact. Reject degenerate or invalid geometry clearly and test transform, scale, winding, reproducibility, and crate hull output.