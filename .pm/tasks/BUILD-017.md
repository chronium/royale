---
id: BUILD-017
title: Add LiteNetLib third-party dependency
track: BUILD
milestone: M5
priority: medium
dependsOn:
- BUILD-003
createdAt: 2026-07-06T19:35:38.4598460Z
modifiedAt: 2026-07-06T19:35:43.4401630Z
---

Add LiteNetLib as a pinned managed third-party source dependency using the repository thirdparty workflow. Fetch https://github.com/RevenantX/LiteNetLib at commit 37cbf5ab608a4dbd0e491c528a0c14c1e09f1cba and document the pin for the networking transport work.

## Implementation Notes

- Added `LITENETLIB_REPO` and `LITENETLIB_COMMIT` to `thirdparty/versions.env`.
- Added `thirdparty/fetch-litenetlib.sh` and included it in `thirdparty/fetch-all.sh`.
- Added `thirdparty/patches/LiteNetLib/README.md`; no project-specific patches are required at this pin.
- Updated `thirdparty/README.md` and PM wiki pages `third-party-dependencies/pins`, `third-party-dependencies/workflow`, and `third-party-dependencies/layout`.
- LiteNetLib remains source-only for this task. No `Royale.Client`, `Royale.Server`, protocol, or transport project references were added.

## Validation Notes

- `sh thirdparty/fetch-litenetlib.sh` fetched `https://github.com/RevenantX/LiteNetLib` at `37cbf5ab608a4dbd0e491c528a0c14c1e09f1cba`.
- Verified expected project path: `thirdparty/repos/LiteNetLib/LiteNetLib/LiteNetLib.csproj`.
- Verified `thirdparty/repos/LiteNetLib/LICENSE.txt` exists and begins with `MIT License`.
- Verified `LiteNetLib.csproj` declares `TargetFrameworks` `net8.0;netstandard2.1` and `PackageLicenseExpression` `MIT`.
- `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui.Net `NU1510` warning about `System.Runtime.CompilerServices.Unsafe`.
- `mcp__pm.validate_project` passed.