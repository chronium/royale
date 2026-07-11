---
id: EDITOR-007
title: Add face snapping
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-005
- EDITOR-006
createdAt: 2026-07-11T18:46:39.4838400Z
modifiedAt: 2026-07-11T18:46:50.8655810Z
---

Provide previewed bounds-based face snapping against collision surfaces, preserving rotation by default with optional local attachment-axis alignment, excluding the selected object's own collider, and committing through one undoable command.