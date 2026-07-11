---
id: EDITOR-016
title: Add standalone SDL GPU integration harness
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-002
createdAt: 2026-07-11T19:54:42.1969120Z
modifiedAt: 2026-07-11T19:54:57.6932790Z
---

Create a small standalone main-thread GPU integration executable for Royale.Rendering. It must initialize SDL from the process entry thread, create a hidden window and SdlGpuDevice, render an indexed scene to an offscreen target, read normalized RGBA pixels, assert dimensions and nonblank output, resize the target, and repeat. Add an environment-gated automated wrapper that launches the harness as a child process and checks its exit code/output. Keep native binary and shader packaging representative of graphical consumers, document supported host requirements, and run it on macOS ARM64. This replaces the xUnit-worker-thread approach that cannot initialize Cocoa.

## Notes

- 2026-07-11 19:54 UTC - Design direction transferred from EDITOR-002: implement `tests/Royale.Rendering.GpuHarness` (or equivalently named executable) with all SDL video/GPU lifecycle work running directly from `Main`, because SDL video APIs must execute on the OS main thread and xUnit test methods run on worker threads. The harness should return a nonzero exit code with actionable diagnostics on failure. An environment-gated xUnit or CI wrapper may spawn the built harness as a child process, inheriting the graphical environment, then assert exit code and captured output. Do not use `SDL_RunOnMainThread` inside xUnit: testhost does not provide an SDL main-thread event loop and synchronous dispatch risks deadlock. Remove or replace the current worker-thread hidden-window test when the harness is established.