---
id: EDITOR-002
title: Extract reusable SDL GPU rendering
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-001
createdAt: 2026-07-11T18:46:38.2166210Z
modifiedAt: 2026-07-11T19:50:51.4867370Z
---

Share SDL GPU device management, swapchain and offscreen render targets, cameras, mesh and material resources, debug primitives, ImGui SDL3_GPU integration, GPU readback, and screenshots without introducing a general-purpose scene engine.

## Notes

- 2026-07-11 19:50 UTC - Implemented the Royale.Rendering extraction and client migration. Added RenderFrame with per-frame scenes, dynamic instance transforms with stable geometry/material resource caching, swapchain and resizable offscreen targets, NativeTextureHandle, normalized RGBA readback, reusable screenshots, shared SdlGpuImGuiBackend/settings, client-only ImGuiDebugOverlay, and Rendering-owned shaders copied to consuming outputs. Royale.Rendering references only Platform, Content, Native, SDL3-CS, ImGui.Net, BlurgText, and SimpleMesh.

  Validation: `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` succeeded; `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings/errors; full `dotnet test Royale.slnx -m:1 --no-restore --no-build` passed (before the final shader packaging assertion, whose affected Client suite then passed 244/244 and Rendering suite passed 73/73). Normal elevated macOS ARM64 client run with OTLP and `--screenshot /tmp/editor-002-client.bmp --screenshot-after-frames 3` opened, rendered, saved a 1920x1080 screenshot, and shut down cleanly; agent inspection confirmed world solids, debug marks, Blurg text, and ImGui telemetry were visible.

  The environment-gated hidden-window offscreen xUnit test is implemented. Two elevated attempts failed before GPU creation because SDL Cocoa is unavailable from the xUnit testhost worker (`SDL video initialization failed: No available video device`; explicit cocoa reports `cocoa not available`). The normal client proves native SDL GPU/swapchain/readback behavior in this environment, but offscreen native execution still requires a suitable main-thread graphical test host.

  Owner validation is still required for world rendering, debug modes, ImGui interaction/capture, text, resizing, screenshot behavior, and overall visual parity. Task remains doing until that validation and the gated offscreen native evidence are resolved.
- 2026-07-11 19:50 UTC - Final post-packaging full solution test run passed: `dotnet test Royale.slnx -m:1 --no-restore --no-build` completed with every project green, including Royale.Client.Tests 244/244 and Royale.Rendering.Tests 73/73.