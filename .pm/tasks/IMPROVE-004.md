---
id: IMPROVE-004
title: Add two-click vertex snapping
track: IMPROVE
milestone: M6
priority: low
dependsOn:
- EDITOR-007
createdAt: 2026-07-13T14:26:16.9299650Z
modifiedAt: 2026-07-13T14:26:29.0876560Z
---

Add a dedicated Vertex Snap selection mode for a selected spatial entity. The first click selects and visibly highlights a source vertex on the selected entity's snapping geometry; the second click selects a destination vertex in the scene and previews/commits a translation that makes the two vertices exactly coincident while preserving rotation and scale. Exclude the selected collider during destination picking and do not quantize the final contact to the grid. Escape, right click, mode toggle, selection/document changes, and misses cancel or retain the current phase without document history; a completed snap is one undoable transform command. Source/destination hover and selected-state feedback must make the two phases unmistakable.