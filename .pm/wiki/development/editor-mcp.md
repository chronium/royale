---
title: Editor MCP
createdAt: 2026-07-11T18:49:21.0289030Z
modifiedAt: 2026-07-13T18:39:21.6733340Z
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

Every mutation includes the expected editor document revision. A stale revision fails clearly instead of overwriting newer human or agent work.

MCP mutations use the same editor command system as interactive actions and are undoable. Save uses the same runtime validation, external-change detection, and atomic replacement as the UI.

Opening or replacing a map is rejected while the current document has unsaved changes. The first version does not expose a force-discard tool.

## Tool Surface

`EDITOR-009` exposes MCP initialization and an empty `tools/list` response. It intentionally ships no inspection or mutation tools.

Follow-on editor MCP tasks own the planned tool surface:

- Editor and active-document status
- Map and asset listing
- Entity listing and inspection
- Validation
- Entity creation, duplication, update, and deletion
- Transform updates and face snapping
- Undo and redo
- Save
- Model contact-sheet capture

Tools operate on intent and editor documents. They do not bypass runtime validation or mutate authoritative game state.

## Model Contact Sheets

Model inspection supports two modes:

- Isolated asset mode renders only the selected asset.
- Context mode renders a selected map instance with configurable nearby geometry, selection highlighting, and an optional collision overlay.

The editor produces one labelled orthographic PNG sheet for the six axis views and one for the eight corner-diagonal views. Views use consistent framing, lighting, background, and scale derived from model bounds.

Offscreen rendering uses the shared SDL GPU rendering path. GPU readback is encoded as PNG with ImageSharp. MCP returns up to two `image/png` content items plus structured view and framing metadata.

Model contact sheets are inspection artifacts and do not modify the map document.