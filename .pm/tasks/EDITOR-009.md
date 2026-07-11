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
modifiedAt: 2026-07-11T18:46:50.8947600Z
---

Use the official .NET ModelContextProtocol SDK to host an opt-in authenticated Streamable HTTP MCP endpoint on loopback, queue operations onto the editor main thread, and expose connection status without storing secrets in source control.