---
title: Automated Gameplay Testing
createdAt: 2026-07-05T15:18:15.6422560Z
modifiedAt: 2026-07-10T15:27:22.6979380Z
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

The test host helper is `WattleScenarioScriptHost`. It creates `Script` with `CoreModules.Preset_HardSandboxWattle`, sets `script.Options.Syntax = ScriptSyntax.Wattle`, registers the test-only scenario wrapper types as Wattle userdata, and exposes one global named `scenario`. The scenario API uses a test-only runtime abstraction with an in-process implementation backed by `Royale.Server.InProcessServerSession` and a loopback UDP implementation backed by `Royale.Server.NetworkServerRuntime`, `Royale.Network.LiteNetLibNetworkTransport`, `Royale.Network.ClientInputSender`, and `Royale.Protocol` serializers. WattleScript remains a test orchestration dependency only.

## Scenario API

The script-visible API is narrow and sandboxed. Scripts receive one global object named `scenario`; no other project-owned globals are installed.

Current `scenario` groups:

* `scenario.server.start(mapId)` starts the original in-process authoritative server runtime for a map such as `graybox`.
* `scenario.server.startUdp(mapId)` starts a loopback UDP scenario runtime for a map such as `graybox`. The test host reserves an ephemeral local UDP port, starts `NetworkServerRuntime` with an unwrapped `LiteNetLibNetworkTransport` on `127.0.0.1`, and connects scripted clients through per-player `SimulatedNetworkTransport` wrappers bound to port `0`. Each wrapper starts with no impairment.
* `scenario.server.stop()` stops the active scenario runtime and invalidates connected script player handles.
* `scenario.server.step(count)` advances the active authoritative server runtime by a positive number of simulation ticks. In UDP mode each tick also polls client and server transports so packets and snapshots can move through the real socket path.
* `scenario.server.isRunning` reports whether a scenario runtime is active.
* `scenario.server.tick` reports the current authoritative server tick, or `0` when stopped.
* `scenario.players.connect()` connects one scripted player and returns a player handle with read-only `playerId`, `connectionId`, and `isConnected` properties. In UDP mode this call blocks until the handshake is accepted and the first decoded snapshot is available, with a bounded timeout.
* `scenario.players.disconnect(player)` disconnects a connected script player handle.
* `scenario.players.input(player, commandTable)` submits one low-level protocol-shaped input command. In in-process mode this enters the in-process server boundary. In UDP mode it is serialized and sent through `ClientInputSender` over `LiteNetLibNetworkTransport`. The call returns `true` when the command is locally valid and submitted, or `false` when the command is well-formed but rejected by protocol validation.
* `scenario.players.count` reports the current connected scripted player count from the active runtime.
* `scenario.observe.latest(player)` returns the latest read-only snapshot wrapper for a connected player. In UDP mode this snapshot is the latest decoded `ServerSnapshot` packet received by that scripted UDP client.
* `scenario.observe.connectedPlayerCount` and `scenario.observe.livingPlayerCount` expose current server counts while the server is running.
* `scenario.clock.tick` mirrors `scenario.server.tick`, or `0` before server start and after stop.
* `scenario.clock.waitTicks(count)` advances the running scenario runtime by exactly `count` simulation ticks and returns the current tick. `count` must be from `1` through `scenario.clock.maxWaitTicks`.
* `scenario.clock.waitUntil(maxTicks, predicate)` evaluates a script predicate at the current tick, then advances up to `maxTicks` ticks until the predicate returns boolean `true`. It returns `true` on success and `false` after exactly `maxTicks` ticks when the predicate never succeeds. `maxTicks` may be `0` for a current-tick-only check.
* `scenario.clock.maxWaitTicks` reports the fixed per-call wait cap, currently `10000` ticks.
* `scenario.artifacts.record(name, value)` stores an in-memory string artifact value for the current host run.
* `scenario.artifacts.count` and `scenario.artifacts.names` expose in-memory artifact metadata.
* `scenario.network.set(player, conditions)` replaces the complete adverse-network configuration for one connected scripted UDP player.
* `scenario.network.current(player)` returns read-only `latencyMs`, `jitterMs`, `lossChance`, `duplicateChance`, `reorderChance`, and nullable `randomSeed` fields.
* `scenario.network.clear(player)` restores that player's no-impairment configuration.

Network condition fields are optional. Omitted latency, jitter, and probability fields become `0`; omitted `randomSeed` becomes `nil`. Latency and jitter are finite non-negative milliseconds. Loss, duplication, and reordering are finite probabilities from `0` through `1`. A seed is an int32 integer or `nil`. Unknown fields, invalid field types, out-of-range values, non-finite values, invalid seeds, missing player handles, disconnected or foreign handles, stopped servers, and in-process scenarios fail with script runtime exceptions. Reapplying a seed restarts that player's deterministic impairment sequence.

