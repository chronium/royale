---
id: GAME-016
title: Add authoritative sprinting
track: GAME
milestone: M6
priority: medium
dependsOn:
- GAME-015
- TEST-005
- BOT-002
createdAt: 2026-07-10T14:59:54.4880070Z
modifiedAt: 2026-07-10T15:00:07.2557520Z
---

Add hold-to-sprint on Left Shift with a 7.0 m/s speed and no stamina system. Sprint is eligible when the desired stance is standing and normalized local movement has any positive forward component (Move.Y > 0), so forward diagonals sprint while pure strafing and backward movement remain at 4.5 m/s walk speed. Reject sprint while crouched. Preserve sprint speed during a jump while Shift remains held and forward input remains eligible. Carry sprint intent through a new protocol input bit and apply the same shared movement rules in offline play, server authority, client prediction/replay, bot/script input validation, telemetry, and deterministic tests.

## Completion notes

Implemented hold-to-sprint through Left Shift across offline play, server authority, client prediction and reconciliation replay, remote interpolation, bots, WattleScript scenarios, telemetry, and server debug logging.

- Standing walk speed remains 4.5 m/s; accepted sprint speed is 7.0 m/s.
- Shared local eligibility requires held sprint intent and `Move.Y > 0` before yaw conversion. Forward diagonals sprint without diagonal amplification; strafing, backward, and stationary input do not.
- Actual crouch stance, including blocked standing transitions, rejects sprint. Standing airborne movement retains sprint while eligible input remains held.
- Added `InputButtons.Sprint = 1 << 5` and appended effective `Sprinting` to player snapshots. Protocol stays at 1.0 with lockstep-only matching client/server builds after this wire-incompatible snapshot change.
- Updated simulation/authority, networking, diagnostics, and automated-gameplay-testing wiki pages.
- Added deterministic coverage for input ownership/release, shared eligibility and speeds, stance/airborne/collision behavior, authoritative and predicted/replayed state, bots/scripts, serializers and malformed booleans, payload bounds, interpolation, telemetry, and server debug logs.

Validation passed:

- `dotnet build Royale.slnx -m:1 --no-restore` — passed with 0 warnings and 0 errors.
- `dotnet test Royale.slnx -m:1 --no-restore` — passed all 917 tests.

Human validation remains required for offline and networked sprint feel: Left Shift hold/release, forward diagonals, strafe/backward rejection, crouch and blocked-stand rejection, and jumping while sprint remains held.
