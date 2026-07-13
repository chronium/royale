---
id: EDITOR-005
title: Add viewport selection, transform gizmos, and grid snapping
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-003
- EDITOR-004
createdAt: 2026-07-11T18:46:38.9766390Z
modifiedAt: 2026-07-13T04:57:32.3372480Z
---

Support single selection through viewport picking or hierarchy and provide ImGuizmo translate, rotate, and scale controls with local or world orientation, selection highlighting, and one undo command per completed manipulation. Render a toggleable world-space XZ construction grid at Y=0. Translation snapping uses a configurable grid size; rotation and scale use independently configurable increments. Grid visibility and snapping are independent toggles, with defaults of 1 metre translation, 15 degrees rotation, and 0.1 scale. Persist these authoring preferences per user outside project source. Add deterministic selection, transform, snapping, undo, settings, and grid-generation tests; validate grid density and gizmo interaction through screenshots; and request owner validation of visibility and manipulation feel.