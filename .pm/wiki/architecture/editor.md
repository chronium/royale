---
title: Game and Map Editor
createdAt: 2026-07-11T18:49:21.0208000Z
modifiedAt: 2026-07-13T11:40:54.5795800Z
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

Map persistence, selection/picking, asset thumbnails, ImGuizmo transforms, undo/redo, and Save/Save As are now implemented by their owning editor tasks. Drag-and-drop placement, creation, deletion, duplication, and general entity property editing remain deferred.

`EDITOR-017` gives captured viewport camera input exclusive pointer ownership. Capture begins only while the visible, focused viewport is hovered with right mouse held. During capture, ImGui's global mouse input is suppressed while SDL events continue to reach its backend; relative mouse mode hides the cursor. Right-mouse release, Escape, viewport closure, focus loss, and editor disposal immediately restore normal cursor and ImGui interaction.

`EDITOR-018` makes viewport navigation editor-specific. While right-mouse capture is active, W/S fly along the full pitched view direction, A/D strafe camera-relative, and Q/E move along world down/up. Combined movement is normalized at 6 m/s; either Shift key boosts it to 18 m/s. While the visible, focused viewport is hovered, wheel input is consumed from ImGui and adds a signed 36 m/s view-direction dolly impulse, clamped to 72 m/s and exponentially damped with a 0.12-second half-life. SDL flipped-wheel direction is normalized. Dolly momentum may finish after hover or visibility ends, but focus loss cancels it.

Owner feel testing reduced the final wheel-dolly tuning to 10% of the initial scale: each vertical notch adds a signed 3.6 m/s impulse and accumulated dolly velocity is clamped to 7.2 m/s. The 0.12-second half-life is unchanged.

A second owner feel pass superseded the 10% trial: final candidate tuning is 10.8 m/s per wheel notch with a 21.6 m/s accumulated cap. Damping remains unchanged.

### Asset Browser

`EDITOR-022` replaces the manifest name list with a read-only icon grid. The browser builds deterministic model entries from the generated `ModelAssetManifest`, orders them by stable asset ID, and retains their render and collision classification. A compact case-insensitive search filters IDs without reordering them. One render-capable asset may be selected by ID; selection survives filtering and panel resizing and is cleared only when a reloaded manifest no longer contains that ID. Collision-only entries remain visible but disabled.

Tiles are 112 logical pixels wide with a 96×96 preview area. The grid derives its column count from the available panel width, keeps tile dimensions fixed while resizing, and always provides at least one column. Labels are clipped within the tile and expose the full ID in a tooltip. Selected, hovered, keyboard-focused, and disabled entries use distinct ImGui states.

The browser accepts a narrow preview provider that resolves an asset ID to an SDL GPU texture handle. Missing previews, collision-only assets, and unsupported future entry kinds use a neutral empty surface; no temporary file-type artwork is part of this contract. `EDITOR-023` owns lazy offscreen model rendering, image encoding, cache invalidation, asynchronous generation, and GPU preview lifetime. Filesystem navigation, import, drag-and-drop placement, and asset mutation remain outside `EDITOR-022`.

`EDITOR-023` supplies model previews only for active `.royaleproject` sessions; standalone JSON documents keep placeholders. The provider lazily queues each visible render asset once, frames combined primitive bounds with a 60-degree camera on the elevated negative-X/negative-Z diagonal and 15% padding so directional model fronts face the viewer, renders a 256×256 identity-transform scene on opaque neutral gray, and preserves the model's materials and directional lighting.

Work is bounded per frame to one render submission, one completed fence readback, and one cached-image upload so ImGui interaction does not wait for GPU completion or PNG encoding. Valid previews are cached under `.cache/thumbnails/<asset-id>-<full-sha256>.png`. The fingerprint covers the renderer version and framing settings, asset/render definition, source path and bytes, and sorted resource paths and bytes. Corrupt or incorrectly sized caches are removed and regenerated; stale files are removed only after a valid replacement exists. Preview failures remain placeholders and are suppressed until project reload or fingerprint change. Provider resources are disposed before project/document replacement and editor shutdown; generated client/server artifacts do not consume the cache.

`EDITOR-024` defines project assets as a physical tree rooted at the package `assets/` directory. Scans do not follow symbolic links. Registered render sources retain their stable global asset ID and thumbnail association; directories and other files use neutral placeholders. Navigation state uses contained portable relative paths, breadcrumbs, deterministic folder-first sorting, and recursive path/ID search.

