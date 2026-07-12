---
title: Architecture Overview
createdAt: 2026-07-05T07:34:45.0706070Z
modifiedAt: 2026-07-12T08:44:40.0672880Z
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

The source tree uses project boundaries for deployment/dependency ownership and domain folders with matching namespaces inside projects. Folder names and namespace suffixes should remain aligned. Cross-domain references use explicit file-level `using` directives; project-wide global usings are intentionally avoided.

```text
src/
  Royale.Client/
    Gameplay/ Input/ Launch/ Networking/ Platform/ Presentation/
    Rendering/
      Cameras/ Debug/ Meshes/ Screenshots/ Shaders/ Text/
    Timing/ UI/
  Royale.Server/
    Bots/ Launch/ Match/ Networking/ Observability/ Sessions/ Simulation/
  Royale.Simulation/
    Combat/ Debug/ Movement/ World/
  Royale.Protocol/
    Framing/ Handshake/ Input/ Snapshots/
  Royale.Network/
    Handshake/ Input/ Simulation/ Snapshots/ Transport/
  Royale.Content/
    Maps/ Models/ Weapons/
  Royale.Diagnostics/
    Logging/ Telemetry/
  Royale.Box3D.Bindings/
    Interop/
  Royale.Box3D/
    Bodies/ Geometry/ Runtime/ Worlds/
  Royale.Native/

tools/
  Royale.AssetPipeline/
    Collision/ Processing/

tests/
  <Project>.Tests/
    domain folders mirroring the production project
    Infrastructure/ for shared native fixtures where required

thirdparty/
  repos/ patches/ artifacts/
```

Executable `Program.cs` files remain at project roots. A small root-level facade such as `ContentCatalog` may remain when it genuinely spans multiple content domains. Single-purpose projects are not split into arbitrary one-file folders.

The client and simulation retain their established responsibility splits. Protocol separates wire framing, handshake messages, player input, and snapshots. Network separates transport concerns from handshake, input, snapshots, and impairment simulation. Server separates runtime authority, sessions, networking, match rules, bots, launch policy, and observability. Tests mirror these domains so feature tests and shared infrastructure are not accumulated in project roots.

Projects should still be split only for meaningful dependency, testing, or deployment boundaries. Domain folders are an internal navigation tool and must not be used to introduce new assembly dependencies or weaken server authority.

`Royale.Platform` is the reusable SDL desktop project. Its `Desktop`, `Input`, and `Timing` domains own window/event lifecycle, relative mouse mode, input state, frame timing, and fixed scheduling for graphical executables. `Royale.Client` retains its composition root plus SDL GPU, ImGui, rendering, cameras, networking, gameplay presentation, and telemetry.

### Readability And Cohesion

Source structure optimizes for obvious ownership and inspectability, not minimum file count or maximum abstraction. Normal C# formatting is required; unrelated declarations, control-flow branches, assertions, and lifecycle steps must not be compressed onto one line. Formatting tools enforce whitespace but do not excuse structurally unreadable code.

Files and methods should have cohesive responsibilities. Production source files must not grow into multi-thousand-line catch-all units; split them earlier when navigation, review, testing, or ownership becomes difficult. Composition roots remain focused on lifecycle and dependency wiring, while substantial behavior belongs in the owning domain folder and matching namespace.

A project boundary represents a meaningful deployment, dependency, platform, authority, or testing boundary. Related domains may share a project, but they must not become a flat pile of cross-domain files at its root. Use domain folders and namespaces for internal organization rather than creating an assembly for every concern.

Abstractions remain need-driven. Prefer concrete types when there is one implementation and no substitution requirement. Interfaces and additional layers are appropriate for real polymorphism, external dependency isolation, platform or ownership boundaries, or useful test seams. Avoid both monolithic god objects and ceremonial forests of one-method interfaces and wrapper types.

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

`Royale.Client` may depend on `Royale.Platform`. `Royale.Platform` depends only on SDL3-CS, `Royale.Native`, and logging; it must not depend on the client, content, simulation, protocol/networking, rendering, or ImGui. Server and simulation projects must not depend on `Royale.Platform`.

## Summary

The project architecture is based on a strict separation between authority and presentation.

The dedicated server owns the game.

The client owns interaction, prediction, and rendering.

Shared code exists to keep rules and data structures consistent, but it does not weaken server authority.

The initial architecture should remain small enough to understand in full while supporting the complete path from input to prediction, networking, authoritative simulation, reconciliation, rendering, elimination, and match reset.