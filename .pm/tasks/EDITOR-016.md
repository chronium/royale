---
id: EDITOR-016
title: Add standalone SDL GPU integration harness
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-002
createdAt: 2026-07-11T19:54:42.1969120Z
modifiedAt: 2026-07-11T20:07:29.4831060Z
---

Create a small standalone main-thread GPU integration executable for Royale.Rendering. It must initialize SDL from the process entry thread, create a hidden window and SdlGpuDevice, render an indexed scene to an offscreen target, read normalized RGBA pixels, assert dimensions and nonblank output, resize the target, and repeat. Add an environment-gated automated wrapper that launches the harness as a child process and checks its exit code/output. Keep native binary and shader packaging representative of graphical consumers, document supported host requirements, and run it on macOS ARM64. This replaces the xUnit-worker-thread approach that cannot initialize Cocoa.

## Notes

- 2026-07-11 19:54 UTC - Design direction transferred from EDITOR-002: implement `tests/Royale.Rendering.GpuHarness` (or equivalently named executable) with all SDL video/GPU lifecycle work running directly from `Main`, because SDL video APIs must execute on the OS main thread and xUnit test methods run on worker threads. The harness should return a nonzero exit code with actionable diagnostics on failure. An environment-gated xUnit or CI wrapper may spawn the built harness as a child process, inheriting the graphical environment, then assert exit code and captured output. Do not use `SDL_RunOnMainThread` inside xUnit: testhost does not provide an SDL main-thread event loop and synchronous dispatch risks deadlock. Remove or replace the current worker-thread hidden-window test when the harness is established.
- 2026-07-11 20:07 UTC - Implemented the standalone macOS ARM64 SDL GPU harness and environment-gated child-process wrapper. The harness runs SDL/Cocoa/window/GPU work from the process entry thread through SdlDesktopHost, renders and validates indexed unit-box RGBA readback at 128x96 and after resizing the same target to 79x61, emits stable START/PASS/SUCCESS or FAILURE diagnostics, and packages SDL/ImGui/Blurg natives plus 18 HLSL/MSL/SPIR-V shader files. Rendering testhost no longer explicitly packages those three macOS native libraries. Validation: `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` passed; `dotnet build Royale.slnx -m:1 --no-restore` passed with 0 warnings/errors; `dotnet test Royale.slnx -m:1 --no-restore --no-build` passed all 13 test assemblies (967 tests total) with GPU execution disabled; direct elevated harness passed both 128x96 (49152 bytes) and 79x61 (19276 bytes) passes; elevated `ROYALE_GPU_TESTS=1 dotnet test tests/Royale.Rendering.Tests/Royale.Rendering.Tests.csproj -m:1 --no-restore --no-build --filter SdlGpuIntegrationTests` passed 1/1. Artifact inspection confirmed the packaged macOS ARM64 native libraries and all 18 shader variants.