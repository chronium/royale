---
id: BUILD-023
title: Use normal parallel .NET validation
track: BUILD
milestone: M6
createdAt: 2026-07-13T09:22:30.6946790Z
modifiedAt: 2026-07-13T09:24:49.9884980Z
---

Remove active single-node build and test guidance from README, build-validation skill and helper script, and PM wiki. Preserve escalation requirements and historical task evidence.

## Notes

- 2026-07-13 09:24 UTC - Removed active single-node .NET validation guidance from README, royale-build-validation/SKILL.md, its validate-dotnet.sh helper, and third-party-dependencies/workflow (including the Linux Docker example). Audited every AGENTS.md and all repository skills: AGENTS.md contained no single-node guidance, and no active -m:1, /m:1, maxcpucount:1, single-node, or single-worker wording remains outside immutable historical task evidence. Escalation/approval guidance was not changed. Validation: bash -n passed for validate-dotnet.sh; the updated helper ran outside the sandbox for required local IPC and completed a normal parallel solution build with 0 warnings/errors plus all 1,158 tests; PM wiki search returned no remaining -m:1 matches; git diff --check passed. The generic skill quick validator could not start because the host Python lacks the yaml module; skill frontmatter was unchanged and the edited shell workflow was syntax-checked and exercised end-to-end.