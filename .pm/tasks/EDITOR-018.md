---
id: EDITOR-018
title: Add view-relative editor camera navigation
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-003
createdAt: 2026-07-12T07:56:31.3784300Z
modifiedAt: 2026-07-12T07:56:37.8531740Z
---

Change editor free-camera translation from FPS-style horizontal movement to full view-relative flight. Add Shift speed boost and mouse-wheel dolly forward/backward at a speed greater than boosted W/S movement. Preserve viewport-hover capture requirements and Q/E vertical movement, with deterministic camera/input tests and owner feel validation.