---
id: SERVER-002
title: Define authoritative simulation state
track: SERVER
milestone: M4
priority: medium
dependsOn:
- SERVER-001
createdAt: 2026-07-04T09:22:03.9474510Z
modifiedAt: 2026-07-06T19:28:41.3965530Z
---

Make the server authoritative over transforms, velocity, health, ammunition, firing, death, zone state, match state, and winners.

## Implementation Notes

Implemented the first server-owned authoritative state model in `Royale.Server`:

- `ServerPlayerId` and `ServerConnectionId` identify server players and optional server connections.
- `AuthoritativePlayerState` owns player id, optional connection id, `KinematicCharacterState`, `PlayerLookState`, `HealthState`, `AuthoritativeWeaponState`, spawn reservation, and a last-processed input sequence placeholder.
- `AuthoritativeWeaponState` owns weapon id, magazine ammunition, reserve ammunition, `WeaponFireState`, and reload placeholders.
- `AuthoritativeSafeZoneState` owns current center/radius, target radius, and last-updated tick initialized from `GameMap.SafeZone`.
- `AuthoritativeMatchState` owns phase, phase-start tick, living-player count, and optional winner.
- `MatchPhase` starts at `WaitingForPlayers`.

Extended `HeadlessServerSimulation` with `Players`, `MatchState`, `SafeZoneState`, `AddPlayer`, `RemovePlayer`, and `TryGetPlayer`. `AddPlayer` allocates monotonically increasing player ids, selects an unoccupied map spawn through `MapSpawnSelector`, reserves the spawn volume, initializes finite transform/velocity/look, sets `HealthState.DefaultPlayer`, and arms the player with `WeaponCatalog.DefaultRifle`.

`Step` still only advances the server-owned static Box3D world and increments `CurrentTick`.

## Non-Goals

SERVER-002 does not add networking, protocol sessions, client commands, authoritative snapshots, movement simulation, combat resolution, ammunition consumption, reloading, match phase transitions, safe-zone shrinking/damage, eliminations, winner selection, match reset, SDL, rendering, ImGui, or client dependencies.

## Documentation

Updated wiki pages:

- `architecture/simulation-and-authority`
- `architecture/runtime-processes`

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed with one pre-existing third-party `NU1510` warning from ImGui.Net.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- `dotnet run --project src/Royale.Server/Royale.Server.csproj -- --map graybox --run-ticks 5` passed and stopped after 5 ticks.
