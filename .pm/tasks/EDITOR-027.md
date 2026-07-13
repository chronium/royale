---
id: EDITOR-027
title: Add constrained gizmo face snapping
track: EDITOR
milestone: M6
dependsOn:
- EDITOR-007
createdAt: 2026-07-13T14:08:28.9962930Z
modifiedAt: 2026-07-13T14:26:29.1042620Z
---

Add an optional Face toggle for the Translate gizmo. While dragging an axis handle, face snapping changes only that axis and preserves the other two coordinates; while dragging a plane handle, it preserves the excluded coordinate. Rotation is unchanged, the selected collider is excluded, exact surface contact takes precedence over grid quantization, unreachable surfaces do not produce a face-snap preview, and release commits one undoable transform command.

## Notes

- 2026-07-13 14:21 UTC - Implemented constrained Translate-gizmo face snapping behind a persisted, default-off `Face` checkbox. Axis handles preserve the other two World/Local coordinates; plane handles preserve the excluded coordinate; rotation/scale stay unchanged. The drag retains a runtime-equivalent collision world, excludes the selected box/model collider, separates the raw gizmo candidate from the displayed exact-contact preview, treats unreachable surfaces as misses, shares hit debug markers, commits one transform command, and restores/disposes on cancellation, document lifecycle, failure, or shutdown. Collision/Box3D failures disable Face and report through Validation, Log, and structured logging. Updated `architecture/editor` with the interaction and lifecycle contract. Validation on 2026-07-13: focused face-snap/settings suite 35/35; full editor suite 198/198; full solution build 0 warnings/0 errors; full solution tests 1,186/1,186; scoped formatter verification passed; `git diff --check` passed; PM validation passed; editor output contains `runtimes/osx-arm64/native/libbox3d.dylib`; automated screenshot smoke test `/tmp/editor-027-smoke.png` confirmed the new toolbar control fits the reset docked layout. Owner validation remains requested for axis/plane target selection, preview readability, exact floor-then-wall workflow, Local/World behavior, miss fallback, right-click/Escape cancellation, grid-snap interaction, and overall snapping feel.
- 2026-07-13 14:26 UTC - Owner validation passed on 2026-07-13: constrained gizmo face snapping works well in the editor. Follow-up edge and vertex selection-mode ideas were captured separately as deferred improvement tasks IMPROVE-003 and IMPROVE-004.