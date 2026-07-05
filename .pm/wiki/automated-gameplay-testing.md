---
title: Automated Gameplay Testing
createdAt: 2026-07-05T15:18:15.6422560Z
modifiedAt: 2026-07-05T15:18:15.6422560Z
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

## Scenario API

The script-visible API should be narrow and sandboxed.

Initial API areas may include:

* Server lifecycle
* Scripted player lifecycle
* Input commands
* Tick advancement
* Network condition controls
* Snapshot observations
* Event observations
* Assertions
* Replay and artifact capture

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