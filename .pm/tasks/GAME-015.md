---
id: GAME-015
title: Add authoritative crouching
track: GAME
milestone: M6
priority: medium
dependsOn:
- GAME-004
- NET-008
- SERVER-005
- COMBAT-003
- TEST-005
- UI-003
- BOT-002
createdAt: 2026-07-10T14:59:54.1748650Z
modifiedAt: 2026-07-10T15:33:40.6437520Z
---

Add crouching end to end as authoritative desired stance. C toggles a client-local crouch latch; every fixed input command carries the resulting desired crouched state through the existing Crouch bit, so repeated or redundant packets cannot retrigger a toggle. Use a 1.1 m crouched capsule, 0.95 m crouched eye height, 2.5 m/s crouch speed, and the existing 0.35 m radius. Allow entering crouch while airborne, reject jumping while crouched, and reject standing until the full 1.8 m capsule has overhead clearance. Replicate authoritative stance for prediction, reconciliation, remote debug capsules, hitscan bounds, telemetry, and WattleScript scenarios. Change capsule height immediately in simulation while smoothing only the local camera over 0.15 seconds. Update protocol compatibility explicitly for the snapshot layout change.

## Notes

- 2026-07-10 15:28 UTC - Implemented authoritative crouching end to end.

  Behavior and authority:
  - Added shared Standing/Crouched kinematic stance with standing defaults, 1.8 m/1.1 m active capsules, 4.5 m/s/2.5 m/s speeds, airborne crouch, crouch-blocked jump, full expansion clearance, and automatic stand retry.
  - C is an owned client-local toggle; commands redundantly carry desired state through InputButtons.Crouch. Server no-command ticks preserve actual stance.
  - Applied stance to offline play, authoritative server, bot/script commands, prediction/reconciliation replay, discrete remote interpolation, hitscan eye/target bounds, and debug capsules.
  - Added 0.95 m crouched eye height and local-only linear camera smoothing completing within 0.15 s, with lifecycle/freecam resets.
  - Added client telemetry, authoritative server diagnostics, and Wattle snapshot/debug observations.

  Protocol decision:
  - PlayerSnapshotState appends one validated Crouched boolean byte. Per-player maximum is 157 bytes and full snapshot maximum is 20,162 bytes.
  - Protocol remains 1.0 by owner decision. This layout is wire-incompatible with pre-GAME-015 builds; deploy client and server in lockstep.

  Validation:
  - dotnet build Royale.slnx -m:1 --no-restore — passed, 0 warnings/errors.
  - dotnet test Royale.slnx -m:1 --no-restore --no-build — passed outside the sandbox after the sandbox denied the VSTest localhost control socket: 897 tests total, 0 failed.
  - git diff --check — passed.
  - Automated native captures: /tmp/game-015-standing.bmp (SHA-1 221f1a6b1c2abc2c46a60f6f1ff7507a72ec8ae7) and /tmp/game-015-crouched-fixed.bmp (SHA-1 9f481c8894217518a8b7a0e04799c7ab0a1c583a). The crouched capture occurred several hundred frames after the owned C press.
  - During human validation, the owner found crouch initially lasted one simulation tick. Root cause was an offline frame path resetting the shared latch as if no network prediction meant disconnect. The offline and network-unavailable paths are now separated; focused client tests and the sustained crouched capture passed afterward.

  Documentation:
  - Updated simulation/authority, physics/combat, networking compatibility, client presentation, diagnostics, and automated gameplay testing wiki sections.

  Human validation still requested:
  - Verify camera transition feel, 2.5 m/s crouch movement feel, and blocked stand-up/automatic retry under a low ceiling.
- 2026-07-10 15:33 UTC - Project-owner human validation passed in a live server/client session: crouching remained active, standing was correctly blocked beneath a low ceiling, and the character automatically returned to standing after moving into sufficient overhead clearance. The owner also exercised the feature on the prototype-arena map and reported the behavior as good.