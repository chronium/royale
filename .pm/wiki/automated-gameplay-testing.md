---
title: Automated Gameplay Testing
createdAt: 2026-07-05T15:18:15.6422560Z
modifiedAt: 2026-07-07T06:00:23.8314780Z
---

## Overview

Automated gameplay testing uses WattleScript to drive deterministic gameplay scenarios against the server-authoritative simulation.

WattleScript is a test orchestration dependency. It is not gameplay scripting, mod scripting, client scripting, or a way for scripts to mutate authoritative game state directly.

The purpose is to make full gameplay flows reproducible, inspectable, and suitable for local development and CI.

## Goals

The automated scenario system should support:

* Deterministic in-process client/server gameplay tests
* Scripted player agents that send normal input commands
* Tick-based waits instead of wall-clock sleeps
* Assertions over snapshots, events, player state, match state, and network behavior
* Replay capture for failures
* Adverse-network simulation
* CI smoke scenarios for the multiplayer match loop

## Authority Boundary

Scripts must preserve the same authority model as real clients.

Scripts may express player intent, such as:

* Move
* Look
* Jump
* Fire
* Reload
* Interact

Scripts must not directly assert authority by mutating server-owned state, such as:

* Player position
* Health
* Damage results
* Pickup ownership
* Safe-zone state
* Match phase
* Winner

When a scenario needs to set up state, it should do so through explicit test-host APIs that are clearly separate from normal gameplay code.

## Runtime Placement

WattleScript belongs in a dedicated gameplay test host or test project.

It must not become a runtime dependency of:

* The game client
* The dedicated server
* Shared authoritative simulation libraries
* Network protocol libraries

The authoritative server should remain able to run without WattleScript present.

Initial integration lives in `tests/Royale.Gameplay.Tests`. The project references the pinned interpreter source at `thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj`; no runtime project under `src/` should reference WattleScript.

The test host helper is `WattleScenarioScriptHost`. It creates `Script` with `CoreModules.Preset_HardSandboxWattle`, sets `script.Options.Syntax = ScriptSyntax.Wattle`, registers the test-only scenario wrapper types as Wattle userdata, and exposes one global named `scenario`. The scenario API wraps `Royale.Server.InProcessServerSession` for server lifecycle, scripted player connection handles, read-only snapshot observation, basic script assertions, clock inspection, and in-memory artifact metadata. WattleScript remains a test orchestration dependency only.

## Scenario API

The script-visible API is narrow and sandboxed. Scripts receive one global object named `scenario`; no other project-owned globals are installed.

Current `scenario` groups:

* `scenario.server.start(mapId)` starts an in-process authoritative server for a map such as `graybox`.
* `scenario.server.stop()` stops the in-process server and invalidates connected script player handles.
* `scenario.server.step(count)` advances the authoritative server by a positive number of simulation ticks.
* `scenario.server.isRunning` reports whether the in-process server is active.
* `scenario.server.tick` reports the current authoritative server tick, or `0` when stopped.
* `scenario.players.connect()` connects one scripted player and returns a player handle with read-only `playerId`, `connectionId`, and `isConnected` properties.
* `scenario.players.disconnect(player)` disconnects a connected script player handle.
* `scenario.players.count` reports the current connected script player count.
* `scenario.observe.latest(player)` returns the latest read-only snapshot wrapper for a connected player.
* `scenario.observe.connectedPlayerCount` and `scenario.observe.livingPlayerCount` expose current server counts while the server is running.
* Snapshot wrappers expose read-only `serverTick`, `localPlayerId`, `acknowledgedInputSequence`, `connectedPlayerCount`, and `livingPlayerCount`.
* `scenario.assert.equal(expected, actual)` throws a script runtime exception when simple script values differ.
* `scenario.assert["true"](value)` and `scenario.assert.isTrue(value)` throw a script runtime exception unless `value` is the boolean `true`. The bracket spelling is required for the member named `true` because `true` is a Wattle keyword and cannot be parsed after `.` as a normal member name.
* `scenario.clock.tick` mirrors `scenario.server.tick`, or `0` before server start and after stop.
* `scenario.artifacts.record(name, value)` stores an in-memory string artifact value for the current host run.
* `scenario.artifacts.count` and `scenario.artifacts.names` expose in-memory artifact metadata.

Current boundaries:

* Scripts cannot directly mutate authoritative player state such as position, health, ammunition, match phase, safe-zone state, or winner.
* The API does not expose `moveTo`, `lookAt`, `shootAt`, low-level input command submission, tick waits, eventual assertions, replay file output, real UDP transport, or adverse-network controls yet.
* Artifact recording is in-memory metadata only; no files are written by `TEST-002`.
* API calls that require a running server or connected player fail explicitly when used after stop or disconnect.

High-level actions such as `moveTo`, `lookAt`, `pickUp`, `shootAt`, and `stayInsideZone` should be implemented through lower-level input commands rather than direct state mutation.

## Execution Model

Scenarios should advance using simulation ticks.

Bounded waits should be expressed in ticks or simulation time, not arbitrary wall-clock sleeps. This keeps tests fast, reproducible, and independent of local machine speed.

The same scenario model should eventually run against:

* In-process transport
* Simulated adverse-network transport
* Real UDP transport

## Replay Artifacts

Failure artifacts should include enough information to reproduce and inspect a scenario:

* Scenario name and version
* Random seed
* Map and configuration
* Inputs by tick
* Network condition changes
* Important server events
* Snapshots around failure
* Assertion failure details

## PM Track

The `TEST` track owns WattleScript-driven automated gameplay testing work.

Initial milestone placement:

* M4: test host, scenario API, tick execution, assertions, low-level player input, in-process transport scenarios
* M5: real UDP scenarios and adverse-network controls
* M6: high-level player actions, deterministic replays, CI artifacts, and complete-match smoke test