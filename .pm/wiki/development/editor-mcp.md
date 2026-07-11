---
title: Editor MCP
createdAt: 2026-07-11T18:49:21.0289030Z
modifiedAt: 2026-07-11T18:49:21.0289030Z
---

## Purpose

The running Royale editor will expose an optional MCP server so an agent and a human can inspect and modify the same live map document.

## Transport And Security

The editor uses the official .NET `ModelContextProtocol` SDK and hosts Streamable HTTP on loopback only. MCP hosting is opt-in.

The endpoint requires authentication. Its secret is supplied from local configuration or environment state and must never be committed. The editor displays endpoint and connection status, but it must not log authentication secrets.

All MCP operations that touch editor or GPU state are queued onto the editor main thread.

## Document Safety

Every mutation includes the expected editor document revision. A stale revision fails clearly instead of overwriting newer human or agent work.

MCP mutations use the same editor command system as interactive actions and are undoable. Save uses the same runtime validation, external-change detection, and atomic replacement as the UI.

Opening or replacing a map is rejected while the current document has unsaved changes. The first version does not expose a force-discard tool.

## Tool Surface

The initial tool surface covers:

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