---
id: EDITOR-020
title: Define the Royale map project format
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
- ASSET-001
createdAt: 2026-07-12T09:08:09.0321060Z
modifiedAt: 2026-07-12T09:33:54.3097030Z
---

Define a versioned directory-based editable map project containing a small Royale project manifest, map source data, imported source assets, generated runtime artifacts, project-local caches, and editor metadata. Specify portable relative paths, ownership, cache invalidation boundaries, ignored/generated content, migration behavior, and the distinction between editable project data and exported runtime content.

## Notes

- 2026-07-12 09:33 UTC - Implemented the version 1 `.royaleproject` format in `Royale.Editor.Projects`: canonical layout/constants and `.gitignore`, strict deterministic manifest serialization, validated contained paths, exact package/project/map identity, explicit unsupported-version failure, map and source-manifest loading, and loaded project data. Source model-manifest loading retains its existing non-empty default while project loading explicitly permits empty manifests for box-only maps. Updated `architecture/editor` with layout, ownership, identity/versioning, cache fingerprints, macOS package intent, and follow-on task ownership. Validation: `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj -m:1 --no-restore` (76 passed); `dotnet test tests/Royale.Content.Tests/Royale.Content.Tests.csproj -m:1 --no-restore` (6 passed); scoped `dotnet format ... --verify-no-changes` passed (workspace-load warning only); `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings and 0 errors; `git diff --check` passed. No current maps/assets were migrated and editor opening behavior was intentionally unchanged for EDITOR-021.