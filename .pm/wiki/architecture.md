---
title: Architecture
createdAt: 2026-07-05T07:34:45.0706070Z
modifiedAt: 2026-07-05T07:34:45.0706070Z
---

## Overview

The project is structured around a dedicated authoritative server, one or more native game clients, and a set of shared libraries containing protocol definitions, simulation concepts, content definitions, and native bindings.

The architecture is intentionally small.

It should support the first complete multiplayer match without prematurely becoming a general-purpose game engine.

The major runtime processes are:

* The game client
* The dedicated server

The client is responsible for presentation and responsive local interaction.

The server is responsible for authoritative simulation and game rules.

Both processes share data structures and selected deterministic gameplay logic, but they do not share authority.

## Architectural Goals

The architecture should make it possible to:

* Run the server without creating a window or graphics device
* Run clients on macOS, Linux, and eventually Windows
* Keep rendering code out of the server
* Keep client presentation code out of the authoritative simulation
* Test simulation logic without launching the full game
* Run a client and server in the same process during development
* Replace the in-process transport with real networking without changing game logic
* Observe and debug simulation, physics, and networking state
* Package native dependencies consistently across platforms

The architecture should not optimize for arbitrary games, editor extensibility, plugins, or scripting.

## Solution Structure

A possible solution layout is:

```text
src/
  Game.Client/
  Game.Server/
  Game.Simulation/
  Game.Protocol/
  Game.Content/
  Game.Platform/
  Game.Rendering/
  Game.Debugging/
  Box3D.Bindings/
  Box3D/

tests/
  Game.Simulation.Tests/
  Game.Protocol.Tests/
  Game.Server.Tests/
  Box3D.Tests/

native/
  box3d/

assets/
  shaders/
  meshes/
  textures/
  maps/
```

The exact number of projects may change as the implementation develops.

Projects should be split when there is a meaningful dependency or deployment boundary, not merely to create a theoretically clean hierarchy.

## Dependency Direction

The most important dependency rule is that low-level and shared projects must not depend on client presentation code.

A simplified dependency graph is:

```text
Game.Client
  ├── Game.Simulation
  ├── Game.Protocol
  ├── Game.Content
  ├── Game.Platform
  ├── Game.Rendering
  ├── Game.Debugging
  └── Box3D

Game.Server
  ├── Game.Simulation
  ├── Game.Protocol
  ├── Game.Content
  └── Box3D

Game.Simulation
  ├── Game.Content
  └── Box3D

Box3D
  └── Box3D.Bindings
```

The dedicated server must not reference:

* SDL windowing
* SDL GPU
* ImGui
* Client UI
* Client-specific input handling
* Rendering assets that are not required for collision or simulation

## Runtime Processes

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

A server process may initially host one match.

Support for multiple matches within one process can be considered later, after the single-match lifecycle is stable.

## Simulation Model

The simulation runs at a fixed tick rate.

The initial target is:

```text
Simulation rate: 60 Hz
Server snapshot rate: 20 Hz
```

The simulation rate and snapshot rate are separate.

The server advances the game 60 times per second but does not need to send the entire state after every simulation tick.

Each simulation tick performs work in an explicit order.

A possible order is:

1. Receive and queue network messages.
2. Select the input command for each player.
3. Update match state.
4. Simulate player movement.
5. Step physics.
6. Resolve weapon actions.
7. Apply damage and eliminations.
8. Update the safe zone.
9. Record authoritative state.
10. Produce snapshots or events when required.

The exact ordering matters because it affects gameplay behavior and prediction.

Once established, it should be documented and tested.

## Fixed-Timestep Loop

The server uses a straightforward fixed-timestep loop.

Conceptually:

```text
while running:
    accumulate elapsed time

    while accumulated time >= fixed step:
        process one simulation tick
        accumulated time -= fixed step

    sleep or yield until more work is available
```

The client also maintains a fixed simulation loop for prediction.

Rendering remains variable-rate.

The client frame performs:

1. Poll platform events.
2. Collect input.
3. Run zero or more fixed prediction ticks.
4. Receive snapshots.
5. Reconcile the local player.
6. Interpolate remote entities.
7. Render the current presentation state.
8. Render debug tools.
9. Present the frame.

A maximum number of catch-up ticks should be enforced so that a paused debugger or stalled frame does not trigger an uncontrolled simulation spiral.

## Authoritative State

The server owns the canonical version of all gameplay-relevant state.

Examples include:

