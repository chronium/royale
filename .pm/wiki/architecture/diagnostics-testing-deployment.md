---
title: Diagnostics, Testing, and Deployment
createdAt: 2026-07-05T16:11:12.4857450Z
modifiedAt: 2026-07-10T02:12:51.9617070Z
---

## Threading Model

The first implementation should prefer a simple threading model.

The client may initially run:

* Platform events
* Fixed prediction
* Networking
* Rendering

on one main thread, with network polling performed non-blockingly.

The server may initially use:

* One simulation thread
* Non-blocking network polling
* Optional background logging or metrics

Multithreading should be introduced only where measurements show it is necessary.

Physics and simulation should not become concurrently mutable without a clear ownership model.

## Error Handling

Native and network boundaries should fail explicitly.

Examples include:

* SDL initialization failure
* GPU device creation failure
* Shader loading failure
* Box3D native library load failure
* Box3D ABI mismatch
* Protocol version mismatch
* Invalid packet
* Connection timeout
* Missing map
* Invalid spawn point
* Snapshot buffer underrun

Errors should include enough context to identify:

* Subsystem
* Operation
* Platform
* Connection
* Player
* Tick
* Relevant resource or file

## Diagnostics

The architecture should expose important runtime information through structured logs and ImGui tools.

## Client Diagnostics

The implemented SDL client telemetry surface is a resizable ImGui `Telemetry` window with collapsible Frame, Simulation, Player, Physics, Server, Network, and Connection sections. It is initially visible and `F3` toggles the complete diagnostic surface, including the separate Training Dummy window, without disabling ImGui frame/render integration.

The surface exposes frame time/FPS, fixed/client/server ticks, prediction queues and corrections, authoritative or offline player state, collision-world state, authoritative match/safe-zone state, transport latency/RTT/jitter/counters/loss/MTU, snapshot/input/error counters, remote interpolation diagnostics, and handshake/session/error state.

Offline mode has no Server or Network section. In connect mode, unavailable connection, acceptance, snapshot, latency, and transport-statistics sources are rendered as explicit waiting/unavailable states. Server gameplay values are read only from the latest accepted authoritative snapshot; client UI never reaches into server simulation state. LiteNetLib types remain confined to `Royale.Network`, and the client consumes only game-owned diagnostics records.

Training Dummy damage history/reset remains a separate specialized ImGui window. Offline kill/respawn and weapon-feedback controls are folded into the Player section. Historical charts, Grafana queries, runtime impairment controls, renderer timing, camera state, content counts, and screenshot diagnostics are deferred to their dedicated tasks and observability tooling.

## Server Diagnostics

* Current tick
* Simulation time
* Connected players
* Living players
* Packets received and sent
* Bytes received and sent
* Invalid packet count
* Input queue depth
* Last processed input per player
* Physics step time
* Match phase
* Safe-zone state

## Debug Visualization

* Player capsules
* Static colliders
* Ground checks
* Shape casts
* Hitscan rays
* Contact points
* Spawn points
* Loot points
* Safe-zone boundary
* Authoritative and predicted positions

## Testing Strategy

Testing should cover multiple layers.

## Unit Tests

Useful for:

* Protocol serialization
* Input buffering
* Sequence comparisons
* Match-state transitions
* Safe-zone interpolation
* Damage calculations
* Fire cadence
* Box3D structure layouts

## Simulation Tests

Run the simulation without rendering.

Examples:

* Player falls and lands
* Player cannot move through a wall
* Jump is rejected while airborne
* Fire cadence is enforced
* Dead players cannot fire
* Zone damage is applied
* Match ends when one player remains
* Match resets cleanly

## In-Process Integration Tests

Run clients and server using in-memory transport.

Examples:

* Two clients connect
* Commands reach the server
* Snapshots return to both clients
* One player damages another
* A complete match reaches the finished state
* Twenty matches run consecutively

## Network Tests

Run with simulated:

* Latency
* Packet loss
* Jitter
* Reordering
* Duplicate packets
* Delayed snapshots

## Cross-Platform Tests

Verify that:

* macOS and Linux clients connect to the same server
* Serialized data is interpreted identically
* Native library packaging works
* Shader assets load correctly
* File paths behave consistently
* Protocol tests pass on all supported platforms

## Deployment Shape

The initial deployment consists of separate client and server artifacts.

## macOS Client

Contains:

* .NET application
* SDL native library
* Box3D native library
* Compiled shaders
* Meshes
* Textures
* Maps
* Configuration

## Linux Client

Contains the equivalent Linux runtime and native libraries.

## Linux Server

Contains:

* .NET server application
* Box3D native library
* Gameplay-relevant map data
* Server configuration

It should not contain:

* SDL GPU dependencies
* Textures
* Client shaders
* ImGui
* Client UI assets

## Architectural Constraints

The following constraints should be preserved throughout the MVP:

1. The server must run without graphics initialization.
2. Client input represents intent, not authoritative state.
3. The server owns combat and match results.
4. Shared simulation code must not depend on rendering.
5. Real and in-process transports must use the same message flow.
6. The first implementation should remain inspectable and debuggable.
7. Performance abstractions should follow profiling, not precede it.
8. New engine systems require a concrete gameplay need.
9. Cross-platform behavior should be tested continuously.
10. Every milestone should end in a demonstrable working state.

## Initial Data Flow

A typical local-player update flows through the system as follows:

```text
Keyboard and mouse
        ↓
Client input collection
        ↓
PlayerInputCommand
        ↓
Local prediction
        ↓
Network transport
        ↓
Server input queue
        ↓
Authoritative simulation
        ↓
Server snapshot
        ↓
Network transport
        ↓
Client reconciliation
        ↓
Smoothed render state
        ↓
SDL GPU renderer
```

A remote player follows a simpler client-side path:

```text
Authoritative server simulation
        ↓
Server snapshots
        ↓
Client snapshot buffer
        ↓
Interpolation
        ↓
Rendered remote player
```