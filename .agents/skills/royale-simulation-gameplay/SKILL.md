---
name: royale-simulation-gameplay
description: Implement or review Royale simulation and gameplay. Use for fixed ticks, player movement, crouch/sprint/jump, kinematic collision, combat, hitscan, health/ammo, pickups, safe zone, match phases, bots' gameplay input, prediction, reconciliation, or gameplay scenarios.
---

# Royale Simulation And Gameplay

## Simulation Contract

- Authoritative simulation runs at a fixed tick independent of rendering and snapshot cadence.
- The server owns gameplay outcomes; clients may predict only for responsiveness.
- Shared movement rules should serve server authority and client prediction without importing presentation concerns.
- The player controller is a kinematic capsule using casts/overlaps and explicit movement rules.
- Initial combat is a server-authoritative hitscan rifle.
- Keep synchronized gameplay focused on players, static map collision, pickups, safe zone, and match state.

## Decision Gates

Inspect the task, wiki, content definitions, simulation order, and existing tests first. Ask before choosing a new gameplay rule, balance value, collision policy, tick ordering, bot behavior, damage model, match transition, or prediction correction contract.

Do not ask when implementing a rule already fixed by PM/wiki or extending an established state/input path mechanically.

## Implementation Rules

- Apply input as intent and validate it at the authority boundary.
- Keep offline play, authority, prediction, reconciliation replay, bot input, and scripted input on shared rules where the task requires parity.
- Make simulation order explicit when outcomes depend on it.
- Preserve deterministic ordering and seeded randomness where practical and observable.
- Do not introduce generalized ECS, scripting, scene, or ability frameworks for a narrow feature.

## Coverage

Test behavior where it lives:

- movement speed/stance, ground/air transitions, slopes, steps, walls, ceilings, and high-speed cases;
- weapon cadence, ammo, raycast ordering, damage, health, death, and elimination;
- match transitions, safe-zone timing/damage, winner selection, and reset;
- server authority plus prediction/reconciliation parity;
- in-process and WattleScript scenarios for multi-system flows.

Update gameplay/simulation wiki pages when rules, timing/order, diagnostics, content formats, or authority behavior change. Request owner validation for movement, camera, combat, or other game feel after automated checks.
