---
id: RENDER-009
title: Add world-space text billboards
track: RENDER
milestone: M3
priority: medium
dependsOn:
- RENDER-008
createdAt: 2026-07-06T15:17:09.7329850Z
modifiedAt: 2026-07-06T15:17:13.8725350Z
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