---
title: Third-Party Dependencies
createdAt: 2026-07-05T16:14:33.1499480Z
modifiedAt: 2026-07-05T16:14:33.1499480Z
---

## Overview

The third-party dependency wiki is split into focused pages so policy, layout, pins, workflows, and maintenance rules stay easy to update.

Start here:

* `third-party-dependencies/overview` - full source-of-truth page for the dependency policy and workflow
* `third-party-dependencies/policy` - no-submodule policy, pinning rules, patches, and rationale
* `third-party-dependencies/layout` - committed third-party directory structure and ignore rules
* `third-party-dependencies/pins` - current dependency pins and their purposes
* `third-party-dependencies/workflow` - fetch, restore, build, patch, and update procedures
* `third-party-dependencies/agent-responsibilities` - responsibilities for agents changing third-party dependencies

The key invariant remains: third-party source is reproduced by scripts from pinned commits, not by Git submodules or committed dependency clones.