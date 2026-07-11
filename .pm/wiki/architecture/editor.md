---
title: Game and Map Editor
createdAt: 2026-07-11T18:49:21.0208000Z
modifiedAt: 2026-07-11T18:49:21.0208000Z
---

## Purpose

Royale will provide a standalone, ImGui-based map editor for authoring the existing runtime map format. The editor exists to make Royale maps easier for humans and agents to build; it is not a general-purpose engine or scene editor.

The first usable editor targets macOS ARM64. Shared graphical code should remain portable so Linux support can be validated later.

## Graphical Process Boundaries

The graphical applications are:

```text
Royale.Client
  -> Royale.Platform
  -> Royale.Rendering

Royale.Editor
  -> Royale.Platform
  -> Royale.Rendering
  -> Royale.Content
  -> Royale.Simulation
  -> Royale.Box3D
```

`Royale.Platform` owns reusable SDL window, event, input plumbing, timing, and desktop application lifecycle behavior.

`Royale.Rendering` owns reusable SDL GPU device and render-target management, cameras, mesh and material resources, debug primitives, ImGui SDL3_GPU integration, GPU readback, and screenshots. It supports swapchain and offscreen targets without becoming a general scene framework.

Client networking, gameplay presentation, telemetry, and client-specific UI remain in `Royale.Client`. The server and shared simulation must not depend on Platform, Rendering, ImGui, SDL windowing, or the editor.

## Editor Workspace

The editor uses ImGui docking with a central viewport and dockable hierarchy, inspector, asset browser, validation, and log panels. ImGui multi-viewport support is deferred.

The first version supports one selected entity at a time. Selection works through viewport picking and the hierarchy. Multi-selection and group transforms are deferred.

ImGuizmo provides translate, rotate, and scale controls. Transform operations support local and world orientation and configurable position, angle, and scale increments. One completed drag produces one undo command.

## Map Documents

The editor loads and writes the existing `GameMap` JSON format. An editor-only mutable document model may retain stable entity identities and command history, but editor metadata must not leak into runtime map JSON.

The complete current map schema is editable:

- Static boxes and static models
- Spawn and loot points
- Navigation nodes and links
- World bounds
- Safe-zone settings

Documents track dirty state and use command-based undo and redo. Save and Save As are explicit operations; the editor does not continuously autosave.

Before saving, the editor runs runtime-equivalent structural, asset, navigation, spawn, bounds, and collision validation. Invalid documents remain dirty and are not written. Writes use a temporary file and atomic replacement. If the source changed externally after loading, the editor rejects the save instead of overwriting newer content.

## Face Snapping

Face snapping is mandatory for the first editor.

The user enters face-snap mode and selects a target collision surface. The editor places the selected object's oriented bounds flush against the hit plane and ignores the selected object's own collider.

Rotation is preserved by default. An optional alignment mode rotates a selected local attachment axis to the target surface normal. The editor displays a preview before commit, and the final placement is one undoable command.

Face snapping is bounds-based for the initial version; it does not promise arbitrary mesh-to-mesh feature matching.

## Playtesting

The initial editor does not embed a playable simulation. Save and Launch validates and saves the map, then starts the normal development server and client using existing launch profiles and the selected map.

## Deferred Capabilities

Physics-assisted placement is planned for decorative objects. It will temporarily simulate selected objects with Box3D using fixed ticks and settling thresholds, then bake stable transforms back into static map data as one cancellable undoable action.

WattleScript map behavior is also deferred. Future work may add authoritative scripted doors, buttons, lights, and other interactions. Script ownership must preserve server authority.