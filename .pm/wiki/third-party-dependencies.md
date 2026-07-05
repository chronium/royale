---
title: Third-Party Dependencies
createdAt: 2026-07-05T07:54:48.9395240Z
modifiedAt: 2026-07-05T12:13:26.8617830Z
---

## Overview

Third-party source dependencies are reproducible without using Git submodules in the main repository.

The main repository contains:

* Scripts that fetch third-party source at pinned commits
* Patch directories for project-specific dependency changes
* Documentation describing why each dependency exists and how it is updated

The main repository must not contain cloned third-party repositories, generated native build artifacts, or submodule metadata.

This keeps the main repo simple to clone and avoids submodule state becoming part of normal development flow.

## Policy

Third-party vendored source must follow these rules:

1. Do not add third-party projects as Git submodules.
2. Do not commit cloned third-party repositories into the main repo.
3. Clone third-party repositories under `thirdparty/repos/` using project-owned shell scripts.
4. Pin every third-party dependency to an explicit full commit SHA.
5. Fetch pinned commits shallowly where possible.
6. Keep project-specific modifications as patch files under `thirdparty/patches/`.
7. Apply patches after cloning or fetching the pinned source.
8. Keep cloned repositories and generated artifacts ignored by `thirdparty/.gitignore`.
9. Document the reason for each dependency and its update process.
10. Ask before changing a pinned third-party revision or adding a new third-party dependency.

## Directory Layout

The committed layout is:

```text
thirdparty/
  .gitignore
  Directory.Build.props
  Directory.Packages.props
  README.md
  versions.env
  fetch-all.sh
  fetch-sdl3-cs.sh
  fetch-box3d.sh
  fetch-imgui-net.sh
  repos/                 # ignored clones, created by scripts
  build/                 # ignored generated native build output
  artifacts/             # ignored generated binaries or packages
  patches/
    SDL3-CS/
      README.md
    box3d/
      README.md
    ImGui.Net/
      README.md
```

The separation is intentional:

* `thirdparty/repos/` contains ignored cloned repositories.
* `thirdparty/patches/` contains committed project patches and placeholder README files.
* `thirdparty/build/` contains ignored temporary build output.
* `thirdparty/artifacts/` contains ignored generated binaries or packages.
* `thirdparty/Directory.Packages.props` prevents root Central Package Management from overriding dependency-owned package versions.
* `thirdparty/Directory.Build.props` records third-party build defaults that apply when a fetched dependency does not provide a nearer build props file.

## Ignore Rules

`thirdparty/.gitignore` ignores cloned repositories and generated artifacts while allowing scripts, docs, version pins, and patches to be committed.

The ignored paths include:

```gitignore
/repos/
/build/
/artifacts/
/**/*.tmp
/**/*.log
/.DS_Store
**/.DS_Store
Thumbs.db
*.swp
*.swo
```

If a future dependency requires committed generated files, document why before changing these rules.

## Version Pins

Pinned revisions live in `thirdparty/versions.env`.

Current pins:

| Dependency | Repository | Commit | Purpose |
| --- | --- | --- | --- |
| SDL3-CS | `https://github.com/ppy/SDL3-CS` | `a0a5276a874c0c48db705696ab7e2adc8b5db0a1` | C# bindings and native availability for SDL3. |
| box3d | `https://github.com/erincatto/box3d` | `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f` | Physics library source for future project-specific native builds and bindings. |
| ImGui.Net | `https://github.com/EvergineTeam/ImGui.Net` | `1f97beecfc9b83e1549e9782757cf85b1777cb9d` | C# ImGui bindings for client development UI. |

Use full commit SHAs, not branch names, tags, or floating references.

Native SDL3 is not pinned separately at this stage. Until platform packaging tasks prove a different requirement, SDL3 native availability is expected to come through the selected SDL3-CS source.

## Fetch Scripts

Fetch all dependencies with:

```sh
sh thirdparty/fetch-all.sh
```

Fetch individual dependencies with:

