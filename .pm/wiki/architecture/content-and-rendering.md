---
title: Content and Rendering
createdAt: 2026-07-05T16:11:12.3546390Z
modifiedAt: 2026-07-06T18:44:04.8470530Z
---

## Content and Map Data

Shared map content lives in `Royale.Content`. The default map id is `graybox` via `ContentCatalog.DefaultMapId` and `MapCatalog.DefaultMapId`.

The committed default map file is `src/Royale.Content/Maps/graybox.json`. The content project copies map JSON files to runtime output under `maps/`, so client and server consumers load the same copied file shape from `AppContext.BaseDirectory/maps/<map-id>.json`.

`MapCatalog.LoadById()` accepts simple ASCII map ids using letters, digits, `-`, and `_`, then loads and validates the matching JSON file. Missing files fail with a clear `FileNotFoundException`; malformed JSON or invalid map shape fails with `InvalidDataException`.

The current `graybox` map uses a larger horizontal test arena than the original M1 blockout. Its gameplay bounds are `-24..24` on X/Z, the safe-zone placeholder radius is `20`, and the rendered/collidable floor is `20 x 20` metres with perimeter walls at approximately `+/-9.9` metres. Individual cover, wall, step, platform, and ramp object sizes remain authored at their original scale. Most X/Z positions were doubled to create more walking room, while the step/ramp/platform assembly was translated as one unit so its internal spacing and ramp approach remain unchanged.

The current minimal schema is:

```json
{
  "id": "graybox",
  "name": "Gray-Box Test Arena",
  "worldBounds": {
    "min": { "x": -24.0, "y": -1.0, "z": -24.0 },
    "max": { "x": 24.0, "y": 5.0, "z": 24.0 }
  },
  "safeZone": {
    "center": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "radius": 20.0
  },
  "spawnPoints": [
    {
      "id": "spawn-north-west",
      "position": { "x": -7.6, "y": 0.0, "z": -7.6 },
      "rotationEuler": { "x": 0.0, "y": 45.0, "z": 0.0 }
    }
  ],
  "lootPoints": [
    {
      "id": "loot-center",
      "position": { "x": 0.0, "y": 0.35, "z": 0.0 }
    }
  ],
  "staticBoxes": [
    {
      "id": "ground-main",
      "position": { "x": 0.0, "y": -0.12, "z": 0.0 },
      "size": { "x": 20.0, "y": 0.24, "z": 20.0 },
      "rotationEuler": { "x": 0.0, "y": 0.0, "z": 0.0 }
    }
  ]
}
```

Static boxes define shared map data used by both client-rendered gray-box geometry and simulation static collision. Their `position`, `size`, and human-editable `rotationEuler` values are converted through the shared `MapStaticBoxTransforms` helper so rendering and collision use the same transform convention.

Client rendering treats each static box as a centered unit-box mesh scaled by `size`, rotated by yaw/pitch/roll Euler angles, and translated by `position`. `GAME-002` uses the same data in `Royale.Simulation` to create one Box3D static body per static box and one box hull shape per body, with hull half-extents of `size / 2`. Shape ids are associated back to source static-box ids for tests and debugging.

Spawn points are gameplay content, not placeholders. `MapCatalog` requires at least one spawn point, each spawn id must be non-empty and unique, and every spawn position must be inside `worldBounds`. `MapSpawnPoint.Position` is the player feet anchor. `GAME-007` uses spawn points in `Royale.Simulation` for deterministic first-valid selection with static-collision clearance checks and caller-provided occupancy reservations; it does not yet integrate spawning into client/server match flow.

Loot points and safe-zone fields are still placeholders at this stage and do not yet drive gameplay behavior. Static map collision and spawn selection exist, but the map content does not yet implement loot collision, safe-zone simulation, mesh collision, height fields, dynamic bodies, or protocol compatibility behavior.

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
* Camera matrices
* Basic lighting
* Debug geometry
* ImGui rendering

