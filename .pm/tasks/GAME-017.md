---
id: GAME-017
title: Add Blender-authored courtyard compound map
track: GAME
milestone: M6
priority: medium
dependsOn:
- ASSET-004
- GAME-011
- GAME-013
- BOT-005
createdAt: 2026-07-11T07:44:40.8258590Z
modifiedAt: 2026-07-11T08:25:16.7385960Z
---

Author and integrate the explicitly selected courtyard-compound map: a 100x100 m low-poly compound with a two-storey building, interior and exterior stairs, shoot-through windows, fenced courtyard and gates, exterior cover, 12 spawns, 12 loot points, and a connected standing-capsule-valid waypoint graph. Commit the editable Blender source and generated render/collision GLBs and map JSON; register separateMesh collision; relax map validation to accept a static box or static model; add gameplay/content/collision/traversal tests and capture engine screenshots.

## Notes

- 2026-07-11 08:25 UTC - Implemented Blender-authored Courtyard Compound and integration. Added editable .blend plus reproducible scene generator, render/collision GLBs, deterministic map JSON, separateMesh asset registration, static-model-only map acceptance, compound-mesh-aware spawn clearance, and tests for map contract, asset packaging, collision registration, and eight simultaneous spawn reservations. Blender/engine validation corrected the rear stair route, interior stair waypointing, upper-floor clearance, and a hidden full-width fence that blocked the south gate. Validation: Blender exporter unit tests 7/7; full solution build 0 warnings/errors; full suite passed except stale 10-asset assertions, all updated and focused asset-pipeline regression now passes; server starts courtyard-compound and validates all 54 navigation links bidirectionally; four deterministic engine captures created at /tmp/courtyard-{exterior,courtyard,ground-floor,upper-floor}.png; git diff --check passes. Human validation remains required for final visual quality, building readability, sightlines, stair feel, interior navigation, collision alignment, and exterior population; debug engine captures currently include the renderer's wireframe overlay.