Project folder names use lowercase ASCII letters, digits, `-`, and `_`. Create rejects existing paths. Rename and move reject merges, rewrite affected render sources/resources and separate collision sources, and rebuild both audience outputs. Delete is restricted to empty, unreferenced folders. Generated content and thumbnail caches remain outside `assets/`.

The `EDITOR-024` UI presents the physical folder hierarchy beside the selected folder's icon grid. Its toolbar provides recursive search, path breadcrumbs, multi-file `Import Assets...`, and folder commands. Import rows retain inclusion state, editable globally unique IDs, external-resource diagnostics, and one of None, Convex, Triangle Mesh, or Separate Mesh collision; Separate Mesh requires a row-associated GLB. Validation keeps the modal open, and a successful batch reloads the manifest, source mesh cache, scene, browser state, selection where its path survives, and thumbnail provider without placing models in the map.

Folder commands operate on physical project folders. Create and rename require portable lowercase names; move accepts another contained folder and rejects merges; delete confirms its restricted empty/unreferenced behavior through the operation label and backend validation. All manifest-changing operations run through the project session so external-change fingerprints and loaded project state remain current.

## Map Documents

`EDITOR-004` introduces one open `EditorMapDocument` at a time. The document owns the mutable `GameMap`, source path and SHA-256 fingerprint, editor-only GUID identities for every static box, static model, spawn point, loot point, navigation waypoint, and navigation link, a monotonic revision, command history, and a saved checkpoint. Editor GUIDs are never part of runtime JSON. Dirty state is derived from the current history position versus the checkpoint, so undo can return a document to clean. Completing a display-name edit in Inspector creates one command; map IDs remain read-only.

Startup accepts `--map-file <path>` for an explicit file. For `--map <id>`, the editor walks from the current directory toward the filesystem root looking for `Royale.slnx`; when found, it prefers `src/Royale.Content/Maps/<id>.json`. Otherwise it reads the packaged `maps/<id>.json` and requires Save As. Open and Save As use SDL native JSON file dialogs, whose callbacks enqueue results for processing on the editor thread. Opening a map rebuilds scene data, hierarchy/inspector content, framing, and selection-dependent state.

`MapCatalog.LoadFile` and `MapCatalog.Validate` are the common runtime/editor loading and validation APIs. Loading permits comments and trailing commas. Saving normalizes source formatting and comments into deterministic, indented camel-case UTF-8 JSON without BOM, with declaration-defined property order, retained array order, LF line endings, and one final newline.

Save validates the in-memory map, checks the current source SHA-256 fingerprint against the loaded or last-saved fingerprint, writes a uniquely named temporary file in the destination directory, flushes it to disk, reloads and validates the temporary output, and atomically moves it over the destination. Failures preserve the original and remove the temporary file. Save As requires a `.json` filename whose stem exactly matches the unchanged runtime map ID. Packaged origins cannot be saved in place.

The title is `Royale Editor - <filename>` with `*` while dirty. File provides Open, Save, and Save As; Edit provides Undo and Redo. Shortcuts are Cmd/Ctrl+O, Cmd/Ctrl+S, Cmd/Ctrl+Shift+S, Cmd/Ctrl+Z, and Cmd/Ctrl+Shift+Z. Open and desktop close requests with unsaved changes show Save / Discard / Cancel. A failed save leaves the document open and dirty and reports the error in Validation and Log.

`DEBT-009` routes New Project, Open Project, Open Map JSON, Convert Map to Project, and Close through a deterministic `EditorDocumentWorkflow` owned by the Documents domain. The workflow is idle, awaiting an unsaved decision, or awaiting save completion; it tells the application to show the prompt, save, continue the original transition, or do nothing. Clean documents continue immediately. For dirty documents, Save continues only after persistence succeeds, Discard continues without saving, and Cancel abandons the transition. Save failure and Save As cancellation return to the unsaved prompt with the original transition retained. Repeated requests cannot replace a transition already in progress.

SDL dialogs, ImGui popup rendering, persistence, logging, and host exit remain application concerns. New Project and Convert use separate destination-dialog state, which is cleared on cancellation, dialog error, completion, or operation failure. Cancelled or failed Open, New, and Convert operations leave the active document and its presentation resources intact.

`BUG-012` defines display-name completion as either Enter submission or deactivation after editing, so clicking elsewhere commits one document command and immediately updates dirty state. Cmd/Ctrl document shortcuts are resolved from SDL key events and consumed before ImGui text editing; Undo and Redo therefore operate on document history consistently regardless of text-field focus.

