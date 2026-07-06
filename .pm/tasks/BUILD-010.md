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

## Validation

- Fresh fetch from the script succeeds from a clean checkout state.
- The BlurgText project or native build entry point needed by Royale is available after fetch.
- `dotnet build Royale.slnx -m:1 --no-restore` passes after restore state is available.
- PM project validation passes.