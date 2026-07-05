---
title: Third-Party Dependency Policy
createdAt: 2026-07-05T16:14:33.1590290Z
modifiedAt: 2026-07-05T16:14:33.1590290Z
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

## Rationale

This approach keeps the main repository free of submodule state while preserving reproducibility.

A developer or CI job can recreate third-party source by running committed scripts. Project-specific changes remain visible as normal patch files, and dependency updates become explicit reviewable changes to version pins and patches.