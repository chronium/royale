---
id: GAME-014
title: Publish prototype arena README screenshot
track: GAME
milestone: M6
priority: medium
dependsOn:
- GAME-013
createdAt: 2026-07-10T14:33:17.4549190Z
modifiedAt: 2026-07-10T14:34:03.2697540Z
---

Rename the project-owner-approved prototype-arena screenshot to a stable documentation asset path and use it as the README hero image. Preserve the historical graybox screenshot, avoid image recompression, verify the committed PNG dimensions/path, and keep documentation aligned with the validated arena.

## Notes

- 2026-07-10 14:34 UTC - Renamed the project-owner-provided screenshot to docs/screenshots/client-prototype-arena.png and replaced the README hero reference/alt text. Preserved docs/screenshots/client-graybox-debug.png as historical documentation and did not recompress or modify the new PNG. Validation: file is a valid non-interlaced RGBA PNG at 2032x1220, README resolves the exact path, and git diff --check passed. No PM wiki update was needed because the arena behavior and documentation source of truth were already current.