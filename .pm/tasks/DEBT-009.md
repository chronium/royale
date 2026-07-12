---
id: DEBT-009
title: Extract and test editor unsaved-document workflow
track: DEBT
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
createdAt: 2026-07-12T09:00:35.7743010Z
modifiedAt: 2026-07-12T09:00:40.9825850Z
---

Move pending Open and Close coordination, Save/Discard/Cancel decisions, dialog cancellation, save failures, and continuation behavior out of private SDL/ImGui orchestration into a deterministic testable component. Add coverage that protects unsaved map data across every workflow outcome.