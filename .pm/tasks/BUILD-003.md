---
id: BUILD-003
title: Add native dependency layout
track: BUILD
milestone: M0
createdAt: 2026-07-04T09:21:21.7632700Z
modifiedAt: 2026-07-04T09:22:33.6202270Z
---

Pin SDL3, Box3D, ImGui, and their C# bindings and define a consistent repository layout for native dependencies.

## Implementation Notes

Added committed `thirdparty/` dependency-management layout without Git submodules or committed cloned repositories.

Pinned dependencies in `thirdparty/versions.env`:

- SDL3-CS: `https://github.com/ppy/SDL3-CS` at `a0a5276a874c0c48db705696ab7e2adc8b5db0a1`
- box3d: `https://github.com/erincatto/box3d` at `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f`
- ImGui.Net: `https://github.com/EvergineTeam/ImGui.Net` at `1f97beecfc9b83e1549e9782757cf85b1777cb9d`

Added deterministic fetch scripts for each dependency plus `fetch-all.sh`. Each script initializes an ignored clone under `thirdparty/repos/`, fetches the pinned commit with depth 1, checks out detached `FETCH_HEAD`, resets and cleans the dependency worktree, then applies any `*.patch` files from the matching patch directory.

No native SDL3 pin was added separately; SDL3 native availability is assumed to come through SDL3-CS until a later platform/native packaging task establishes a different requirement.

Updated the `third-party-dependencies` wiki page to reflect the committed layout, selected repositories, pins, fetch behavior, patch policy, and update workflow.

## Validation

- `sh -n thirdparty/fetch-all.sh thirdparty/fetch-sdl3-cs.sh thirdparty/fetch-box3d.sh thirdparty/fetch-imgui-net.sh`
- `git check-ignore -v thirdparty/repos/SDL3-CS thirdparty/build/output thirdparty/artifacts/package thirdparty/fetch.log thirdparty/tmp/file.tmp`
- `dotnet build Royale.slnx -m:1 --no-restore`
- `dotnet test Royale.slnx -m:1 --no-restore`

The fetch scripts were shell-parse checked but not network-executed in this sandboxed implementation pass, so no dependency clones or generated native artifacts were created.