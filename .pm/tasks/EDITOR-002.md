---
id: EDITOR-002
title: Extract reusable SDL GPU rendering
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-001
createdAt: 2026-07-11T18:46:38.2166210Z
modifiedAt: 2026-07-11T18:46:50.7883210Z
---

Share SDL GPU device management, swapchain and offscreen render targets, cameras, mesh and material resources, debug primitives, ImGui SDL3_GPU integration, GPU readback, and screenshots without introducing a general-purpose scene engine.