---
title: Runtime Processes
createdAt: 2026-07-05T16:10:17.2894450Z
modifiedAt: 2026-07-05T20:00:39.1541700Z
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

`--offline` selects local offline startup. `--connect <host>` selects an intended remote server host and uses `--port <port>` for the endpoint port. The modes are mutually exclusive, so `--offline --connect <host>` is invalid. Until networking transport work lands, connect mode only captures and logs the intended remote endpoint; it does not open a network connection.

Arguments are intentionally strict: unknown flags, missing values, empty values, invalid ports outside `1..65535`, and screenshot frame counts less than `1` are startup errors. `--screenshot-after-frames` requires `--screenshot`; `--screenshot` without an explicit frame count captures after frame `1`.

Map selection is syntactic only at this stage. The launch parser records a non-empty map id, but registry lookup and content loading validation belong to later content and map tasks.

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

The server defaults to port `7777` and map `graybox` (`ContentCatalog.DefaultMapId`).

Supported server flags are:

```text
--port <port>
--map <map-id>
```

The startup log includes the selected protocol version, map id, port, simulation tick rate, and headless status. Server argument parsing rejects unknown flags, missing or empty values, and invalid ports outside `1..65535`.

As with the client, server map selection is syntactic only until map registry and loading behavior are implemented.