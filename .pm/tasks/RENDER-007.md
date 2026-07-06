---
id: RENDER-007
title: Add debug render view modes
track: RENDER
milestone: M1
dependsOn:
- RENDER-006
createdAt: 2026-07-06T08:38:34.7506180Z
modifiedAt: 2026-07-06T08:38:45.4583910Z
---

Add debug render mode keybindings after primitive debug drawing exists: F5 normal world only, F6 world plus debug wireframes, F7 debug wireframes only, F8 solid collision-world rendering.

## Notes

- Implemented `RenderViewMode`, render routing predicates, and `RenderViewModeController` with default `WorldAndDebug` mode.
- Added global `F5`-`F8` handling in `SdlApplication`; current mode is exposed through `SdlApplication.RenderViewMode` and shown in the diagnostic title/overlay.
- Routed `SdlGpuDevice.PresentFrame` by mode: world solids, debug wireframes, debug-only clear, or solid collision-world geometry.
- `CollisionSolids` renders the same static box map data used to build the local `MapStaticCollisionWorld`; no filled geometry is derived from Box3D debug callbacks.
- Updated `architecture/content-and-rendering` with the F5-F8 workflow.

## Verification

- `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui.Net `NU1510` warning.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- PM project validation passed.
- Screenshot smoke check passed for default `WorldAndDebug`: `/tmp/royale-render-007-world-and-debug.png` shows world solids, debug wireframes, and the overlay render mode text.
- The other render modes are covered by controller/global-control/routing tests; no interactive manual inspection was performed for F5, F7, or F8 in this run.