---
title: Physics and Combat
createdAt: 2026-07-05T16:11:12.3492260Z
modifiedAt: 2026-07-07T07:29:43.9370780Z
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

`RENDER-006` extends the low-level Box3D binding surface with debug draw support: `b3DebugDraw`, `b3DebugShape`, `b3Sphere`, `b3DefaultDebugDraw`, `b3World_Draw`, and managed delegate types for debug shape creation/destruction plus shape, segment, transform, point, sphere, capsule, bounds, box, and string drawing callbacks. The bindings remain low-level P/Invoke types and are covered by native layout and callback tests.

## Static Map Collision

`GAME-002` creates static Box3D collision from `GameMap.StaticBoxes` in `Royale.Simulation`. The map collision world is a disposable, game-specific owner for one Box3D world, one static body per static box, and one box hull shape per body. Each collider keeps the source static-box id associated with the created shape id so tests and debugging can resolve query hits back to map content.

Static box collision uses the same shared `position`, `size`, and yaw/pitch/roll `rotationEuler` transform convention as client rendering. Box hull half-extents are `size / 2`, matching the centered unit-box render mesh.

This type exists to build and query gray-box map collision for gameplay systems. Internally it owns its Box3D world, static bodies, and static shapes through the PHYS-009 wrappers while preserving its public raw ID and query behavior. Cast and overlap calls still use the low-level query bindings with `world.Id` and hit shape IDs because managed query wrappers are not part of PHYS-009.

`GAME-007` adds reusable spawn selection on top of static overlap queries. `MapSpawnPoint.Position` is the player feet anchor, and `MapSpawnSelector.CreateReservation()` builds a standing clearance AABB above that point using the default player radius `0.35`, height `1.8`, and ground clearance `0.05`. `TrySelectSpawn()` scans spawn points in map order and returns the first candidate whose clearance AABB does not overlap static map collision or caller-provided `SpawnReservation` AABBs. AABB touching without positive overlap is allowed. The selector is deterministic and does not randomize; battle-royale spawn randomization belongs to later match-flow work.

`GAME-004` adds focused capsule query helpers to `MapStaticCollisionWorld` for simulation movement. `CastCapsuleMover()` wraps `b3World_CastMover` for a feet-anchored vertical capsule, and `CollectCapsuleCollisionPlanes()` wraps `b3World_CollideMover` to return contact planes with optional source collider metadata. These helpers are intentionally game-specific wrappers over the low-level Box3D query API, not a general managed query abstraction.

`MapStaticCollisionWorld` now configures Box3D `createDebugShape` and `destroyDebugShape` callbacks when the world is created. The callbacks capture geometry-only local wire segments for Box3D hull, capsule, and sphere debug shapes as managed handles stored behind Box3D's opaque `userShape` pointer. This keeps collision-world debug geometry backed by Box3D while avoiding any SDL, renderer, or client UI dependency in simulation code.

## Player Controller

The player is represented as a kinematic capsule rather than a freely simulated dynamic rigid body.

`KinematicCharacterController` in `Royale.Simulation` is the shared movement implementation for server authority and future client prediction. It takes fixed-timestep `KinematicCharacterInput` values and returns a new `KinematicCharacterState` through `KinematicCharacterStepResult`.

`KinematicCharacterState.Position` is the player feet anchor. The capsule is vertical, with its lower sphere center at `Position.Y + Radius` and upper sphere center at `Position.Y + Height - Radius`.

Default controller tuning is:

* Radius: `0.35`
* Height: `1.8`
* Walk speed: `4.5 m/s`
* Jump apex height: `1.1 m`
* Gravity: `20 m/s^2`
* Max step height: `0.35 m`
* Slope limit: `45 degrees`

Movement behavior is intentionally direct and inspectable for the MVP:

1. Probe and settle walkable ground using a short downward capsule mover cast.
2. Convert normalized 2D movement input directly to horizontal target velocity.
3. Accept jump only when grounded.
4. Apply gravity while airborne.
5. Move horizontally with capsule mover casts and slide remaining motion along collision planes.
6. When grounded and blocked horizontally, attempt a basic step by moving up, moving horizontally, then settling down.
7. Move vertically, stopping upward motion on ceiling collision and downward motion on walkable ground.
8. Recover small penetrations by nudging along collision plane normals.
9. Reprobe ground and return the updated state.

Slope checks use collision plane normals and treat normals with `Y >= cos(45 degrees)` as walkable. Steeper planes are blocking or airborne contacts, not ground. The first implementation prioritizes deterministic, testable behavior over polished feel; more exhaustive collision edge cases remain in the GAME-006 collision test task.

The controller is simulation-only. `GAME-005` uses it from a client-owned local offline player controller for first-person camera movement, but that local capsule is not authoritative server state and does not add networking, match state, animation, audio, or combat behavior.

