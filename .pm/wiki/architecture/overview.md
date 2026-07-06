---
title: Architecture Overview
createdAt: 2026-07-05T07:34:45.0706070Z
modifiedAt: 2026-07-06T17:38:18.2417530Z
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

The current solution layout is:

```text
src/
  Royale.Client/
    Gameplay/        client-owned offline gameplay fixtures and feedback
    Input/           input state and input-to-game command mapping
    Launch/          client command-line options
    Platform/        SDL application, window, GPU device, and mouse-mode glue
    Presentation/    client camera mode and render-view mode controllers
    Rendering/
      Cameras/       render cameras, free camera, and gameplay camera view math
      Debug/         debug primitive lists, debug scene building, and line rendering
      Meshes/        static mesh geometry, instances, draw constants, and renderer
      Screenshots/   BMP screenshot writing
      Shaders/       shader asset selection helpers
      Text/          Blurg text rendering, screen text, and world text billboards
    Shaders/         HLSL shader sources compiled by the client build
    Timing/          fixed-update accumulator
    UI/              ImGui backend and diagnostics state
  Royale.Server/
  Royale.Simulation/
    Combat/          health, damage, weapon fire, hitscan rays, and hit resolution
    Debug/           simulation-side debug geometry descriptions
    Movement/        player input samples, look state, view settings, and character controller
    World/           simulation settings, map collision, spawn selection, and static world queries
  Royale.Protocol/
  Royale.Content/
  Royale.Diagnostics/
  Royale.Native/
  Royale.Box3D.Bindings/
  Royale.Box3D/

tests/
  Royale.Client.Tests/
  Royale.Server.Tests/
  Royale.Simulation.Tests/
  Royale.Protocol.Tests/
  Royale.Content.Tests/
  Royale.Diagnostics.Tests/
  Royale.Native.Tests/
  Royale.Box3D.Tests/

thirdparty/
  repos/             ignored fetched source dependencies
  patches/           committed project-specific dependency patches
  artifacts/         ignored generated native artifacts
```

`Royale.Client.Platform` should stay focused on SDL/platform boundaries. Client presentation concerns such as input mapping, launch parsing, fixed timing, camera/render mode control, ImGui diagnostics, rendering, and screenshots live in their own folders and namespaces.

`Royale.Client.Rendering` is split by rendering responsibility. Shared mode enums can remain at the rendering root, while cameras, debug drawing, meshes, shader helpers, screenshots, and text rendering stay in focused subnamespaces.

`Royale.Simulation` is split by gameplay domain. Combat, movement, world/collision, and simulation debug helpers should remain independent of client presentation code.

Projects should be split when there is a meaningful dependency, testing, or deployment boundary, not merely to create a theoretically clean hierarchy.

## Dependency Direction

The most important dependency rule is that low-level and shared projects must not depend on client presentation code.

A simplified dependency graph is:

```text
Royale.Client
  ├── Royale.Simulation
  ├── Royale.Protocol
  ├── Royale.Content
  ├── Royale.Diagnostics
  ├── Royale.Box3D
  └── Royale.Native

Royale.Server
  ├── Royale.Simulation
  ├── Royale.Protocol
  ├── Royale.Content
  ├── Royale.Box3D
  └── Royale.Diagnostics

Royale.Simulation
  ├── Royale.Content
  └── Royale.Box3D

Royale.Box3D
  └── Royale.Box3D.Bindings

Royale.Box3D.Bindings
  └── Royale.Native
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