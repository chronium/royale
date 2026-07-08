---
name: royale-simulation-gameplay
description: Royale simulation and gameplay rules. Use for fixed tick, movement, player controller, combat, hitscan rifle, damage, health, ammo, safe zone, match phases, eliminations, winner, reset, prediction, reconciliation, or gameplay tests.
---

# Royale Simulation and Gameplay Discipline

Use this skill for simulation timing, movement, combat, weapons, damage, health, ammo, safe-zone behavior, match phases, eliminations, winner selection, reset, prediction, reconciliation, and gameplay tests.

## Simulation principles

Simulation should be deterministic enough for prediction and reconciliation to stay understandable, but do not overbuild determinism before the MVP needs it.

- Use a fixed simulation tick for server authority.
- Keep render rate separate from simulation rate.
- Bound catch-up ticks so stalls do not create uncontrolled simulation spirals.
- Keep the player controller shared between server and client prediction where practical.
- The initial player controller is a kinematic capsule, not a freely simulated dynamic rigid body.
- The initial weapon is a server-authoritative hitscan rifle.
- The initial synchronized gameplay objects are players, static map collision, weapon pickups, and safe-zone state.

Gameplay behavior changes must be reflected in tests and wiki documentation.

## Authority and ownership

The server owns authoritative gameplay state:

- Simulation.
- Movement validation.
- Combat.
- Health.
- Ammunition.
- Safe-zone state.
- Match phases.
- Eliminations.
- Winners.
- Match reset.

The client may predict and render, but server snapshots/events correct the client.

Shared simulation code may exist so server and client prediction use the same rules, but sharing code must not weaken server authority.

When ownership is unclear, default gameplay-relevant state to the server.

## Gameplay scope for MVP

Prefer small, complete gameplay loops over generalized systems.

Early synchronized gameplay objects:

- Players.
- Static map collision.
- Weapon pickups.
- Safe-zone state.

Early combat:

- Server-authoritative hitscan rifle.
- Fire cadence enforced by server rules.
- Damage rules tested where they live.
- Client can show feedback, but cannot authoritatively apply damage.

Early match flow:

- Players connect.
- Players move.
- Players fight.
- Players can be eliminated.
- Players can spectate after elimination.
- A winner can be produced.
- The match can reset into another match.

## Tests to add or update

Add tests at the level where gameplay behavior lives.

Expected gameplay/simulation test areas:

- Player movement collision cases.
- Fixed tick and bounded catch-up behavior.
- Input buffering and sequence comparisons.
- Weapon fire cadence and damage rules.
- Health and elimination rules.
- Match-state transitions.
- Safe-zone interpolation and damage.
- Headless server simulation.
- In-process client/server integration.
- Consecutive match reset behavior.

## Workflow

Before implementation:

- Use `royale-pm-workflow` to confirm the selected task.
- Read relevant wiki pages before changing gameplay rules.
- Ask the project owner before inventing new gameplay rules, physics behavior, or combat contracts.
- Identify whether behavior belongs in server, shared simulation, protocol, client prediction, or rendering feedback.

While implementing:

- Keep gameplay-relevant state server-authoritative.
- Keep prediction/reconciliation understandable and diagnosable.
- Keep simulation rate independent from rendering rate.
- Avoid generalized engine systems unless the selected task has a concrete gameplay need.
- Avoid unrelated refactoring.

After implementation:

- Add/update behavior tests.
- Run relevant validation through `royale-build-validation`.
- Update wiki documentation if gameplay rules, simulation tick order/timing, authority boundaries, diagnostics, or content formats changed.
- Request human validation when game feel, movement feel, camera feel, or combat feel cannot be fully validated by automated tests.
