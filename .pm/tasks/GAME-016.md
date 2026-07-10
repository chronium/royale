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