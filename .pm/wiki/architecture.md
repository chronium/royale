---
title: Architecture
createdAt: 2026-07-05T16:10:17.2845730Z
modifiedAt: 2026-07-11T18:49:29.7939910Z
---

## Overview

The architecture wiki is split into focused pages so each area can be kept current without turning one document into a catch-all.

Start here:

* `architecture/overview` - project shape, architectural goals, solution layout, and dependency direction
* `architecture/runtime-processes` - game client and dedicated server responsibilities
* `architecture/simulation-and-authority` - tick model, authority boundaries, prediction, reconciliation, interpolation, and ownership
* `architecture/networking` - transport, connection, protocol, replication, versioning, and in-process development mode
* `architecture/physics-and-combat` - Box3D ownership, player controller, combat flow, and match state machine
* `architecture/content-and-rendering` - map/content data, SDL GPU rendering, shaders, screenshot validation, and presentation state
* `architecture/diagnostics-testing-deployment` - threading, error handling, diagnostics, testing, deployment shape, constraints, and data flow
* `diagnostics` - concrete logging implementation, log shape, sinks, and lifecycle log policy

The main invariant across all pages is unchanged: the dedicated server owns gameplay authority; the client owns interaction, prediction, presentation, and rendering.

Editor architecture:

* `architecture/editor` - graphical process boundaries, docked workspace, map documents, ImGuizmo transforms, face snapping, validation, playtesting, and deferred editor capabilities
* `development/editor-mcp` - live editor MCP transport, security, document revisions, mutation tools, and model contact sheets