---
id: DEBT-011
title: Handle degenerate triangles during collision import
track: DEBT
milestone: M6
priority: low
createdAt: 2026-07-12T19:35:35.6251550Z
modifiedAt: 2026-07-12T19:35:40.4393280Z
---

Define and implement robust collision-pipeline behavior for GLB primitives containing degenerate triangles. Reproduce with the Kenney Vehicle.glb body node, preserve actionable diagnostics, decide whether degenerate faces are safely filtered or the asset requires an explicit collision override, and add regression coverage without weakening validation for genuinely invalid collision geometry.