---
id: RENDER-002
title: Draw one indexed cube
track: RENDER
milestone: M0
createdAt: 2026-07-04T09:21:32.4778450Z
modifiedAt: 2026-07-04T09:22:33.6211080Z
---

Render indexed geometry with vertex and index buffers, transforms, depth testing, and a basic graphics pipeline.

## Completion Notes

- Added a client-only `CubeRenderer` that loads the compiled `basic` shaders, creates SDL GPU vertex/index buffers, uploads indexed cube geometry through a transfer buffer/copy pass, and draws indexed triangle-list geometry.
- Changed the cube to face-local vertices with distinct face colors so the first render reads clearly as a cube, not a single interpolated face.
- Added depth-buffer creation with `D32_FLOAT` and resize-aware recreation tied to the acquired swapchain pixel size.
- Added shader asset selection for backend-specific file extensions and entry points. The Metal shadercross output uses `main0`, so entry-point selection is explicit and unit tested.
- Added development screenshot mode: `--screenshot <path> --screenshot-after-frames <n>`. It captures the presented swapchain frame through SDL GPU readback, writes a BMP, and exits after capture.
- Replaced the clear-only client presentation path with color/depth clear plus indexed cube rendering.
- Updated the architecture wiki to document the screenshot validation workflow.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- Screenshot validation passed with `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-build -- --screenshot /tmp/royale-cube.bmp --screenshot-after-frames 5`.
- Inspected `/tmp/royale-cube.png` converted from the BMP; it shows a three-face colored cube.