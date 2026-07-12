---
id: EDITOR-026
title: Face model thumbnails toward the viewer
track: EDITOR
milestone: M6
createdAt: 2026-07-12T18:51:49.7851960Z
modifiedAt: 2026-07-12T18:54:09.9384490Z
---

Rotate the cached model-thumbnail camera to the opposite elevated diagonal so directional model fronts face the viewer. Keep identity model transforms and existing framing/density, bump the thumbnail renderer fingerprint to invalidate prior cache entries, update deterministic camera tests and editor documentation, validate regenerated Kenney thumbnails, request owner visual confirmation, and commit the focused change.

## Notes

- 2026-07-12 18:54 UTC - Rotated the model-thumbnail camera from the elevated positive-X/positive-Z diagonal to the elevated negative-X/negative-Z diagonal while preserving identity model transforms, 60-degree projection, padding, density, background, and lighting. Bumped the renderer fingerprint to model-thumbnail-v2 and updated its settings signature so v1 cache entries regenerate and are cleaned up after valid replacements. Added a deterministic camera-quadrant assertion and updated the editor architecture wiki.

  Validation: Royale.Rendering.Tests passed 90/90; Royale.Editor.Tests passed 92/92. Sequential full solution build passed with 0 warnings/errors. Formatter verification and git diff --check passed. Runtime editor capture /tmp/editor-026-front-facing.png regenerated the Kenney set and visibly presents the target face, stair approach, slope face, and doorway front toward the viewer. Owner visual confirmation remains requested in the reopened editor.