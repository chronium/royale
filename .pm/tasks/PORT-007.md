---
id: PORT-007
title: Produce a macOS editor application bundle
track: PORT
milestone: M8
priority: low
dependsOn:
- EDITOR-008
- EDITOR-023
- EDITOR-025
createdAt: 2026-07-12T09:08:17.7238270Z
modifiedAt: 2026-07-12T09:08:28.3677490Z
---

Package Royale.Editor as a native macOS ARM64 .app bundle with its managed application, SDL and other native libraries, shaders, editor resources, and document/project associations. Ensure map projects and caches remain outside the application bundle, native Open/Save dialogs work from the bundle, paths do not depend on the launch working directory, and document signing/notarization requirements separately from the initial unsigned development bundle.