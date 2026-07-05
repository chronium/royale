---
title: Third-Party Agent Responsibilities
createdAt: 2026-07-05T16:15:06.4975700Z
modifiedAt: 2026-07-05T16:15:06.4975700Z
---

## Agent Responsibilities

Agents working on third-party dependencies must:

* Use the PM board task for the dependency work.
* Move the task to `doing` before changing files.
* Ask before adding a dependency, changing a pin, or introducing a patch whose purpose is not obvious.
* Keep the wiki updated with dependency layout and update procedure changes.
* Keep patches minimal and reviewable.
* Verify patch application from a clean fetch when network access is available.
* Move the PM task to `done` only after the scripts, patches, docs, and validation are complete.

## Commit Scope

Third-party dependency commits should contain only the files required for the dependency change:

* Fetch scripts
* Version pins
* Patch files
* Documentation
* Project files required to consume the dependency

Do not commit cloned repositories or generated build output.

## Review Focus

Review third-party changes for:

* Pinned commit correctness
* Patch necessity and scope
* Reproducibility from a clean fetch
* Native/runtime packaging impact
* Cross-platform behavior
* Documentation accuracy