`set` affects packets queued afterward. Packets already queued keep their prior drop/duplicate/reorder decision and due time. Conditions are per player and apply to both directions at the scripted client wrapper; the unwrapped server transport means each direction is impaired exactly once.

Current assertion helpers:

* `scenario.assert.equal(expected, actual)` throws a script runtime exception when simple script values differ.
* `scenario.assert["true"](value)` and `scenario.assert.isTrue(value)` throw a script runtime exception unless `value` is the boolean `true`. The bracket spelling is required for the member named `true` because `true` is a Wattle keyword and cannot be parsed after `.` as a normal member.
* `scenario.assert.near(expected, actual, tolerance)` compares numeric values with an inclusive absolute tolerance and reports expected, actual, tolerance, and delta when it fails.
* `scenario.assert.state(name, predicate)` evaluates a script predicate immediately and throws with the supplied state name when the predicate does not return `true`.
* `scenario.assert.event(type)` throws unless a test-host event of the supplied type has already been recorded.
* `scenario.assert.eventually(maxTicks, predicate, description)` evaluates a predicate at the current tick, then advances through the active scenario runtime by up to `maxTicks` simulation ticks. It returns normally when the predicate becomes `true` and throws a script runtime exception with the supplied description after exactly `maxTicks` ticks when it never succeeds.

Snapshot wrappers expose read-only script data:

* Top-level snapshot fields: `serverTick`, `localPlayerId`, `acknowledgedInputSequence`, `connectedPlayerCount`, and `livingPlayerCount`.
* `snapshot.players` returns player snapshot wrappers sorted by player id.
* `snapshot.player(playerId)` returns the matching player snapshot wrapper or `nil`.
* `snapshot.match` exposes `phase`, `phaseStartedTick`, `livingPlayerCount`, and `winnerPlayerId`.
* `snapshot.safeZone` exposes `center`, `currentRadius`, `targetRadius`, and `lastUpdatedTick`.
* Player snapshots expose `playerId`, `position`, `velocity`, `look`, `currentHealth`, `maxHealth`, `health`, `alive`, and `weapon`.
* Vector wrappers expose `x`, `y`, and `z`. Look wrappers expose `yawRadians` and `pitchRadians`. Health wrappers expose `current` and `max`.
* Weapon wrappers expose `weaponId`, `ammoInMagazine`, `reserveAmmo`, `nextAllowedFireTick`, `lastFiredTick`, `isReloading`, and `reloadCompleteTick`.

Test-host events are deterministic harness observations only. They are not authoritative gameplay events, network replication messages, or a protocol compatibility contract. They exist to make Wattle scenario failures easier to diagnose.

Current `scenario.events` fields:

* `scenario.events.count` reports the number of recorded test-host events.
* `scenario.events.types` returns the recorded event types in recording order.
* `scenario.events.latest` returns the latest event wrapper or `nil` when no events are recorded.
* `scenario.events.clear()` clears the in-memory event history for the current host run.

Recorded test-host event types:

* `server.started`, `server.stopped`, and `server.stepped`
* `player.connected` and `player.disconnected`
* `player.input.accepted` and `player.input.rejected`
* `clock.wait.satisfied` and `clock.wait.timeout`
* `network.conditions.changed` with the affected player id and invariant ordered condition details

Event wrappers expose `type`, `tick`, optional `playerId`, and optional `detail`.

`scenario.players.input` command tables are flat and explicit. Required numeric fields are `sequence`, `clientTick`, `moveX`, `moveY`, `yawRadians`, and `pitchRadians`. Optional boolean fields default to `false`: `jump`, `fire`, `reload`, `interact`, and `crouch`.

Example:

```wattle
scenario.players.input(player, {
    sequence = 7,
    clientTick = 120,
    moveX = 0.0,
    moveY = 1.0,
    yawRadians = 0.25,
    pitchRadians = 0.0,
    fire = true
});
```

Malformed command tables fail with a script runtime exception, including missing required numeric fields, non-number required fields, non-boolean button fields, missing player handles, disconnected players, and stopped servers. Protocol-invalid but well-formed commands return `false` and are not acknowledged by later snapshots.

Clock waits and eventual assertions fail with script runtime exceptions for invalid tick counts, missing running servers, non-function predicates, non-boolean predicate results, and predicate exceptions. Wait helpers are deterministic server stepping utilities from the script perspective. UDP mode may use bounded internal wall-clock yields while stepping because real loopback packet delivery depends on OS socket polling.

Current boundaries:

* Scripts cannot directly mutate authoritative player state such as position, health, ammunition, match phase, safe-zone state, or winner.
* The API does not expose `moveTo`, `lookAt`, `shootAt`, replay file output, or external dedicated-server process launching.
* UDP mode is loopback-only inside the gameplay test process. It exercises real UDP packets, LiteNetLib transport lifecycle, protocol framing, handshake, input serialization, and snapshot delivery, but it does not launch a separate `Royale.Server` process.
* UDP mode adds test-host-controlled latency, jitter, loss, duplication, and reordering around scripted clients. It still does not add client-side prediction, reconciliation, or interpolation.
* Scripts do not receive `PlayerInputCommand`, `InputButtons`, server simulation objects, network transport objects, or authoritative player state directly.
* Test-host events are not authoritative gameplay event types.
* Artifact recording is in-memory metadata only; no files are written by `TEST-002`.
* API calls that require a running server or connected player fail explicitly when used after stop or disconnect.

