---
id: RENDER-008
title: Add Blurg SDL GPU text rendering
track: RENDER
milestone: M3
priority: medium
dependsOn:
- BUILD-010
- RENDER-004
createdAt: 2026-07-06T12:57:58.3127590Z
modifiedAt: 2026-07-06T12:58:12.0193720Z
---

Integrate BlurgText into the client renderer for game-facing and world text outside ImGui, using SDL GPU atlas textures and screen-space textured quads inspired by Technicraft's `BlurgTextRenderer`.

## Intent

BlurgText is the project text renderer for player-facing and world-space text. ImGui remains development tooling and diagnostics only.

## Requirements

- Add a client/rendering-owned Blurg text renderer that wraps BlurgText atlas allocation and update callbacks with SDL GPU textures.
- Provide a small measure/draw API for screen-space text suitable for future HUD labels and debug-visible game labels outside ImGui.
- Draw text through a simple textured-quad path; do not introduce a general UI framework, retained widget tree, or full sprite system unless a concrete rendering need appears while implementing the task.
- Prefer the Technicraft approach as reference: system fonts first, default font resolution, atlas texture map keyed by Blurg user data, rounded screen-space quads, and explicit disposal.
- Keep the implementation client/rendering-only. The dedicated server must not reference Blurg, SDL GPU, textures, font assets, or UI code.
- Do not add world-space health bars in this task; use this as the foundation for future in-game text.

## Validation

- Unit tests cover default font/text-renderer state that can be tested without opening a window.
- A screenshot or visual smoke path demonstrates at least one string rendered outside ImGui through SDL GPU.
- `dotnet build Royale.slnx -m:1 --no-restore` passes.
- `dotnet test Royale.slnx -m:1 --no-restore` passes.
- PM project validation passes.

## Human Validation

Ask the project owner to visually validate rendered text quality, alignment, and readability because text appearance cannot be fully validated through automated tests.