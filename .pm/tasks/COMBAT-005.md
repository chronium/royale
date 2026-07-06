---
id: COMBAT-005
title: Add death and respawn debug flow
track: COMBAT
milestone: M3
dependsOn:
- COMBAT-004
createdAt: 2026-07-04T09:21:58.7310200Z
modifiedAt: 2026-07-06T14:44:40.6850030Z
---

Disable movement on death, switch to a spectator or free camera, and allow debug respawning.

## Notes

- 2026-07-06 - Implemented a client-only offline debug death/respawn flow for the local player. `LocalPlayerController` now owns mutable player health, exposes explicit debug damage/kill/respawn methods, blocks gameplay look and fixed updates while dead, and resets player health, spawn position, velocity, look, weapon cadence, shot count, and last combat outputs on respawn.
- Dead local-player fixed updates do not move, jump, fire, advance rifle cadence state, increment local shot count, resolve hitscan, or damage the training dummy. Respawn intentionally leaves training dummy health and damage history unchanged.
- `SdlApplication` switches to the existing freecam when the local player transitions alive-to-dead and back to gameplay camera mode on debug respawn. F2 remains the manual gameplay/freecam toggle; no kill or respawn hotkeys were added.
- Added an ImGui `Player` diagnostics window showing health/alive state with `Kill Player` and `Respawn Player` buttons. These controls call debug methods and are not gameplay input, server match elimination, respawn timers, final HUD, animation, audio, networking, or player-vs-player damage.
- Updated `architecture/physics-and-combat` with the COMBAT-005 local debug death/respawn contract.
- Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui.Net `NU1510` warning; `dotnet test Royale.slnx -m:1 --no-restore` passed with the same warning.
- 2026-07-06 14:44 UTC - Project owner completed manual human validation after implementation. The local debug death/respawn flow was verified interactively: killing the player, freecam transition, disabled dead-player gameplay control, and debug respawn behavior worked as expected.