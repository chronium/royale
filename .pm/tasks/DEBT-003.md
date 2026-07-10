---
id: DEBT-003
title: Enforce strict JSON for generated asset content
track: DEBT
priority: low
dependsOn:
- ASSET-001
- ASSET-002
createdAt: 2026-07-10T12:51:21.7959060Z
modifiedAt: 2026-07-10T12:51:27.9153630Z
---

Align model manifest and collision artifact parsing with the documented strict JSON contract by rejecting comments and trailing commas, while preserving case-sensitive properties, string enums, and unknown-member rejection. Add parser tests.