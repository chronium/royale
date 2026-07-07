---
title: Simulation and Authority
createdAt: 2026-07-05T16:10:17.3093740Z
modifiedAt: 2026-07-07T07:21:44.0136070Z
---

## Simulation Model

The simulation runs at a fixed tick rate.

```text
Simulation rate: 60 Hz
Server snapshot rate: 20 Hz
```

The simulation rate and snapshot rate are separate. The server advances the game 60 times per second but does not need to send the entire state after every simulation tick.

Each simulation tick performs work in an explicit order. A possible order is:

1. Receive and queue network messages.
2. Select the input command for each player.
3. Update match state.
4. Simulate player movement.
5. Step physics.
6. Resolve weapon actions.
7. Apply damage and eliminations.
8. Update the safe zone.
9. Record authoritative state.
10. Produce snapshots or events when required.

The exact ordering matters because it affects gameplay behavior and prediction. Once established, it should be documented and tested.

## Fixed-Timestep Loop

The server uses a straightforward fixed-timestep loop.

```text
while running:
    accumulate elapsed time

    while accumulated time >= fixed step:
        process one simulation tick
        accumulated time -= fixed step

    sleep or yield until more work is available
```

The SERVER-001 headless server runtime advances at `SimulationSettings.TickRateHz` (`60 Hz`). Each initial server tick steps the server-owned Box3D static map collision world once using `SimulationSettings.FixedDeltaSeconds`, then increments the authoritative server tick counter. The loop bounds catch-up work to avoid uncontrolled simulation spirals after stalls.

No networking, authoritative player state, snapshots, input processing, match phases, safe zone updates, or combat resolution are part of the initial SERVER-001 server tick. The first server tick is structurally empty beyond the Box3D static-world step and authoritative tick increment.

A finite validation mode is available through `--run-ticks <count>`; it runs exactly that many fixed ticks as fast as possible and exits. Without `--run-ticks`, the dedicated server runs until Ctrl+C or process shutdown.

The client also maintains a fixed simulation loop for prediction. Rendering remains variable-rate.

The client frame performs:

1. Poll platform events.
2. Collect input.
3. Run zero or more fixed prediction ticks.
4. Receive snapshots.
5. Reconcile the local player.
6. Interpolate remote entities.
7. Render the current presentation state.
8. Render debug tools.
9. Present the frame.

A maximum number of catch-up ticks should be enforced so that a paused debugger or stalled frame does not trigger an uncontrolled simulation spiral.

## Authoritative State

The server owns the canonical version of all gameplay-relevant state.

Examples include:

```text
Player
  Player ID
  Connection ID
  Position
  Rotation
  Velocity
  Health
  Alive state
  Current weapon
  Ammunition
  Fire cooldown
  Last processed input sequence

Match
  Match phase
  Phase start tick
  Countdown state
  Living-player count
  Winner
  Safe-zone centre
  Safe-zone radius
  Safe-zone target radius
```

The client should not send state such as:

```text
My position is X
I hit player Y
Player Y is dead
I picked up item Z
I won the match
```

Instead, the client sends intent:

```text
Move in this direction
Look by this amount
Jump
Fire
Reload
Interact
```

The server interprets that intent using authoritative state.

### Initial Server State Model

SERVER-002 introduces the first concrete authoritative state container in `Royale.Server`. `HeadlessServerSimulation` owns:

* `IReadOnlyDictionary<ServerPlayerId, AuthoritativePlayerState>` for active players
* `AuthoritativeMatchState` for phase, phase-start tick, living-player count, and optional winner
* `AuthoritativeSafeZoneState` initialized from `GameMap.SafeZone`

Server-owned identifiers are `ServerPlayerId` and `ServerConnectionId`. Player IDs are allocated monotonically by the simulation and are not reused when a player is removed.

