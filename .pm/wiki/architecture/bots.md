---
title: Bot Architecture
createdAt: 2026-07-10T05:21:34.8623220Z
modifiedAt: 2026-07-11T07:00:49.3377750Z
---

## Status

`BOT-001` implements explicit server-owned bot participant identity. Human and bot participants share the authoritative player ID allocator, spawn selection and reservations, movement/combat state, health, weapons, match lifecycle storage, snapshots, and living-player accounting.

`BOT-002` implements the deterministic server-owned bot input boundary. Controllers submit `BotInputIntent` movement, yaw, pitch, and button intent through `InProcessServerSession.TrySubmitBotInput` or `NetworkServerRuntime.TrySubmitBotInput`. The session validates intent with `PlayerInputCommandValidation`, assigns a per-bot sequence beginning at `1`, and stamps the authoritative generation tick saturated to `uint.MaxValue`.

`BOT-014` schedules those commands in per-bot delayed FIFOs. Network runtimes derive the delay from current usable human one-way-latency samples; transport-independent callers default to zero. Each authoritative step consumes at most one due bot command in ascending player-ID order and combines it with at most one queued human command per client before calling `HeadlessServerSimulation.Step`. Bots therefore use the same movement, look, jump, combat, health, ammunition, and match-phase gates as humans.

Bots still have no connection ID, network peer, client connection, human input queue, or per-client snapshot queue. Removing a bot clears its delayed commands and sequencing state. Total and per-player queued-input diagnostics include the complete delayed backlog. Player snapshots expose nullable last-processed input sequence and decision-tick metadata for every participant.

The automatic BR-002 minimum-player threshold counts humans only. The explicit development/test `ForceStart()` hook accepts any non-empty participant roster, including bot-only matches. WattleScript does not expose bot creation or bot input controls. Autonomous decisions, navigation, combat policy, rendering differences, names, UI, and admin commands remain later BOT-track work.

`BOT-004` implements preparation-period participant takeover. During `Countdown`, each successful human admission converts the lowest-player-ID bot slot in place, preserving player ID, spawn reservation, transform, velocity, look, health, and weapon state while clearing delayed bot input, processed-sequence acknowledgement, and decision-tick metadata. If that human disconnects during `Countdown`, the same slot converts back to a bot with fresh sequencing beginning at `1`. During `WaitingForPlayers`, humans are admitted only below the configured target. A full-human `Countdown` and every phase from `Playing` through `Resetting` reject new connections; no replacement bot is introduced after `Playing` begins.

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

Bots do not receive a zero-latency reaction advantage merely because their controllers run inside the authoritative server.

`BOT-014` samples `INetworkTransportDiagnostics` whenever `NetworkServerRuntime.TrySubmitBotInput` generates a command. The sample includes only currently accepted, connected human peers with available non-negative one-way latency. It uses the arithmetic mean of those samples and rounds up to 60 Hz simulation ticks. A bot-only session, a transport without diagnostics, or a set with no usable samples produces zero added delay. `BotInputDelayDiagnostics` exposes the latest sampled-human count, average one-way latency in milliseconds, and effective delay ticks through the runtime.

`InProcessServerSession.TrySubmitBotInput` accepts an explicit non-negative delay in ticks and defaults it to zero for transport-independent callers. Each bot may add at most one validated command per authoritative decision tick. The command retains its generation tick as `ClientTick`, receives the normal per-bot sequence, and enters a per-bot FIFO with a fixed scheduled processing tick. Zero delay retains next-step processing. Already queued commands are never rescheduled when latency or peer membership changes.

Scheduled ticks are monotonic within each bot FIFO, so a newer command cannot pass an older command when sampled latency falls. Each authoritative simulation step visits bots in ascending player-ID order and consumes at most one due command per bot. Full delayed queue depth is included in aggregate and per-player diagnostics; bot removal, preparation-period human takeover, disconnect conversion, and session disposal clear or recreate the relevant queue and sequencing state.

OpenTelemetry exposes aggregate `royale.server.bots.input_delay.latency` and `royale.server.bots.input_delay.ticks` gauges without per-player labels. Structured diagnostics emit the same sampled-human count, latency, and delay-tick values only when the aggregate sample changes.

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

### Navigation Direction

`BOT-005` implements a small map-owned undirected waypoint graph for both production arenas. Waypoints use unique portable string IDs and standing-player feet positions; links name two waypoint IDs and are traversable in both directions.

`MapCatalog` requires non-empty navigation data, finite in-bounds waypoint positions, valid unique IDs, valid non-self undirected links without reversed duplicates, one connected component, and a waypoint within 2 metres in 3D of every spawn and loot point.

`MapNavigationGraph` lives in shared simulation, indexes waypoints by ordinal ID, exposes ordinal waypoint and neighbor ordering, and resolves nearest waypoints deterministically with ordinal ID tie-breaking. Server simulation constructs and retains this graph beside the authoritative static collision world.

Runtime construction validates that every waypoint supports a clear grounded standing capsule and walks every link in both directions through the shared kinematic character controller at ordinary standing walk speed. Arrival tolerances are 0.35 metres horizontally and vertically; the deterministic budget is link travel time plus two seconds. Graybox explicitly covers its repaired step/ramp/platform chain, and prototype-arena covers its corrected stairs/platform route.

`BOT-005` does not add pathfinding, decisions, movement generation, recovery behavior, rendering, protocol fields, or bot transform mutation. Navmesh generation and runtime queries remain deferred to `DEBT-007`.

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
