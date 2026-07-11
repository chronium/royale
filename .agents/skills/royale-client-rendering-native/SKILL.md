---
name: royale-client-rendering-native
description: Implement or review Royale client platform, rendering, and native integration. Use for SDL3, SDL GPU, ImGui SDL3_GPU backend, shaders, cameras, meshes, text, debug drawing, screenshots, input capture, native bindings, or visual validation.
---

# Royale Client Rendering And Native Integration

## Boundaries

- Rendering and platform code are client-only.
- Keep renderer abstractions thin and game-specific.
- Do not introduce render graphs, material graphs, generalized scene systems, or editors without a concrete task.
- ImGui is development tooling, not the final player-facing UI.
- Rendering may smooth presentation but must not hide authoritative/prediction divergence from diagnostics.

## Established Stack

- SDL3 owns windowing, events, relative mouse mode, high-DPI behavior, and SDL GPU device lifecycle.
- ImGui must use the SDL3 event path and SDL3_GPU renderer backend.
- `shadercross` on `PATH` compiles HLSL for current backends; do not create an unapproved DXIL workflow.
- Blurg handles non-ImGui game/world text.
- SimpleMesh handles model loading and build-time mesh/collision asset work.
- Box3D debug geometry should flow through the actual debug adapter where supported.

Keep native calls narrow, validate return/error paths, preserve ownership/disposal, and test C# memory layouts at ABI boundaries.

## Input And Frame Rules

- Separate event polling, fixed simulation/prediction, variable rendering, and presentation.
- Respect ImGui input capture, gameplay input ownership, and free-camera/gameplay mode separation.
- Use SDL timing APIs in the SDL client path; do not add unrelated server graphics/platform dependencies.

## Validation

Add deterministic tests for transforms, selection/routing, serialization, input ownership, and resource lifecycle where possible. Native or packaging work also requires artifact/runtime validation.

Rendering appearance, camera/input feel, UI behavior, platform integration, and audiovisual feedback require owner validation after automated checks. Use screenshot tooling for agent-side visual evidence when available, but do not treat it as a substitute for requested human feel checks.

Update the wiki when rendering architecture, shader/native workflow, debug controls, keybindings, asset handling, or platform behavior changes.
