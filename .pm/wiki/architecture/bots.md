---
title: Bot Architecture
createdAt: 2026-07-10T05:21:34.8623220Z
modifiedAt: 2026-07-10T08:11:16.3141320Z
---

## Status

`BOT-001` implements explicit server-owned bot participant identity. Human and bot participants share the authoritative player ID allocator, spawn selection and reservations, movement/combat state, health, weapons, match lifecycle storage, snapshots, and living-player accounting.

`BOT-002` implements the deterministic server-owned bot input boundary. A future controller submits `BotInputIntent` movement, yaw, pitch, and button intent through `InProcessServerSession.TrySubmitBotInput` or `NetworkServerRuntime.TrySubmitBotInput`. The session validates intent with `PlayerInputCommandValidation`, assigns a per-bot sequence beginning at `1`, stamps the current authoritative decision tick saturated to `uint.MaxValue`, and retains at most one pending command for the next step.

Each authoritative step consumes bot pending commands in ascending player-ID order and combines them with at most one queued human command per client before calling `HeadlessServerSimulation.Step`. Bots therefore use the same movement, look, jump, combat, health, ammunition, and match-phase gates as humans. Missing bot input produces no command, which means neutral movement and released buttons while the existing look orientation remains unchanged.

Bots still have no connection ID, network peer, client connection, human input queue, or per-client snapshot queue. Removing a bot clears its pending command and sequencing state. Total and per-player queued-input diagnostics include pending bot commands. Player snapshots expose nullable last-processed input sequence and client/decision tick metadata for every participant, allowing a human recipient to inspect bot processing alongside authoritative transform and combat state.

The automatic BR-002 minimum-player threshold counts humans only. The explicit development/test `ForceStart()` hook accepts any non-empty participant roster, including bot-only matches. WattleScript does not expose bot creation or bot input controls. Lobby filling, autonomous decisions, navigation, combat policy, delayed input scheduling, rendering differences, names, UI, and admin commands remain later BOT-track work.

`BOT-004` implements preparation-period participant takeover. During `Countdown`, each successful human admission converts the lowest-player-ID bot slot in place, preserving player ID, spawn reservation, transform, velocity, look, health, and weapon state while clearing bot input, processed-sequence acknowledgement, and decision-tick metadata. The accepted client receives that preserved player ID and snapshots immediately identify the slot as human.

If that human disconnects during `Countdown`, the same slot converts back to a bot with no connection and fresh bot input sequencing beginning at `1`. During `WaitingForPlayers`, humans are admitted only below the configured target. A full-human `Countdown` and every phase from `Playing` through `Resetting` reject new connections; no replacement bot is introduced after `Playing` begins.

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

`BOT-002` intentionally processes one validated pending bot command on the next authoritative step without added delay. It establishes the sequence and decision-tick metadata that delayed scheduling will build on; it does not sample latency or create fake network peers.

`BOT-014` will replace this immediate pending slot with scheduled delayed processing. For each newly generated bot input command, the planned scheduler computes the arithmetic mean of the latest one-way latency samples for currently connected human peers, rounds that duration up to whole 60 Hz simulation ticks, and retains the command until its scheduled authoritative processing tick. Bots and disconnected peers are excluded, and a bot-only match uses zero added delay. Changes in population or samples affect newly generated commands only.

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
