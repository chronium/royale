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
modifiedAt: 2026-07-10T15:00:07.2155270Z
---

Add crouching end to end as authoritative desired stance. C toggles a client-local crouch latch; every fixed input command carries the resulting desired crouched state through the existing Crouch bit, so repeated or redundant packets cannot retrigger a toggle. Use a 1.1 m crouched capsule, 0.95 m crouched eye height, 2.5 m/s crouch speed, and the existing 0.35 m radius. Allow entering crouch while airborne, reject jumping while crouched, and reject standing until the full 1.8 m capsule has overhead clearance. Replicate authoritative stance for prediction, reconciliation, remote debug capsules, hitscan bounds, telemetry, and WattleScript scenarios. Change capsule height immediately in simulation while smoothing only the local camera over 0.15 seconds. Update protocol compatibility explicitly for the snapshot layout change.