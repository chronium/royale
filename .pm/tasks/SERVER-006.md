---
id: SERVER-006
title: Run two local clients against one simulation
track: SERVER
milestone: M4
priority: medium
dependsOn:
- SERVER-003
- SERVER-004
- SERVER-005
- COMBAT-005
createdAt: 2026-07-04T09:22:04.2997180Z
modifiedAt: 2026-07-06T19:28:57.2519100Z
---

Allow two local or synthetic clients to move, shoot, damage, and kill through one authoritative simulation.

## Notes

Implemented SERVER-006 through the in-process session and headless server boundary.

- Added `HeadlessServerSimulation.Create(GameMap map)` and `InProcessServerSession.Create(GameMap map)` so tests can use deterministic programmatic maps without changing command-line map loading.
- Added `HeadlessServerSimulation.Step(IReadOnlyDictionary<ServerPlayerId, PlayerInputCommand> inputCommands)` while keeping `Step()` as a no-input wrapper.
- `InProcessServerSession.Step()` now drains queued valid commands per connected client, keeps the latest drained command per player for the tick, applies those commands through the authoritative simulation, and snapshots every connected client.
- Server-owned input application now sets look, converts local movement through shared `PlayerMovementIntent.ToWorldMovement()`, steps alive players through `KinematicCharacterController`, and acknowledges the latest processed valid command sequence.
- Server-authoritative rifle fire now enforces alive state, cadence, ammo, magazine consumption, hitscan against static geometry and other alive players, self-exclusion, player damage, death, and living-player-count refresh.
- Dead players remain snapshotted but do not update look, move, jump, fire, consume ammo, or advance weapon cadence.
- Deliberately left out winner selection, combat events, reload, elimination removal, respawn, UDP replication, client prediction, reconciliation, and snapshot send-rate throttling.

## Validation

- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- Added server tests for movement/look snapshots, latest-command-wins behavior, in-process rifle damage visible to both clients, four-hit kill/living count, dead-player no-op behavior, and programmatic test-map creation.
- Updated `architecture/simulation-and-authority`, `architecture/networking`, and `architecture/physics-and-combat` to reflect SERVER-006 behavior.