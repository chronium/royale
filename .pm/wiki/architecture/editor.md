---
title: Game and Map Editor
createdAt: 2026-07-11T18:49:21.0208000Z
modifiedAt: 2026-07-12T08:11:06.1194700Z
---

## Purpose

Royale will provide a standalone, ImGui-based map editor for authoring the existing runtime map format. The editor exists to make Royale maps easier for humans and agents to build; it is not a general-purpose engine or scene editor.

The first usable editor targets macOS ARM64. Shared graphical code should remain portable so Linux support can be validated later.

## Graphical Process Boundaries

The graphical applications are:

```text
Royale.Client
  -> Royale.Platform
  -> Royale.Rendering

Royale.Editor
  -> Royale.Platform
  -> Royale.Rendering
  -> Royale.Content
  -> Royale.Simulation
  -> Royale.Box3D
```

`Royale.Platform` owns reusable SDL window, event, input plumbing, timing, and desktop application lifecycle behavior.

`Royale.Rendering` owns reusable SDL GPU device and render-target management, cameras, mesh and material resources, debug primitives, ImGui SDL3_GPU integration, GPU readback, and screenshots. It supports swapchain and offscreen targets without becoming a general scene framework.

Client networking, gameplay presentation, telemetry, and client-specific UI remain in `Royale.Client`. The server and shared simulation must not depend on Platform, Rendering, ImGui, SDL windowing, or the editor.

### Implemented Platform Lifecycle

`EDITOR-001` established `Royale.Platform` as the shared SDL desktop boundary. It owns SDL/native initialization, checked window ownership, logical and pixel size refresh, relative mouse mode, per-frame input transitions, performance-counter frame timing, bounded fixed-tick scheduling, exit requests, and SDL shutdown. Native binary packaging remains with executable projects.

Graphical applications implement `ISdlDesktopApplication`. `SdlDesktopHost` runs callbacks in this order: measure frame time; begin the input frame; poll events; refresh window size and process close requests; forward every SDL event; run variable update; run monotonically numbered bounded fixed updates; render; and apply the configured SDL idle delay. Applications request shutdown through `SdlDesktopHost.RequestExit()` and dispose their ImGui, GPU, gameplay, and networking resources before disposing the host.

### Implemented Rendering Boundary

`EDITOR-002` established `Royale.Rendering`. It references only `Royale.Platform`, `Royale.Content`, `Royale.Native`, SDL3-CS, ImGui.Net, BlurgText, and SimpleMesh; it does not depend on client, server, simulation, protocol, network, or Box3D projects.

`RenderFrame` supplies the current camera, static scene, render-view mode, debug primitives, world text, and clear color for each draw. `SdlGpuDevice` no longer owns an immutable map scene. Static geometry, material textures, samplers, and pipelines are cached independently of instance transforms, so a caller can replace scenes or instance lists without recreating the device. Resources remain resident until device disposal for the initial implementation.

`SdlGpuDevice.PresentFrame` renders to the swapchain. `CreateOffscreenTarget` and `RenderOffscreen` use resizable owned color/depth textures in the swapchain-compatible color format. Offscreen color textures include sampler usage, and `SdlGpuOffscreenTarget.NativeTextureHandle` exposes the SDL GPU texture pointer for the later editor viewport. Multi-viewport ImGui remains disabled.

Both target paths can return `GpuImageReadback`. Eight-bit BGRA and RGBA source textures are normalized to RGBA bytes before screenshot or image consumers receive them. BMP encoding remains reusable in `Royale.Rendering`.

The low-level `SdlGpuImGuiBackend` owns context/native resolver setup, SDL event forwarding, capture state, frame setup, SDL GPU draw submission, and disposal. `SdlGpuImGuiSettings` leaves docking off by default and permits the editor to opt in. Client telemetry, player controls, and training-dummy windows remain in `ImGuiDebugOverlay`.

Shader HLSL sources and compilation now belong to `Royale.Rendering`; generated SPIR-V, MSL, and HLSL files are copied into the client executable output. Native SDL, ImGui, and Blurg binary packaging remains executable/test-host responsibility.

## Editor Workspace

`EDITOR-003` provides the first standalone macOS ARM64 `Royale.Editor` executable. It depends on Platform, Rendering, Content, and Diagnostics, but not Client, Server, Protocol, Network, Simulation, or Box3D. It opens `graybox` by default and accepts `--map <id>`, `--screenshot <path>`, `--screenshot-after-frames <count>`, and `--reset-layout`.

