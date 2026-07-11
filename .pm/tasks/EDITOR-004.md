---
id: EDITOR-004
title: Add map documents, undo, and atomic persistence
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-003
createdAt: 2026-07-11T18:46:38.7302880Z
modifiedAt: 2026-07-11T18:46:50.8235720Z
---

Load the existing GameMap JSON format into an editor document with stable entity identities, dirty-state tracking, command-based undo and redo, runtime validation, external-change detection, and explicit atomic Save and Save As.