```sh
sh thirdparty/fetch-sdl3-cs.sh
sh thirdparty/fetch-box3d.sh
sh thirdparty/fetch-imgui-net.sh
```

Each fetch script is deterministic and safe to rerun:

1. Initialize the ignored repository under `thirdparty/repos/<dependency>` if needed.
2. Set the `origin` remote to the URL in `versions.env`.
3. Fetch the pinned commit with `git fetch --depth 1 origin <commit>`.
4. Check out detached `FETCH_HEAD`.
5. Reset hard to `FETCH_HEAD` and clean ignored/untracked files inside the dependency clone.
6. Apply any `*.patch` files from the matching patch directory with `git apply --3way`.

The scripts should fail clearly if the pinned commit cannot be fetched or a patch cannot be applied.

## Restore and Build Notes

SDL3-CS is consumed from the fetched source project at `thirdparty/repos/SDL3-CS/SDL3-CS/SDL3-CS.csproj`.

For a fresh checkout after fetching SDL3-CS, restore the solution with the binding's desktop-target property:

```sh
dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1
```

This avoids requiring Android workloads for the SDL3-CS Android target during desktop client work. After restore has produced assets files, the normal no-restore build and test commands remain:

```sh
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

When a client project consumes SDL3-CS from source by project reference, it must explicitly copy the runtime-native SDL library from `thirdparty/repos/SDL3-CS/native/<rid>/` into the client output. Project references build the managed binding but do not automatically place the native package asset beside the consuming executable.

`Royale.Client` currently copies only `SDL3` itself for desktop RIDs needed by the platform window task: `osx-arm64`, `osx-x64`, `linux-arm64`, `linux-x64`, `win-arm64`, and `win-x64`. Additional SDL satellite libraries such as SDL3_image, SDL3_mixer, or SDL3_ttf should be copied only when a task introduces a concrete dependency on them.

## Patch Policy

Project-specific changes to third-party code must be stored as patches under `thirdparty/patches/<dependency>/`.

Patch filenames should be ordered and descriptive:

```text
thirdparty/patches/SDL3-CS/
  0001-fix-native-library-resolution.patch
  0002-adjust-generated-bindings-for-net10.patch
```

Generate patches from the dependency repository after making the required change:

```sh
git -C thirdparty/repos/SDL3-CS diff --binary > thirdparty/patches/SDL3-CS/0001-description.patch
```

Keep patches focused. If a patch contains unrelated changes, split it before committing.

When updating a dependency commit, re-apply the patch series and refresh patches only when needed.

No project-specific patches are currently required for SDL3-CS, box3d, or ImGui.Net.

## Updating a Third-Party Dependency

To update a dependency:

1. Ask before changing the pinned revision.
2. Update the relevant commit SHA in `thirdparty/versions.env`.
3. Run the dependency fetch script.
4. Re-apply existing patches.
5. Fix or refresh patches if they no longer apply.
6. Build and test the project areas affected by the dependency.
7. Document the update in the relevant PM task and wiki page.
8. Commit only scripts, version pin changes, patch files, and documentation.

Do not commit the cloned dependency repository or generated build output.

## Build Artifacts

Native build outputs should go under ignored directories such as:

```text
thirdparty/build/
thirdparty/artifacts/
```

Project packaging tasks may copy selected final native binaries into project-controlled runtime or packaging directories later, but generated third-party build directories should remain ignored.

## Agent Responsibilities

Agents working on third-party dependencies must:

* Use the PM board task for the dependency work.
* Move the task to `doing` before changing files.
* Ask before adding a dependency, changing a pin, or introducing a patch whose purpose is not obvious.
* Keep the wiki updated with dependency layout and update procedure changes.
* Keep patches minimal and reviewable.
* Verify patch application from a clean fetch when network access is available.
* Move the PM task to `done` only after the scripts, patches, docs, and validation are complete.

## Rationale

This approach keeps the main repository free of submodule state while preserving reproducibility.

A developer or CI job can recreate third-party source by running committed scripts. Project-specific changes remain visible as normal patch files, and dependency updates become explicit reviewable changes to version pins and patches.