Step-up attempts are only accepted when the elevated move settles on a walkable surface meaningfully above the original feet height. This prevents a failed step near an obstacle edge from briefly routing the capsule upward and backward before landing back on the floor, which caused visible jitter around the graybox `step-low` obstacle.

Penetration recovery uses the overlap depth reported by Box3D mover planes. Walkable planes can correct small vertical ground contact, while non-walkable planes only push when overlap exceeds the controller skin width. This avoids oscillation when pressing into a wall at a shallow angle: near-contact wall planes are used for clipping but no longer trigger an oversized recovery push on alternate ticks.

Step-up attempts also compare progress along the intended horizontal movement direction. A step is accepted only if the elevated path lands on walkable ground above the current feet height and advances farther along input than the flat slide did. This preserves valid small steps while rejecting shortcut paths that move sideways or backward around obstacle edges.

## Combat Flow

The first weapon is a server-authoritative hitscan rifle.

The client:

1. Detects fire input.
2. Predicts local visual feedback where a local sandbox path exists.
3. Sends the fire input as part of the current command for networked play.

The server:

1. Checks whether the player is alive.
2. Checks whether the weapon is equipped.
3. Checks fire cadence.
4. Checks ammunition.
5. Computes the authoritative shot direction from server-owned position and look.
6. Performs the raycast against static map geometry and other alive players.
7. Applies damage to the closest valid player target hit.
8. Updates ammunition and cooldown for accepted shots.
9. Refreshes authoritative living-player count after damage/death.

The client may immediately show:

* Muzzle flash
* Recoil
* Temporary tracer
* Firing animation

The client must wait for authoritative confirmation before treating another player as damaged or dead.

SERVER-006 does not add combat events, reload behavior, winner selection, elimination removal, respawn, lag compensation, or real UDP replication. Simultaneous in-process combat is resolved deterministically by ascending server player id.

### Health and Damage

`COMBAT-004` adds the first simulation-owned reusable health and damage model. `HealthState.DefaultPlayer` starts players at `100/100` HP with `Alive == true`.

Damage application is driven by `DamageController.Apply()` using a weapon-backed `DamageRequest`, the authoritative `HitscanHit`, and a target health table keyed by target id. Static hits and no-hit results apply no damage. Target hits apply the weapon's raw damage to the matching target entry only; missing target ids or missing health entries return a no-damage result and do not mutate other health state.

The default rifle deals `25` damage, so four valid target hits reduce default player health to `0`. Health clamps at zero, and reaching zero sets `Alive == false`. Applying damage to an already-dead target is a no-op that preserves `0` health and `Alive == false`.

SERVER-006 applies this model to server-owned player-vs-player rifle hits in the in-process session path. The shooter is excluded from target candidates, only alive players are target candidates, static geometry blocks farther player targets, and killed players remain in snapshots with health `0` and `Alive == false`. There is still no armor, healing, limb multiplier, distance falloff, randomness, friendly-fire team rule, authoritative damage history, final combat UI, respawn timer, or spectator UX.

### Local Debug Death And Respawn

`COMBAT-005` adds a client-only offline death and respawn debug flow for the local player controller. The local player owns mutable `HealthState` for this sandbox path and exposes explicit debug methods to apply damage, kill the player, and respawn the player. These methods are diagnostics hooks, not gameplay input commands and not server-authoritative match elimination.

While the local player is dead, gameplay look updates and gameplay fixed updates are ignored. Dead fixed updates do not move the capsule, accept jumps, fire the rifle, advance rifle cadence state, increment the local shot count, resolve hitscan, or damage the training dummy. The SDL client switches to the existing freecam when the local player transitions alive-to-dead; the freecam is the temporary spectator placeholder for this task. On debug respawn, the SDL client switches back to gameplay camera mode.

Debug respawn restores player health, spawn feet position, zero velocity, default look, ready rifle cadence, cleared last fire/hit/damage outputs, and spawn-derived gameplay camera state. It does not reset the training dummy health or damage history.

The ImGui `Player` diagnostics window displays local player health and alive state and exposes `Kill Player` and `Respawn Player` buttons. No kill or respawn hotkeys, networking, server match elimination, respawn timer, final HUD, animation, audio, or player-vs-player damage are part of this client-only debug contract.

### Weapon Feedback

`COMBAT-006` adds client-only presentation feedback for local offline rifle shots. When `LocalPlayerController.FixedUpdate()` accepts a rifle shot on the existing cadence path, the client records one transient feedback shot containing the hitscan origin, tracer end, shot direction, hit type, optional target id or static collider id, and optional damage result. Misses use the rifle range end as the tracer end.

The feedback state is presentation data owned by the client player controller. It is updated once per rendered frame by the SDL client, expires by elapsed render time, is cleared by local debug respawn, and is not emitted while the local player is dead. The inspectable shot feedback lifetime is `3.0` seconds so hit markers and tracer direction can be reviewed in debug views. It does not change fire cadence, hitscan direction, damage application, ammo rules, match state, networking, or server authority.

