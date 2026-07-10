---
title: Bot Architecture
createdAt: 2026-07-10T05:21:34.8623220Z
modifiedAt: 2026-07-10T05:23:38.3777380Z
---

## Status

This page records the planned bot architecture owned by the `BOT` PM track. The behavior described here is not implemented until its corresponding BOT tasks are complete. Current match-start behavior remains defined by `BR-002`.

## Purpose

Bots fill otherwise empty match slots so a match can start without waiting indefinitely for human players. They are server-owned participants, not network clients, and they must obey the same authoritative movement, combat, damage, ammunition, pickup, safe-zone, elimination, winner, and reset rules as humans.

Bots generate ordinary tick-stamped player input commands. Bot logic must not directly mutate transforms, health, ammunition, pickups, match state, or winner state.

## Planned Lobby Flow

The planned lobby has four configurable population and timing values:

* Minimum human player count
* Target total participant count
* Waiting duration, defaulting to five minutes
* Preparation duration, defaulting to two minutes

The target must be greater than or equal to the minimum. The existing `Countdown` phase represents the preparation period; no sixth match phase is planned.

While `WaitingForPlayers`:

* Human connections occupy participant slots.
* Reaching the minimum human count enters `Countdown` immediately.
* If the five-minute waiting duration expires first, the server enters `Countdown` even with no human players.

When `Countdown` begins, the server creates enough bots to fill the roster to the target participant count. Humans may continue to connect during the two-minute preparation period. Each accepted human replaces one bot deterministically until no bots remain or the target is human-filled.

Once `Playing` begins, the roster is locked for that match. Bots are not replaced during active play. Late human connections wait for the next match or spectate once that behavior exists.

A bot-only match is valid when no humans connect before the waiting timeout.

## Input Delay Fairness

Bots should not receive a zero-latency reaction advantage merely because their controllers run inside the authoritative server.

For each newly generated bot input command, the server computes the arithmetic mean of the latest one-way latency samples for currently connected human peers. That duration is rounded up to whole 60 Hz simulation ticks, and the command is queued until its scheduled authoritative processing tick.

Bots and disconnected peers are excluded from the average. A bot-only match uses zero added input delay. Changes to the connected population or latency samples affect newly generated commands only; commands already queued retain their scheduled processing tick.

The sampled human average and effective bot delay must be observable and covered by deterministic tests.

## Bot Gameplay Scope

The full MVP bot path includes:

* Explicit bot participant identity
* Fixed-tick input generation
* Map navigation and recovery
* Perception and target selection
* Rifle combat
* Safe-zone response
* Loot pickup and reload behavior
* Elimination, winner, and reset integration
* Diagnostics and observability
* Deterministic WattleScript scenarios

The concrete navigation representation, perception ranges, target-selection policy, reaction timing, and accuracy tuning are intentionally undecided. Agents must ask the project owner before making those gameplay contracts.

## Lifecycle And Authority

Bot state is match-scoped and owned by the server. Bot decisions run on authoritative simulation ticks and consume server-visible observations only. Bots do not create fake UDP peers and do not bypass the normal input-processing or gameplay systems.

Resetting destroys bot controllers, goals, targets, navigation progress, and participant identities. The next waiting phase rebuilds its roster from current human connections and applies the fill policy again.

## PM Task Sequence

The BOT track is staged as follows:

1. Participant identity and authoritative integration
2. Normal player-input generation
3. Lobby filling and human replacement
4. Navigation data and movement
5. Perception and combat
6. Safe-zone and loot behavior
7. Match lifecycle integration
8. Diagnostics and deterministic scenario coverage
