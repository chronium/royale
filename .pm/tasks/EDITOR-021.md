---
id: EDITOR-021
title: Add map project creation and lifecycle
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
- EDITOR-020
createdAt: 2026-07-12T09:08:09.2281650Z
modifiedAt: 2026-07-12T16:57:16.4971530Z
---

Let the editor create, open, validate, and atomically save directory-based Royale map projects. Restore the most recent project where appropriate, expose project identity and paths in the editor, resolve maps and assets relative to the project root, and preserve the existing standalone JSON opening path as an explicit import or compatibility workflow.

## Notes

- 2026-07-12 16:57 UTC - Implemented `.royaleproject` lifecycle: `--project` and startup precedence/conflict handling; atomic recent-project state with invalid-recent fallback; transactional starter-project creation and standalone-map conversion with referenced-only source assets; source-manifest mesh loading; project sessions and authoritative fingerprints; in-place project save; native folder dialogs; File menu New/Open Project/Open Map JSON/Convert/Save/Exit; project path/title presentation; and save/discard/cancel routing that validates candidates before active-session replacement. Updated `architecture/editor` with the lifecycle contract. Validation: `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj -m:1 --no-restore` (84 passed); Content tests (6 passed); Rendering tests (73 passed); formatter verify passed (workspace-load warning only); `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings/errors; native editor `--map graybox` three-frame screenshot smoke completed and the rendered workspace was visually inspected. Owner validation remains requested for native folder dialogs and the interactive New/Open/Convert/recent-restoration workflow.