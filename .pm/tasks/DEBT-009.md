---
id: DEBT-009
title: Extract and test editor unsaved-document workflow
track: DEBT
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
createdAt: 2026-07-12T09:00:35.7743010Z
modifiedAt: 2026-07-13T08:17:35.5506640Z
---

Move pending Open and Close coordination, Save/Discard/Cancel decisions, dialog cancellation, save failures, and continuation behavior out of private SDL/ImGui orchestration into a deterministic testable component. Add coverage that protects unsaved map data across every workflow outcome.

## Notes

- 2026-07-13 08:17 UTC - Implemented a deterministic EditorDocumentWorkflow in Royale.Editor.Documents for New Project, Open Project, Open Map, Convert, and Close. EditorApplication now delegates unsaved decisions and save outcomes to the workflow while retaining SDL dialogs, ImGui rendering, persistence, logging, and host exit. Replaced the overloaded PendingOperation with narrow project-destination state that clears on cancellation, dialog error, completion, and failure. Candidate document/project resources are built before active resources are replaced, and project documents are marked saved only after project reload and fingerprint refresh succeed. Added focused workflow coverage for all clean/dirty transitions, Save/Discard/Cancel, in-place and Save As success, save failure, Save As cancellation, repeated requests, and cleanup; persistence/session tests explicitly preserve dirty in-memory state on failed standalone and project saves. Updated architecture/editor with the deterministic workflow and cancellation contract and removed the obsolete statement that New Project is disabled.

  Automated validation (2026-07-13): `dotnet test tests/Royale.Editor.Tests/Royale.Editor.Tests.csproj -m:1 --no-restore` passed 154/154; `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings and 0 errors; `dotnet test Royale.slnx -m:1 --no-restore` passed all 1,141 tests; `dotnet format Royale.slnx --no-restore --verify-no-changes --include src/Royale.Editor tests/Royale.Editor.Tests` passed (workspace-load warning only); `git diff --check` passed.

  Owner validation requested and pending for native Open/Close behavior and Save As cancellation because these flows depend on ImGui and platform-native dialogs.