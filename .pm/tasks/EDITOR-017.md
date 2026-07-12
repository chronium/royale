---
id: EDITOR-017
title: Suppress ImGui interaction during viewport camera capture
track: EDITOR
milestone: M6
priority: high
dependsOn:
- EDITOR-003
createdAt: 2026-07-12T07:56:31.1493200Z
modifiedAt: 2026-07-12T07:56:37.8932690Z
---

While the editor viewport owns right-mouse camera capture, prevent ImGui hover and activation behavior from reacting to captured pointer input so camera look cannot highlight or accidentally activate docked UI. Restore normal ImGui input immediately when capture ends. Add deterministic input-ownership coverage and owner validation.