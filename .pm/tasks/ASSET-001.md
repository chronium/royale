---
id: ASSET-001
title: Define model asset manifest and build pipeline
track: ASSET
milestone: M6
priority: high
dependsOn:
- BUILD-012
- RENDER-010
createdAt: 2026-07-10T06:15:36.0869950Z
modifiedAt: 2026-07-10T08:45:30.2078950Z
---

Define a project-owned model asset manifest with stable asset IDs, source GLB paths, render metadata, collision mode, and optional separate collision-source path. Add deterministic build-time processing with explicit generated-output locations, incremental rebuild behavior, actionable validation errors, and packaging boundaries so clients receive render assets plus collision data while servers receive only gameplay-relevant collision artifacts. Use the existing Kenney crate as the first fixture and preserve source attribution.

## Notes

- 2026-07-10 08:18 UTC - Planning decision: use a strict JSON model manifest, matching existing maps and launch profiles. Generated render/collision artifacts are deterministic build outputs under intermediate/output directories and are not committed. The planned asset batch continues through GAME-011.
- 2026-07-10 08:45 UTC - Implemented the strict version-1 JSON model asset manifest and deterministic audience-specific build pipeline. Added explicit GLB/resource declarations, path/ID/schema validation, generated-field separation, sorted normalized catalogs, uncommitted obj outputs, incremental MSBuild tracking, client/server build and publish integration, the Kenney crate colormap source, and focused pipeline tests. Client build/publish contains model-assets.json, crate.glb, and Textures/colormap.png. Server build/publish contains only its collision-projected model-assets.json and excludes GLBs, textures, SimpleMesh, SDL/ImGui/Blurg, and the non-publishable pipeline tool. Validation: dotnet build Royale.slnx -m:1 --no-restore passed with 0 warnings/errors; dotnet test Royale.slnx -m:1 --no-restore --no-build passed 831 tests; unchanged client rebuild preserved the generation stamp; sequential client/server dotnet publish checks passed. The asset and deployment wiki sections were updated. No human validation required.