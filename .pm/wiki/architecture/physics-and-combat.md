---
title: Physics and Combat
createdAt: 2026-07-05T16:11:12.3492260Z
modifiedAt: 2026-07-06T05:56:24.8886700Z
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

The current Box3D binding surface includes foundational native value types, the minimum world lifecycle APIs needed to create, validate, fixed-step, and destroy a world, the low-level body lifecycle, type, transform, and linear velocity APIs from PHYS-006, the primitive shape APIs from PHYS-007 needed for gray-box static map collision and future capsule controller work, and the low-level world query APIs from PHYS-008 needed for raycasts, overlaps, shape casts, and capsule mover casts.

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

World query bindings include:

* `b3DefaultQueryFilter`
* `b3World_CastRayClosest`
* `b3World_CastRay`
* `b3World_OverlapAABB`
* `b3World_OverlapShape`
* `b3World_CastShape`
* `b3World_CastMover`
* `b3World_CollideMover`

Query callback delegates include:

* `B3OverlapResultFcn`
* `B3CastResultFcn`
* `B3MoverFilterFcn`
* `B3PlaneResultFcn`

Body bindings include:

* `b3DefaultBodyDef`
* `b3CreateBody`
* `b3DestroyBody`
* `b3Body_IsValid`
* `b3Body_GetType`
* `b3Body_SetType`
* `b3Body_GetPosition`
* `b3Body_GetRotation`
* `b3Body_GetTransform`
* `b3Body_SetTransform`
* `b3Body_GetLinearVelocity`
* `b3Body_SetLinearVelocity`

Primitive shape bindings include:

* `b3DefaultShapeDef`
* `b3MakeBoxHull`
* `b3MakeCubeHull`
* `b3CreateHullShape`
* `b3CreateCapsuleShape`
* `b3DestroyShape`
* `b3Shape_IsValid`
* `b3Shape_GetType`
* `b3Shape_GetBody`
* `b3Shape_GetWorld`
* `b3Shape_IsSensor`
* `b3Shape_GetFilter`
* `b3Shape_SetFilter`

Supporting value bindings for that subset include `b3MotionLocks`, `b3BodyDef`, `b3SurfaceMaterial`, `b3ShapeDef`, `b3Filter`, `b3QueryFilter`, `b3Capsule`, `b3ShapeProxy`, `b3RayResult`, `b3TreeStats`, `b3PlaneResult`, `b3HullData`, and the embedded box hull storage used by `b3BoxHull`.

This remains a low-level P/Invoke surface. Body names and user data; angular velocity; local/world point conversion; forces and impulses; mass APIs; material mutation; event toggles; mesh, height-field, compound, and sphere shape APIs; body-specific query APIs; runtime native-library resolution; and debug draw are deferred to their owning tasks.

PHYS-009 introduced a focused managed ownership layer in `Royale.Box3D` over the currently bound world, body, and shape APIs. `Box3DWorld`, `Box3DBody`, and `Box3DShape` expose their native IDs for low-level binding calls, make `Dispose()` idempotent, and throw `ObjectDisposedException` from wrapper operations after disposal. Ownership is recursive: worlds track bodies, bodies track shapes, disposing a shape leaves its body and world valid, disposing a body invalidates attached shape wrappers, and disposing a world invalidates all body and shape wrappers. Query wrappers remain deferred; callers should keep using the low-level query bindings with wrapper IDs until a query-specific task adds a managed API.

The current binding assumes the pinned Box3D build uses single-precision coordinates without `BOX3D_DOUBLE_PRECISION`. `b3Pos` maps to a three-float position and `b3WorldTransform` maps to the same layout as `b3Transform`.

Every layout-sensitive binding type must have tests that verify native size and representative field offsets against the pinned Box3D headers. Pointer fields, native bool fields, inline fixed arrays, nested structs, and opaque IDs require explicit coverage because they are the most likely to drift silently.

Native Box3D tests currently require the macOS ARM64 artifact at `thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.0.1.0.dylib`; build it with `sh thirdparty/build-box3d-macos.sh` before running tests on macOS ARM64 until final runtime native-library packaging is completed.

## Static Map Collision

`GAME-002` creates static Box3D collision from `GameMap.StaticBoxes` in `Royale.Simulation`. The map collision world is a disposable, game-specific owner for one Box3D world, one static body per static box, and one box hull shape per body. Each collider keeps the source static-box id associated with the created shape id so tests and debugging can resolve query hits back to map content.

Static box collision uses the same shared `position`, `size`, and yaw/pitch/roll `rotationEuler` transform convention as client rendering. Box hull half-extents are `size / 2`, matching the centered unit-box render mesh.

This type exists to build and query gray-box map collision for gameplay systems. Internally it owns its Box3D world, static bodies, and static shapes through the PHYS-009 wrappers while preserving its public raw ID and query behavior. Cast and overlap calls still use the low-level query bindings with `world.Id` and hit shape IDs because managed query wrappers are not part of PHYS-009.

`GAME-007` adds reusable spawn selection on top of static overlap queries. `MapSpawnPoint.Position` is the player feet anchor, and `MapSpawnSelector.CreateReservation()` builds a standing clearance AABB above that point using the default player radius `0.35`, height `1.8`, and ground clearance `0.05`. `TrySelectSpawn()` scans spawn points in map order and returns the first candidate whose clearance AABB does not overlap static map collision or caller-provided `SpawnReservation` AABBs. AABB touching without positive overlap is allowed. The selector is deterministic and does not randomize; battle-royale spawn randomization belongs to later match-flow work.

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
