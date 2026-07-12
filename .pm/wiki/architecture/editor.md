---
title: Game and Map Editor
createdAt: 2026-07-11T18:49:21.0208000Z
modifiedAt: 2026-07-12T09:17:11.7923570Z
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

A second owner feel pass superseded the 10% trial: final candidate tuning is 10.8 m/s per wheel notch with a 21.6 m/s accumulated cap. Damping remains unchanged.

### Asset Browser

`EDITOR-022` replaces the manifest name list with a read-only icon grid. The browser builds deterministic model entries from the generated `ModelAssetManifest`, orders them by stable asset ID, and retains their render and collision classification. A compact case-insensitive search filters IDs without reordering them. One render-capable asset may be selected by ID; selection survives filtering and panel resizing and is cleared only when a reloaded manifest no longer contains that ID. Collision-only entries remain visible but disabled.

Tiles are 112 logical pixels wide with a 96×96 preview area. The grid derives its column count from the available panel width, keeps tile dimensions fixed while resizing, and always provides at least one column. Labels are clipped within the tile and expose the full ID in a tooltip. Selected, hovered, keyboard-focused, and disabled entries use distinct ImGui states.

The browser accepts a narrow preview provider that resolves an asset ID to an SDL GPU texture handle. Missing previews, collision-only assets, and unsupported future entry kinds use a neutral empty surface; no temporary file-type artwork is part of this contract. `EDITOR-023` owns lazy offscreen model rendering, image encoding, cache invalidation, asynchronous generation, and GPU preview lifetime. Filesystem navigation, import, drag-and-drop placement, and asset mutation remain outside `EDITOR-022`.

## Map Documents

`EDITOR-004` introduces one open `EditorMapDocument` at a time. The document owns the mutable `GameMap`, source path and SHA-256 fingerprint, editor-only GUID identities for every static box, static model, spawn point, loot point, navigation waypoint, and navigation link, a monotonic revision, command history, and a saved checkpoint. Editor GUIDs are never part of runtime JSON. Dirty state is derived from the current history position versus the checkpoint, so undo can return a document to clean. Completing a display-name edit in Inspector creates one command; map IDs remain read-only.

Startup accepts `--map-file <path>` for an explicit file. For `--map <id>`, the editor walks from the current directory toward the filesystem root looking for `Royale.slnx`; when found, it prefers `src/Royale.Content/Maps/<id>.json`. Otherwise it reads the packaged `maps/<id>.json` and requires Save As. Open and Save As use SDL native JSON file dialogs, whose callbacks enqueue results for processing on the editor thread. Opening a map rebuilds scene data, hierarchy/inspector content, framing, and selection-dependent state.

`MapCatalog.LoadFile` and `MapCatalog.Validate` are the common runtime/editor loading and validation APIs. Loading permits comments and trailing commas. Saving normalizes source formatting and comments into deterministic, indented camel-case UTF-8 JSON without BOM, with declaration-defined property order, retained array order, LF line endings, and one final newline.

Save validates the in-memory map, checks the current source SHA-256 fingerprint against the loaded or last-saved fingerprint, writes a uniquely named temporary file in the destination directory, flushes it to disk, reloads and validates the temporary output, and atomically moves it over the destination. Failures preserve the original and remove the temporary file. Save As requires a `.json` filename whose stem exactly matches the unchanged runtime map ID. Packaged origins cannot be saved in place.

The title is `Royale Editor - <filename>` with `*` while dirty. File provides Open, Save, and Save As; Edit provides Undo and Redo. Shortcuts are Cmd/Ctrl+O, Cmd/Ctrl+S, Cmd/Ctrl+Shift+S, Cmd/Ctrl+Z, and Cmd/Ctrl+Shift+Z. Open and desktop close requests with unsaved changes show Save / Discard / Cancel. A failed save leaves the document open and dirty and reports the error in Validation and Log. New remains disabled until a default-new-map contract is defined.

`BUG-012` defines display-name completion as either Enter submission or deactivation after editing, so clicking elsewhere commits one document command and immediately updates dirty state. Cmd/Ctrl document shortcuts are resolved from SDL key events and consumed before ImGui text editing; Undo and Redo therefore operate on document history consistently regardless of text-field focus.

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