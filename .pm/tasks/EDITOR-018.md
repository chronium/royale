---
id: EDITOR-018
title: Add view-relative editor camera navigation
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-003
createdAt: 2026-07-12T07:56:31.3784300Z
modifiedAt: 2026-07-12T08:11:06.1254750Z
---

Change editor free-camera translation from FPS-style horizontal movement to full view-relative flight. Add Shift speed boost and mouse-wheel dolly forward/backward at a speed greater than boosted W/S movement. Preserve viewport-hover capture requirements and Q/E vertical movement, with deterministic camera/input tests and owner feel validation.

## Notes

- 2026-07-12 08:09 UTC - Implemented editor-specific view-relative flight and smooth wheel dolly. Input is represented with named flags rather than positional boolean clusters. Focused validation passed: Platform 23 tests, Rendering 73 tests, Editor 33 tests. `dotnet build Royale.slnx -m:1 --no-restore` passed with 0 warnings/errors, and `dotnet test Royale.slnx -m:1 --no-restore` passed all suites. Wiki `architecture/editor` updated. Editor launched successfully for combined owner UI/camera validation; owner result remains required before moving to done.
- 2026-07-12 08:11 UTC - Owner feedback found the initial wheel dolly too fast. Reduced impulse and velocity cap to 10% (3.6 m/s per notch, 7.2 m/s cap) while preserving the 0.12 s half-life. Editor tests passed (33) and full solution build passed with 0 warnings/errors. Re-launching for owner feel validation.