---
id: RENDER-009
title: Add world-space text billboards
track: RENDER
milestone: M3
priority: medium
dependsOn:
- RENDER-008
createdAt: 2026-07-06T15:17:09.7329850Z
modifiedAt: 2026-07-06T16:07:23.0030890Z
---

Add world-space text billboard rendering on top of the Blurg SDL GPU text renderer. Support both camera-facing billboards for player-facing labels and fixed-facing billboards that preserve an authored world orientation.

## Intent

World-space labels are needed for game-facing readable information in the 3D world, such as future player names, training dummy labels, pickup labels, and debug-visible gameplay annotations. This task builds on the Blurg text renderer and should not introduce a general UI framework.

## Requirements

- Depend on `RENDER-008` so Blurg screen-space text and SDL GPU atlas rendering exist first.
- Add a small client/rendering-owned world text API that accepts text, world position, sizing, color/style, alignment or anchor, and billboard mode.
- Support at least two billboard modes:
  - Camera-facing: rotates toward the active render camera so text remains readable to the player.
  - Fixed-facing: uses a provided world orientation or basis so text remains attached to an authored direction.
- Project world text into the existing render path and draw it with Blurg-rendered textured quads.
- Keep the implementation client/rendering-only. The dedicated server must not reference Blurg, SDL GPU, textures, fonts, or UI code.
- Do not add health bars, player-name replication, loot UI, networking, or final HUD behavior in this task.

## Validation

- Unit tests cover world-to-screen placement math, camera-facing orientation, fixed-facing orientation, culling or behind-camera handling, and stable sizing behavior where possible without opening a window.
- A screenshot or visual smoke path demonstrates one camera-facing label and one fixed-facing label rendered in the world outside ImGui.
- `dotnet build Royale.slnx -m:1 --no-restore` passes.
- `dotnet test Royale.slnx -m:1 --no-restore` passes.
- PM project validation passes.

## Human Validation

Ask the project owner to visually validate readability, facing behavior, scale, occlusion expectations, and whether camera-facing versus fixed-facing labels feel distinct enough.

## Notes

- 2026-07-06 15:45 UTC - Implementation started from the approved plan: client/rendering-only world-space Blurg text billboards with world-unit sizing, camera-facing and fixed-facing modes, overlay rendering after the world/debug pass and before ImGui, tests, wiki update, validation, and a focused commit. Scope excludes gameplay, protocol, server, HUD layout, health bars, replication, and content-schema changes.
- 2026-07-06 15:52 UTC - Implemented world-space Blurg text billboards as client/rendering-only presentation state. Added world-unit label sizing, camera-facing and fixed-facing modes, CPU projection into arbitrary screen-space text quads, batching with existing texture draw-command grouping, and smoke labels for the training dummy and an authored fixed world label. Updated architecture/content-and-rendering. Validation: `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, PM `validate_project`, and screenshot capture to `/tmp/royale-render009.bmp` passed. The startup screenshot visibly confirms the fixed world label outside ImGui; the camera-facing training-dummy label is attached to the dummy and needs human validation by turning/freecam because the current default gameplay view starts with the dummy behind the camera.
- 2026-07-06 16:00 UTC - Human validation found per-glyph outline artifacts on projected world text. Follow-up fix: round projected quad screen positions to integer pixels before upload, matching the existing screen-text behavior.
- 2026-07-06 16:01 UTC - Rounded projected text quad screen positions in `TextQuadBatchBuilder` before upload and updated client text rendering coverage to assert integer-snapped projected corners. Validation passed: `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, and screenshot capture to `/tmp/royale-render009-rounded.bmp`.
- 2026-07-06 16:02 UTC - Human validation confirmed integer snapping did not remove the projected world-text outlines. Continue investigation as likely sampler/filtering, atlas edge bleed, or alpha blending behavior rather than projected pixel position jitter.
- 2026-07-06 16:07 UTC - Compared against Technicraft: its Blurg labels project one world anchor to screen and then draw coherent screen-space text, avoiding independent per-glyph perspective projection. Updated Royale world text projection to derive one affine screen-space billboard basis per label from the projected anchor/right/up extents, while keeping world-unit sizing and camera/fixed-facing modes. Also switched Blurg atlas textures to BGRA to match the known working Technicraft/Blurg byte layout. Validation passed: `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, and screenshot capture to `/tmp/royale-render009-affine-bgra.bmp`.
- 2026-07-06 16:13 UTC - Human validation found two remaining visual issues: fixed-facing labels jolted into giant strips when viewed nearly parallel to their authored plane, and Blurg glyph rectangles still showed edge artifacts. Follow-up fix initializes each Blurg BGRA atlas to transparent white before glyph uploads so linear filtering at glyph UV edges samples deterministic zero-alpha white padding instead of undefined/dark padding. World-label shadow offsets are now applied after projection as true screen pixels, and fixed-facing labels are culled when their authored plane is edge-on to the camera. Validation passed: `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, client screenshot capture to `/tmp/royale-render009-atlas-clear.bmp`, and PM `validate_project`.
