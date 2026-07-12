---
id: EDITOR-021
title: Add map project creation and lifecycle
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
- EDITOR-020
createdAt: 2026-07-12T09:08:09.2281650Z
modifiedAt: 2026-07-12T09:08:28.2930870Z
---

Let the editor create, open, validate, and atomically save directory-based Royale map projects. Restore the most recent project where appropriate, expose project identity and paths in the editor, resolve maps and assets relative to the project root, and preserve the existing standalone JSON opening path as an explicit import or compatibility workflow.