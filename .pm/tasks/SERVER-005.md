---
id: SERVER-005
title: Define server snapshots
track: SERVER
milestone: M4
priority: medium
dependsOn:
- SERVER-002
- SERVER-004
createdAt: 2026-07-04T09:22:04.2100430Z
modifiedAt: 2026-07-06T19:28:49.8643370Z
---

Define snapshots containing server tick, acknowledged input, player state, health, alive state, and match state.

## Completion Notes

Implemented the first protocol-owned server snapshot DTOs in `Royale.Protocol`:

- `ServerSnapshot`
- `PlayerSnapshotState`
- `WeaponSnapshotState`
- `MatchSnapshotState`
- `SafeZoneSnapshotState`
- `ServerSnapshotMatchPhase`

Added `HeadlessServerSimulation.CreateSnapshot(ServerPlayerId? recipientPlayerId = null)` as the server-owned mapper from authoritative state. Player entries are sorted by player id. Recipient player id and acknowledged input sequence are top-level snapshot fields; both are `null` when no recipient is supplied, and unknown recipients throw `InvalidOperationException`.

The snapshot contract deliberately excludes connection ids, spawn reservations, collision internals, and client presentation state. This task did not add serialization, UDP transport, send cadence, prediction, reconciliation, interpolation, or command processing.

Updated `architecture/simulation-and-authority` with the concrete SERVER-005 snapshot contract.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore`
- `dotnet test Royale.slnx -m:1 --no-restore`

Both commands passed. The build/test output included an existing third-party `NU1510` warning from `thirdparty/repos/ImGui.Net/Generator/Evergine.Bindings.Imgui/Evergine.Bindings.Imgui.csproj`.