---
id: RENDER-001
title: Compile cross-platform shaders
track: RENDER
milestone: M0
createdAt: 2026-07-04T09:21:32.3888160Z
modifiedAt: 2026-07-04T09:22:33.6209810Z
---

Create a shader build pipeline that produces compatible binaries for Metal, Vulkan, and Direct3D backends.

## Implementation Notes

- `Royale.Client` now treats `Shaders/**/*.vert.hlsl`, `Shaders/**/*.frag.hlsl`, and `Shaders/**/*.comp.hlsl` as shader source items with explicit stage metadata.
- The client build runs `shadercross` after build to emit SPIR-V (`.spv`) and Metal (`.msl`) files into `$(OutputPath)shaders/%(RecursiveDir)`.
- The original HLSL files are copied to the same output tree for Direct3D/DXIL-facing development until a DXIL output flow is chosen.
- `shadercross` is a required executable on `PATH`; `SDL_shadercross` is not vendored through `thirdparty`.
- Added the initial minimal shader pair for `RENDER-002`: `Shaders/basic.vert.hlsl` and `Shaders/basic.frag.hlsl`.
- No graphics pipeline or cube draw work was added in this task.

## Validation

- `shadercross --help` passed and confirmed HLSL, SPIR-V, MSL, vertex, fragment, and compute options.
- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- Confirmed build output contains `shaders/basic.vert.spv`, `shaders/basic.frag.spv`, `shaders/basic.vert.msl`, `shaders/basic.frag.msl`, and copied HLSL sources.
- Updated `AGENTS.md` and the architecture wiki for the new build dependency and shader output convention.