`BUG-013` treats the Inspector display-name control as a fixed 256-byte ImGui UTF-8 field: 255 payload bytes plus the required null terminator. Synchronizing a longer runtime map name preserves the authoritative `GameMap.Name` and presents only the longest prefix composed of complete UTF-8 sequences. Inspector and Validation warn that the source name is preserved and that editing and committing the field will replace it with the visible prefix. Loading, undo/redo synchronization, focusing, or leaving the field without editing does not create a rename command. The limit belongs only to this editor control; runtime map loading and `SetMapNameCommand` remain unrestricted.

## Complete Map Schema Editing

`EDITOR-006` makes every existing `GameMap` field authorable without changing the runtime JSON schema. Hierarchy groups provide Add for static boxes, static models, spawn points, loot points, navigation waypoints, and navigation links. Links are hierarchy-selectable but remain non-spatial and never receive a transform gizmo. A selected spatial entity can be duplicated in place; navigation links cannot be duplicated because an exact copy would violate undirected-link uniqueness. Delete always confirms, and waypoint confirmation reports the number of incident links that will also be removed.

The Inspector edits box ID/position/rotation/size; model ID/asset/position/rotation/scale; spawn ID/position/rotation; loot ID/position; waypoint ID/position; link endpoints; map display name; world bounds; and safe-zone centre/radius. The runtime map ID remains read-only. A completed text, vector, numeric, combo, or drag interaction contributes one document command. Blank or duplicate IDs, non-finite values, non-positive box sizes and safe-zone radii, zero model scales, unknown or identical link endpoints, and duplicate undirected links are rejected without entering history.

Structural commands preserve editor GUIDs and array order across undo/redo while reindexing identities after insertion and removal. Static boxes and models share their runtime-ID namespace. Generated IDs are lowercase portable slugs and use `-2`, `-3`, and later suffixes. Duplication copies every authored property but assigns a new runtime ID and editor GUID. Waypoint rename rewrites all referencing link endpoints in the same command. Waypoint deletion removes incident links in the same command; undo restores the waypoint, links, order, and original GUIDs. Newly added or duplicated entities become selected and rebuild scene and picking presentation immediately.

The Asset Browser exposes `Place Selected` for render-capable model entries and starts a model drag payload from render-capable tiles. A viewport drop resolves the cursor ray against the `Y = 0` grid plane; button and hierarchy placement use the viewport-centre ray. Placement uses active translation snapping, clamps the result to world bounds, and falls back to the bounds centre when the ray has no forward grid-plane intersection. New models use unit scale and an asset-derived unique ID; new boxes are one-metre cubes. Other new spatial entities use the same placement resolver.

Document editing intentionally permits temporary runtime invalidity, including removing required entities, inverting bounds, or leaving navigation disconnected. After every document command, the Validation panel refreshes a lightweight `MapCatalog.Validate` attempt and shows the first current error. Save and project persistence continue to run authoritative runtime validation and reject invalid final state. Face snapping, physics-assisted placement, scripting, and advanced validation/playtest tooling remain owned by their follow-up work.

## Map Project Format

`EDITOR-020` defines one editable map project as a directory package named `<map-id>.royaleproject`. macOS will later register this directory extension as a document package; other platforms treat it as an ordinary directory. The format remains owned by `Royale.Editor` and introduces no runtime or server dependency.

```text
arena.royaleproject/
  project.json
  map/
    arena.json
  assets/
    model-assets.json
    meshes/
    textures/
  generated/
    client/
    server/
  .cache/
    thumbnails/
  .gitignore
```

`project.json` is strict, versioned JSON with `version`, `id`, `map`, and `assetManifest` fields. Version 1 uses `map/<id>.json` and `assets/model-assets.json`. The package-directory stem, project ID, map filename stem, and `GameMap.Id` must agree exactly. Project IDs are lowercase ASCII letters, digits, `-`, or `_`. Manifest paths use `/`, are relative to the project root, and cannot contain empty, `.` or `..` segments. Unsupported versions fail clearly. Version 1 has no migration; future migrations must be explicit, sequential steps rather than silent rewrites.

The project owns exactly one map. `map/`, `assets/`, `project.json`, and imported authoring sources are authoritative source-control content. An empty source model manifest is valid for box-only maps. `generated/` and `.cache/` are reproducible project-local data, may be deleted safely, and are ignored by canonical `.gitignore` entries `/generated/` and `/.cache/`. User-specific editor layout remains in OS application data.

Runtime-artifact fingerprints include source assets, the source manifest, import settings, pipeline version, and target audience. Thumbnail fingerprints include the model and its resources, preview settings, and thumbnail-renderer version.