High-level actions such as `moveTo`, `lookAt`, `pickUp`, `shootAt`, and `stayInsideZone` should be implemented through lower-level input commands rather than direct state mutation.

`scenario.server.forceStart()` invokes the explicit authoritative force-start hook in both in-process and loopback UDP runtimes. It returns the string `Started` after entering `Countdown`. Rejection is explicit: zero connected players throws `scenario.server.forceStart failed: at least one connected player is required`, and a phase other than `WaitingForPlayers` throws `scenario.server.forceStart failed: the match is not waiting for players`.

Every call records one deterministic test-host event. Success records `server.force_start.accepted` with detail `Started`; rejection records `server.force_start.rejected` with detail `NoPlayers` or `MatchNotWaiting` before throwing. These remain harness observations, not protocol or authoritative gameplay event types.

### Crouch commands and observations

The optional `crouch` field on `scenario.players.input` is a desired-state boolean. Scripts send `crouch = true` to request crouch and `crouch = false` to request standing; omission defaults to false for that command. Snapshot player wrappers expose `crouched` and `capsuleHeight`. Server debug player wrappers expose `crouched`, `stance`, and `capsuleHeight`. These remain observations and intent through the normal protocol boundary, not direct authoritative mutation.

### Bot Participant Observability

Wattle snapshot wrappers distinguish connected humans from the full authoritative roster. `connectedPlayerCount` counts snapshot players whose participant kind is human; `participantCount` counts all humans and bots; and `botPlayerCount` counts bots. Player snapshot and server debug wrappers expose `kind` as the stable `Human` or `Bot` name and `isBot` as a boolean. `scenario.observe` also exposes connected-human, participant, bot, and living-player counts from the active runtime.

The script API intentionally has no bot creation or removal methods. BOT-001 tests participant lifecycle through C# session APIs while WattleScript remains observational until later bot-controller and lobby tasks define script-facing behavior.

## Execution Model

Scenarios should advance using simulation ticks.

Bounded waits are expressed in ticks, not arbitrary wall-clock sleeps. `scenario.clock.waitTicks` is for exact deterministic advancement. `scenario.clock.waitUntil` is for bounded polling of script-observable state and evaluates the predicate once before stepping, then once after each tick advanced. This keeps in-process tests fast, reproducible, and independent of local machine speed.

`scenario.assert.eventually` follows the same tick-based polling model as `scenario.clock.waitUntil`, but it throws on timeout instead of returning `false`, making it suitable for assertions that need bounded eventual consistency diagnostics.

Loopback UDP mode keeps the same script-facing tick model. One shared manual `TimeProvider` advances by one 60 Hz fixed simulation step before each scenario tick, so configured latency and jitter are deterministic and do not sleep in wall-clock time. The harness still performs bounded internal socket polling and short wall-clock yields while advancing ticks because LiteNetLib and OS loopback delivery do not guarantee that packets sent immediately before a tight CPU-only tick loop will be visible to the receiver without giving the socket layer time to run.

The scenario model runs against:

* In-process transport
* Real loopback UDP transport
* Simulated adverse-network wrappers around real loopback UDP scripted clients

## File-Backed Scenarios

Reusable Wattle scenario files live under `tests/Royale.Gameplay.Tests/Scenarios/` and are copied to the gameplay test output with `CopyToOutputDirectory=PreserveNewest`.

`WattleScenarioRunner` discovers `.wattle` files from `Scenarios` under `AppContext.BaseDirectory` by default. Set `ROYALE_SCENARIO_DIR` to point at another directory when iterating on copied, edited, or temporary scenarios without changing source files or rebuilding the project.

File-backed scenarios execute through `WattleScenarioScriptHost` and the existing `ScenarioApi`. They preserve the command/snapshot boundary: scripts connect clients, enqueue protocol-shaped input commands, advance the authoritative server by bounded ticks, and assert against snapshots or test-host events. They must not call server simulation internals directly.

Initial in-process scenario coverage includes two connected clients observing both player snapshots, queued input commands being acknowledged by authoritative snapshots, player debug state inspection, and protocol-invalid input being rejected without acknowledging or moving the player.

Initial loopback UDP scenario coverage includes two scripted UDP clients observing each other through decoded snapshots and a movement input command being serialized, delivered, acknowledged, and reflected in authoritative snapshot state.

The current UDP scenarios are:

* `udp-two-clients-see-each-other.wattle`
* `udp-input-acknowledgement.wattle`
* `udp-adverse-network-controls.wattle`

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
