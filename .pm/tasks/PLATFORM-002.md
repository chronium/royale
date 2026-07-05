---
id: PLATFORM-002
title: Create the SDL GPU device
track: PLATFORM
milestone: M0
createdAt: 2026-07-04T09:21:26.7550920Z
modifiedAt: 2026-07-04T09:22:33.6205830Z
---

Create and manage the SDL GPU device, swapchain, command buffers, presentation, and resize handling.

## Implementation Notes

- Added a client-platform SDL GPU wrapper that creates the device with SPIR-V, MSL, and DXIL shader format support requested.
- The wrapper records supported shader formats, exposes deterministic preferred format selection for later shader loading, claims the SDL window, and releases the window before destroying the GPU device.
- The client frame now performs a clear-only GPU presentation pass by acquiring a command buffer, waiting for the swapchain texture, clearing it when available, and submitting the command buffer.
- `SdlWindow` now keeps logical size and drawable pixel size separately while keeping the raw `SDL_Window*` internal to the client platform layer.
- No protocol, simulation, content, server, shader pipeline, mesh buffer, depth buffer, ImGui backend, or cube rendering changes were made.
- Architecture wiki already documented SDL GPU as client presentation ownership, so no wiki update was needed.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore`
- `dotnet test Royale.slnx -m:1 --no-restore`
- Launched the macOS client with `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-restore`; the SDL GPU device created, claimed the window, and continued presenting until stopped with Ctrl-C.
- User visually confirmed the expected dark blue-gray clear color was displayed on macOS.
- Interactive resize and Linux/Windows client verification remain to be performed on those local platforms.