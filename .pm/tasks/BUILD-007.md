---
id: BUILD-007
title: Split project overview and third-party wiki pages
track: BUILD
createdAt: 2026-07-05T16:13:25.1517710Z
modifiedAt: 2026-07-05T16:16:00.0000000Z
---

Restructure the long project overview and third-party dependency wiki pages into focused subpages using PM wiki tools, preserving source-of-truth content and replacing each root page with a concise index.

## Completion Notes

- Used PM wiki rename support to move `project-overview` to `project-overview/overview` and `third-party-dependencies` to `third-party-dependencies/overview` while preserving created timestamps.
- Recreated root `project-overview` and `third-party-dependencies` pages as concise landing pages.
- Split project overview content into:
  - `project-overview/overview`
  - `project-overview/goals`
  - `project-overview/technology-stack`
  - `project-overview/game-scope`
  - `project-overview/development-principles`
- Split third-party dependency content into:
  - `third-party-dependencies/overview`
  - `third-party-dependencies/policy`
  - `third-party-dependencies/layout`
  - `third-party-dependencies/pins`
  - `third-party-dependencies/workflow`
  - `third-party-dependencies/agent-responsibilities`
- Preserved source-of-truth content while reducing duplicate long-form pages.