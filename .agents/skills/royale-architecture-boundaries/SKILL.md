---
name: royale-architecture-boundaries
description: Apply Royale architecture and authority boundaries. Use for project or namespace layout, dependency direction, server/client/shared ownership, MVP scope, engine abstraction decisions, or architecture wiki changes.
---

# Royale Architecture Boundaries

## Goal

Build the smallest complete cross-platform, server-authoritative battle-royale loop. Do not optimize the architecture for arbitrary games, editors, plugins, scripting, or hypothetical scale.

## Ownership

Server-owned gameplay state includes simulation, validated movement, combat, health, ammunition, pickups, safe zone, match phases, eliminations, winner, and reset.

Client-owned state includes platform/input devices, rendering, audiovisual feedback, prediction, interpolation, presentation smoothing, camera, player UI, and development tooling.

Shared simulation may define rules used by both authority and prediction. It must not let the client authoritatively mutate gameplay. In-process development paths must preserve the same command/snapshot boundary as real networking.

## Dependency Direction

- Client may depend on simulation, protocol, network, content, diagnostics, Box3D, and native/platform libraries.
- Server may depend on simulation, protocol, network, content, diagnostics, and Box3D, but never client rendering/UI or graphics initialization.
- Simulation may depend on content and Box3D, not client presentation.
- Box3D wrappers depend on focused bindings; bindings depend on native resolution.
- Split projects only for a real dependency, deployment, or testing boundary.

Within projects, use domain folders with matching namespace suffixes. Keep executable entry points and true cross-domain facades at project roots. Use explicit file-level imports rather than project-wide global usings.

## Decision Gates

Inspect the wiki and nearby code first. Ask the owner before choosing a new:

- authority owner or cross-process responsibility;
- project/deployment boundary;
- gameplay, protocol, file-format, physics, rendering, or platform contract;
- dependency that changes the architecture or distribution model.

Routine placement inside an established domain is discoverable and should not trigger a question.

Update the architecture wiki when runtime processes, dependency direction, ownership, data flow, deployment shape, testing boundaries, or source-layout policy changes.