Rifle recoil is also presentation-only. Each accepted local shot adds a small camera pitch kick that decays over a short render-time window. The recoil offset is applied only while creating the render camera; it does not mutate `PlayerLookState` and therefore does not affect aiming, hitscan resolution, or future server-authoritative combat.

F6/F7 debug-line rendering draws the active feedback through existing debug primitives: a small yellow muzzle cross offset `0.35m` forward from the camera-origin shot point, a yellow-orange tracer from that muzzle marker to the hit point or range end, and an impact cross for target or static hits. The muzzle marker size is `0.048m`; the impact marker remains at the ray end. The ImGui `Player` diagnostics window reports the last shot result, transient hit-marker state, hit identity, applied damage, and remaining feedback lifetime. These diagnostics are development tooling, not a final player HUD.

### Hitscan Raycasts

`COMBAT-003` adds simulation-owned hitscan query types for rifle shots. `HitscanRay.FromPlayerLook()` builds the shot from the player's feet-anchored `KinematicCharacterState.Position` plus `PlayerViewSettings.EyeHeight`; its direction uses the same yaw and pitch convention as `RenderCamera.Forward`, and its length is the weapon `RangeMeters`.

`HitscanResolver` queries static map collision through `MapStaticCollisionWorld.CastRayClosest()` and wraps the closest static hit with point, normal, fraction, distance, and source static collider metadata. Callers may also pass feet-anchored vertical capsule `HitscanTarget` values using the same radius and height convention as the kinematic character controller. The nearest valid hit wins across static geometry and capsule targets, so static geometry blocks farther targets.

Hitscan resolution reports hit geometry and target ids only. SERVER-006 uses it from the headless server to resolve player-vs-player rifle shots after cadence and ammunition checks. Ammunition mutation, cooldown mutation, and health mutation remain outside `HitscanResolver` itself.

### Offline Training Dummy

`COMBAT-007` adds a client-owned offline training dummy as a development fixture for validating rifle damage and cadence without a second player. The stable target id is `training-dummy`. The dummy starts from `HealthState.DefaultPlayer`, uses a feet-anchored vertical capsule with the same default radius `0.35` and height `1.8` convention as player hitscan targets, and is placed near the selected local spawn in the graybox arena. Tests can inject explicit dummy placement.

Local offline firing resolves rifle hitscan against both static map collision and the training dummy target. Static geometry wins over a farther dummy target. When the dummy is hit, damage is applied through `DamageController.Apply()` using the existing health table contract. Dead dummy targets remain queryable for diagnostics, but COMBAT-004 dead-target no-op rules prevent further damage or damage-history entries.

The dummy keeps a client-only diagnostics history of the 16 most recent applied damage entries, newest first. Each entry records tick, weapon id, raw damage, applied damage, remaining health, hit distance, hit point, and nullable placeholders for future hit region, falloff multiplier, and random modifier.

The ImGui `Training Dummy` window is diagnostics tooling, not player-facing HUD and not authoritative combat behavior. It displays dummy health/alive state and recent damage history. Its reset button is a diagnostics-only exception that restores dummy health and clears history; it is not reachable from gameplay input and does not define respawn or server-authoritative reset behavior.

F6/F7 debug primitive modes draw the dummy capsule through the client debug line path so the target location is inspectable alongside the local player capsule and static collision debug geometry.

### Rifle Definition

`COMBAT-001` defines the first shared weapon entry in `Royale.Content` as code-backed catalog data, not JSON content. The stable weapon id is `rifle`, exposed through `ContentCatalog.DefaultWeaponId`, `WeaponCatalog.DefaultWeaponId`, and `WeaponCatalog.DefaultRifle`.

Canonical rifle tuning is:

* Fire model: hitscan
* Automatic: true
* Damage: `25`
* Magazine size: `30`
* Fire rate: `10 shots/sec`
* Fire interval: `0.1 seconds`
* Reload time: `2.0 seconds`
* Range: `120 meters`

`COMBAT-002` adds the first fire-intent and cadence path. `PlayerInputSample` carries `Fire` as player intent alongside movement, jump, and look delta. The client maps held left mouse button input to `Fire`; existing ImGui capture and mouse ownership rules still determine whether mouse input reaches gameplay.

Rifle cadence is enforced by `WeaponFireController` in `Royale.Simulation` using fixed simulation ticks. The default rifle's `0.1 second` fire interval resolves to `6` ticks at `SimulationSettings.TickRateHz` (`60 Hz`). A fresh `WeaponFireState` can fire immediately on the first eligible tick with `Fire == true`; after a shot at tick `T`, the next allowed shot is tick `T + intervalTicks`. Holding fire only emits shots on eligible ticks. Releasing fire does not reset cooldown, and pressing again after cooldown fires immediately.

The local offline client player and the headless server both use the default-rifle cadence model. Server-authoritative accepted shots consume one magazine round; reload remains deferred.

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