The current static mesh renderer owns one SDL GPU pipeline and shader pair, uploads a built-in unit box mesh once, and draws static map geometry by pushing per-instance vertex constants for each `StaticMeshInstance`. The vertex constants contain the world-view-projection matrix and world-inverse matrix so static mesh normals can be transformed for world-space lighting. The client loads the selected map id from `ClientLaunchOptions.MapId`, reads the copied JSON through `MapCatalog`, and converts each `staticBoxes` entry into a render instance. It is not a scene graph, ECS, material system, mesh asset loader, culling system, batching system, or instancing API.

The built-in static box mesh stores per-face outward unit normals instead of debug colors. Static gray-box geometry uses a fixed neutral gray albedo and a simple flat lighting model in the basic shader: ambient intensity `0.35` plus one normalized down-diagonal directional diffuse light with intensity `0.65`. The renderer pushes fixed fragment lighting constants for that albedo and light. This is intentionally not a material system, render graph, shadow system, or per-instance lighting API.

Rendering consumes a small `RenderCamera` value containing position, yaw, pitch, field of view, near plane, and far plane. Projection aspect ratio comes from the acquired swapchain pixel dimensions, with zero width or height falling back to a safe 1:1 aspect ratio.

Gameplay view and freecam are separate client presentation modes. The client starts in gameplay view, `F2` toggles between gameplay view and freecam, `F1` remains the explicit SDL relative mouse capture toggle, and `Escape` releases capture before quitting. Freecam movement is only applied while freecam mode is active.

The debug camera remains a free-fly controller that can produce a `RenderCamera`. It starts at approximately `(2.8, 2.1, 2.8)`, looks toward the origin, uses `W/A/S/D` for horizontal local movement, `Space` for up, `Left Ctrl` for down, and rotates from mouse deltas only while SDL relative mouse mode is enabled. This camera is renderer/debug presentation state and is not the gameplay first-person controller or a server-authoritative player view.

Gameplay view now renders from a local offline player capsule instead of a temporary fixed camera anchor. `LocalPlayerController` selects a valid spawn with `MapSpawnSelector`, owns a client-side `MapStaticCollisionWorld`, advances `KinematicCharacterController` during fixed gameplay ticks, and keeps the `PlayerLookState` updated from mouse input while gameplay mode is active. `GameplayView` creates the first-person `RenderCamera` from the capsule feet anchor plus `PlayerViewSettings.DefaultEyeHeight` (`1.62` metres), using the local gameplay yaw and pitch.

The local player capsule is client-owned presentation and prediction state for offline startup. It does not add protocol messages, authoritative server player state, combat, snapshots, reconciliation, or networking behavior.

ImGui uses Evergine's generated ImGui.Net/cimgui bindings plus a project-owned native `royale_imgui` shim. The shim is built with Dear ImGui's SDL3 platform backend and SDL_GPU renderer backend.

UI-001 owns ImGui context lifetime, SDL3 backend lifetime, SDL_GPU backend lifetime, SDL event forwarding, display size/framebuffer scale/delta-time updates, and frame begin. UI-002 owns overlay construction and draw-data submission.

Active ImGui rendering uses this SDL GPU ordering:

1. Poll SDL events and forward them to ImGui.
2. Begin the ImGui frame after event polling.
3. Build the debug overlay window before rendering.
4. Acquire the SDL_GPU command buffer and swapchain texture.
5. Render scene geometry in the depth-enabled main pass.
6. Call ImGui render to produce draw data.
7. Call `ImGui_ImplSDLGPU3_PrepareDrawData()` before beginning the render pass that will draw ImGui.
8. Begin a color-only ImGui render pass over the swapchain texture with load `LOAD` and store `STORE`.
9. Call `ImGui_ImplSDLGPU3_RenderDrawData()` inside that SDL_GPU render pass.
10. End the ImGui render pass.
11. Perform screenshot readback after ImGui rendering when screenshot mode is active.
12. Submit the command buffer.

The initial debug overlay window is titled `Royale` and shows frame delta/FPS, fixed ticks this frame, total fixed tick, mouse capture state, and current render view mode. It intentionally does not expose gameplay controls.

