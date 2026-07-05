---
title: Third-Party Dependencies Overview
createdAt: 2026-07-05T07:54:48.9395240Z
modifiedAt: 2026-07-05T16:15:30.8886390Z
---

## Overview

Third-party source dependencies are reproducible without using Git submodules in the main repository.

The main repository contains:

* Scripts that fetch third-party source at pinned commits
* Patch directories for project-specific dependency changes
* Documentation describing why each dependency exists and how it is updated

The main repository must not contain cloned third-party repositories, generated native build artifacts, or submodule metadata.

This keeps the main repo simple to clone and avoids submodule state becoming part of normal development flow.

## Current Dependencies

| Dependency | Repository | Commit | Purpose |
| --- | --- | --- | --- |
| SDL3-CS | `https://github.com/ppy/SDL3-CS` | `a0a5276a874c0c48db705696ab7e2adc8b5db0a1` | C# bindings and native availability for SDL3. |
| box3d | `https://github.com/erincatto/box3d` | `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f` | Physics library source for future project-specific native builds and bindings. |
| ImGui.Net | `https://github.com/EvergineTeam/ImGui.Net` | `1f97beecfc9b83e1549e9782757cf85b1777cb9d` | C# ImGui bindings for client development UI. |

Use full commit SHAs, not branch names, tags, or floating references.

Native SDL3 is not pinned separately at this stage. Until platform packaging tasks prove a different requirement, SDL3 native availability is expected to come through the selected SDL3-CS source.

## Reproduction Model

A developer or CI job can recreate third-party source by running committed scripts. Project-specific changes remain visible as normal patch files, and dependency updates become explicit reviewable changes to version pins and patches.