---
id: DEBT-002
title: Validate external GLB resource declarations
track: DEBT
priority: medium
dependsOn:
- ASSET-001
- RENDER-011
createdAt: 2026-07-10T12:51:21.5214530Z
modifiedAt: 2026-07-10T12:51:27.8773080Z
---

Make the model asset pipeline reject render GLBs whose external resources are not declared in the manifest. Validate referenced resource completeness at build time and add a negative test proving an omitted texture fails before client startup.