---
id: TEST-010
title: Add latency, loss, jitter, and reordering controls
track: TEST
milestone: M5
priority: medium
dependsOn:
- TEST-009
- NET-009
createdAt: 2026-07-05T15:17:25.7757690Z
modifiedAt: 2026-07-06T19:30:20.5783150Z
---

Expose adverse-network controls to scripts so scenarios can reproduce and validate behavior under delayed, dropped, duplicated, and reordered packets.

## Implementation notes — 2026-07-10

* Added `scenario.network.set(player, conditions)`, `scenario.network.current(player)`, and `scenario.network.clear(player)` for connected players in loopback UDP scenarios.
* Condition tables replace the full per-player configuration, reject unknown/malformed fields, and expose latency, jitter, loss, duplication, reordering, and nullable deterministic seeds.
* Wrapped each scripted UDP client with `SimulatedNetworkTransport` while leaving the server transport unwrapped, so each packet direction is impaired once.
* Added live `SimulatedNetworkTransport` condition replacement and `CurrentConditions`. New conditions affect subsequently queued packets; already queued packets preserve their decisions and due time. Reapplying a seed resets the random sequence.
* Added a shared manual UDP scenario clock that advances one 60 Hz simulation step per scenario tick while retaining short real loopback polling yields.
* Added invariant `network.conditions.changed` test-host events with player ids and full ordered condition details.
* Added transport unit tests, Wattle host/API tests, and `udp-adverse-network-controls.wattle` covering latency, total loss, duplication/reordering with redundant inputs, and recovery after clear.
* Updated `automated-gameplay-testing` and `architecture/networking`.
* Cross-platform validation remains deferred to `NET-010`. No manual gameplay validation is required because the behavior is deterministic and covered through the real loopback UDP scenario path.

## Validation

* `dotnet build Royale.slnx -m:1 --no-restore` — passed with 0 warnings and 0 errors.
* `dotnet test tests/Royale.Network.Tests/Royale.Network.Tests.csproj -m:1 --no-restore` — passed, 63 tests.
* `dotnet test tests/Royale.Gameplay.Tests/Royale.Gameplay.Tests.csproj -m:1 --no-restore` — passed, 106 tests.
* `dotnet test Royale.slnx -m:1 --no-restore` — passed, 663 tests across all test projects.
* PM project validation — passed with no issues.
