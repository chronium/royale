---
title: Content and Rendering
createdAt: 2026-07-05T16:11:12.3546390Z
modifiedAt: 2026-07-12T19:09:04.2488670Z
---

## Content and Map Data

Shared map content lives in `Royale.Content`. The default map id is `graybox` via `ContentCatalog.DefaultMapId` and `MapCatalog.DefaultMapId`.

The committed default map file is `src/Royale.Content/Maps/graybox.json`. The content project copies map JSON files to runtime output under `maps/`, so client and server consumers load the same copied file shape from `AppContext.BaseDirectory/maps/<map-id>.json`.

`MapCatalog.LoadById()` accepts simple ASCII map ids using letters, digits, `-`, and `_`, then loads and validates the matching JSON file. Missing files fail with a clear `FileNotFoundException`; malformed JSON or invalid map shape fails with `InvalidDataException`.

The current `graybox` map is a `40 x 40` metre tactical arena for eight-player matches. Gameplay bounds remain `-24..24` on X/Z, the initial safe-zone placeholder remains centered at the origin with radius `20`, and perimeter walls sit at approximately `+/-19.9` metres. The map defines 12 ordered candidate spawns (eight outer and four inner) and eight placeholder loot points. Eighteen interior primitives form four recognizable combat zones: an angled wall maze to the north, tall and long sightline cover to the east, an angled wall with mixed-height cover to the south, and the preserved step/ramp/platform assembly with two blockers to the west. Four staggered center objects break opening and diagonal sightlines while leaving traversal gaps. The ramp cluster retains its original internal offsets and transform; the map continues to use only the existing primitive box schema.

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
      "id": "outer-north",
      "position": { "x": 0.0, "y": 0.0, "z": -17.0 },
      "rotationEuler": { "x": 0.0, "y": 180.0, "z": 0.0 }
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
      "size": { "x": 40.0, "y": 0.24, "z": 40.0 },
      "rotationEuler": { "x": 0.0, "y": 0.0, "z": 0.0 }
    }
  ]
}
```

Static boxes define shared map data used by both client-rendered gray-box geometry and simulation static collision. Their `position`, `size`, and human-editable `rotationEuler` values are converted through the shared `MapStaticBoxTransforms` helper so rendering and collision use the same transform convention.

Client rendering treats each static box as a centered unit-box mesh scaled by `size`, rotated by yaw/pitch/roll Euler angles, and translated by `position`. `GAME-002` uses the same data in `Royale.Simulation` to create one Box3D static body per static box and one box hull shape per body, with hull half-extents of `size / 2`. Shape ids are associated back to source static-box ids for tests and debugging.

Spawn points are gameplay content, not placeholders. `MapCatalog` requires at least one spawn point, each spawn id must be non-empty and unique, and every spawn position must be inside `worldBounds`. `MapSpawnPoint.Position` is the player feet anchor. `GAME-007` uses spawn points in `Royale.Simulation` for deterministic first-valid selection with static-collision clearance checks and caller-provided occupancy reservations. `BR-003` integrates them into authoritative admission: the server randomizes eligible candidates, preserves each selected spawn's authored yaw, and reserves its player footprint. Graybox spawn yaw values use the gameplay convention where yaw `0` faces world `-Z`; all twelve are authored to face the initial safe-zone center.

Loot points and safe-zone fields are still placeholders at this stage and do not yet drive gameplay behavior. Static map collision, generated model collision, and spawn selection exist, but the map content does not yet implement loot collision, safe-zone simulation, height fields, dynamic bodies, or protocol compatibility behavior.

Rendering-only data may remain client-specific.

The server should not need textures or shader assets.

### Map-Authored Navigation

`BOT-005` adds required `navigation` data to `GameMap`:

```json
"navigation": {
  "waypoints": [
    { "id": "center", "position": { "x": 0.0, "y": 0.0, "z": 0.0 } }
  ],
  "links": [
    { "from": "center", "to": "east-route" }
  ]
}
```

Waypoint positions are standing-player feet anchors. IDs contain only ASCII letters, digits, `-`, or `_` and are unique using ordinal comparison. Links are undirected: endpoints must exist and differ, and duplicate or reversed-duplicate pairs are invalid. The graph must be non-empty and connected. Waypoints must be finite and inside `worldBounds`; every spawn and loot position must be finite, in bounds, and within 2 metres in 3D of a waypoint.

After static collision is built, shared simulation checks clear grounded standing-capsule placement and walks each authored link in both directions with the real kinematic controller. This validation exposed and corrected two authored collision seams: graybox now has a controller-walkable lead-in and ramp/platform overlap, while prototype-arena's stairs are oriented toward and aligned with the raised platform. These are content corrections, not new movement rules.

The validated `MapNavigationGraph` is retained only by authoritative server simulation for later bot work. Navigation does not affect rendering, protocol data, human/bot input, or authoritative transforms in `BOT-005`.

### Prototype Combat Arena

`GAME-013` adds `src/Royale.Content/Maps/prototype-arena.json` as a second, explicitly selected map. `graybox` remains the client, server, development, production, `ContentCatalog`, and `MapCatalog` default. Launch the arena with `--map prototype-arena`; no map-selection UI or implicit profile change is introduced.

`prototype-arena` retains graybox's `-24..24` X/Z world bounds, `-1..5` vertical bounds, origin-centered radius-20 safe-zone placeholder, 12 inward-facing spawn candidates (eight outer and four protected inner), and eight placeholder loot points. Its visible play surface is a 40×40 scaled `kenney-floor-square` triangle mesh at Y=0. A single 40×40 primitive `ground-fallback` ends at Y=-0.02 and remains hidden below the model floor as reliable fallback collision.

Forty-five static model instances form five readable regions: a staggered west crate yard with short wall lanes; a north 8×8 raised platform with separate stairs and slope approaches; a south doorway compound with four corners, two openings, side walls, and interior cover; an east six-column courtyard with targets and transverse walls; and an open central crossroads with four staggered crates. Four model-backed perimeter walls close the 40×40 visible floor. All architecture remains server-authoritative generated static collision; there are no dynamic doors, animation, dynamic bodies, loot behavior, navigation data, lighting changes, editor, or protocol/schema additions.

Automated validation covers map load/counts/unique IDs/bounds, all asset categories, scene batching, 46 static colliders, valid and simultaneously reservable spawns, visible-floor/platform/boundary raycasts, and the front doorway opening at player height. Traversal feel, cover readability, combat sightlines, and visual collision alignment still require project-owner play validation.

Deterministic captures can select an initial render view and hide ImGui telemetry without changing defaults. `--render-view` accepts `normal`, `world-and-debug`, `debug-only`, or `collision-solids`; `--hide-telemetry` starts with the F3-controlled diagnostic windows hidden. Example:

```text
dotnet run --project src/Royale.Client/Royale.Client.csproj --no-build --no-restore -- --offline --map prototype-arena --camera-mode freecam --camera-position 25,24,25 --camera-look-at 0,0,0 --render-view normal --hide-telemetry --screenshot /tmp/prototype-arena-normal.png --screenshot-after-frames 5
```

`BUG-006` raises both south-compound doorway instances to vertical scale `2.5`. Since the source opening reaches local Y=`0.8`, this provides 2.0 metres of clear height for the default 1.8-metre player capsule while preserving doorway width and the rest of the compound layout. Simulation validation casts the full default capsule through both openings rather than relying on a single waist-height ray.

`BUG-007` aligns the complete wall family at a 2.5-metre world-space top. Ground-level `kenney-wall` and `kenney-wall-corner` instances use vertical scale `2.5`; both doorway arches already use `2.5`, while the north platform rails remain at position Y=`1.0` and scale Y=`1.5`. Horizontal transforms and all non-wall assets remain unchanged. Content validation asserts the common world-space top across all 21 wall, corner, and doorway instances.

`BUG-008` corrects the north raised-platform approaches after owner visual validation of `BOT-005`. The platform and slope apex are both at Y=1.0, and the slope instance is centered inward at X=2.0 so its outer edge does not overhang the platform. The stairs return to their non-overlapping Z=-6.0 placement, face the platform at yaw 90 degrees, and use vertical position 0.2 / scale 0.7. A `kenney-floor-thick` plinth at `(-2, 0, -5.5)` with scale `(4, 1, 5)` runs continuously from the 0.2-metre approach step to the platform edge, fully supporting the stairs instead of leaving them floating. Navigation retains an explicit landing approach and validates ordinary standing walking in both directions. Project-owner visual and movement validation approved the final arrangement.

The final `BUG-008` map contains 46 static model instances and 47 total static colliders, superseding the original `GAME-013` counts above; the added model is the stair-support plinth.

### Map-Authored Static Models

`GAME-011` adds `staticModels` to shared map JSON. Each entry declares a unique instance `id`, stable model `assetId`, `position`, `rotationEuler`, and explicit non-zero 3D `scale`. Instance IDs share the static-content namespace with `staticBoxes`; transforms must be finite and the placement origin must remain inside world bounds. The shared transform convention is `scale * yaw/pitch/roll rotation * translation` through `MapStaticModelTransforms`. GLB import/node transforms remain baked asset-local data and are not duplicated in map content.

The default graybox map owns `crate-south-east`, referencing `kenney-crate` at `(6, 0, 5)`, yaw `25.714286` degrees, and uniform scale `1.25`. `MapStaticMeshScene` groups instances by asset ID, resolves each ID through the generated client catalog/cache, and emits one render batch per asset primitive with the map instance transforms. Missing render assets fail with map and asset context. There is no hard-coded crate preview placement.

### Model Asset Build Pipeline

Model assets are declared in the strict, case-sensitive JSON manifest `assets/model-assets.json`. Manifest version `1` assigns stable lowercase asset IDs and separates optional render content from an explicit collision mode. Render entries name a GLB source plus every required relative resource; paths must be portable, remain under the assets root, and exist at build time. Collision modes are `none`, `convex`, `triangleMesh`, and `separateMesh`; only `separateMesh` accepts a separate GLB source. Generated collision artifact paths cannot be authored in the source manifest.

`tools/Royale.AssetPipeline` validates and normalizes the source manifest during client and server builds. Outputs are deterministic and uncommitted under each project's `obj/<configuration>/<framework>/royale-assets/<audience>` tree. MSBuild input/output tracking skips generation when the manifest and source files are unchanged. Client output receives the normalized runtime catalog, declared GLBs, and declared render resources. Server output receives a collision-only catalog and never receives GLBs, textures, SimpleMesh, or the pipeline tool as runtime dependencies.

The first source asset is `kenney-crate`, using `meshes/kenney-prototype-kit/crate.glb` and its required `Textures/colormap.png`. Both remain CC0 Kenney Prototype Kit content with attribution recorded beside the sources. Map placement transforms remain separate map-owned data; the asset manifest does not add an import-transform layer.

`DEBT-002` validates external GLB dependencies before generated output is created. The source-manifest loader inspects GLB 2.0 JSON `buffers[].uri` and `images[].uri` values, resolves non-data URIs relative to the render GLB, and requires the resulting asset-root-relative path to appear exactly in that asset's `render.resources`. Missing declarations fail with asset, GLB, URI, and resolved-path context, preventing a render asset from reaching client startup without all of its external files.

`GAME-012` expands the committed Kenney Prototype Kit set to ten reusable environment assets, all sharing `meshes/kenney-prototype-kit/Textures/colormap.png`: `kenney-column`, `kenney-crate`, `kenney-floor-square`, `kenney-floor-thick`, `kenney-shape-slope`, `kenney-stairs`, `kenney-target-a-round`, `kenney-wall`, `kenney-wall-corner`, and `kenney-wall-doorway`. Floor-square, stairs, wall-corner, and wall-doorway use `triangleMesh`; the remaining assets use `convex`. No dynamic bodies, animation, doors, browser, editor, or generalized material system is introduced.

The selected Kenney models use valid glTF `UNSIGNED_BYTE` indices, supported through the focused SimpleMesh project patch recorded under `thirdparty/patches/SimpleMesh`. Triangle-mesh cooking deterministically discards degenerate source/canonicalized faces while retaining and sorting valid faces; it fails if none remain. Convex cooking retains its stricter degenerate-triangle rejection. Tests build deterministic client and server outputs, load every model through `StaticMeshAssetCache`, and create a valid native Box3D shape from every generated artifact.

`BUG-005` addresses the Kenney doorway's paired opposite-winding coplanar faces at their two consumers. Client GLB import preserves all authored vertices, indices, normals, and UVs; the SDL static-mesh pipeline uses counter-clockwise front faces with backface culling so only the camera-facing member of each pair is rasterized. Triangle-mesh collision cooking separately removes position-index duplicate surfaces before deterministic ordering because rasterizer culling does not apply to Box3D. The committed Kenney source remains unchanged, close-range depth fighting is avoided, and generated collision falls from 152 source triangles to 76 unique surfaces.

`EDITOR-024` extracts processing and collision generation into the non-executable `src/Royale.AssetPipeline` library. `tools/Royale.AssetPipeline` remains the thin command-line wrapper used by `Royale.AssetPipeline.targets`, so existing MSBuild invocation and client/server audience rules remain unchanged. The editor uses the library in-process to validate candidate manifests and build staged generated directories before committing project sources. `GlbExternalResourceInspector` is the reusable GLB 2.0 JSON-resource inspection boundary.

#### Convex Collision Artifacts

`ASSET-002` cooks `convex` assets during the build by loading all transformed triangle geometry through the pinned SimpleMesh dependency and passing a canonical point set through SimpleMesh Quickhull. Collision positions are snapped to a one-micrometer grid to remove exporter floating-point noise before deterministic sorting. The generated version `1` JSON artifact stores `kind: convex` and canonical support vertices only; it intentionally has no triangle indices because Box3D constructs and owns its native hull topology from those points. Triangle indices belong to `triangleMesh` artifacts.

Convex artifacts are written as `collision/<asset-id>.json`, referenced from the generated catalog, and produced for both client and server audiences. They are build products under intermediate/output directories and are not committed. The cook rejects missing triangle geometry, invalid indices, non-finite vertices, degenerate triangles, coplanar point sets, and unsupported collision modes with asset-specific diagnostics. Source GLB hierarchy transforms are baked into artifact-local vertices; map placement transforms remain map-owned and are not applied by the asset pipeline.

#### Triangle Collision Artifacts

`ASSET-003` cooks `triangleMesh` from the asset's render GLB and `separateMesh` from the explicitly declared collision GLB. Both paths bake the source hierarchy transforms, snap positions to the shared one-micrometer collision grid, sort unique vertices and triangles deterministically, and preserve each triangle's winding. The version `1` artifact uses `kind: triangleMesh` with indexed vertices.

A `separateMesh` source is build-only. Generated client and server catalogs clear its source path and retain only the collision artifact reference. Client output includes the render GLB and declared render resources but not the separate collision GLB; server output includes neither source GLB nor any render material/texture data. The build tool assembly itself participates in MSBuild input tracking so cooker changes invalidate generated outputs.

### Blender Map Authoring Contract

`ASSET-004` adds `tools/blender/royale_map_export.py` as the reusable authoring/export entry point. Blender is an authoring dependency only: the .NET build and runtime consume committed GLBs and map JSON and never invoke or reference Blender.

A map scene owns exactly these top-level contract collections:

* `Royale.Render`: visible mesh geometry with base-colour materials.
* `Royale.Collision`: simplified static mesh geometry; exported without materials.
* `Royale.Spawns`: Empty markers named `spawn.<id>`.
* `Royale.Loot`: Empty markers named `loot.<id>`.
* `Royale.Navigation`: Empty markers named `waypoint.<id>`; the optional `royale_links` custom property is either a comma-separated string or string array of waypoint IDs.

Markers must belong to exactly their declared marker collection. IDs use ASCII letters, digits, `-`, or `_`, and are unique within their marker kind. Navigation links are canonicalized into sorted undirected pairs, so reciprocal authoring produces one JSON link. Marker transforms must be finite and may use only Blender Z yaw. Blender local `+Y` is marker forward. Positions convert from Blender to Royale as `(x, z, -y)`; converted yaw uses Royale's convention where zero faces world `-Z`.

The scene properties are `royale_map_id`, `royale_map_name`, `royale_bounds_min`, `royale_bounds_max`, `royale_safe_zone_center`, `royale_safe_zone_radius`, and `royale_output_asset_id`. Bounds and safe-zone vectors are stored directly in Royale coordinates. Export emits one identity-transformed static model instance at the origin, deterministic sorted marker/link JSON, a material-bearing render GLB, and a material-free collision GLB. GLB export applies modifiers and normals with Y-up conversion and excludes animations, cameras, and lights.

Run inside Blender with `--validate-only` to check the complete scene contract without writing outputs, or with `--output-root <repository>` to export committed deliverables. Failures include the collection, marker, property, or link context.

### Courtyard Compound

`GAME-017` adds `courtyard-compound` as an explicitly selected 100×100 m map (`--map courtyard-compound`); `graybox` remains the default. The map places one identity-transformed `courtyard-compound-environment` model with committed render and simplified collision GLBs using `separateMesh` collision.

The Blender source defines a two-storey open compound, three courtyard gates, interior and exterior stair routes, solid window sills with shoot-through apertures, perimeter boundaries, exterior cover, 12 spawns, 12 loot points, and a connected waypoint graph. Server startup validates every navigation link in both directions with the standing controller. Static-content validation accepts maps containing at least one static box or static model.

The editable source and reproducible generator live under `assets/meshes/courtyard-compound/`. Blender remains authoring-only; normal .NET builds consume the committed GLBs and generated map JSON.

#### Validation Status

Owner validation found that `courtyard-compound` does not meet the quality bar for a finished gameplay arena. The reusable export path, committed assets, map loading, collision cooking, spawn handling, and navigation validation remain technically useful, but repeated Blender MCP-driven geometry correction left clipping and broader level-quality problems while requiring excessive iteration.

Treat this map as an experimental pipeline and automated-validation fixture. `graybox` remains the default and `prototype-arena` remains the established authored combat arena. Any future courtyard redesign or replacement is tracked by `DEBT-008` and must use human-led level design; Blender MCP procedural generation is not the primary map-authoring workflow.

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

Gameplay view and freecam are separate client presentation modes. By default, the client starts in gameplay view; `F2` toggles between gameplay view and freecam, `F1` remains the explicit SDL relative mouse capture toggle, and `Escape` releases capture before quitting. `--camera-mode freecam` starts the client in freecam for deterministic validation, while `--camera-position x,y,z` sets the freecam position and `--camera-look-at x,y,z` aims the debug camera without mutating gameplay/player state. Freecam movement is only applied while freecam mode is active.

The debug camera remains a free-fly controller that can produce a `RenderCamera`. Without launch overrides, it starts at approximately `(2.8, 2.1, 2.8)`, looks toward the origin, uses `W/A/S/D` for horizontal local movement, `Space` for up, `Left Ctrl` for down, and rotates from mouse deltas only while SDL relative mouse mode is enabled. This camera is renderer/debug presentation state and is not the gameplay first-person controller or a server-authoritative player view.

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

The current development diagnostics surface is the resizable ImGui `Telemetry` window documented on the `diagnostics` and `architecture/diagnostics-testing-deployment` pages. Its default-open Renderer section reports live camera, render-view, mouse-capture, loaded map/content, model-asset cache, and screenshot scheduling/output state; Frame remains limited to frame time/FPS, and fixed-tick values remain in Simulation. It intentionally does not expose gameplay controls.

`RENDER-006` adds the first debug primitive rendering path. `DebugPrimitiveList` is a project-owned CPU line list for game-specific debug visuals such as the local player capsule, spawn markers, safe-zone boundary, and future ray/contact markers. `Box3DDebugDrawAdapter` calls `b3World_Draw` for the local static collision world and converts Box3D debug shapes and callbacks into the same line list.

`DebugLineRenderer` owns a separate SDL GPU line-list pipeline and `debug_line` shader pair. It uploads frame-local line vertices before the main render pass, then draws the line list after static solids inside the main pass. Its pipeline keeps the depth target attached but disables depth testing and depth writes so debug lines remain visible through solid gray-box geometry when that mode is active.

`RENDER-007` adds client render view modes through `RenderViewModeController`. Startup defaults to `WorldAndDebug` during the debug-heavy M1 milestone. `F5` selects normal world solids only, `F6` selects world solids plus debug wireframes, `F7` selects debug wireframes over a cleared frame, and `F8` selects solid collision-world rendering. These hotkeys are global controls like `F1`, `F2`, and `Escape`, so they remain available while ImGui has keyboard capture.

`CollisionSolids` does not derive filled geometry from Box3D debug callbacks. It renders the same `GameMap.StaticBoxes` source data used to create the local `MapStaticCollisionWorld`, preserving the shared `position`, `size`, and yaw/pitch/roll transform convention. The active render view mode is exposed from `SdlApplication`, included in the Telemetry window's Renderer section, and appended to the diagnostic window title.

`RENDER-008` adds the first game-facing text path outside ImGui. `BlurgTextRenderer` owns BlurgText lifetime, enables system fonts, resolves a default font from a conservative family list (`SF Pro`, `DejaVu Sans`, `Noto Sans`, `Liberation Sans`, `Segoe UI`, `Arial`), and fails clearly if no default font can be resolved. Blurg atlas allocation/update callbacks create SDL GPU RGBA atlas textures and upload changed atlas rectangles through transfer buffers. `TextQuadRenderer` draws Blurg rectangles as screen-space textured quads with alpha blending, no depth testing, and no depth writes.

The current smoke draw is a fixed `Royale BlurgText` label at the top-left of the frame, drawn through BlurgText and SDL GPU before ImGui. It is intentionally not a retained UI framework, HUD layout system, world-space billboard system, health bar implementation, bundled font asset, or server dependency. ImGui remains diagnostics-only development tooling.

`RENDER-009` extends the Blurg text path with rendering-owned world-space text billboards. `WorldTextBillboard` values carry text, world position, world-unit height, anchor, foreground and shadow colors, and either camera-facing or fixed-facing mode. Camera-facing labels derive their right/up basis from the active `RenderCamera`; fixed-facing labels preserve an authored world basis supplied by the caller.

World text remains client/rendering presentation state. It is projected on the CPU from Blurg glyph rectangles into arbitrary screen-space textured quads, then batched through the existing `TextQuadRenderer`. The pass is color-only with no depth testing or depth writes, so labels render as readable overlays after world/debug geometry and before ImGui. Depth-tested, raycast-occluded, replicated player-name, health-bar, loot UI, HUD-layout, and server-owned label behavior are intentionally deferred.

`RENDER-010` adds a render-only SimpleMesh GLB smoke asset. The client project references the pinned vendored SimpleMesh `net8.0` project and copies committed mesh assets from `assets/meshes/**` into runtime output under the same relative path. `SimpleMeshStaticMeshLoader` converts supported triangle geometry from `assets/meshes/kenney-prototype-kit/crate.glb` into `StaticMeshGeometry` by applying node transforms to positions and normals and rejecting empty, non-triangle, invalid, or non-16-bit-compatible geometry.

The crate is drawn by the same basic static mesh shader, depth target, and flat lighting used for gray-box solids. Map `staticBoxes` still render through the built-in centered unit-box mesh path; the crate is a separate client/rendering smoke mesh batch. This does not add map schema fields, collision generation, SimpleMesh convex hull use, textures, materials, animation, skinning, mesh library management, protocol behavior, or server dependencies.

### PNG Images And GPU Readback

`Royale.Rendering` owns the shared tightly packed RGBA PNG codec used by screenshots and editor thumbnail caches. PNG encoding uses centrally pinned `StbImageWriteSharp 1.16.7`; decoding uses `StbImageSharp 2.30.15`. Both dependencies are isolated from content, simulation, protocol, and server projects. Client and editor screenshot targets must have a case-insensitive `.png` extension and are rejected during launch-option validation before SDL graphics initialization. BMP output is not supported.

SDL GPU exposes synchronous readback for one-shot process-exiting screenshots and a separate fence-backed asynchronous offscreen readback for progressive editor work. Following SDL's download guidance, thumbnail fence waits run on a worker thread; later frames consume at most one completed RGBA readback and upload at most one sampled texture, so ImGui interaction never performs a synchronous GPU wait. Owned transfer buffers, fences, offscreen targets, and uploaded sampled textures are released on completion, failure, replacement, or shutdown.

### Manifest-Addressed Model Rendering

`StaticMeshAssetCache` reads the generated client `assets/model-assets.json` catalog and caches loaded assets by stable ID. `SimpleMeshStaticMeshLoader` uses `Model.FromFile` so relative and embedded GLB image resources populate `Model.Images`; node transforms are applied to positions and inverse-transpose normals, while UVs, triangle-group material boundaries, linear base-color factors, and referenced image bytes are preserved as small project-owned render primitives.

The static mesh vertex layout carries position, normal, and one UV set. The SDL GPU mesh pipeline binds one base-color texture and sampler per material batch, multiplies the sampled color by the material factor and directional lighting, and uses a shared white fallback for untextured gray-box geometry. PNG/JPEG bytes are decoded through SDL3 `SDL_LoadSurface_IO`, converted to RGBA32, uploaded as sRGB SDL GPU textures, cached for shared material image data, and disposed with the renderer. The Kenney nearest-filtered atlas uses repeat addressing. This is intentionally limited to opaque base-color materials; it does not add a material graph, PBR pipeline, animation, skinning, or server dependency.

`GAME-011` supplies model instances exclusively from shared map content. The deterministic crate validation uses freecam position `(8, 2.2, 8)` looking at `(6, 0.5, 5)`, captures after five frames, and verifies the textured map-authored crate plus aligned Box3D debug hull without human validation.

## Shader Build Pipeline

Client shader sources live under `src/Royale.Client/Shaders/` as HLSL files using stage suffixes:

* `.vert.hlsl`
* `.frag.hlsl`
* `.comp.hlsl`

The client build requires `shadercross` to be available on `PATH`. After `Royale.Client` builds, MSBuild compiles each shader source to SPIR-V (`.spv`) and Metal (`.msl`) under the client output `shaders/` directory, preserving recursive shader folders. The original HLSL source is also copied to the same output tree for Direct3D/DXIL-facing development until a specific DXIL output flow is chosen.

`SDL_shadercross` is not vendored through `thirdparty`; it is treated as an external local build tool dependency.

## macOS SDL GPU Integration Validation

`tests/Royale.Rendering.GpuHarness` is the supported macOS ARM64 integration path for SDL GPU lifecycle and offscreen rendering. Cocoa video initialization, the hidden SDL window, GPU device, targets, rendering, readback, and disposal all execute in a standalone process whose entry thread runs `SdlDesktopHost`; xUnit worker threads must not initialize the Cocoa backend and must not use `SDL_RunOnMainThread` as a substitute for an SDL main-thread event loop.

The harness renders an indexed unit box to a 128×96 offscreen target, validates normalized opaque RGBA readback against a distinctive clear color, resizes the same target to 79×61, and repeats. Its output packages the macOS ARM64 SDL, ImGui, and Blurg native libraries plus Rendering-owned HLSL, MSL, and SPIR-V shaders in the same runtime layout as graphical consumers.

Run the built harness directly with `dotnet tests/Royale.Rendering.GpuHarness/bin/Debug/net10.0/Royale.Rendering.GpuHarness.dll`. The environment-gated `SdlGpuIntegrationTests` wrapper executes the packaged DLL from the Rendering test output when `ROYALE_GPU_TESTS=1`, captures both output streams, enforces a 30-second timeout, kills the process tree on timeout, and requires both exit code zero and `GPU_HARNESS_SUCCESS`. Without the environment variable, the wrapper does not touch SDL.

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
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 -- --screenshot /tmp/royale-frame.png --screenshot-after-frames 5
```

Deterministic validation captures can start directly in freecam with invariant-culture camera vectors. `--camera-mode` accepts `gameplay` or `freecam`; `--camera-position x,y,z` and `--camera-look-at x,y,z` are accepted only with `--camera-mode freecam` and do not mutate gameplay/player state.

```text
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 -- --offline --map graybox --camera-mode freecam --camera-position 4,2.2,3 --camera-look-at 1.75,0.7,-1.35 --screenshot /tmp/royale-crate.png --screenshot-after-frames 5
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

### Crouch presentation

Simulation stance and collision height change immediately. Only the local rendered gameplay eye height is smoothed: it moves linearly between the standing `1.62 m` and crouched `0.95 m` targets and reaches the target within `0.15 s`. The smoother snaps to the current target when no gameplay player exists and resets after offline respawn, network disconnect, and freecam use so presentation state cannot leak between player lifecycles.

Local predicted and remote debug capsules use each player's actual stance height. Remote cameras, player animation, models, weapon poses, and stance animation remain outside `GAME-015`.