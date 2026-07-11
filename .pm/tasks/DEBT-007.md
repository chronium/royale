---
id: DEBT-007
title: Evaluate and introduce navmesh navigation
track: DEBT
milestone: M8
priority: low
dependsOn:
- BOT-010
createdAt: 2026-07-11T06:28:06.7649350Z
modifiedAt: 2026-07-11T06:28:10.9076830Z
---

Evaluate replacing or supplementing authored bot waypoint graphs with navmesh support for larger and more geometrically complex maps. Cover a concrete bake or generation workflow, map asset representation, validation, runtime path queries, off-mesh or vertical traversal needs, debug visualization, deterministic tests, and a migration or coexistence strategy for existing waypoint-authored maps. This is post-MVP technical debt and must not expand BOT-005.