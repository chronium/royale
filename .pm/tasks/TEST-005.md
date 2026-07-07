---
id: TEST-005
title: Implement low-level scripted player input
track: TEST
milestone: M4
priority: medium
dependsOn:
- TEST-002
- SERVER-004
createdAt: 2026-07-05T15:17:24.5471300Z
modifiedAt: 2026-07-06T19:29:56.7257650Z
---

Allow scripts to produce the same movement, look, and button input commands used by real clients without directly mutating authoritative state.

## Result

Implemented `scenario.players.input(player, commandTable)` in the Wattle scenario API. The method parses a flat script table into protocol-facing input command data and submits it through `InProcessServerSession.TryEnqueueInputCommand`; scripts still cannot mutate authoritative player state directly.

Command table shape:

* Required numeric fields: `sequence`, `clientTick`, `moveX`, `moveY`, `yawRadians`, `pitchRadians`
* Optional boolean fields defaulting to `false`: `jump`, `fire`, `reload`, `interact`, `crouch`

Well-formed commands that fail protocol validation, such as overlength movement, return `false` and are not acknowledged by snapshots. Malformed script calls throw `ScriptRuntimeException`, including missing required numeric fields, non-number fields, non-boolean button fields, missing player handles, disconnected players, and stopped servers.

Updated the `automated-gameplay-testing` wiki Scenario API section with the new method, command table format, validation behavior, and remaining boundaries.

## Validation

* `dotnet test tests/Royale.Gameplay.Tests/Royale.Gameplay.Tests.csproj -m:1 --no-restore` passed: 35 tests.
* `dotnet build Royale.slnx -m:1 --no-restore` passed with one existing `NU1510` warning from `thirdparty/repos/ImGui.Net/Generator/Evergine.Bindings.Imgui/Evergine.Bindings.Imgui.csproj`.
* `dotnet test Royale.slnx -m:1 --no-restore` passed: 427 tests.