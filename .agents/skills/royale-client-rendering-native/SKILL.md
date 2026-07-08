---
name: royale-client-rendering-native
description: Royale client rendering, native platform, SDL3, SDL GPU, ImGui, shaders, debug UI, frame timing, visual validation, Box3D bindings, or native dependency layout work.
---

# Royale Client Rendering and Native Platform

Use this skill for client rendering, SDL3/SDL GPU, ImGui, shader handling, debug UI, frame timing, visual validation, native dependency layout, and C# native binding work.

## Rendering and UI principles

The renderer should remain thin and game-specific.

Initial rendering needs are:

- Static meshes.
- Depth testing.
- Camera matrices.
- Basic lighting.
- Debug geometry.
- ImGui.

There is no initial requirement for:

- Deferred rendering.
- Render graphs.
- Material graphs.
- Dynamic global illumination.
- Streaming terrain.
- Generalized scene editing.

ImGui is development tooling, not the final player-facing interface.

Rendering, SDL windowing, SDL GPU, ImGui, and client UI must not become server dependencies.

## Diagnostics to expose

Diagnostics should expose:

- Frame and simulation timing.
- Client and server ticks.
- Snapshot buffering.
- Prediction corrections.
- Input queue depth.
- Physics step timing.
- Player state.
- Weapon state.
- Safe-zone and match state.
- Packet counts, loss, latency, jitter, and invalid packets.

## Shader workflow

Client shader builds require `shadercross` on `PATH`.

The client compiles HLSL sources under `src/Royale.Client/Shaders/` to SPIR-V (`.spv`) and Metal (`.msl`) outputs after build.

The original HLSL files are copied for Direct3D/DXIL-facing development until a DXIL flow is explicitly chosen.

`SDL_shadercross` is a local build tool dependency and is not vendored through `thirdparty`.

Do not invent a DXIL flow unless explicitly chosen.

## Native dependencies

The project uses SDL3, SDL GPU, Box3D, and ImGui-related bindings or integration.

- Keep native dependency layout explicit and consistent across supported runtime identifiers.
- Pin native dependency versions once they are chosen.
- Keep Box3D bindings focused on the API surface needed by the game.
- Verify native memory layouts for C# bindings with tests.
- Package only the native libraries required by each artifact.
- The Linux server package must not depend on SDL GPU, ImGui, textures, client shaders, or graphics initialization.
- If a native dependency decision is unclear, ask before assuming.

## Frame and simulation separation

- Keep render rate separate from simulation rate.
- Client rendering may interpolate and display reconciliation corrections.
- Client rendering must not become authoritative gameplay state.
- Avoid hiding simulation problems behind rendering smoothing unless diagnostics still expose the correction.

## Implementation workflow

Before changing client/native/rendering code:

- Use `royale-pm-workflow` to confirm the selected PM task.
- Use `royale-architecture-boundaries` when dependency direction or project structure changes.
- Inspect nearby rendering/native code and existing tests.
- Identify whether the work is rendering-only, platform/native, input, prediction display, diagnostic UI, or packaging.

While implementing:

- Keep rendering abstractions game-specific and inspectable.
- Avoid broad engine systems unless they directly serve the selected task.
- Do not add server references to client UI/rendering packages.
- Keep unsafe/native interop narrow and tested.
- Keep user-controlled data escaped in any UI or generated HTML.

After implementation:

- Use `royale-build-validation` to run relevant build/tests.
- Add or update tests for native layouts, packaging expectations, or deterministic logic where possible.
- Update wiki pages if rendering architecture, debug workflow, native dependency layout, shader workflow, build/packaging, or platform support changed.
- Request human validation for visual appearance, input feel, camera feel, combat feel, platform-specific behavior, audio/visual feedback, or UI/debug tooling.
