---
id: EDITOR-001
title: Extract the shared desktop platform layer
track: EDITOR
milestone: M6
priority: high
createdAt: 2026-07-11T18:46:37.9511440Z
modifiedAt: 2026-07-11T18:46:50.7385930Z
---

Move reusable SDL window, event, timing, input plumbing, and desktop application lifecycle behavior out of Royale.Client while preserving client behavior and keeping graphical dependencies out of the server.