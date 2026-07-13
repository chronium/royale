---
id: EDITOR-009
title: Host the live editor MCP server
track: EDITOR
milestone: M8
priority: low
dependsOn:
- EDITOR-004
- EDITOR-008
createdAt: 2026-07-11T18:46:39.9899850Z
modifiedAt: 2026-07-13T18:44:21.5601440Z
---

Use the official stable .NET ModelContextProtocol ASP.NET Core package to host an opt-in stateless Streamable HTTP endpoint at http://127.0.0.1:<port>/mcp. Bind only to IPv4 loopback, allow only 127.0.0.1 or localhost Host values, reject every Origin-bearing request, expose editor status, and queue editor or GPU operations onto the editor main thread. The trusted development-machine endpoint is deliberately unauthenticated and does not support browsers or remote access.

## Notes

- 2026-07-13 18:44 UTC - Implemented the opt-in live editor MCP host using centrally pinned ModelContextProtocol.AspNetCore 1.4.1.

  Decisions and scope:
  - The endpoint is deliberately unauthenticated under the trusted development-machine model.
  - --mcp enables http://127.0.0.1:<port>/mcp; --mcp-port defaults to 51238 and requires --mcp.
  - The stateless Streamable HTTP host binds only to IPv4 loopback, disables legacy SSE, does not configure CORS, accepts only localhost/127.0.0.1 Host values, and rejects every Origin-bearing request.
  - EDITOR-009 exposes initialization and an empty tools/list only. Future editor/document/GPU tools must use the main-thread dispatcher.
  - The docked MCP status window reports lifecycle, endpoint, active/accepted counts, activity, rejections, and sanitized errors. Shutdown fails pending dispatcher work and stops MCP before rendering/native disposal.

  Validation:
  - dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1: passed.
  - dotnet build Royale.slnx --no-restore: passed with 0 warnings and 0 errors.
  - dotnet test Royale.slnx --no-restore --no-build: 1,234 passed, 0 failed.
  - Editor-focused build and test: 235 passed, 0 failed.
  - dotnet format Royale.slnx --no-restore --verify-no-changes --include src/Royale.Editor tests/Royale.Editor.Tests: passed (workspace-load warning only, exit 0).
  - git diff --check: passed.
  - Native macOS editor smoke: MCP listened on 127.0.0.1:51239, initialization negotiated protocol 2025-11-25, tools/list returned an empty list, the MCP Status dock tab appeared, screenshots were captured, and the editor exited cleanly.

  Wiki development/editor-mcp now records the trusted-local transport/security contract and empty EDITOR-009 tool surface.

  Owner validation requested for the native UI/platform behavior: status-panel content and toggling, port-conflict presentation, MCP client connection, and clean shutdown.