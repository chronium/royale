---
name: royale-architecture-boundaries
description: Royale architecture and authority boundaries. Use for project layout, server/client/shared dependency direction, MVP scope, source-of-truth architecture docs, gameplay ownership, or avoiding engine overbuilding.
---

# Royale Architecture Boundaries

Use this skill when a task changes architecture, dependency direction, project structure, authority ownership, source-of-truth architecture docs, or MVP scope.

## Project philosophy

Royale is an experimental cross-platform, server-authoritative battle royale built from the ground up in .NET.

The goal is a small complete multiplayer game loop, not a general-purpose engine. Prefer concrete, inspectable systems that serve the MVP over speculative abstractions.

The first complete version should prove:

- A Linux dedicated server can run headlessly.
- macOS and Linux clients can connect to the same server.
- Windows client support can be added later without changing core gameplay contracts.
- Players can move, fight, be eliminated, spectate, produce a winner, and reset into another match.
- The server remains authoritative over gameplay state.

## Authority boundaries

Preserve separation between authority and presentation.

The server owns:

- Authoritative simulation.
- Movement validation.
- Combat.
- Health.
- Ammunition.
- Safe-zone state.
- Match phases.
- Eliminations.
- Winners.
- Match reset.

The client owns:

- Windowing.
- Input devices.
- Rendering.
- Audio and visual feedback.
- Local prediction.
- Interpolation.
- Reconciliation display.
- Development UI.

Shared simulation code may exist so server and client prediction use the same rules, but sharing code must not weaken server authority.

Rendering, SDL windowing, SDL GPU, ImGui, and client UI must not become server dependencies.

Client input represents intent, not authoritative state.

When ownership is unclear, default gameplay-relevant state to the server.

## Expected solution shape

The planned solution may include projects similar to:

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

Split projects only when there is a meaningful dependency, testing, or deployment boundary. Do not create a theoretical hierarchy that makes the code harder to move through.

## Architecture-change workflow

Before changing architecture:

- Use `royale-pm-workflow` to identify the selected PM task and relevant wiki page.
- Read the relevant code and wiki source of truth before modifying behavior.
- Identify which side owns the behavior: server, client, shared simulation, protocol, content, rendering, or tooling.
- Ask the project owner before locking in new gameplay rules, protocol contracts, file formats, physics behavior, platform policy, or rendering architecture.

While implementing:

- Keep dependency direction explicit.
- Do not introduce client rendering/UI dependencies into the server.
- Do not let in-process development shortcuts bypass the real client/server ownership model.
- Avoid speculative engine abstractions unless the selected PM task has a concrete gameplay need.
- Prefer the smallest coherent architecture change that satisfies the task.

After implementation:

- Update the `architecture` wiki page if runtime architecture, authority boundaries, data flow, networking, physics, testing, deployment shape, or dependency direction changed.
- Document significant architectural deviations in task notes and wiki.
- Use `royale-build-validation` for relevant build/test commands.
