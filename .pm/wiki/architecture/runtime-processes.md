---
title: Runtime Processes
createdAt: 2026-07-05T16:10:17.2894450Z
modifiedAt: 2026-07-10T07:36:42.4879820Z
---

## Game Client

The client is responsible for:

* Creating the native window
* Polling SDL events
* Reading keyboard and mouse input
* Rendering the world
* Rendering player-facing UI
* Rendering development tools
* Sending input commands to the server
* Predicting local movement
* Replaying unacknowledged inputs after reconciliation
* Buffering and interpolating remote snapshots
* Displaying effects based on authoritative events

The client may predict outcomes for responsiveness, but predicted state is always temporary.

The server can correct:

* Player position
* Velocity
* Health
* Ammunition
* Weapon state
* Alive state
* Match state

The client should be built around the assumption that corrections will happen.

### Client Launch Arguments

The client defaults to offline mode, port `7777`, and map `graybox` (`ContentCatalog.DefaultMapId`).

Supported client flags are:

```text
--offline
--connect <host>
--port <port>
--map <map-id>
--screenshot <path>
--screenshot-after-frames <frame-count>
```

`--offline` selects local offline startup. `--connect <host>` selects UDP connect mode and uses `--port <port>` for the remote endpoint port. The modes are mutually exclusive, so `--offline --connect <host>` is invalid.

In offline mode the SDL client creates `LocalPlayerController` and runs local offline movement/weapon feedback. In connect mode the SDL client creates `NetworkClientRuntime`, starts a UDP transport on an ephemeral local port, connects to the remote endpoint, completes the handshake after the transport reports the peer as connected, sends fixed-tick `ClientInput` commands, receives `ServerSnapshot` packets, and renders snapshot players as debug capsules. Connect mode does not run offline local physics for authority.

Arguments are intentionally strict: unknown flags, missing values, empty values, invalid ports outside `1..65535`, and screenshot frame counts less than `1` are startup errors. `--screenshot-after-frames` requires `--screenshot`; `--screenshot` without an explicit frame count captures after frame `1`.

Map selection loads through the local content catalog so rendering and debug map markers match the selected server map. Protocol/content compatibility is still enforced by the network handshake fields.

## Dedicated Server

The server is responsible for:

* Accepting client connections
* Assigning connection and player identifiers
* Running the fixed-timestep simulation
* Processing input commands
* Validating player actions
* Updating physics
* Resolving weapon fire
* Applying damage
* Managing health and death
* Managing the safe zone
* Managing match phases
* Determining the winner
* Sending authoritative snapshots
* Resetting the match

The server should run from a terminal or container without requiring a graphical environment.

A server process may initially host one match. Support for multiple matches within one process can be considered later, after the single-match lifecycle is stable.

### Server Launch Arguments

The server defaults to port `7777`, map `graybox` (`ContentCatalog.DefaultMapId`), a two-human match-start minimum, an eight-participant target, a five-minute wait, and a two-minute preparation period.

Supported server flags are:

```text
--port <port>
--map <map-id>
--run-ticks <positive-integer>
--minimum-players <count>
--target-players <count>
--waiting-seconds <positive-integer>
--preparation-seconds <positive-integer>
```

While `WaitingForPlayers` is active, reaching the human minimum or exhausting the waiting duration starts preparation. The authoritative server immediately fills remaining target slots with bot participants and uses the existing `Countdown` phase for the configured preparation duration. A successful remote `ForceStart` follows the same fill-and-prepare path. Human minimum and target counts are inclusive `1..ProtocolConstants.MaxSnapshotPlayers`, and the minimum cannot exceed the target. Durations must be positive whole seconds and must fit when converted to fixed simulation ticks.

Bots never satisfy the automatic human minimum. The waiting timeout may start a bot-only roster. Disconnects during preparation do not cancel or pause it. Automatic fill reason and bot totals are emitted through structured logs, `royale.server.players.bots`, and startup activity tags.

By default the server runs until Ctrl+C or process shutdown. `--run-ticks` is a deterministic validation option: when present, the server runs exactly that many fixed simulation ticks as fast as possible, then exits.

At startup the server creates `NetworkServerRuntime`, starts `LiteNetLibNetworkTransport` on the selected UDP port, loads the selected map and `MatchStartSettings` into the authoritative in-process session, and logs the selected protocol version, port, map id, lobby counts and durations, simulation tick rate, headless status, finite or infinite run mode, and that UDP listen is enabled.

Each fixed server tick polls the UDP transport, accepts handshakes, queues validated human and bot input for accepted participants, steps the authoritative session once, and sends due recipient snapshots at the 20 Hz snapshot cadence. Disconnects remove the peer mapping and authoritative human player.

Server argument parsing rejects unknown flags, missing or empty values, invalid ports outside `1..65535`, player counts outside `1..128`, a minimum above the target, duration overflow, and `--run-ticks` values that are not positive integers. There is no CLI force-start flag; remote authenticated force-start belongs to `OBS-006`.