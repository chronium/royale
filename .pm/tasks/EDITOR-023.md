---
id: EDITOR-023
title: Generate and cache model thumbnails
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-002
- EDITOR-020
- EDITOR-022
createdAt: 2026-07-12T09:08:09.6076830Z
modifiedAt: 2026-07-12T09:08:28.3219310Z
---

Use Royale.Rendering offscreen rendering and the existing screenshot/readback pipeline to lazily generate model preview images for the asset browser. Frame models consistently, cache previews under the active map project's cache directory using content and renderer-version fingerprints, regenerate missing or stale entries, upload previews without blocking normal interaction, and fall back safely when generation fails.