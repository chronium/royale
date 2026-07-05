---
title: Architecture Overview
createdAt: 2026-07-05T07:34:45.0706070Z
modifiedAt: 2026-07-05T16:11:32.8324730Z
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

## Summary

The project architecture is based on a strict separation between authority and presentation.

The dedicated server owns the game.

The client owns interaction, prediction, and rendering.

Shared code exists to keep rules and data structures consistent, but it does not weaken server authority.

The initial architecture should remain small enough to understand in full while supporting the complete path from input to prediction, networking, authoritative simulation, reconciliation, rendering, elimination, and match reset.