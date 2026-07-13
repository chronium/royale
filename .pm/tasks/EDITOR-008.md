---
id: EDITOR-008
title: Add validation and playtest launching
track: EDITOR
milestone: M6
priority: medium
dependsOn:
- EDITOR-004
- EDITOR-006
- EDITOR-007
createdAt: 2026-07-11T18:46:39.7379620Z
modifiedAt: 2026-07-13T17:05:45.5555010Z
---

Expose runtime-equivalent map, asset, navigation, spawn, bounds, and collision validation and add a Save and Launch workflow that starts the normal development server and client with the saved map.

## Notes

- 2026-07-13 17:05 UTC - Implemented runtime-equivalent validation and managed Save and Launch. Added client/server `--map-file` and direct `--asset-root` overrides, map-ID matching when `--map` is explicit, custom collision/render loading through offline play, network prediction/reconciliation, and authoritative server simulation, plus the post-initialization `ROYALE_SERVER_READY` marker. The editor now reports structured schema/bounds, manifests, render assets/resources, collision/Box3D, physical navigation, and all-spawn collision/safe-zone/overlap stages; project validation builds isolated client/server outputs and stale results track document/asset revisions. Save and Launch preserves validated outputs, resumes standalone Save As only after success, captures prefixed child output, replaces/stops paired process trees, handles timeout/early/unexpected exit, and cleans artifacts on stop/shutdown.

  Validation: documented formatter verification passed; `git diff --check` passed; `dotnet build Royale.slnx --no-restore -m:1 --disable-build-servers` succeeded with 0 warnings/errors; `dotnet test Royale.slnx --no-restore --no-build -m:1 --disable-build-servers` passed all 1,213 tests. A finite development server smoke using both overrides emitted `ROYALE_SERVER_READY map=graybox port=7777` and completed one tick. Native macOS editor screenshot smoke loaded and rendered graybox with the updated build. The raw SDL window was not exposed through macOS accessibility, so automated interaction with the Validation tab was unavailable.

  Updated wiki pages `architecture/editor` and `development/launching`. Owner validation requested for Validation panel layout/report readability, Save and Launch client startup and server/client log capture, relaunch replacement, and Stop Playtest behavior.