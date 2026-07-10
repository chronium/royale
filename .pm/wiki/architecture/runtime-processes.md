---
title: Runtime Processes
createdAt: 2026-07-05T16:10:17.2894450Z
modifiedAt: 2026-07-10T07:47:22.4275030Z
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

The client defaults to offline mode, port `7777`, map `graybox` (`ContentCatalog.DefaultMapId`), and gameplay camera mode.

Supported client flags are:

```text
--config <path>
--offline
--connect <host>
--port <port>
--map <map-id>
--camera-mode <gameplay|freecam>
--camera-position <x,y,z>
--camera-look-at <x,y,z>
--screenshot <path>
--screenshot-after-frames <frame-count>
```

`--config` explicitly selects one JSON profile. It may appear anywhere exactly once, and relative paths resolve against the current working directory. Client configuration properties are `mode`, `connectHost`, `port`, `mapId`, `cameraMode`, `cameraPosition`, `cameraLookAt`, `screenshotPath`, and `screenshotAfterFrames`. Properties are optional and merge in this fixed order: built-in defaults, selected profile, explicit CLI arguments. There is no implicit profile discovery, environment detection, inheritance, environment-variable expansion, or secrets handling.

The committed `config/client.production.json` profile selects offline gameplay on port `7777` with `graybox`. `config/client.development.json` selects connect gameplay to `127.0.0.1:7777` with `graybox`. Only client profiles are copied into client build and publish output.

`--offline` overrides the profile mode and clears a configured host. `--connect <host>` overrides the mode and configured host. Explicitly supplying both CLI mode flags remains invalid. After all merging, connect mode requires a host and offline mode forbids one.

In offline mode the SDL client creates `LocalPlayerController` and runs local offline movement/weapon feedback. In connect mode the SDL client creates `NetworkClientRuntime`, starts a UDP transport on an ephemeral local port, connects to the remote endpoint, completes the handshake after the transport reports the peer as connected, sends fixed-tick `ClientInput` commands, receives `ServerSnapshot` packets, and renders snapshot players as debug capsules. Connect mode does not run offline local physics for authority.

JSON parsing uses `System.Text.Json`, allows comments and trailing commas, and rejects malformed documents, unknown or incorrectly cased fields, missing files, and invalid values. Existing cross-field validation runs after merging: ports must be `1..65535`; freecam vectors require freecam mode and distinct position/look-at values; screenshot frame counts require a screenshot path; and a screenshot path without a count captures after frame `1`.

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
--config <path>
--port <port>
--map <map-id>
--run-ticks <positive-integer>
--minimum-players <count>
--target-players <count>
--waiting-seconds <positive-integer>
--preparation-seconds <positive-integer>
```

`--config` explicitly selects one JSON profile. It may appear anywhere exactly once, and relative paths resolve against the current working directory. Server configuration properties are `port`, `mapId`, `runTicks`, `minimumPlayers`, `targetPlayers`, `waitingSeconds`, and `preparationSeconds`. Properties are optional and merge in this fixed order: built-in defaults, selected profile, explicit CLI arguments. There is no implicit profile discovery, environment detection, inheritance, environment-variable expansion, or secrets handling.

The committed `config/server.production.json` profile uses the built-in production values. `config/server.development.json` keeps the same endpoint and roster but reduces waiting and preparation to five seconds each. Only server profiles are copied into server build and publish output.

JSON parsing uses `System.Text.Json`, allows comments and trailing commas, and rejects malformed documents, unknown or incorrectly cased fields, missing files, invalid ports or run tick counts, invalid player counts, a minimum above the target, and duration overflow. Existing validation runs after all profile and CLI merging.

While `WaitingForPlayers` is active, reaching the human minimum or exhausting the waiting duration starts preparation. The authoritative server immediately fills remaining target slots with bot participants and uses the existing `Countdown` phase for the configured preparation duration. A successful remote `ForceStart` follows the same fill-and-prepare path. Human minimum and target counts are inclusive `1..ProtocolConstants.MaxSnapshotPlayers`, and the minimum cannot exceed the target. Durations must be positive whole seconds and must fit when converted to fixed simulation ticks.

Bots never satisfy the automatic human minimum. The waiting timeout may start a bot-only roster. Disconnects during preparation do not cancel or pause it. Automatic fill reason and bot totals are emitted through structured logs, `royale.server.players.bots`, and startup activity tags.

By default the server runs until Ctrl+C or process shutdown. `--run-ticks` is a deterministic validation option: when present, the server runs exactly that many fixed simulation ticks as fast as possible, then exits.

At startup the server creates `NetworkServerRuntime`, starts `LiteNetLibNetworkTransport` on the selected UDP port, loads the merged map and `MatchStartSettings` into the authoritative in-process session, and logs the selected protocol version, port, map id, lobby counts and durations, simulation tick rate, headless status, finite or infinite run mode, and that UDP listen is enabled.

Each fixed server tick polls the UDP transport, accepts handshakes, queues validated human and bot input for accepted participants, steps the authoritative session once, and sends due recipient snapshots at the 20 Hz snapshot cadence. Disconnects remove the peer mapping and authoritative human player. There is no CLI force-start flag; remote authenticated force-start belongs to `OBS-006`.