```text
Player
  Player ID
  Connection ID
  Position
  Rotation
  Velocity
  Health
  Alive state
  Current weapon
  Ammunition
  Fire cooldown
  Last processed input sequence

Match
  Match phase
  Phase start tick
  Countdown state
  Living-player count
  Winner
  Safe-zone centre
  Safe-zone radius
  Safe-zone target radius
```

The client should not send state such as:

```text
My position is X
I hit player Y
Player Y is dead
I picked up item Z
I won the match
```

Instead, the client sends intent:

```text
Move in this direction
Look by this amount
Jump
Fire
Reload
Interact
```

The server interprets that intent using authoritative state.

## Input Commands

Client input is represented as discrete commands associated with simulation ticks.

For example:

```csharp
public readonly record struct PlayerInputCommand(
    uint Sequence,
    uint ClientTick,
    Vector2 Move,
    Vector2 LookDelta,
    InputButtons Buttons);
```

Each command should include:

* A monotonically increasing sequence number
* The client simulation tick
* Movement input
* Look input or resulting view orientation
* Button states

Possible buttons include:

```csharp
[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Jump = 1 << 0,
    Fire = 1 << 1,
    Reload = 1 << 2,
    Interact = 1 << 3,
    Crouch = 1 << 4
}
```

The server records the most recent processed input sequence for each player.

That sequence is returned in snapshots so the client knows which predicted inputs have been acknowledged.

## Client-Side Prediction

Without prediction, local movement would only update after a complete client-server round trip.

The client therefore applies local input immediately.

For each fixed tick, the client:

1. Creates an input command.
2. Stores it in an unacknowledged input buffer.
3. Applies it to the local predicted player.
4. Sends it to the server.

The same movement rules should be used by both client and server wherever practical.

This does not require every part of the simulation to be perfectly deterministic.

The main requirement is that the movement implementation is sufficiently consistent for corrections to remain small under normal conditions.

## Server Reconciliation

Snapshots include the authoritative local-player state and the sequence number of the last processed input.

When a snapshot arrives, the client:

1. Replaces the predicted player state with the authoritative server state.
2. Removes acknowledged inputs from the local input buffer.
3. Replays all remaining unacknowledged inputs.
4. Updates the rendered presentation state.

Small corrections may be visually smoothed.

Large corrections should be applied immediately or over a very short interval so that visual smoothing does not hide serious simulation divergence.

Debug tooling should expose:

* Correction distance
* Correction frequency
* Number of replayed inputs
* Oldest unacknowledged input
* Client tick difference
* Server tick difference

## Remote-Player Interpolation

Remote players are not predicted from local input.

The client stores a short history of received snapshots and renders remote players slightly behind the latest server state.

For example:

```text
Server snapshot rate: 20 Hz
Interpolation delay: approximately 100 ms
```

The client selects two snapshots surrounding the render timestamp and interpolates between them.

Position may initially use linear interpolation.

Rotation may use quaternion interpolation.

When snapshots are missing, the client may briefly extrapolate using the last known velocity, but long extrapolation should be avoided.

## Physics Architecture

Box3D is used on both client and server.

The server physics world is authoritative.

The client may also maintain a physics world for:

* Local movement prediction
* Camera interaction
* Collision visualization
* Static map queries
* Non-authoritative presentation effects

The initial implementation should avoid networked dynamic rigid bodies.

The first synchronized gameplay objects should be:

* Players
* Static map collision
* Weapon pickups
* Safe-zone state

This keeps state ownership and replication straightforward.

## Player Controller

The player is represented as a kinematic capsule rather than a freely simulated dynamic rigid body.

Movement is controlled explicitly using shape casts, overlap tests, and position correction.

The controller is responsible for:

* Horizontal movement
* Gravity
* Jumping
* Ground detection
* Slope handling
* Wall sliding
* Step handling
* Ceiling collision
* Penetration recovery

A conceptual update may be:

1. Read desired movement input.
2. Apply acceleration or target velocity.
3. Apply gravity.
4. Test ground state.
5. Attempt horizontal capsule movement.
6. Slide along blocking geometry.
7. Attempt step movement where appropriate.
8. Apply vertical movement.
9. Resolve remaining penetration.
10. Update grounded state and velocity.

The same controller logic should be used by the server and client prediction.

## Combat Flow

The first weapon is a server-authoritative hitscan rifle.

The client:

1. Detects fire input.
2. Predicts local visual feedback.
3. Sends the fire input as part of the current command.

