---
id: GAME-010
title: Expand gray-box arena for eight players
track: GAME
milestone: M6
priority: high
dependsOn:
- GAME-009
- GAME-007
createdAt: 2026-07-10T06:09:50.6835430Z
modifiedAt: 2026-07-10T06:36:40.2336020Z
---

Expand the gray-box arena to a 40 x 40 metre playable floor for an eight-player match. Author 12 valid, separated candidate spawn points and add additional gray-box primitive obstacles arranged as deliberate cover, sightline breaks, and coherent traversal clusters. Keep object scale readable, preserve explicit primitive collision, update coordinate-sensitive content/simulation tests, update the content-and-rendering wiki source of truth, and require deterministic screenshot plus human play validation of layout, spawn clearance, navigation, and combat sightlines.

## Notes

- 2026-07-10 06:36 UTC - Implemented the 40 x 40 m graybox arena using the existing primitive schema only: 12 ordered spawn candidates, eight loot placeholders, 18 interior primitives across north/east/south/west/center, preserved ramp cluster offsets/transform, radius-20 safe zone, and unchanged -24..24 world bounds. Added content/collision coverage for counts, uniqueness, bounds, safe-zone inclusion, zone categories, ramp spacing, all-12 spawn reservation viability, spawn/loot/dummy static clearance, opening-direction sightline breaks, and relocated raycast/overlap coordinates. Updated architecture/content-and-rendering. Validation passed: `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` (needed existing SimpleMesh restore metadata), `dotnet build Royale.slnx -m:1 --no-restore` (0 warnings/errors), `dotnet test Royale.slnx -m:1 --no-restore` (747 passed), PM `validate_project`, and elevated deterministic overview capture at `/tmp/royale-game-010.bmp`. Human validation remains required for four-zone readability, spawn protection/routes, 10-15 m combat lanes, ramp playability, navigation around cover, and F5-F8 world/collision render modes.