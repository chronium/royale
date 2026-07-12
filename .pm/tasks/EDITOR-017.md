---
id: EDITOR-017
title: Suppress ImGui interaction during viewport camera capture
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-003
createdAt: 2026-07-12T07:56:31.1493200Z
modifiedAt: 2026-07-12T08:03:38.9899410Z
---

While the editor viewport owns right-mouse camera capture, prevent ImGui hover and activation behavior from reacting to captured pointer input so camera look cannot highlight or accidentally activate docked UI. Restore normal ImGui input immediately when capture ends. Add deterministic input-ownership coverage and owner validation.

## Notes

- 2026-07-12 08:03 UTC - Implemented explicit viewport input ownership, idempotent ImGui global mouse suppression, relative-mode synchronization, and immediate release on RMB release, Escape, viewport closure, focus loss, and disposal. Focused validation passed: `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj -m:1 --no-restore` (23 tests) and `dotnet test tests/Royale.Rendering.Tests/Royale.Rendering.Tests.csproj -m:1 --no-restore` (73 tests). Wiki `architecture/editor` updated. Owner UI validation remains required before moving to done.