The server:

1. Checks whether the player is alive.
2. Checks whether the weapon is equipped.
3. Checks fire cadence.
4. Checks ammunition.
5. Computes the authoritative shot direction.
6. Performs the raycast.
7. Applies damage to the closest valid hit.
8. Updates ammunition and cooldown.
9. Emits an authoritative combat event.

The client may immediately show:

* Muzzle flash
* Recoil
* Temporary tracer
* Firing animation

The client must wait for authoritative confirmation before treating another player as damaged or dead.

## Networking Layers

Networking should be divided into several conceptual layers.

### Transport

The transport moves packets between endpoints.

It should expose a small interface and hide the chosen UDP implementation from game code.

```csharp
public interface INetworkTransport : IDisposable
{
    void Send(NetworkEndpoint endpoint, ReadOnlySpan<byte> packet);
    void Poll(INetworkEventHandler handler);
}
```

The same higher-level connection code should work with:

* Real UDP transport
* In-process transport
* Test transport
* Simulated-loss transport

### Connection

The connection layer manages:

* Handshake
* Session identifiers
* Timeouts
* Packet sequencing
* Duplicate detection
* Acknowledgements
* Connection state
* Disconnect reasons

### Protocol

The protocol layer serializes and deserializes messages.

Initial message types may include:

```text
ClientHello
ServerAccept
ServerReject
ClientInput
ServerSnapshot
ServerEvent
ClientDisconnect
ServerDisconnect
```

### Replication

The replication layer converts authoritative simulation state into network snapshots and applies snapshots to client-side representations.

Early snapshots can be simple and redundant.

Optimization should come after protocol behavior is correct and observable.

## Protocol Versioning

Every connection handshake should include a protocol version.

A client and server with incompatible versions should fail clearly rather than attempting to continue.

For example:

```text
Protocol major version
Protocol minor version
Build identifier
Content or map version
```

The first implementation does not need sophisticated backwards compatibility.

It only needs explicit incompatibility detection.

## In-Process Development Mode

Before real networking is introduced, the client should be able to communicate with a server simulation through in-memory queues.

This mode should preserve the same conceptual boundaries as real networking:

```text
Client
  produces serialized or structured commands

In-process transport
  moves commands between queues

Server
  processes commands and produces snapshots

In-process transport
  returns snapshots to the client
```

The client should not call arbitrary server gameplay methods directly.

Keeping the communication boundary intact makes it easier to introduce UDP later without rewriting the simulation architecture.

An in-process mode also helps with:

* Integration tests
* Automated match tests
* Debugging prediction
* Running multiple simulated clients
* Reproducing packet sequences

## Content and Map Data

The first map should use a deliberately simple format.

A basic JSON file may define:

* Static boxes
* Static mesh references
* Spawn points
* Loot points
* World bounds
* Initial safe-zone centre
* Initial safe-zone radius

For example:

```json
{
  "name": "test-map",
  "spawnPoints": [
    { "position": [0, 2, 0], "rotation": 0 },
    { "position": [20, 2, 20], "rotation": 180 }
  ],
  "staticBoxes": [
    {
      "position": [0, -0.5, 0],
      "size": [100, 1, 100]
    }
  ],
  "lootPoints": []
}
```

Client and server should load the same gameplay-relevant map data.

Rendering-only data may remain client-specific.

The server should not need textures or shader assets.

## Rendering Architecture

The renderer should remain thin and specific.

Initial responsibilities include:

* SDL GPU device management
* Swapchain handling
* Shader loading
* Buffer creation
* Texture creation
* Static mesh rendering
* Camera constants
* Basic lighting
* Debug geometry
* ImGui rendering

### Shader Build Pipeline

Client shader sources live under `src/Royale.Client/Shaders/` as HLSL files using stage suffixes:

* `.vert.hlsl`
* `.frag.hlsl`
* `.comp.hlsl`

The client build requires `shadercross` to be available on `PATH`. After `Royale.Client` builds, MSBuild compiles each shader source to SPIR-V (`.spv`) and Metal (`.msl`) under the client output `shaders/` directory, preserving recursive shader folders. The original HLSL source is also copied to the same output tree for Direct3D/DXIL-facing development until a specific DXIL output flow is chosen.

`SDL_shadercross` is not vendored through `thirdparty`; it is treated as an external local build tool dependency.

A simple render sequence is sufficient:

