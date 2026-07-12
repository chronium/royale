---
id: EDITOR-024
title: Add project asset import and physical folders
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-021
- ASSET-003
createdAt: 2026-07-12T09:08:09.7982390Z
modifiedAt: 2026-07-12T19:22:44.4679940Z
---

Add transactional multi-model GLB import to active .royaleproject sessions and turn the asset browser into a physical folder browser rooted at assets/. Imports use editable globally stable IDs, per-row collision settings, reusable asset-pipeline processing, external-resource validation, staged generated outputs, journaling, rollback, and recovery. The browser scans without following symlinks, supports folder navigation/search and create/rename/move/delete operations, rewrites manifest paths transactionally, preserves selection, and keeps generated outputs/caches outside the asset tree. Add focused tests, update editor/project-format/asset-pipeline/third-party workflow documentation, and request owner UI validation.

## Notes

- 2026-07-12 19:10 UTC - 2026-07-12 implementation pass: extracted the reusable non-executable Royale.AssetPipeline library while retaining a thin CLI/MSBuild wrapper; added strict GLB 2.0 external-resource inspection, portable asset ID/folder paths, physical asset-tree scanning without symlink traversal, breadcrumbs/search/model association, transactional batch import staging/journaling/recovery, folder create/move/rename/delete services with manifest path rewrites, multi-select GLB/separate-collision dialog support, session import refresh, and focused tests. Validation passed: `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1`; `dotnet build Royale.slnx -m:1 --no-restore`; `dotnet test Royale.slnx -m:1 --no-restore --no-build` (all suites); `git diff --check`. Wiki updated: editor asset browser/project format, content/rendering asset pipeline, third-party workflow. Remaining before done: wire the Import Assets modal and two-pane physical browser/folder commands into EditorApplication/ImGui, add deeper import rollback/recovery/folder transaction integration tests, run Kenney visual workflow, and obtain owner UI validation. Task remains doing.
- 2026-07-12 19:22 UTC - 2026-07-12 continuation: wired the physical two-pane asset browser, breadcrumbs/recursive search/path selection, multi-file import modal with editable IDs and all collision modes, row-associated separate collision chooser, diagnostics/inclusion/removal, and project-folder Create/Rename/Move/Delete commands into EditorApplication. Consolidated import and folder-move commits behind one internal directory transaction that stages assets plus both generated audiences and journals each swap for rollback/startup recovery. Added project-session folder methods and physical-browser preservation coverage. Validation passed: `dotnet build Royale.slnx -m:1 --no-restore` (0 warnings/errors); `dotnet test Royale.slnx -m:1 --no-restore --no-build` (all 14 suites, 1,089 tests); focused editor suite 102/102; `git diff --check`. Editor wiki updated for delivered UI and transaction semantics. Remaining: owner visual/UI validation of modal, folder browser, previews, navigation, and errors; the requested Kenney `column-low.glb` fixture is not present locally (`column.glb` is), so the exact two-file visual import workflow was not captured. Task intentionally remains doing until owner validation.