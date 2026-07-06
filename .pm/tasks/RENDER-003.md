---
id: RENDER-003
title: Add camera matrices
track: RENDER
milestone: M1
priority: high
dependsOn:
- RENDER-002
createdAt: 2026-07-04T09:21:32.5682330Z
modifiedAt: 2026-07-05T20:57:22.4017170Z
---

Implement perspective projection, camera position and orientation, free-look controls, and resize-aware aspect ratio.

## Implementation Notes

Added a testable client-side `DebugCamera` model using `System.Numerics` for position, yaw, pitch clamping, view matrix creation, resize-aware perspective projection, and transposed world-view-projection output for SDL GPU shaders.

The cube renderer keeps its existing cube rotation but now receives camera state from the SDL application loop. The app maps `W/A/S/D`, `Space`, and `Left Ctrl` into free-fly camera movement, and applies mouse delta to yaw/pitch only while SDL relative mouse mode is enabled.

This remains renderer/debug presentation state only. It does not change server, protocol, simulation, physics, gameplay authority, or final player camera behavior.

Updated wiki page: `architecture/content-and-rendering`.

Validation passed:

```text
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 -- --screenshot /tmp/royale-camera.bmp --screenshot-after-frames 5
```

Screenshot output was a 1280x720 32-bit BMP with non-clear rendered pixels. Interactive client run was also confirmed by the project owner on 2026-07-06.