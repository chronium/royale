---
id: GAME-009
title: Expand gray-box map horizontal spacing
track: GAME
milestone: M2
priority: medium
createdAt: 2026-07-06T18:41:38.4606310Z
modifiedAt: 2026-07-06T18:41:41.6637640Z
---

Double the gray-box map's usable horizontal footprint by extending the floor and perimeter walls and increasing horizontal spacing between authored elements while preserving individual object sizes. Keep the ramp/step cluster together as a single layout unit.

## Implementation Notes

- Doubled graybox X/Z gameplay bounds from `-12..12` to `-24..24`.
- Increased safe-zone placeholder radius from `10` to `20`.
- Expanded `ground-main` from `10 x 10` to `20 x 20` metres.
- Moved perimeter walls to approximately `+/-9.9` metres and doubled their long axis.
- Doubled horizontal placement for spawns, loose cover, the center wall, and non-cluster loot points.
- Translated the `step-low`, `ramp-platform-approach`, and `platform-high` cluster as one unit so the ramp/step/platform internal spacing remains unchanged.
- Kept all individual object sizes unchanged except the floor and perimeter walls required to expand the arena.
- Updated map/content tests and coordinate-sensitive collision tests.
- Updated `architecture/content-and-rendering` to document the expanded graybox layout.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- `validate_project` passed.
- Existing warning remains: ImGui.Net emits `NU1510` for `System.Runtime.CompilerServices.Unsafe`; this was not introduced by the map change.

## Human Validation

Please visually validate the expanded gray-box map in the client. In particular, check that the arena feels less cramped horizontally and that the ramp/step/platform cluster still plays like one coherent unit.