---
id: EDITOR-024
title: Import model assets into map projects
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-021
- ASSET-003
createdAt: 2026-07-12T09:08:09.7982390Z
modifiedAt: 2026-07-12T09:08:28.3374490Z
---

Add an editor import workflow that copies source models and referenced textures into the active project, assigns stable project-local asset IDs, runs the existing render and collision artifact pipeline, records imported and generated files in the project manifest, invalidates affected thumbnail caches, and reports conflicts or unsupported content without partially modifying the project.