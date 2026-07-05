---
title: Simulation and Authority
createdAt: 2026-07-05T16:10:17.3093740Z
modifiedAt: 2026-07-05T16:10:17.3093740Z
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

## Input Commands

Client input is represented as discrete commands associated with simulation ticks.

```csharp
public readonly record struct PlayerInputCommand(
    uint Sequence,
    uint ClientTick,
    Vector2 Move,
    Vector2 LookDelta,
    InputButtons Buttons);
```

Each command should include:

* A monotonically increasing sequence number
* The client simulation tick
* Movement input
* Look input or resulting view orientation
* Button states

Possible buttons include:

```csharp
[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Jump = 1 << 0,
    Fire = 1 << 1,
    Reload = 1 << 2,
    Interact = 1 << 3,
    Crouch = 1 << 4
}
```

The server records the most recent processed input sequence for each player. That sequence is returned in snapshots so the client knows which predicted inputs have been acknowledged.

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