---
id: PHYS-011
title: Bind generated convex hull and static mesh collision
track: PHYS
milestone: M6
priority: medium
dependsOn:
- PHYS-007
createdAt: 2026-07-10T06:15:47.9545450Z
modifiedAt: 2026-07-10T06:16:06.2242710Z
---

Extend the focused Box3D C# binding and managed ownership wrappers for arbitrary convex hull creation, hull destruction, mesh definitions, mesh creation and destruction, and static mesh shapes required by generated model collision artifacts. Verify native layouts and ownership lifetimes, reject invalid generated geometry safely, preserve low-level binding access, and add focused hull/mesh creation, query, debug-draw, and disposal tests.