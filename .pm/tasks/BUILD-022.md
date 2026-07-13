---
id: BUILD-022
title: Add quick development launch scripts
track: BUILD
milestone: M6
createdAt: 2026-07-13T06:28:50.7466110Z
modifiedAt: 2026-07-13T06:30:35.4210780Z
---

Add repository-root-independent shell scripts under launch/ for starting the editor, a development server with a required map ID, an offline client, and a connected development client. Use existing JSON launch profiles, --no-restore --no-build, passthrough CLI overrides, and an overridable OTLP endpoint defaulting to http://127.0.0.1:4317. Document usage, validate shell syntax and argument forwarding, update the development workflow wiki, and commit the focused change.

## Notes

- 2026-07-13 06:30 UTC - Implemented executable POSIX launch wrappers under `launch/` for the editor, required-map development server, offline client, and connected development client. Scripts resolve the repository root from their own location, use existing JSON profiles with `--no-restore --no-build`, forward remaining CLI arguments, and default the overridable OTLP endpoint to `http://127.0.0.1:4317`. Added `launch/README.md` and wiki page `development/launching`. Validation: `sh -n` passed for all four scripts; missing server map returned usage and exit 2; `./launch/server.sh graybox --run-ticks 1` loaded port 7777/map graybox/development timing and exited cleanly after one tick; `git diff --check` passed.