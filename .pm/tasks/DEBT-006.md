---
id: DEBT-006
title: Redesign agent instructions and skill routing
track: DEBT
milestone: M6
createdAt: 2026-07-11T06:06:12.7894230Z
modifiedAt: 2026-07-11T06:09:52.5082780Z
---

Redesign AGENTS.md and all eight repository skills for newer autonomous coding agents. Keep the project contract model-agnostic, reduce duplicated context, make skill triggers discriminative, require inspection before questions, define explicit decision gates for product contracts and ambiguous dirty worktrees, preserve PM/wiki/git/server-authority requirements, and validate all skill metadata and instructions.

## Notes

- 2026-07-11 06:09 UTC - Redesigned AGENTS.md and all eight repository skills for grounded autonomous agents.

  - AGENTS.md now owns only always-on project invariants, explicit decision gates, PM/wiki lifecycle, git hygiene, completion requirements, and skill routing.
  - Domain skills were reduced from 765 total lines to 360 lines and now avoid repeating PM/source-control/build boilerplate.
  - Skill descriptions are more discriminative so only relevant domains load.
  - Added explicit inspect-first behavior: ask for undiscoverable project-contract decisions, not routine or locally discoverable implementation details.
  - Preserved PM-MCP-only mutations, wiki source-of-truth requirements, server authority, dirty-worktree handling, task-prefixed commits, human validation, OTLP elevation, and native/shader constraints.
  - Added development/agent-workflow wiki source-of-truth page.
  - Validation: all skill names match directories; all eight frontmatters passed equivalent YAML/schema/name/description checks; git diff --check passed; PM validation passed.
  - The bundled Python quick_validate.py could not start because host Python lacks PyYAML. No dependency was installed; equivalent validation passed with Ruby/Psych.
  - No .NET build/test was run because only agent instructions, skill Markdown, PM metadata, and wiki documentation changed.