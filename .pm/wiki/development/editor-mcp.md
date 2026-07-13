---
title: Editor MCP
createdAt: 2026-07-11T18:49:21.0289030Z
modifiedAt: 2026-07-13T19:08:47.2027610Z
---

## Purpose

The running Royale editor will expose an optional MCP server so an agent and a human can inspect and modify the same live map document.

## Transport And Security

The editor uses the official stable .NET `ModelContextProtocol.AspNetCore` SDK and hosts stateless Streamable HTTP at `http://127.0.0.1:<port>/mcp`. Legacy SSE is disabled. The editor does not configure CORS.

Hosting is opt-in with `--mcp`. The default port is `51238`; `--mcp-port <port>` overrides it and is invalid without `--mcp`. A requested port that cannot be bound aborts editor startup with a clear error. Additional editor instances must choose another port explicitly.

This endpoint follows a trusted development-machine threat model and is deliberately unauthenticated. It has no credentials or secrets. Kestrel binds only to IPv4 loopback, requests must carry a `Host` value of `127.0.0.1` or `localhost`, and every request containing an `Origin` header is rejected. Remote access, browser clients, DNS-rebinding access, and browser-originated requests are unsupported.

The editor exposes a dockable MCP status window while hosting is enabled. It shows lifecycle state, endpoint, active and accepted request counts, recent request activity, rejected requests, and sanitized startup or transport failures.

All future MCP operations that read or mutate editor, document, rendering, or GPU state must use the editor main-thread dispatcher. The dispatcher executes queued work at the beginning of the editor update and propagates results, exceptions, cancellation, and shutdown failures without running queued-work continuations on the render thread.

## Document Safety

Every mutation and explicit validation call includes `expectedRevision`. The operation runs only when it matches the active document's committed revision; stale failures report both expected and current revisions and make no change. Reads and mutations are rejected while a transform gizmo, standalone face-snap preview, or constrained face-snap preview is active, so MCP never observes or overwrites transient presentation state.

MCP targets only the active map document. It does not open, replace, close, convert, or Save As documents. Stable editor GUIDs identify entities for the lifetime of that document. Entity mutations use the normal editor command history, preserve human selection unless its selected entity is deleted, and refresh scene and lightweight validation presentation. No-op replacements and transforms do not enter history or increment the revision.

`save` is in-place only and does not increment the document revision. Project documents use project fingerprint checks and project save; standalone documents use their existing JSON path and fingerprint. Both paths run runtime map validation, temporary-file verification, and atomic replacement. A document without an existing writable destination reports that Save As is required. External source, project-manifest, or asset-manifest changes report a conflict rather than overwriting them.

## Tool Surface

`EDITOR-010` registers attributed tools through the official SDK. Every tool advertises an input schema, structured output schema, a closed-world hint, and an accurate read-only/destructive hint.

Read-only inspection tools are:

- `get_editor_status`: document type/path, map ID/name, revision, dirty and undo/redo state, selection, and source-manifest fingerprint when the active document is a project.
- `get_map`: map name, immutable ID, world bounds, safe zone, entity counts, and revision.
- `list_assets`: model IDs plus relative render source/resources and collision mode/source/artifact.
- `list_entities`: stable GUID, kind, display ID, and collection index, with an optional kind filter.
- `get_entity`: complete kind-specific definition, complete transform where spatial, transform capabilities, and revision.
- `validate_map`: runtime-equivalent staged validation for `expectedRevision`; it also publishes the report to the editor Validation panel.

Mutation tools are `set_map_name`, `set_world_bounds`, `set_safe_zone`, `create_entity`, `duplicate_entity`, `replace_entity`, `set_entity_transform`, `delete_entity`, `snap_entity_to_face`, `undo`, `redo`, and `save`. Each requires `expectedRevision`. Entity definitions form a discriminated union using `kind`: `staticBox`, `staticModel`, `spawnPoint`, `lootPoint`, `navigationWaypoint`, or `navigationLink`. Vectors always use explicit `x`, `y`, and `z` fields.

Create appends to the relevant collection. Duplicate inserts after its source and uses the editor's existing unique-ID behavior; navigation links cannot be duplicated. Replacement must retain entity kind. Waypoint runtime-ID replacement rewrites incident links as part of the same revision. Waypoint deletion cascades to incident links. Static-model creation and replacement require a render-capable asset in the active manifest.

`set_entity_transform` accepts the complete position, rotation, and scale-or-size transform. Unsupported rotation or scale changes are rejected; position-only and position/rotation entities must echo their unchanged unsupported components. Complete-transform no-ops are suppressed.

`snap_entity_to_face` accepts a stable entity GUID, world-space ray origin and direction, positive finite maximum distance, alignment enabled, and one alignment axis: `positiveX`, `negativeX`, `positiveY`, `negativeY`, `positiveZ`, or `negativeZ`. It reuses the collision-backed face-snap session, excludes the selected box/model collider, and returns hit point, normal, fraction, and collider metadata. A miss succeeds without changing revision; a changed hit creates one transform command.

Expected failures return controlled MCP errors for stale revisions, active previews, invalid definitions, missing entities or assets, unsupported transforms, save destinations, and fingerprint conflicts. Internal exception details are not exposed. Contact-sheet capture, asset import or mutation, document opening or replacement, Save As, and force-discard remain outside this tool set.

## Model Contact Sheets

Model inspection supports two modes:

- Isolated asset mode renders only the selected asset.
- Context mode renders a selected map instance with configurable nearby geometry, selection highlighting, and an optional collision overlay.

The editor produces one labelled orthographic PNG sheet for the six axis views and one for the eight corner-diagonal views. Views use consistent framing, lighting, background, and scale derived from model bounds.

Offscreen rendering uses the shared SDL GPU rendering path. GPU readback is encoded as PNG with ImageSharp. MCP returns up to two `image/png` content items plus structured view and framing metadata.

Model contact sheets are inspection artifacts and do not modify the map document.