`RENDER-006` adds the first debug primitive rendering path. `DebugPrimitiveList` is a project-owned CPU line list for game-specific debug visuals such as the local player capsule, spawn markers, safe-zone boundary, and future ray/contact markers. `Box3DDebugDrawAdapter` calls `b3World_Draw` for the local static collision world and converts Box3D debug shapes and callbacks into the same line list.

`DebugLineRenderer` owns a separate SDL GPU line-list pipeline and `debug_line` shader pair. It uploads frame-local line vertices before the main render pass, then draws the line list after static solids inside the main pass. Its pipeline keeps the depth target attached but disables depth testing and depth writes so debug lines remain visible through solid gray-box geometry when that mode is active.

`RENDER-007` adds client render view modes through `RenderViewModeController`. Startup defaults to `WorldAndDebug` during the debug-heavy M1 milestone. `F5` selects normal world solids only, `F6` selects world solids plus debug wireframes, `F7` selects debug wireframes over a cleared frame, and `F8` selects solid collision-world rendering. These hotkeys are global controls like `F1`, `F2`, and `Escape`, so they remain available while ImGui has keyboard capture.

`CollisionSolids` does not derive filled geometry from Box3D debug callbacks. It renders the same `GameMap.StaticBoxes` source data used to create the local `MapStaticCollisionWorld`, preserving the shared `position`, `size`, and yaw/pitch/roll transform convention. The active render view mode is exposed from `SdlApplication`, included in the debug overlay, and appended to the diagnostic window title.

`RENDER-008` adds the first game-facing text path outside ImGui. `BlurgTextRenderer` owns BlurgText lifetime, enables system fonts, resolves a default font from a conservative family list (`SF Pro`, `DejaVu Sans`, `Noto Sans`, `Liberation Sans`, `Segoe UI`, `Arial`), and fails clearly if no default font can be resolved. Blurg atlas allocation/update callbacks create SDL GPU RGBA atlas textures and upload changed atlas rectangles through transfer buffers. `TextQuadRenderer` draws Blurg rectangles as screen-space textured quads with alpha blending, no depth testing, and no depth writes.

The current smoke draw is a fixed `Royale BlurgText` label at the top-left of the frame, drawn through BlurgText and SDL GPU before ImGui. It is intentionally not a retained UI framework, HUD layout system, world-space billboard system, health bar implementation, bundled font asset, or server dependency. ImGui remains diagnostics-only development tooling.

`RENDER-009` extends the Blurg text path with rendering-owned world-space text billboards. `WorldTextBillboard` values carry text, world position, world-unit height, anchor, foreground and shadow colors, and either camera-facing or fixed-facing mode. Camera-facing labels derive their right/up basis from the active `RenderCamera`; fixed-facing labels preserve an authored world basis supplied by the caller.

World text remains client/rendering presentation state. It is projected on the CPU from Blurg glyph rectangles into arbitrary screen-space textured quads, then batched through the existing `TextQuadRenderer`. The pass is color-only with no depth testing or depth writes, so labels render as readable overlays after world/debug geometry and before ImGui. Depth-tested, raycast-occluded, replicated player-name, health-bar, loot UI, HUD-layout, and server-owned label behavior are intentionally deferred.

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
7. Draw BlurgText screen-space and world-positioned text in a color-only pass with the swapchain texture loaded.
8. Render ImGui to produce draw data.
9. Prepare ImGui draw data for SDL_GPU.
10. Begin the ImGui render pass with the swapchain texture loaded.
11. Render ImGui draw data.
12. End the ImGui render pass.
13. Submit the command buffer.

For render validation, the client supports a development screenshot mode:

```text
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 -- --screenshot /tmp/royale-frame.bmp --screenshot-after-frames 5
```

The screenshot path captures the presented swapchain frame through SDL GPU readback after BlurgText and ImGui rendering, writes a BMP, and exits the client after the requested frame.

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
