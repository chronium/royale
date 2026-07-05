---
id: BUILD-008
title: Forbid direct PM storage edits in agent instructions
track: BUILD
createdAt: 2026-07-05T19:10:23.2746470Z
modifiedAt: 2026-07-05T19:10:23.2746470Z
---

Update repository agent instructions so PM board and wiki mutations must use the PM MCP tools, and direct writes to .pm storage are forbidden.

## Completion Notes

- Added a dedicated PM Storage Protection section to `AGENTS.md`.
- Clarified that `.pm/` may be read for inspection but must not be manually written.
- Required PM MCP tools for task creation, state changes, task markdown updates, wiki create/edit/rename/delete, and project metadata changes.
- Added a validation checklist item confirming no direct `.pm/` storage edits were made.
- No wiki update was needed because this is repository-agent workflow guidance rather than product or architecture source-of-truth content.