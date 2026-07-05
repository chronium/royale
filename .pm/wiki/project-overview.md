---
title: Project Overview
createdAt: 2026-07-04T16:54:42.2894730Z
modifiedAt: 2026-07-04T16:54:42.2894730Z
---

## Introduction

This project is an experimental cross-platform multiplayer battle royale built from the ground up in .NET.

The immediate goal is not to reproduce the scale or feature set of a commercial game such as PUBG. Instead, the project is intended to prove that a small custom technology stack can support the complete lifecycle of a server-authoritative multiplayer match across macOS, Linux, and potentially Windows.

The first meaningful version will be intentionally small:

* A dedicated Linux server
* macOS and Linux clients
* A small gray-box map
* A handful of players
* First-person movement
* One weapon
* Server-authoritative combat
* A shrinking safe zone
* Elimination, spectating, and a winner
* Automatic match reset

Once that complete loop works reliably, the project can grow in scale and complexity.

## Project Goals

The project has several primary technical goals.

### Cross-platform support

The client should run natively on:

* macOS
* Linux
* Windows, as a later milestone

The dedicated server should run headlessly on Linux without requiring a graphics environment.

All supported clients should connect to the same server implementation and use the same network protocol.

### Server-authoritative multiplayer

The server owns the canonical game state, including:

* Player movement
* Health
* Damage
* Weapon fire cadence
* Ammunition
* Death and elimination
* Safe-zone state
* Match phases
* Winner determination

Clients send input commands rather than directly changing authoritative state.

Local movement prediction and server reconciliation will be used to keep movement responsive, while remote players will be rendered using buffered snapshot interpolation.

### A focused custom engine

The project uses a small custom engine designed specifically for this game.

It is not intended to become a general-purpose engine or compete with Unity, Unreal Engine, or Godot.

New abstractions should be introduced only when the game requires them. The project should avoid speculative systems such as:

* A general-purpose scene editor
* A universal entity-component framework
* A material graph
* A render graph
* A scripting language
* A plugin ecosystem

The game is the product. The engine exists only to support it.

### Incremental vertical slices

Development is organized around milestones that produce demonstrable outcomes.

Examples include:

* Opening a window and rendering a cube
* Running Box3D through C# bindings
* Moving a first-person character through a collision map
* Completing an offline combat encounter
* Running an authoritative local simulation
* Connecting macOS and Linux clients to a Linux server
* Completing an entire battle-royale match

Subsystem tasks may belong to different tracks, but milestones combine those tasks into complete vertical slices.

## Technology Stack

### .NET

The game client, dedicated server, simulation, networking, tooling, and managed bindings are written in C# on .NET 10.

Shared game logic should remain independent of presentation and platform-specific code wherever practical.

### SDL3

SDL3 provides the platform layer, including:

* Window creation
* Input
* Event handling
* High-DPI support
* Relative mouse input
* Native platform integration

### SDL GPU

SDL GPU provides the rendering abstraction.

The renderer will initially support only what the game requires:

* Static meshes
* Depth testing
* Camera matrices
* Basic lighting
* Debug geometry
* ImGui rendering

The project will use precompiled shaders suitable for the graphics backends used on macOS, Linux, and Windows.

### Box3D

Box3D provides 3D collision detection, rigid-body simulation, and spatial queries.

Because the library exposes a C API, the project will include its own focused C# bindings.

The bindings will initially cover only the features required by the game:

* World lifecycle
* Bodies
* Shapes
* Transforms
* Raycasts
* Shape casts
* Overlap queries
* Collision filters

The player controller will be implemented as a kinematic capsule rather than as a freely simulated rigid body.

### ImGui

ImGui is used for development tooling and diagnostics.

Planned tools include:

* Frame and simulation statistics
* Network statistics
* Collision visualization
* Player state inspection
* Latency and packet-loss simulation
* Match controls
* An in-game development console

ImGui is not intended to define the final player-facing interface.

## High-Level Architecture

The solution is divided into several major components.

### Client

The client is responsible for:

* Window and input handling
* Rendering
* Audio and visual feedback
* Local movement prediction
* Snapshot interpolation
* Server reconciliation
* Player-facing UI
* Development tools

The client does not decide authoritative combat or match results.

### Server

The dedicated server is responsible for:

* Running the authoritative fixed-timestep simulation
* Processing player inputs
* Validating movement and weapon use
* Applying damage
* Managing players and connections
* Controlling match phases
* Updating the safe zone
* Determining eliminations and the winner
* Publishing snapshots to clients

The server runs without SDL window or GPU initialization.

### Shared simulation

Shared simulation code defines the common game concepts used by both the server and client.

This may include:

* Input command structures
* Character movement rules
* Weapon definitions
* Match state
* Player state
* Timing constants
* Prediction-compatible simulation logic

The server remains authoritative even where simulation code is shared.

### Network protocol

The protocol defines:

* Connection handshake
* Protocol versioning
* Input command packets
* Snapshot packets
* Sequencing
* Acknowledgements
* Connection and player identifiers

Early versions will favor simplicity over bandwidth efficiency.

Full snapshots and straightforward serialization are acceptable until profiling demonstrates a need for delta compression or more sophisticated encoding.

## Initial Gameplay Scope

The first complete match should support:

1. Players connect to a dedicated server.
2. The server waits for the minimum number of players.
3. A countdown begins.
4. Players spawn at separate locations.
5. Players move through a small gray-box map.
6. Players find or receive a single rifle.
7. Combat is resolved by the server.
8. The safe zone shrinks over time.
9. Players outside the safe zone take damage.
10. Dead players spectate.
11. The final surviving player wins.
12. The server resets and begins another match.

The initial target is a short match lasting approximately three to five minutes.

## Non-Goals for the First Version

The following features are deliberately outside the initial scope:

* One hundred players
* Large streamed terrain
* Vehicles
* Parachuting
* Complex weapon ballistics
* Multiple weapon classes
* Armour and attachments
* Inventory grids
* Character animation
* Cosmetic progression
* User accounts
* Matchmaking
* Persistent statistics
* Voice chat
* Anti-cheat systems
* Global server infrastructure
* Advanced lag compensation
* Destructible environments

These may be explored later, but none are required to prove the core architecture.

## Development Principles

### Build the smallest complete system

A complete two-player match is more valuable than an incomplete foundation intended for one hundred players.

### Keep the server authoritative

Client-side convenience must not become client-side authority.

### Prefer explicit code over premature frameworks

A small amount of duplication is acceptable when the alternative is introducing an abstraction before its real requirements are understood.

### Make systems observable

Rendering, physics, simulation, and networking should expose enough debug information to understand failures without relying entirely on a debugger.

### Profile before optimizing

The first implementation should be structurally correct and easy to inspect.

Optimization work should be driven by measurements, particularly for:

* Simulation time
* Physics queries
* Snapshot size
* Packet frequency
* Memory allocation
* Rendering submissions
* Prediction corrections

### Keep milestones demonstrable

Every milestone should end with something visible, playable, testable, or deployable.

## Definition of the MVP

The MVP is considered successful when:

* A Linux dedicated server can be started independently.
* A macOS client can join it.
* A Linux or Windows client can join the same match.
* Players can move and see one another correctly.
* Local movement remains responsive under realistic latency.
* The server resolves firing, damage, death, and elimination.
* The safe zone shrinks and applies damage.
* One player is declared the winner.
* The match resets without restarting the server.
* Multiple matches can run consecutively without corrupting state or leaking significant resources.

At that point, the project will have proven its central premise:

> A small, custom .NET stack can support a complete cross-platform, server-authoritative battle-royale game loop.