---
id: SERVER-001
title: Start a headless simulation
track: SERVER
milestone: M4
priority: high
createdAt: 2026-07-04T09:22:03.8604740Z
modifiedAt: 2026-07-06T19:32:31.7823500Z
---

Run map loading, Box3D, and fixed simulation ticks without initializing an SDL window or GPU device.

## Implementation Notes

- Added `--run-ticks <positive-integer>` to server launch parsing. Omitted means the server runs until Ctrl+C or process shutdown; present means the server runs exactly that many fixed ticks as fast as possible, then exits.
- Added `HeadlessServerSimulation`, which loads `MapCatalog.LoadById(options.MapId)`, owns a `MapStaticCollisionWorld`, exposes current tick, map id, static collider count, and disposed state, and advances the Box3D static world once per server tick.
- Added `ServerSimulationLoop`, which uses `SimulationSettings.TickRateHz`, supports finite validation runs, supports cancellation for indefinite runs, and bounds catch-up ticks for realtime indefinite execution.
- Added `MapStaticCollisionWorld.Step` and centralized `SimulationSettings.FixedDeltaSeconds` plus `SimulationSettings.PhysicsSubStepCount` for the initial server-side Box3D step.
- Server startup logging now reports protocol, port, map id, static collider count, tick rate, headless status, and finite or infinite run mode.
- No networking, player state, snapshots, input processing, match phases, safe-zone updates, or combat resolution were added as part of SERVER-001.

## Documentation

- Updated `architecture/runtime-processes` with server map loading, static collision world creation, `--run-ticks`, and startup logging behavior.
- Updated `architecture/simulation-and-authority` with the initial fixed 60 Hz headless tick behavior and current SERVER-001 non-goals.
- Updated `README.md` server examples with finite `--run-ticks` validation.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed. Existing warning: ImGui.Net `System.Runtime.CompilerServices.Unsafe` package pruning warning `NU1510`.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- `dotnet run --project src/Royale.Server/Royale.Server.csproj -- --map graybox --run-ticks 5` passed and logged shutdown after 5 ticks.