`EDITOR-021` owns project creation/opening and compatibility import from the current standalone JSON workflow. `EDITOR-023` owns thumbnail generation and cache lifecycle. `EDITOR-025` owns runtime-only and source-inclusive exports. Existing repository maps and shared assets are not migrated by `EDITOR-020`.

Asset imports are project-only and batch-oriented. IDs are editable lowercase portable slugs and remain independent of paths. Each included GLB selects `none`, `convex`, `triangleMesh`, or `separateMesh`; separate mesh requires another GLB. External GLB resources preserve their relative topology. Duplicate IDs, existing destinations, traversal, missing resources, malformed GLB containers, and differing shared-resource bytes fail before source mutation.

Import builds staged client and server outputs first, then commits new source files, the source manifest, and generated directories under a project-local journal. Project open recovers an incomplete journal by removing moved files and restoring the manifest backup. A successful import reloads the project so manifest fingerprints and source content are current; imported models are not placed in the map automatically.

`EDITOR-024` transaction hardening supersedes the earlier file-by-file commit description: import and folder moves stage the complete source tree plus both generated audience trees. One internal journal records the staged, live, and backup directories and the completed swap checkpoint. Failure or startup recovery restores all three live trees, including failures before, during, or between generated-output swaps.

Shared external GLB resources may reuse an existing import destination only when the existing and incoming files are byte-for-byte identical. Primary model destinations and separate collision destinations remain exclusive, and differing shared-resource bytes remain a preflight error.

### Project Lifecycle

`EDITOR-021` makes `.royaleproject` packages the editor's primary authoring document while retaining standalone map JSON as a compatibility document. Startup chooses an explicit `--project`, `--map-file`, or `--map` target in that order, then the most recently opened valid project, then normal default-map resolution. `--project` is mutually exclusive with both map options. The latest successfully opened, created, or converted package is persisted atomically in `Royale/Editor/recent-project.json` under OS application data. Missing, malformed, or invalid recent state is logged and falls back to the default map.

New Project collects a lowercase project ID and display name and creates `<id>.royaleproject` under a selected parent. Creation stages the entire package in a temporary sibling, validates it with `RoyaleProjectLoader`, and renames it into place without merging or overwriting. The starter arena contains a 20×20 metre floor, bounds `(-10,-1,-10)` to `(10,5,10)`, a radius-9 safe zone at zero, spawn points at `(-4,0,0)` and `(4,0,0)`, matching navigation waypoints and one link, and no loot or model assets. Canonical source, generated, cache, manifest, and ignore paths are created even when empty.

Convert Map to Project retains the map ID and display name. It discovers the nearest source `model-assets.json`, filters it to model IDs referenced by the map, and copies each render source, declared resource, and separate collision source using validated relative paths. Model-free maps receive an empty source manifest. Missing IDs or files fail the staged transaction; conversion does not generate runtime artifacts.

A project session owns the loaded project, mutable map document, source manifest, resolved paths, and SHA-256 fingerprints for `project.json`, the source manifest, and map. Candidate projects load and build their source-model mesh cache and scene completely before replacing the active session. Generated collision or render outputs are not required for editor presentation. The Inspector exposes project root, map, source manifest, generated client/server, and thumbnail-cache paths; the window title uses the package name and dirty marker.

Project Save is in-place only. It rejects external changes to any authoritative fingerprint, atomically saves and validates the map, reloads project metadata, and refreshes fingerprints. Standalone JSON retains Save As. New, Open Project, Open Map JSON, Convert, and Close all pass through Save / Discard / Cancel protection, and failed creation, opening, conversion, validation, or save leaves the current document intact.

## Grid and Transform Snapping

`EDITOR-005` adds one selection state keyed by the document-owned `EditorEntityIdentity.EditorId`. Hierarchy selection and viewport picking update the same state; loading another document clears it. Static boxes and models support translate, rotate, and scale; spawn points support translate and rotate; loot points and navigation waypoints support translate only. Navigation links remain non-spatial. Viewport picking uses the nearest ray hit against oriented box bounds, transformed model mesh bounds, or visible marker proxies, and does not pick through a hovered or active gizmo. The Inspector reports the selected type, display ID, position, rotation, and size or scale read-only. Creation, deletion, duplication, and editable entity properties remain deferred.

The viewport toolbar provides Translate, Rotate, Scale, Local/World, Grid, Snap, and the active increment. `W`, `E`, and `R` select transform modes while the viewport is hovered and focused, relative-mouse camera capture is inactive, no text or modal owns input, and no manipulation is active. Unsupported operations are disabled; position-only entities automatically use Translate. System.Numerics matrices are transposed at the narrow ImGuizmo adapter boundary.

