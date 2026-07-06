---
id: BUILD-015
title: Reorganize Royale.Client file structure
track: BUILD
milestone: M1
createdAt: 2026-07-06T17:24:44.8685800Z
modifiedAt: 2026-07-06T17:24:44.8685800Z
---

Restructure `Royale.Client` folders so `Platform` contains SDL/platform glue instead of mixed client presentation concerns. Move launch options, input mapping, camera/render mode controllers, timing helpers, and ImGui diagnostics into focused folders with matching namespaces. Keep the change mechanical and behavior-preserving; do not introduce new abstractions or alter gameplay/rendering behavior.

## Implementation Notes

- Moved client launch parsing into `Royale.Client.Launch`.
- Moved input state, gameplay input mapping, debug camera input mapping, and input ownership into `Royale.Client.Input`.
- Moved fixed-step accumulation into `Royale.Client.Timing`.
- Moved camera-mode and render-view mode controllers into `Royale.Client.Presentation`.
- Moved ImGui backend, capture state, and diagnostics state into `Royale.Client.UI`.
- Left `Royale.Client.Platform` focused on SDL application, window, GPU device, and relative mouse mode plumbing.
- Updated client tests to reference the new namespaces without changing behavior.
- Updated `architecture/overview` so the wiki reflects the current client folder layout and actual `Royale.*` dependency direction.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- `validate_project` passed.
- Existing warning remains: ImGui.Net emits `NU1510` for `System.Runtime.CompilerServices.Unsafe`; this was not introduced by the restructure.