---
id: EDITOR-019
title: Evaluate a native macOS editor font
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-003
createdAt: 2026-07-12T07:56:31.6082930Z
modifiedAt: 2026-07-12T07:56:37.9118540Z
---

Evaluate replacing the default ImGui font on macOS with an appropriate system font such as San Francisco. Confirm legal/system-path availability, fallback behavior, DPI scaling, glyph coverage, packaging implications, and cross-platform policy before adopting it. Record the chosen font contract and visually validate readability.