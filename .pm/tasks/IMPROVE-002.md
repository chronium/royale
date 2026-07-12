---
id: IMPROVE-002
title: Render PNG previews in the asset browser
track: IMPROVE
milestone: M6
priority: low
dependsOn:
- EDITOR-024
createdAt: 2026-07-12T19:42:45.5341660Z
modifiedAt: 2026-07-12T19:42:45.5840030Z
---

Display PNG files in the physical editor asset browser using thumbnails of their actual image contents instead of the generic file placeholder. Reuse the existing SDL GPU/ImGui preview lifecycle where practical, preserve model thumbnail behavior, load images without blocking every frame, cache or reuse GPU textures for the active project, dispose resources on project reload and editor shutdown, and retain a neutral placeholder for unreadable or unsupported images. Add focused lifecycle and classification tests, update the editor wiki, and request owner visual validation.