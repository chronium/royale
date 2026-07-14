---
id: EDITOR-011
title: Add isolated model contact sheets
track: EDITOR
milestone: M8
priority: low
dependsOn:
- EDITOR-010
- EDITOR-023
createdAt: 2026-07-11T18:46:40.4960620Z
modifiedAt: 2026-07-14T12:11:21.2800080Z
---

Render a selected render-capable manifest asset offscreen from six axis and eight corner-diagonal orthographic views, compose one or two labelled consistently framed PNG contact sheets with the established StbImageWriteSharp codec, and return image content plus structured framing and view metadata through MCP.

## Notes

- 2026-07-14 12:07 UTC - Implemented isolated model contact sheets with explicit orthographic RenderCamera projection/up vectors, shared bounds framing, screen-space Blurg labels, deterministic axis/diagonal composition, manual MCP image results and structured metadata, and an editor-owned one-active-request capture lifecycle. Captures submit at most one view per frame; SDL GPU fence waits and final StbImageWriteSharp PNG composition run off the editor thread, with cancellation and shutdown drainage.

  Validation on 2026-07-14: scoped `dotnet format Royale.slnx --no-restore --verify-no-changes` passed (workspace-load warning only); `dotnet build Royale.slnx --no-restore` passed with 0 warnings/errors; `dotnet test Royale.slnx --no-restore --no-build` passed all 1,252 tests; opt-in `ROYALE_GPU_TESTS=1` SDL GPU test passed all fourteen real Kenney-crate views. Live Streamable HTTP MCP calls returned axis-only and default-order axis-plus-diagonal PNGs in approximately 0.27s and 0.4s, with correct MIME types, content indexes, dimensions, framing metadata, labels, separators, consistent common scale, distinct orientations, and no clipping. Updated `development/editor-mcp`, `architecture/content-and-rendering`, and `architecture/editor`. Project-owner visual confirmation remains requested for label/orientation/readability/scale/clipping in their editor environment.
- 2026-07-14 12:11 UTC - Post-review addendum: added deterministic capture-service coverage for concurrent busy rejection, cancellation between views with in-flight drainage, GPU failure cleanup, and shutdown cleanup. Final full-suite rerun passed all 1,256 tests; the zero-warning build, formatter verification, and opt-in native fourteen-view Kenney test also passed after these changes.