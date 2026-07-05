---
title: Content and Rendering
createdAt: 2026-07-05T16:11:12.3546390Z
modifiedAt: 2026-07-05T18:34:22.3248970Z
---

## Content and Map Data

The first map should use a deliberately simple format.

A basic JSON file may define:

* Static boxes
* Static mesh references
* Spawn points
* Loot points
* World bounds
* Initial safe-zone centre
* Initial safe-zone radius

For example:

```json
{
  "name": "test-map",
  "spawnPoints": [
    { "position": [0, 2, 0], "rotation": 0 },
    { "position": [20, 2, 20], "rotation": 180 }
  ],
  "staticBoxes": [
    {
      "position": [0, -0.5, 0],
      "size": [100, 1, 100]
    }
  ],
  "lootPoints": []
}
```

Client and server should load the same gameplay-relevant map data.

Rendering-only data may remain client-specific.

The server should not need textures or shader assets.

## Rendering Architecture

The renderer should remain thin and specific.

Initial responsibilities include:

* SDL GPU device management
* Swapchain handling
* Shader loading
* Buffer creation
* Texture creation
* Static mesh rendering
* Camera constants
* Basic lighting
* Debug geometry
* ImGui rendering

ImGui uses Evergine's generated ImGui.Net/cimgui bindings plus a project-owned native `royale_imgui` shim. The shim is built with Dear ImGui's SDL3 platform backend and SDL_GPU renderer backend.

UI-001 owns ImGui context lifetime, SDL3 backend lifetime, SDL_GPU backend lifetime, SDL event forwarding, display size/framebuffer scale/delta-time updates, and frame begin/end. It does not submit ImGui draw data.

UI-002 owns draw-data submission. The required SDL_GPU ordering is:

1. Call ImGui render to produce draw data.
2. Call `ImGui_ImplSDLGPU3_PrepareDrawData()` before beginning the render pass that will draw ImGui.
3. Call `ImGui_ImplSDLGPU3_RenderDrawData()` inside that SDL_GPU render pass.

## Shader Build Pipeline

Client shader sources live under `src/Royale.Client/Shaders/` as HLSL files using stage suffixes:

* `.vert.hlsl`
* `.frag.hlsl`
* `.comp.hlsl`

The client build requires `shadercross` to be available on `PATH`. After `Royale.Client` builds, MSBuild compiles each shader source to SPIR-V (`.spv`) and Metal (`.msl`) under the client output `shaders/` directory, preserving recursive shader folders. The original HLSL source is also copied to the same output tree for Direct3D/DXIL-facing development until a specific DXIL output flow is chosen.

`SDL_shadercross` is not vendored through `thirdparty`; it is treated as an external local build tool dependency.

## Render Sequence

A simple render sequence is sufficient:

1. Acquire the swapchain texture.
2. Begin the main render pass.
3. Draw static geometry.
4. Draw players and pickups.
5. Draw debug geometry.
6. End the main pass.
7. Prepare ImGui draw data for SDL_GPU.
8. Begin the ImGui render pass.
9. Render ImGui draw data.
10. End the ImGui render pass.
11. Submit the command buffer.

For render validation, the client supports a development screenshot mode:

```text
dotnet run --project src/Royale.Client/Royale.Client.csproj -- --screenshot /tmp/royale-frame.bmp --screenshot-after-frames 5
```

The screenshot path captures the presented swapchain frame through SDL GPU readback, writes a BMP, and exits the client after the requested frame.

There is no initial requirement for:

* Deferred rendering
* Render graphs
* Material graphs
* Dynamic global illumination
* Shadow systems
* GPU-driven culling
* Streaming terrain

## Presentation State

The client should distinguish authoritative simulation state from rendered presentation state.

For example, a remote player may have:

```text
Latest authoritative snapshot state
Previous snapshot state
Interpolated render transform
Visual animation state
Temporary effects
```

Likewise, the local player may have:

```text
Authoritative server state
Predicted simulation state
Smoothed render state
Camera state
Weapon visual state
```

This distinction prevents rendering concerns from contaminating gameplay authority.
