---
id: BUILD-021
title: Codify readable and pragmatic source structure
track: BUILD
milestone: M6
createdAt: 2026-07-12T08:43:01.4676370Z
modifiedAt: 2026-07-12T08:44:53.4957900Z
---

Update repository and implementation guidance to prohibit compressed code and oversized cross-domain files/projects while preserving pragmatic, need-driven abstractions.

## Notes

- 2026-07-12 08:44 UTC - Codified pragmatic readability and source-structure policy in AGENTS.md, royale-source-control-implementation, royale-architecture-boundaries, and architecture/overview. The policy prohibits compressed unrelated statements/declarations, multi-thousand-line catch-all files, flat cross-domain project roots, and domain behavior in composition roots. It also guards against overengineering: project splits require meaningful boundaries, concrete types are preferred by default, and interfaces/layers require real substitution, isolation, ownership, platform, or testing needs. Validation: `git diff --check` passed; PM project validation passed. No build/test run was needed because only repository instructions, skills, and architecture documentation changed.