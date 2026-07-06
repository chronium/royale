---
id: GAME-003
title: Implement player look controls
track: GAME
milestone: M2
dependsOn:
- RENDER-003
createdAt: 2026-07-04T09:21:52.9525530Z
modifiedAt: 2026-07-05T20:58:56.7673660Z
---

Add mouse-driven yaw and pitch with pitch clamping, relative mouse mode, and cursor release behavior.

## Notes

- 2026-07-06 - Implemented shared gameplay-facing input and look types in `Royale.Simulation`: `PlayerInputSample`, `PlayerLookState`, `PlayerLookSettings`, and `PlayerLookController`. Mouse deltas update yaw and clamped pitch, and non-finite deltas are ignored so look state remains valid.
- 2026-07-06 - Added client gameplay input mapping for `W/A/S/D`, `Space`, and mouse look gated by relative mouse mode. Gameplay input is sampled locally but is not yet applied to player movement, serialized into protocol commands, or sent to a server.
- 2026-07-06 - Split render camera data from camera controllers with `RenderCamera`. The client now starts in gameplay view, `F2` toggles gameplay/freecam, `F1` toggles relative mouse capture, `Escape` releases capture before quitting, and freecam movement only applies in freecam mode.
- 2026-07-06 - Gameplay view uses `PlayerLookState` with a temporary fixed camera anchor. First-person attachment to the player capsule remains `GAME-005`.
- 2026-07-06 - Updated wiki pages `architecture/simulation-and-authority` and `architecture/content-and-rendering` to document gameplay input/look state, view modes, and the remaining `GAME-005` camera attachment boundary.
- 2026-07-06 - Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed; `dotnet test Royale.slnx -m:1 --no-restore` passed. Both commands emitted the existing ImGui.Net `NU1510` warning about `System.Runtime.CompilerServices.Unsafe` not being pruned.
