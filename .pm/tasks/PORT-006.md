---
id: PORT-006
title: Enable Linux x64 development runtime
track: PORT
milestone: M5
priority: low
dependsOn:
- BUILD-004
- PLATFORM-002
- RENDER-001
- RENDER-008
- UI-002
- PHYS-010
createdAt: 2026-07-10T01:56:36.2490070Z
modifiedAt: 2026-07-10T01:56:42.2109300Z
---

Make the source-built Linux x64 client and dedicated server runnable for cross-platform development validation. Add reproducible Linux native builds and runtime copying for the existing SDL3 GPU ImGui backend and BlurgText integration, verify SDL3, Box3D, shaders, assets, and working-directory behavior, and document a development launch workflow on a mainstream Linux environment. This task does not produce self-contained release packages, add platform CI, or complete Mac-to-Linux gameplay validation.