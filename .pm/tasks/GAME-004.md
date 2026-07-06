---
id: GAME-004
title: Implement a kinematic capsule controller
track: GAME
milestone: M2
dependsOn:
- GAME-002
- PHYS-008
createdAt: 2026-07-04T09:21:53.0444130Z
modifiedAt: 2026-07-05T20:59:00.6709010Z
---

Implement walking, gravity, jumping, ground detection, wall sliding, slope limits, and basic step handling with shape casts.

## Implementation Notes

Implemented a simulation-only feet-anchored kinematic capsule controller in `Royale.Simulation`:

- `KinematicCharacterController`
- `KinematicCharacterSettings`
- `KinematicCharacterState`
- `KinematicCharacterInput`
- `KinematicCharacterStepResult`

`KinematicCharacterState.Position` is the player feet anchor. Default tuning is radius `0.35`, height `1.8`, walk speed `4.5 m/s`, jump apex `1.1 m`, gravity `20 m/s^2`, max step height `0.35 m`, and slope limit `45 degrees`.

Added focused `MapStaticCollisionWorld` helpers for the controller:

- `CastCapsuleMover()` wraps `b3World_CastMover` for feet-anchored vertical capsules.
- `CollectCapsuleCollisionPlanes()` wraps `b3World_CollideMover` and maps collision planes back to static collider metadata when available.

The controller uses fixed-timestep input, direct target horizontal velocity, gravity while airborne, grounded-only jump acceptance, short downward ground probing, horizontal capsule casts, wall sliding by projecting motion along blocking planes, steep-slope rejection through plane normals, basic grounded step-up handling, ceiling collision, and small penetration recovery.

No client input, camera, networking, server match state, animation, combat, or presentation integration was added in this task.

Deferred edge cases for later collision coverage include more exhaustive multi-plane corner behavior, moving platform behavior, tuned step feel on irregular geometry, and broader slope fixture coverage beyond the focused steep rejection test.

## Validation

Added `Royale.Simulation.Tests.KinematicCharacterControllerTests` covering:

- Falling under gravity and landing on the graybox floor
- Grounded state after landing
- Jump accepted while grounded
- Jump rejected while airborne
- Horizontal movement across clear ground
- Wall collision preventing passage through static geometry
- Wall sliding preserving tangential movement
- Steep slope rejection
- Basic step-up over a low obstacle
- Ceiling collision stopping upward motion
- Penetration recovery from a small initial overlap

Ran:

- `dotnet build Royale.slnx -m:1 --no-restore` - passed with existing ImGui.Net NU1510 warning
- `dotnet test Royale.slnx -m:1 --no-restore` - passed, 170 tests

## Documentation

Updated `architecture/physics-and-combat` with the feet-anchor contract, controller defaults, update behavior, slope rule, query helper approach, and simulation-only scope. `architecture/simulation-and-authority` did not need an update because the existing fixed-timestep and shared movement authority description remained accurate.
