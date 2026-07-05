---
title: Third-Party Dependencies
createdAt: 2026-07-05T07:54:48.9395240Z
modifiedAt: 2026-07-05T07:54:48.9395240Z
---

## Overview

Third-party source dependencies should be reproducible without using Git submodules in the main repository.

The main repository should contain:

* Scripts that fetch third-party source at pinned commits
* Patch files needed to adapt those dependencies for this project
* Documentation describing why each dependency exists and how it is updated

The main repository should not contain cloned third-party repositories, generated native build artifacts, or submodule metadata.

This keeps the main repo simpler to clone and avoids submodule state becoming part of normal development flow.

## Policy

Third-party vendored source must follow these rules:

1. Do not add third-party projects as Git submodules.
2. Do not commit cloned third-party repositories into the main repo.
3. Clone third-party repositories under `thirdparty/` using project-owned shell scripts.
4. Pin every third-party dependency to an explicit commit SHA.
5. Fetch pinned commits shallowly where possible.
6. Keep project-specific modifications as patch files under `thirdparty/patches/`.
7. Apply patches after cloning or fetching the pinned source.
8. Keep cloned repositories and generated artifacts ignored by `thirdparty/.gitignore`.
9. Document the reason for each dependency and its update process.
10. Ask before changing a pinned third-party revision or adding a new third-party dependency.

## Directory Layout

The intended layout is:

```text
thirdparty/
  .gitignore
  README.md
  versions.env
  fetch-all.sh
  fetch-sdl3-cs.sh
  repos/
    SDL3-CS/              # ignored clone, created by scripts
  patches/
    SDL3-CS/
      0001-example.patch
  build/
    ...                   # ignored generated native build output
  artifacts/
    ...                   # ignored packaged output
```

The exact script names may change, but the separation should remain clear:

* `thirdparty/repos/` contains ignored cloned repositories.
* `thirdparty/patches/` contains committed project patches.
* `thirdparty/build/` contains ignored temporary build output.
* `thirdparty/artifacts/` contains ignored generated binaries or packages.

## Ignore Rules

`thirdparty/.gitignore` should ignore cloned repositories and generated artifacts while allowing scripts, docs, version pins, and patches to be committed.

A starting point:

```gitignore
/repos/
/build/
/artifacts/
/**/*.tmp
/**/*.log
.DS_Store
```

If a future dependency requires committed generated files, document why before changing these rules.

## Version Pins

Pinned revisions should live in a small committed file such as `thirdparty/versions.env`.

Example:

```sh
SDL3_CS_REPO="https://github.com/flibitijibibo/SDL3-CS.git"
SDL3_CS_COMMIT="<commit-sha>"
```

Use full commit SHAs, not branch names, tags, or floating references.

Tags may be mentioned in comments for human context, but scripts should fetch and check out the exact commit.

## Fetch Script Pattern

A fetch script should be deterministic and safe to rerun.

For a pinned commit, prefer `git fetch --depth 1 origin <commit>` over a normal clone of a branch.

Example pattern for `thirdparty/fetch-sdl3-cs.sh`:

```sh
#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$SCRIPT_DIR/versions.env"

DEST="$SCRIPT_DIR/repos/SDL3-CS"

if [ ! -d "$DEST/.git" ]; then
    mkdir -p "$DEST"
    git -C "$DEST" init
    git -C "$DEST" remote add origin "$SDL3_CS_REPO"
fi

git -C "$DEST" fetch --depth 1 origin "$SDL3_CS_COMMIT"
git -C "$DEST" checkout --detach FETCH_HEAD

git -C "$DEST" reset --hard FETCH_HEAD
git -C "$DEST" clean -xfd

PATCH_DIR="$SCRIPT_DIR/patches/SDL3-CS"
if [ -d "$PATCH_DIR" ]; then
    for patch in "$PATCH_DIR"/*.patch; do
        [ -e "$patch" ] || continue
        git -C "$DEST" apply --3way "$patch"
    done
fi
```

The script may be adjusted for portability, but it should preserve these properties:

* It checks out the pinned commit exactly.
* It can be rerun without manual cleanup.
* It applies committed patches after restoring the dependency source.
* It fails clearly if the pinned commit or patch cannot be applied.

## Patch Policy

Project-specific changes to third-party code must be stored as patches under `thirdparty/patches/<dependency>/`.

Patch filenames should be ordered and descriptive:

```text
thirdparty/patches/SDL3-CS/
  0001-fix-native-library-resolution.patch
  0002-adjust-generated-bindings-for-net10.patch
```

Patches should be generated from the dependency repository after making the required change:

```sh
git -C thirdparty/repos/SDL3-CS diff --binary > thirdparty/patches/SDL3-CS/0001-description.patch
```

Keep patches focused. If a patch contains unrelated changes, split it before committing.

When updating a dependency commit, re-apply the patch series and refresh patches only when needed.

## First Dependency: SDL3-CS

The first third-party source dependency is:

```text
Name: SDL3-CS
Repository: https://github.com/flibitijibibo/SDL3-CS
Purpose: C# bindings for SDL3 and related SDL APIs
Location: thirdparty/repos/SDL3-CS
Patches: thirdparty/patches/SDL3-CS
```

SDL3-CS should be fetched by script at a pinned commit.

Do not add it as a submodule.

Do not commit the cloned `SDL3-CS` working tree.

If local changes are needed for this project, capture them as ordered patch files under `thirdparty/patches/SDL3-CS/` and document the reason for each patch.

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
* Verify patch application from a clean fetch.
* Move the PM task to `done` only after the scripts, patches, docs, and validation are complete.

## Rationale

This approach keeps the main repository free of submodule state while preserving reproducibility.

A developer or CI job can recreate third-party source by running committed scripts. Project-specific changes remain visible as normal patch files, and dependency updates become explicit reviewable changes to version pins and patches.