1. Acquire the swapchain texture.
2. Begin the main render pass.
3. Draw static geometry.
4. Draw players and pickups.
5. Draw debug geometry.
6. End the main pass.
7. Render ImGui.
8. Submit the command buffer.

There is no initial requirement for:

* Deferred rendering
* Render graphs
* Material graphs
* Dynamic global illumination
* Shadow systems
* GPU-driven culling
* Streaming terrain

## Presentation State

The client should distinguish authoritative simulation state from rendered presentation state.

For example, a remote player may have:

```text
Latest authoritative snapshot state
Previous snapshot state
Interpolated render transform
Visual animation state
Temporary effects
```

Likewise, the local player may have:

```text
Authoritative server state
Predicted simulation state
Smoothed render state
Camera state
Weapon visual state
```

This distinction prevents rendering concerns from contaminating gameplay authority.

## Match State Machine

The battle-royale lifecycle is controlled by a server-side state machine.

```text
WaitingForPlayers
    ↓
Countdown
    ↓
Playing
    ↓
Finished
    ↓
Resetting
    ↓
WaitingForPlayers
```

### WaitingForPlayers

* Accept players
* Spawn or prepare them in a non-active state
* Wait for the minimum player count
* Allow a development force-start command

### Countdown

* Lock the participant list if required
* Select spawn points
* Reset player state
* Begin a short countdown

### Playing

* Enable movement and combat
* Update the safe zone
* Apply zone damage
* Track living players
* Detect the winner

### Finished

* Stop combat
* Announce the winner
* Allow spectating
* Wait briefly before reset

### Resetting

* Destroy match-scoped entities
* Clear temporary state
* Reset the physics world or restore map state
* Prepare the next match

Match transitions should be driven by server ticks rather than wall-clock timers wherever practical.

## State Ownership

A simple ownership rule should apply:

| State                          | Owner                 |
| ------------------------------ | --------------------- |
| Window size                    | Client                |
| Input device state             | Client                |
| Camera smoothing               | Client                |
| Visual effects                 | Client                |
| Predicted local transform      | Client, temporary     |
| Authoritative player transform | Server                |
| Health                         | Server                |
| Ammunition                     | Server                |
| Weapon cooldown                | Server                |
| Pickup availability            | Server                |
| Safe-zone state                | Server                |
| Match phase                    | Server                |
| Winner                         | Server                |
| Rendering resources            | Client                |
| Physics bindings               | Shared infrastructure |
| Static map gameplay data       | Shared content        |

Whenever ownership is unclear, gameplay-relevant state should default to the server.

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

### Client diagnostics

* FPS
* Render time
* Fixed ticks per frame
* Local client tick
* Latest server tick
* Ping
* Jitter
* Packet loss
* Snapshot buffer size
* Prediction correction distance
* Unacknowledged input count
* Player position and velocity
* Grounded state
* Weapon state

### Server diagnostics

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

### Debug visualization

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

### Unit tests

Useful for:

* Protocol serialization
* Input buffering
* Sequence comparisons
* Match-state transitions
* Safe-zone interpolation
* Damage calculations
* Fire cadence
* Box3D structure layouts

### Simulation tests

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

### In-process integration tests

Run clients and server using in-memory transport.

Examples:

* Two clients connect
* Commands reach the server
* Snapshots return to both clients
* One player damages another
* A complete match reaches the finished state
* Twenty matches run consecutively

### Network tests

Run with simulated:

* Latency
* Packet loss
* Jitter
* Reordering
* Duplicate packets
* Delayed snapshots

### Cross-platform tests

Verify that:

* macOS and Linux clients connect to the same server
* Serialized data is interpreted identically
* Native library packaging works
* Shader assets load correctly
* File paths behave consistently
* Protocol tests pass on all supported platforms

## Deployment Shape

The initial deployment consists of separate client and server artifacts.

### macOS client

Contains:

* .NET application
* SDL native library
* Box3D native library
* Compiled shaders
* Meshes
* Textures
* Maps
* Configuration

### Linux client

Contains the equivalent Linux runtime and native libraries.

### Linux server

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

## Summary

The project architecture is based on a strict separation between authority and presentation.

The dedicated server owns the game.

The client owns interaction, prediction, and rendering.

Shared code exists to keep rules and data structures consistent, but it does not weaken server authority.

The initial architecture should remain small enough to understand in full while supporting the complete path from input to prediction, networking, authoritative simulation, reconciliation, rendering, elimination, and match reset.