`AuthoritativePlayerState` contains the server player id, optional connection id, `KinematicCharacterState`, `PlayerLookState`, `HealthState`, `AuthoritativeWeaponState`, the spawn reservation, and the last processed input sequence. `AddPlayer` selects a valid unoccupied map spawn through `MapSpawnSelector`, reserves that spawn volume, initializes finite position, velocity, and look from the spawn point, sets `HealthState.DefaultPlayer`, and arms the player with `WeaponCatalog.DefaultRifle`.

`AuthoritativeWeaponState` stores the current weapon id, magazine ammunition, reserve ammunition, `WeaponFireState`, and reload placeholders. SERVER-006 consumes magazine ammunition and advances rifle fire cadence for accepted server-authoritative shots. Reload behavior remains future work.

`HeadlessServerSimulation.Step()` remains a no-input convenience wrapper. `HeadlessServerSimulation.Step(IReadOnlyDictionary<ServerPlayerId, PlayerInputCommand>)` validates supplied commands, applies movement/look/combat intent for the tick, refreshes living-player count, steps the static collision world, and increments the authoritative server tick. Commands for unknown players or protocol-invalid commands fail explicitly.

## Input Commands

Client input commands are protocol-owned intent messages associated with client simulation ticks. They are not authoritative gameplay state; the server interprets accepted commands against server-owned player, weapon, match, and map state.

```csharp
public readonly record struct PlayerInputCommand(
    uint Sequence,
    uint ClientTick,
    Vector2 Move,
    float YawRadians,
    float PitchRadians,
    InputButtons Buttons);
```

Each command includes:

* `Sequence`: a client-assigned command sequence number. It is `uint` for the initial protocol shape; wraparound ordering is future networking work.
* `ClientTick`: the client simulation tick that produced the command.
* `Move`: local two-axis movement intent, bounded to unit length.
* `YawRadians` and `PitchRadians`: the client's resulting view orientation for the command. Pitch uses the same conceptual `-89` to `89` degree range as gameplay look state.
* `Buttons`: a bitmask of discrete button intent.

Defined buttons are:

```csharp
[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Jump = 1 << 0,
    Fire = 1 << 1,
    Reload = 1 << 2,
    Interact = 1 << 3,
    Crouch = 1 << 4,
}
```

`PlayerInputCommandValidation` accepts only finite movement and look values, movement vectors whose length is at most `1.0` plus a small tolerance, pitch within the allowed look range, and button masks containing only defined bits. Yaw is validated for finiteness but is not clamped by the protocol helper.

`InProcessServerSession` accepts only valid `PlayerInputCommand` values, rejects invalid commands before queueing, drains valid commands before each in-process server step, and passes the latest drained command per connected player to `HeadlessServerSimulation.Step(...)`. If multiple valid commands are queued for one player before a step, only the latest drained command affects that server tick and becomes the top-level acknowledged sequence in the next recipient snapshot. Sequence wraparound handling remains future networking/protocol work; SERVER-006 uses simple monotonic `uint` sequences.

For alive players, an accepted command sets server-owned look from yaw/pitch, converts local movement through the command yaw into a world X/Z movement vector, applies jump intent, and may fire the equipped rifle. Players with no command for a tick are stepped with neutral movement and no fire intent. Dead players may still have a latest valid sequence acknowledged, but they do not update look, move, jump, fire, consume ammo, or advance weapon cadence.

Local offline gameplay input still has a shared pre-protocol sample type:

```csharp
public readonly record struct PlayerInputSample(
    Vector2 Move,
    bool Jump,
    bool Fire,
    Vector2 LookDelta);
```

`PlayerInputSample` captures local intent before network command sequencing exists. `Move.X` is local strafe right/left, `Move.Y` is local forward/back, `Jump` and `Fire` are button intent, and `LookDelta` is raw mouse movement accepted only while relative mouse mode is enabled.

`PlayerMovementIntent.ToWorldMovement()` is the shared helper for converting local movement through gameplay yaw before passing world X/Z intent to `KinematicCharacterController`. The local offline player and the headless server both use this helper. Shared gameplay look state exists as `PlayerLookState`, `PlayerLookSettings`, and `PlayerLookController`.

## Server Snapshots

