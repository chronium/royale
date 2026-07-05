---
title: Physics and Combat
createdAt: 2026-07-05T16:11:12.3492260Z
modifiedAt: 2026-07-05T18:08:31.9762300Z
---

## Physics Architecture

Box3D is used on both client and server.

The server physics world is authoritative.

The client may also maintain a physics world for:

* Local movement prediction
* Camera interaction
* Collision visualization
* Static map queries
* Non-authoritative presentation effects

The initial implementation should avoid networked dynamic rigid bodies.

The first synchronized gameplay objects should be:

* Players
* Static map collision
* Weapon pickups
* Safe-zone state

This keeps state ownership and replication straightforward.

## Box3D Binding Scope

The current Box3D binding surface includes foundational native value types, the minimum world lifecycle APIs needed to create, validate, fixed-step, and destroy a world, and the narrow body plus hull-shape subset needed by the upstream Box3D Hello World falling-box example.

Foundational bindings include:

* Math values
* Opaque IDs
* Core body and shape enums
* Filters
* Diagnostics structs
* Query and cast result structs

World lifecycle bindings include:

* `b3DefaultWorldDef`
* `b3CreateWorld`
* `b3DestroyWorld`
* `b3GetWorldCount`
* `b3GetMaxWorldCount`
* `b3World_IsValid`
* `b3World_Step`

The Hello World subset adds:

* `b3DefaultBodyDef`
* `b3CreateBody`
* `b3Body_IsValid`
* `b3Body_GetPosition`
* `b3Body_GetRotation`
* `b3DefaultShapeDef`
* `b3MakeBoxHull`
* `b3MakeCubeHull`
* `b3CreateHullShape`

Supporting value bindings for that subset include `b3MotionLocks`, `b3BodyDef`, `b3SurfaceMaterial`, `b3ShapeDef`, `b3HullData`, and the embedded box hull storage used by `b3BoxHull`.

This remains a low-level P/Invoke surface. Managed world, body, and shape ownership wrappers; broader body and shape lifecycle APIs; mesh, capsule, and sphere shape APIs; runtime native-library resolution; queries; debug draw; and gameplay-specific physics systems are deferred to their owning tasks.

The current binding assumes the pinned Box3D build uses single-precision coordinates without `BOX3D_DOUBLE_PRECISION`. `b3Pos` maps to a three-float position and `b3WorldTransform` maps to the same layout as `b3Transform`.

Every layout-sensitive binding type must have tests that verify native size and representative field offsets against the pinned Box3D headers. Pointer fields, native bool fields, inline fixed arrays, nested structs, and opaque IDs require explicit coverage because they are the most likely to drift silently.

Native Box3D tests currently require the macOS ARM64 artifact at `thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.0.1.0.dylib`; build it with `sh thirdparty/build-box3d-macos.sh` before running tests on macOS ARM64 until final runtime native-library packaging is completed.

## Player Controller

The player is represented as a kinematic capsule rather than a freely simulated dynamic rigid body.

Movement is controlled explicitly using shape casts, overlap tests, and position correction.

The controller is responsible for:

* Horizontal movement
* Gravity
* Jumping
* Ground detection
* Slope handling
* Wall sliding
* Step handling
* Ceiling collision
* Penetration recovery

A conceptual update may be:

1. Read desired movement input.
2. Apply acceleration or target velocity.
3. Apply gravity.
4. Test ground state.
5. Attempt horizontal capsule movement.
6. Slide along blocking geometry.
7. Attempt step movement where appropriate.
8. Apply vertical movement.
9. Resolve remaining penetration.
10. Update grounded state and velocity.

The same controller logic should be used by the server and client prediction.

## Combat Flow

The first weapon is a server-authoritative hitscan rifle.

The client:

1. Detects fire input.
2. Predicts local visual feedback.
3. Sends the fire input as part of the current command.

The server:

1. Checks whether the player is alive.
2. Checks whether the weapon is equipped.
3. Checks fire cadence.
4. Checks ammunition.
5. Computes the authoritative shot direction.
6. Performs the raycast.
7. Applies damage to the closest valid hit.
8. Updates ammunition and cooldown.
9. Emits an authoritative combat event.

The client may immediately show:

* Muzzle flash
* Recoil
* Temporary tracer
* Firing animation

The client must wait for authoritative confirmation before treating another player as damaged or dead.

## Match State Machine

The battle-royale lifecycle is controlled by a server-side state machine.

```text
WaitingForPlayers
    ↓
Countdown
    ↓
Playing
    ↓
Finished
    ↓
Resetting
    ↓
WaitingForPlayers
```

## WaitingForPlayers

* Accept players
* Spawn or prepare them in a non-active state
* Wait for the minimum player count
* Allow a development force-start command

## Countdown

* Lock the participant list if required
* Select spawn points
* Reset player state
* Begin a short countdown

## Playing

* Enable movement and combat
* Update the safe zone
* Apply zone damage
* Track living players
* Detect the winner

## Finished

* Stop combat
* Announce the winner
* Allow spectating
* Wait briefly before reset

## Resetting

* Destroy match-scoped entities
* Clear temporary state
* Reset the physics world or restore map state
* Prepare the next match

Match transitions should be driven by server ticks rather than wall-clock timers wherever practical.
