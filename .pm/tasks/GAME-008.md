---
id: GAME-008
title: Fix player collision jitter
track: GAME
milestone: M2
createdAt: 2026-07-06T07:46:06.7743770Z
modifiedAt: 2026-07-06T08:02:05.8941810Z
---

Investigate and fix local player jitter when pushing into or sliding along static map obstacles at shallow angles. Preserve normal sliding and stepping behavior and add regression coverage for problematic wall and step-edge contacts.

## Notes

- 2026-07-06 08:00 UTC - Current fix covers two reproduced jitter paths: (1) shallow-angle wall slides where near-contact non-walkable planes were over-corrected every other tick, and (2) edge/corner step attempts where a step path could move sideways or backward while landing back near floor height. Recovery now uses Box3D plane overlap depth, only pushes non-walkable planes beyond skin width, still corrects walkable ground contact, and accepts step-up only when it improves progress along input. Added simulation regressions for shallow 10/15/20 degree wall slides and `step-low` edge sliding. Verified with `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, and PM validation. Waiting on manual client confirmation before marking done.
- 2026-07-06 08:02 UTC - Manual client verification confirmed the updated collision behavior works well for the reported `step-low` and shallow-angle wall jitter cases.