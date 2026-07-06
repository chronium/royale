---
id: BUILD-010
title: Add BlurgText third-party dependency
track: BUILD
milestone: M3
priority: medium
dependsOn:
- BUILD-003
createdAt: 2026-07-06T12:57:52.6502240Z
modifiedAt: 2026-07-06T12:58:11.9920860Z
---

Fetch BlurgText from https://github.com/CallumDev/blurgtext at pinned commit `ea49c33b27ad55cc811dc8be4c9829ed4367d936` through the thirdparty script workflow, without submodules in the main repo and without Technicraft local patches.

## Intent

BlurgText is the planned text stack for game-facing text outside ImGui, including future player HUD text and world-space labels. ImGui remains the development tooling UI.

## Requirements

- Add a `thirdparty` fetch script that clones `https://github.com/CallumDev/blurgtext` at depth 1 for commit `ea49c33b27ad55cc811dc8be4c9829ed4367d936`.
- Follow the existing third-party layout: fetched repositories stay under ignored `thirdparty/repos/`, patches would live under `thirdparty/patches/`, and the main repository must not use Git submodules.
- Do not carry over Technicraft's old local macOS override or patch; native macOS support is considered upstreamed for this pinned commit.
- Document the BlurgText pin, fetch workflow, build/runtime artifact expectations, and license considerations in the third-party wiki pages.
- Keep this task focused on dependency acquisition and build/package shape; do not implement Royale text rendering here.

## Implementation Notes

- Added `BLURGTEXT_REPO` and `BLURGTEXT_COMMIT` to `thirdparty/versions.env`.
- Added `thirdparty/fetch-blurgtext.sh`, which initializes or reuses `thirdparty/repos/blurgtext`, fetches the pinned commit, resets and cleans the checkout, initializes required upstream submodules, and applies optional patches from `thirdparty/patches/blurgtext/*.patch`.
- Registered BlurgText in `thirdparty/fetch-all.sh`.
- Added `thirdparty/patches/blurgtext/README.md`; no project-specific patches are currently required.
- Updated `thirdparty/README.md` and the third-party wiki pages for the pin, fetch workflow, layout, managed project path, native build shape, deferred runtime packaging boundary, and license notice expectations.
- Kept the change fetch-only: no Royale project reference, native build script, renderer integration, HUD text, or runtime packaging was added.

## Validation

- `sh thirdparty/fetch-blurgtext.sh` succeeds and checks out `ea49c33b27ad55cc811dc8be4c9829ed4367d936`.
- `thirdparty/repos/blurgtext/dotnet/BlurgText/BlurgText.csproj` exists after fetch.
- Required BlurgText submodules are populated at their pinned commits: `deps/libraqm`, `deps/SheenBidi`, `deps/libunibreak`, `deps/plutosvg`, and `deps/plutosvg/plutovg`.
- `thirdparty/patches/blurgtext/` contains no `*.patch` files, so no project patches are applied.
- `sh thirdparty/fetch-all.sh` succeeds with the new BlurgText step included.
- Initial `dotnet build Royale.slnx -m:1 --no-restore` failed because `fetch-all` removed ignored restore assets under `thirdparty/repos/SDL3-CS/SDL3-CS/obj/`, matching the documented need to restore after deterministic third-party refreshes.
- `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` succeeds, with the existing ImGui.Net `NU1510` warning.
- `dotnet build Royale.slnx -m:1 --no-restore` succeeds after restore, with the existing ImGui.Net `NU1510` warning.
- PM project validation passes.