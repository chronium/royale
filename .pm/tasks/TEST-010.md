---
id: TEST-010
title: Add latency, loss, jitter, and reordering controls
track: TEST
milestone: M5
priority: medium
dependsOn:
- TEST-009
- NET-009
createdAt: 2026-07-05T15:17:25.7757690Z
modifiedAt: 2026-07-06T19:30:20.5783150Z
---

Expose adverse-network controls to scripts so scenarios can reproduce and validate behavior under delayed, dropped, duplicated, and reordered packets.