A manipulation previews directly in the editor presentation without adding history. Escape restores the original transform. Mouse release rejects non-finite values, non-positive box sizes, and zero model scale; suppresses no-ops; and otherwise creates one before/after command resolved through the stable editor identity. Undo and redo restore the complete transform while preserving selection. Whole-map validation remains on normal validation and save rather than every drag.

The depth-tested construction grid covers map bounds rounded outward on the XZ plane at `Y = 0`, emphasizes both world axes and every tenth line, and uses a bounded coarser visual subdivision for exceptionally dense settings. Translation snapping continues to use the exact configured increment even when presentation is coarsened. Non-mesh spatial entities have distinct spawn, loot, and navigation markers, and the selected entity receives an orange wire-bounds highlight.

Defaults are grid visible, snapping enabled, world orientation, Translate mode, 1 metre translation/grid spacing, 15 degrees rotation, and 0.1 scale. Positive finite increments are clamped to practical UI ranges. Grid visibility, snapping, operation, orientation, and all increments are atomically persisted per user at `Royale/Editor/editor-settings.json` under OS application data. Missing, malformed, invalid, or unsupported settings fall back to defaults without preventing startup.

Validation on 2026-07-13 completed the documented formatter check, a zero-warning full solution build, and all 1,113 solution tests. Native macOS screenshot validation confirmed bundled cimguizmo startup, toolbar layout, the depth-aware grid, emphasized axes, and spatial markers. Owner validation remains required for picking accuracy, all three gizmo modes, grid readability and changed spacing, snapping behavior, and the `W`/`E`/`R` workflow.

## Face Snapping

`EDITOR-007` adds Face Snap as a viewport-toolbar mode for every selected spatial entity: static boxes, static models, spawn points, loot points, and navigation waypoints. Navigation links remain non-spatial. The editor now directly references `Royale.Simulation` and `Royale.Box3D`; this supersedes the earlier `EDITOR-003` dependency statement while preserving the prohibition on Client, Server, Protocol, and Network dependencies. Editor build and publish output includes the Box3D native library.

Entering the mode creates one retained `MapStaticCollisionWorld`. A project document first rebuilds `generated/server` from its source asset manifest through `AssetPipelineProcessor`, including valid empty manifests for box-only projects, then loads collision from that generated asset root. A standalone JSON document loads packaged collision assets. Box3D raycasts return the managed collider, point, normal, and fraction. The selected static box or model content ID is filtered in the general callback so a ray can continue to geometry behind it.

Placement uses the existing oriented picking bounds for boxes and models and the existing axis-aligned editor proxy bounds for spawn, loot, and navigation markers. The support distance along the hit normal places the nearest bound point exactly on the target plane. Face Snap deliberately ignores translation-grid quantization because post-placement rounding would break exact contact.

Rotation is preserved by default. Optional alignment rotates one selected local attachment axis (`+X`, `-X`, `+Y`, `-Y`, `+Z`, or `-Z`) onto the target normal before support distance is calculated; alignment defaults off with `+Y` selected and handles parallel and anti-parallel directions. Spawn rotations may align while their marker proxy remains axis-aligned. Loot points and navigation waypoints are translation-only, so alignment controls are disabled for them.

While active, cursor raycasts update the entity preview without adding document history and draw the hit point, normal, and a small target-plane indicator. Normal viewport picking, transform gizmos and hotkeys, translation snapping, and right-mouse camera capture are suppressed. A miss restores the original transform and cannot commit. Left click commits the current hit as one `SetEntityTransformCommand`; Escape, right click, toolbar cancellation, selection or document edits, document transitions, save, replacement, and editor shutdown restore the original preview state and dispose the collision world. Undo and redo operate on the complete committed transform.

Collision generation or Box3D failures restore the document, exit the mode, and report actionable text in Validation and Log plus the structured logger. Automated coverage includes floor, wall, rotated surfaces, non-uniform and proxy bounds, all alignment axes, anti-parallel rotation, preview/miss/cancel, one-command undo/redo, project collision regeneration, and a native filtered-ray test that skips the selected collider.

## Playtesting

The initial editor does not embed a playable simulation. Save and Launch validates and saves the map, then starts the normal development server and client using existing launch profiles and the selected map.

## Deferred Capabilities

Physics-assisted placement is planned for decorative objects. It will temporarily simulate selected objects with Box3D using fixed ticks and settling thresholds, then bake stable transforms back into static map data as one cancellable undoable action.

WattleScript map behavior is also deferred. Future work may add authoritative scripted doors, buttons, lights, and other interactions. Script ownership must preserve server authority.