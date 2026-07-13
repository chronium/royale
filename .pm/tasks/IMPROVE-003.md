---
id: IMPROVE-003
title: Add two-click edge snapping
track: IMPROVE
milestone: M6
priority: low
dependsOn:
- EDITOR-007
createdAt: 2026-07-13T14:26:16.6509960Z
modifiedAt: 2026-07-13T14:26:29.0712210Z
---

Add a dedicated Edge Snap selection mode for a selected spatial entity. The first click selects and visibly highlights a source edge on the selected entity's snapping geometry; the second click selects a destination edge in the scene and previews/commits the whole-entity transform so the source edge coincides with the destination edge. Preserve scale, exclude the selected collider during destination picking, make parallel and anti-parallel edge alignment deterministic, and keep exact coincidence independent of grid quantization. Escape, right click, mode toggle, selection/document changes, and misses cancel or retain the current phase without document history; a completed snap is one undoable transform command. Before implementation, confirm the rotation/alignment policy and endpoint/tangent choice against the editor interaction contract.