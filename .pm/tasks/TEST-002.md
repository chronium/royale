---
id: TEST-002
title: Define the script-visible scenario API
track: TEST
milestone: M4
priority: medium
dependsOn:
- TEST-001
createdAt: 2026-07-05T15:17:23.8146100Z
modifiedAt: 2026-07-07T06:00:35.6436330Z
---

Expose a narrow sandboxed API for server lifecycle, scripted players, network controls, observations, assertions, clocks, and test artifacts.

## Notes

- 2026-07-07 06:00 UTC - Implemented the first script-visible scenario API in `tests/Royale.Gameplay.Tests`.
  
  API shape:
  - One Wattle global: `scenario`.
  - Groups: `scenario.server`, `scenario.players`, `scenario.observe`, `scenario.assert`, `scenario.clock`, and `scenario.artifacts`.
  - Server/player lifecycle wraps `Royale.Server.InProcessServerSession` for `start(mapId)`, `stop()`, `step(count)`, `isRunning`, `tick`, `players.connect()`, `players.disconnect(player)`, and `players.count`.
  - Observations are read-only wrapper snapshots for connected scripted players: server tick, local player id, acknowledged input sequence, connected player count, and living player count.
  - Assertions expose `equal(expected, actual)`, `isTrue(value)`, and the reserved-name member `scenario.assert["true"](value)` because Wattle parses `true` as a keyword after `.`.
  - Artifacts are in-memory string records only, with count and sorted names metadata.
  
  Deferred work remains out of scope: tick waits, eventual assertions, scripted input submission, high-level movement/combat helpers, replay files, real UDP scenarios, and adverse-network controls.
  
  Validation:
  - `dotnet test tests/Royale.Gameplay.Tests/Royale.Gameplay.Tests.csproj -m:1 --no-restore` passed with 15 gameplay tests.
  - `dotnet build Royale.slnx -m:1 --no-restore` passed with one existing ImGui.Net NU1510 warning.
  - `dotnet test Royale.slnx -m:1 --no-restore` passed.
  - Dependency isolation check passed: WattleScript is referenced only by `tests/Royale.Gameplay.Tests`.