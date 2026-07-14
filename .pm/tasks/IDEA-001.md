---
id: IDEA-001
title: Explore brush-based orthographic map authoring
track: IDEA
priority: none
createdAt: 2026-07-14T14:07:06.2346520Z
modifiedAt: 2026-07-14T14:07:09.2543580Z
---

Explore a Hammer-style grid-first map-authoring mode with four synchronized viewports: one textured perspective world view and top, front, and side orthographic wireframe views. Investigate authoring textured geometric brushes and primitives such as boxes, wedges, cylinders, columns, arches, and partial circular forms with precise grid snapping and coordinated selection and manipulation across views. Before implementation, define whether brushes remain editable parametric source data, how they compile or export into Royale render geometry and Box3D collision, how textures and materials are mapped, and which limited constructive operations are justified without turning the editor into a general-purpose CSG engine. This is a long-term concept, not a near-term implementation commitment.