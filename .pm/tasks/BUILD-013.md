---
id: BUILD-013
title: Add public contribution and security docs
track: BUILD
milestone: M1
createdAt: 2026-07-06T16:54:02.5667900Z
modifiedAt: 2026-07-06T16:54:02.5667900Z
---

Add short root `CONTRIBUTING.md` and `SECURITY.md` files for public repository polish. The contribution guide should point to `AGENTS.md`, the PM board workflow, third-party dependency policy, and the core ask-before-assuming rule. The security note should describe the project as an experimental prototype and ask reporters to use GitHub private vulnerability reporting or security advisories when enabled.

## Notes

- 2026-07-06 - Added root `CONTRIBUTING.md` pointing contributors to `AGENTS.md`, PM task workflow, wiki source-of-truth expectations, ask-before-assuming, third-party dependency policy, validation expectations, and task-prefixed commits.
- Added root `SECURITY.md` describing Royale as an experimental prototype and requesting private vulnerability reporting through GitHub private vulnerability reporting or Security Advisories when enabled.
- Validation: PM project validation passed. Full build/test was not run because this only adds repository documentation.
