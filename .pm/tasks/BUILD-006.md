---
id: BUILD-006
title: Split architecture wiki into focused pages
track: BUILD
createdAt: 2026-07-05T16:08:53.8482120Z
modifiedAt: 2026-07-05T16:12:30.0000000Z
---

Restructure the long architecture wiki page into focused architecture subpages using PM wiki tools, preserving source-of-truth content and replacing the root architecture page with a concise index.

## Completion Notes

- Used PM wiki rename support to move the original monolithic `architecture` page to `architecture/overview` while preserving its created timestamp.
- Replaced root `architecture` with a concise landing page linking to focused architecture subpages.
- Split architecture content into:
  - `architecture/overview`
  - `architecture/runtime-processes`
  - `architecture/simulation-and-authority`
  - `architecture/networking`
  - `architecture/physics-and-combat`
  - `architecture/content-and-rendering`
  - `architecture/diagnostics-testing-deployment`
- Preserved the source-of-truth architecture topics while making each page smaller and easier to maintain.