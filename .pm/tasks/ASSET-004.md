---
id: ASSET-004
title: Add reusable Blender map export workflow
track: ASSET
milestone: M6
priority: medium
dependsOn:
- ASSET-003
- BOT-005
createdAt: 2026-07-11T07:44:33.7958070Z
modifiedAt: 2026-07-11T07:48:04.0649070Z
---

Define and implement the reusable Blender-authored map scene contract and deterministic exporter. Validate render/collision/marker collections, marker IDs and links, transforms and ownership; convert Blender coordinates and marker yaw to Royale; export render and collision GLBs plus deterministic map JSON; support validation-only and export modes; document the authoring contract. Blender remains authoring-only and committed outputs remain .NET build inputs.

## Notes

- 2026-07-11 07:48 UTC - 2026-07-11 - Implemented the reusable Blender map exporter and Blender-independent contract validator. The scene contract covers render/collision/spawn/loot/navigation collections, required map properties, marker naming/ownership, finite yaw-only transforms, `(x,z,-y)` coordinate conversion, Royale yaw, canonical undirected links, deterministic JSON, selected render GLB export, and material-free collision GLB export. Added seven focused Python tests for coordinate conversion, stability/canonicalization, missing collections, malformed/duplicate markers, invalid links, and unsupported transforms. Validation passed: `python3 -m unittest discover -s tools/blender -p 'test_*.py' -v` (7 passed), `python3 -m py_compile ...`, and `git diff --check`. Updated the Content and Rendering wiki with the complete Blender authoring/export contract. Actual courtyard scene construction and Blender-native export are intentionally assigned to dependent GAME-017.