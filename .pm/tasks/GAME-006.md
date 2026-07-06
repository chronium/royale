---
id: GAME-006
title: Add player collision tests
track: GAME
milestone: M2
dependsOn:
- GAME-004
createdAt: 2026-07-04T09:21:53.2208710Z
modifiedAt: 2026-07-05T20:59:08.3065640Z
---

Test falling, walls, corners, slopes, ceilings, steps, and high-speed collision edge cases.

## Completion Notes

Added focused kinematic capsule controller tests for:

- High-speed movement into a thin wall.
- Diagonal movement into perpendicular corner walls.
- Repeated corner pressure remaining finite and stable.
- Walkable slope grounding below the configured slope limit.
- Oversized obstacle blocking step-up above `MaxStepHeight`.
- Low-ceiling jump recovery without penetration or popping through.
- Collision-heavy movement staying finite.
- Non-finite movement input being ignored by current controller behavior.

Existing coverage already exercised falling onto the floor, stable landing velocity/grounding, normal wall blocking, wall sliding, steep non-walkable slopes, low step-up, upward ceiling collision, and penetration recovery.

No production code changed. The physics/controller wiki remains accurate, so no wiki update was needed.

Validation:

- `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui.Net `NU1510` warning.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.