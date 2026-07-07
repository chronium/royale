---
id: BUILD-018
title: Add WattleScript third-party dependency
track: BUILD
milestone: M4
createdAt: 2026-07-07T04:34:18.1486670Z
modifiedAt: 2026-07-07T04:45:00.0000000Z
---

Pin WattleScript under `thirdparty` using the project fetch-script workflow, without submodules. Add a depth-1 clone script for the selected WattleScript repository and commit, document the pin in the third-party wiki, and ensure cloned repositories and generated artifacts remain ignored.

## Completion Notes

- Pinned `https://github.com/WattleScript/wattlescript` to upstream `main` commit `b8ccc1930733c25c8a25e6087fc29a4c555562fe`.
- Added `thirdparty/fetch-wattlescript.sh` and wired it into `thirdparty/fetch-all.sh`.
- Added the stable patch location `thirdparty/patches/wattlescript/`; no project-specific patches are currently required.
- Documented WattleScript in `thirdparty/README.md` and PM wiki third-party dependency pages.
- Confirmed the pinned interpreter project is `thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj` and targets `netstandard2.0`.
- Confirmed the upstream license is BSD 3-Clause and notes KopiLua-derived string-library parts.
- No references were added to `Royale.Client`, `Royale.Server`, `Royale.Simulation`, or `Royale.Protocol`; WattleScript remains staged for future automated gameplay test orchestration work owned by `TEST-001`.

## Validation

- `sh thirdparty/fetch-wattlescript.sh` passed after network access was allowed.
- `test -f thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj` passed.
- `sh -n thirdparty/fetch-wattlescript.sh` passed.
- `sh -n thirdparty/fetch-all.sh` passed.
- `dotnet restore thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj` passed.
- `dotnet build thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj -m:1 --no-restore` passed with six upstream warnings and no errors.