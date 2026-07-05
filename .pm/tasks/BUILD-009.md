---
id: BUILD-009
title: Document get_next_task readiness guidance
track: BUILD
createdAt: 2026-07-05T21:08:18.1896950Z
modifiedAt: 2026-07-05T21:08:53.2560010Z
---

Update repository agent instructions to explain when agents should use get_next_task with readyOnly true versus the default blocked-task-aware mode.

## Notes

- 2026-07-05 21:08 UTC - Updated `AGENTS.md` with guidance for `get_next_task`: use `readyOnly: true` before implementation, use the default mode for planning or blocker diagnosis, and inspect dependency readiness and blockers before acting. No wiki update was needed because this is repository-agent workflow guidance.