---
id: EDITOR-028
title: Add annotated MCP spatial region capture
track: EDITOR
milestone: M8
priority: low
dependsOn:
- EDITOR-012
createdAt: 2026-07-14T14:04:07.6269950Z
modifiedAt: 2026-07-14T14:04:10.5504870Z
---

Add a read-only editor MCP tool that captures an arbitrary world-space region as top-down and elevated-diagonal PNG views annotated with deterministic point identifiers, region bounds, coordinate references, nearby map geometry, and optional collision overlays. Accept explicit candidate points or a bounded XZ probe grid, plus an optional box or manifest-asset placement volume with rotation and scale. Test proposed placements against authoritative map collision while allowing non-penetrating support contact. Return structured metadata alongside the images containing each probe position, availability result, overlapping entity or collider identities, nearby entities, camera/view information, and the inspected bounds. Keep structured collision results as the geometric source of truth and cap probe density so annotations remain readable.