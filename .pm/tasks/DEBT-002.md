---
id: DEBT-002
title: Validate external GLB resource declarations
track: DEBT
priority: medium
dependsOn:
- ASSET-001
- RENDER-011
createdAt: 2026-07-10T12:51:21.5214530Z
modifiedAt: 2026-07-10T13:34:09.4328300Z
---

Make the model asset pipeline reject render GLBs whose external resources are not declared in the manifest. Validate referenced resource completeness at build time and add a negative test proving an omitted texture fails before client startup.

## Notes

- 2026-07-10 13:34 UTC - Implemented source-manifest validation for external GLB buffer/image URIs. Non-data URIs are resolved relative to the render GLB and must be explicitly listed in render.resources. Added positive and negative GLB-container fixtures. Validation: `dotnet test tests/Royale.AssetPipeline.Tests/Royale.AssetPipeline.Tests.csproj -m:1 --no-restore` passed (17/17). Updated architecture/content-and-rendering.