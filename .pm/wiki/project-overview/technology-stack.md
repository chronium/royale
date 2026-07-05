---
title: Technology Stack
createdAt: 2026-07-05T16:14:02.2310850Z
modifiedAt: 2026-07-05T16:14:02.2310850Z
---

## .NET

The game client, dedicated server, simulation, networking, tooling, and managed bindings are written in C# on .NET 10.

Shared game logic should remain independent of presentation and platform-specific code wherever practical.

## SDL3

SDL3 provides the platform layer, including:

* Window creation
* Input
* Event handling
* High-DPI support
* Relative mouse input
* Native platform integration

## SDL GPU

SDL GPU provides the rendering abstraction.

The renderer will initially support only what the game requires:

* Static meshes
* Depth testing
* Camera matrices
* Basic lighting
* Debug geometry
* ImGui rendering

The project will use precompiled shaders suitable for the graphics backends used on macOS, Linux, and Windows.

## Box3D

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

## ImGui

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