SERVER-005 defines the first protocol-owned server snapshot DTOs in `Royale.Protocol` and a server-owned mapper in `HeadlessServerSimulation.CreateSnapshot`. The DTOs are transfer shapes shared by client and server; they do not move gameplay authority out of `Royale.Server`.

`ServerSnapshot` contains:

* `ServerTick`
* optional `LocalPlayerId`
* optional top-level `AcknowledgedInputSequence`
* ordered replicated player states
* match state
* safe-zone state

Snapshots are recipient-specific only for acknowledgement and local-player identity. When `CreateSnapshot` is called without a recipient, `LocalPlayerId` and `AcknowledgedInputSequence` are `null`. When a recipient is supplied, it must be an active server player; unknown recipients fail explicitly. The acknowledgement comes from the recipient player's `LastProcessedInputSequence`, which SERVER-006 updates from the latest valid command processed for that recipient during an in-process server step.

Player snapshot entries are sorted by player id for deterministic tests and future wire stability. Each player entry contains replicated gameplay state only: player id, position, velocity, yaw, pitch, current health, max health, alive state, and weapon state. Weapon state includes weapon id, magazine ammo, reserve ammo, next allowed fire tick, last fired tick, reload state, and optional reload completion tick.

Snapshots now reflect server-authoritative movement, look, rifle ammo/cadence, health, alive state, and living-player count after input application. Killed players remain in snapshots with `Alive == false` and health `0`. Snapshot DTOs still exclude server connection ids, spawn reservations, collision internals, client presentation state, rendering data, and UI data.

Serialization, UDP transport, snapshot send cadence, interpolation, prediction, reconciliation, winner selection, combat events, reload replication, and match reset remain future work.

## Client-Side Prediction

Without prediction, local movement would only update after a complete client-server round trip. The client therefore applies local input immediately.

For each fixed tick, the client:

1. Creates an input command.
2. Stores it in an unacknowledged input buffer.
3. Applies it to the local predicted player.
4. Sends it to the server.

The same movement rules should be used by both client and server wherever practical. This does not require every part of the simulation to be perfectly deterministic. The main requirement is that the movement implementation is sufficiently consistent for corrections to remain small under normal conditions.

## Server Reconciliation

Snapshots include the authoritative local-player state and the sequence number of the last processed input.

When a snapshot arrives, the client:

1. Replaces the predicted player state with the authoritative server state.
2. Removes acknowledged inputs from the local input buffer.
3. Replays all remaining unacknowledged inputs.
4. Updates the rendered presentation state.

Small corrections may be visually smoothed. Large corrections should be applied immediately or over a very short interval so that visual smoothing does not hide serious simulation divergence.

Debug tooling should expose:

* Correction distance
* Correction frequency
* Number of replayed inputs
* Oldest unacknowledged input
* Client tick difference
* Server tick difference

## Remote-Player Interpolation

Remote players are not predicted from local input. The client stores a short history of received snapshots and renders remote players slightly behind the latest server state.

```text
Server snapshot rate: 20 Hz
Interpolation delay: approximately 100 ms
```

The client selects two snapshots surrounding the render timestamp and interpolates between them. Position may initially use linear interpolation. Rotation may use quaternion interpolation.

When snapshots are missing, the client may briefly extrapolate using the last known velocity, but long extrapolation should be avoided.

## State Ownership

| State                          | Owner                 |
| ------------------------------ | --------------------- |
| Window size                    | Client                |
| Input device state             | Client                |
| Camera smoothing               | Client                |
| Visual effects                 | Client                |
| Predicted local transform      | Client, temporary     |
| Authoritative player transform | Server                |
| Health                         | Server                |
| Ammunition                     | Server                |
| Weapon cooldown                | Server                |
| Pickup availability            | Server                |
| Safe-zone state                | Server                |
| Match phase                    | Server                |
| Winner                         | Server                |
| Rendering resources            | Client                |
| Physics bindings               | Shared infrastructure |
| Static map gameplay data       | Shared content        |

Whenever ownership is unclear, gameplay-relevant state should default to the server.