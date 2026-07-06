---
id: SERVER-004
title: Define input commands
track: SERVER
milestone: M4
priority: medium
dependsOn:
- SERVER-002
createdAt: 2026-07-04T09:22:04.1208430Z
modifiedAt: 2026-07-06T20:05:05.4382120Z
---

Define versionable tick-stamped player movement, look, and button command messages.

## Notes

- 2026-07-06 20:05 UTC - Implemented protocol-owned input command types: `InputButtons : ushort`, `PlayerInputCommand` with `Sequence`, `ClientTick`, `Move`, `YawRadians`, `PitchRadians`, and `Buttons`, plus `PlayerInputCommandValidation`. Validation rejects non-finite movement/look values, movement longer than unit length beyond a small tolerance, pitch outside the `-89` to `89` degree look range, and button masks containing undefined bits. SERVER-004 remains message-shape and validation only: no networking, serialization, input queues, prediction, reconciliation, movement/combat processing, or `LastProcessedInputSequence` updates were added.