The editor enables ImGui docking while leaving multi-viewport disabled. Its first-run or reset layout places Hierarchy on the left, Inspector on the right, Asset Browser/Validation/Log as bottom tabs, and Viewport in the center. File contains Exit while document commands are disabled. View toggles panels; Window resets the default layout. ImGui persists docking state at the per-user application-data path `Royale/Editor/imgui.ini`; no layout state is written into the repository.

The shell is read-only. Hierarchy lists static boxes, static models, spawn points, loot points, and navigation nodes. Inspector summarizes map identity, bounds, safe zone, and counts. Asset Browser lists manifest IDs and render availability. Validation reports successful runtime content loading. Log keeps a bounded editor message stream while console logging remains active.

The central viewport displays the selected map through a resizable SDL GPU offscreen target. Logical ImGui size is converted to framebuffer pixels using the high-DPI scale and target recreation is suppressed unless pixel dimensions change. The camera is initially framed from map bounds with a far plane derived from map extent. Hold right mouse over the viewport for relative-mouse look; while captured, use WASD horizontally and Q/E down/up. Release right mouse or press Escape to release capture. Full editor screenshots capture the composed docked UI and exit after completion.

Map mutation, persistence, selection/picking, asset thumbnails, drag-and-drop placement, ImGuizmo transforms, undo/redo, and Save/Save As remain deferred to later editor tasks.

`EDITOR-017` gives captured viewport camera input exclusive pointer ownership. Capture begins only while the visible, focused viewport is hovered with right mouse held. During capture, ImGui's global mouse input is suppressed while SDL events continue to reach its backend; relative mouse mode hides the cursor. Right-mouse release, Escape, viewport closure, focus loss, and editor disposal immediately restore normal cursor and ImGui interaction.

`EDITOR-018` makes viewport navigation editor-specific. While right-mouse capture is active, W/S fly along the full pitched view direction, A/D strafe camera-relative, and Q/E move along world down/up. Combined movement is normalized at 6 m/s; either Shift key boosts it to 18 m/s. While the visible, focused viewport is hovered, wheel input is consumed from ImGui and adds a signed 36 m/s view-direction dolly impulse, clamped to 72 m/s and exponentially damped with a 0.12-second half-life. SDL flipped-wheel direction is normalized. Dolly momentum may finish after hover or visibility ends, but focus loss cancels it.

Owner feel testing reduced the final wheel-dolly tuning to 10% of the initial scale: each vertical notch adds a signed 3.6 m/s impulse and accumulated dolly velocity is clamped to 7.2 m/s. The 0.12-second half-life is unchanged.

## Map Documents

The editor loads and writes the existing `GameMap` JSON format. An editor-only mutable document model may retain stable entity identities and command history, but editor metadata must not leak into runtime map JSON.

The complete current map schema is editable:

- Static boxes and static models
- Spawn and loot points
- Navigation nodes and links
- World bounds
- Safe-zone settings

Documents track dirty state and use command-based undo and redo. Save and Save As are explicit operations; the editor does not continuously autosave.

Before saving, the editor runs runtime-equivalent structural, asset, navigation, spawn, bounds, and collision validation. Invalid documents remain dirty and are not written. Writes use a temporary file and atomic replacement. If the source changed externally after loading, the editor rejects the save instead of overwriting newer content.

## Face Snapping

Face snapping is mandatory for the first editor.

The user enters face-snap mode and selects a target collision surface. The editor places the selected object's oriented bounds flush against the hit plane and ignores the selected object's own collider.

Rotation is preserved by default. An optional alignment mode rotates a selected local attachment axis to the target surface normal. The editor displays a preview before commit, and the final placement is one undoable command.

Face snapping is bounds-based for the initial version; it does not promise arbitrary mesh-to-mesh feature matching.

## Playtesting

The initial editor does not embed a playable simulation. Save and Launch validates and saves the map, then starts the normal development server and client using existing launch profiles and the selected map.

## Deferred Capabilities

Physics-assisted placement is planned for decorative objects. It will temporarily simulate selected objects with Box3D using fixed ticks and settling thresholds, then bake stable transforms back into static map data as one cancellable undoable action.

WattleScript map behavior is also deferred. Future work may add authoritative scripted doors, buttons, lights, and other interactions. Script ownership must preserve server authority.