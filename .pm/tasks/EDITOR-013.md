---
id: EDITOR-013
title: Add physics-assisted drop placement
track: EDITOR
milestone: M8
priority: low
dependsOn:
- EDITOR-006
- EDITOR-007
createdAt: 2026-07-11T18:46:41.0019130Z
modifiedAt: 2026-07-11T18:46:50.9524300Z
---

Temporarily simulate decorative objects with Box3D using fixed ticks and settling thresholds, then bake stable transforms back into the static map as one cancellable undoable editor command.