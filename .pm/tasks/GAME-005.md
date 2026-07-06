---
id: GAME-005
title: Add first-person camera
track: GAME
milestone: M2
dependsOn:
- GAME-003
- RENDER-003
createdAt: 2026-07-04T09:21:53.1323690Z
modifiedAt: 2026-07-06T07:41:25.7109480Z
---

Attach the camera to the player at a stable eye height and orientation.

## Notes

- 2026-07-06 07:41 UTC - Implemented local offline first-person camera attachment. Added `PlayerViewSettings.DefaultEyeHeight` at 1.62m, a client-owned `LocalPlayerController` that selects a valid map spawn, owns static collision and kinematic movement, converts local input through gameplay yaw, and renders from feet position plus eye height. `SdlApplication` now advances the local player only in gameplay mode; freecam remains independent. Updated architecture wiki pages for rendering, input conversion, and the local/offline nature of this work. Verified with `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, and PM project validation.