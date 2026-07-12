---
id: EDITOR-025
title: Export self-contained map packages
track: EDITOR
milestone: M8
priority: low
dependsOn:
- EDITOR-008
- EDITOR-024
createdAt: 2026-07-12T09:08:09.9996420Z
modifiedAt: 2026-07-12T09:08:28.3524870Z
---

Export a validated map project as a deterministic zipped Royale map package. Offer runtime-only export containing map/config data and processed runtime meshes, textures, and collision artifacts, or source-inclusive export that additionally contains original imported authoring assets. Exclude editor caches in both modes, version the package manifest, validate before export, and avoid platform-specific absolute paths.