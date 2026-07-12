---
id: EDITOR-004
title: Add map documents, undo, and atomic persistence
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-003
createdAt: 2026-07-11T18:46:38.7302880Z
modifiedAt: 2026-07-12T08:51:28.0027650Z
---

Load the existing GameMap JSON format into an editor document with stable entity identities, dirty-state tracking, command-based undo and redo, runtime validation, external-change detection, and explicit atomic Save and Save As.

## Notes

- 2026-07-12 08:28 UTC - Implemented single-map editor documents with editor-only identities, command history/checkpoints, display-name undo/redo, repository-aware and explicit source resolution, shared map-file validation/serialization, queued SDL Open/Save As dialogs, atomic fingerprint-checked persistence, shortcuts, dirty title, and Save/Discard/Cancel prompts. Updated architecture/editor with the resulting contract. Validation: `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings/errors; `dotnet test Royale.slnx -m:1 --no-restore` passed all 1,009 tests; focused Editor tests passed 42/42; native macOS smoke `dotnet run --project src/Royale.Editor/Royale.Editor.csproj --no-restore --no-build -- --map-file /tmp/editor-004-graybox.json --screenshot /tmp/editor-004.bmp --screenshot-after-frames 3` loaded and rendered the temporary explicit map successfully. Owner validation requested for interactive native Open/Save As dialogs, Cmd/Ctrl shortcuts, dirty indicator, undo/redo, external-change error presentation, and Save/Discard/Cancel behavior for Open and Close.
- 2026-07-12 08:51 UTC - Owner validation saved graybox through the editor and explicitly chose to retain the resulting canonical JSON normalization. Content tests passed 6/6 before committing the format/order-only rewrite separately.