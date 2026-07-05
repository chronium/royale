---
title: Third-Party Dependency Workflow
createdAt: 2026-07-05T16:15:06.4438470Z
modifiedAt: 2026-07-05T18:33:42.1692550Z
---

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
6. Fetch any pinned submodules required by the dependency's committed source tree.
7. Apply any `*.patch` files from the matching patch directory with `git apply --3way`.

The scripts should fail clearly if the pinned commit cannot be fetched, a required submodule cannot be fetched, or a patch cannot be applied.

`fetch-imgui-net.sh` also initializes the pinned native submodule graph required by Evergine's generated binding surface: cimgui, Dear ImGui, cimplot/implot, cimnodes/imnodes, and cimguizmo/ImGuizmo.

## Restore and Build Notes

SDL3-CS is consumed from the fetched source project at `thirdparty/repos/SDL3-CS/SDL3-CS/SDL3-CS.csproj`.

ImGui.Net is consumed from the fetched source project at `thirdparty/repos/ImGui.Net/Generator/Evergine.Bindings.Imgui/Evergine.Bindings.Imgui.csproj`.

For a fresh checkout after fetching SDL3-CS or ImGui.Net, restore the solution with the binding's desktop-target property:

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

## ImGui Native Shim Build

Build the project-owned macOS ARM64 ImGui shared library from the repository root with:

```sh
sh thirdparty/build-imgui-macos.sh
```

The script refreshes the pinned ImGui.Net source, verifies SDL3 development headers are available through `pkg-config sdl3`, compiles cimgui plus Dear ImGui's SDL3 platform backend and SDL_GPU renderer backend, and installs the generated library into:

```text
thirdparty/artifacts/imgui/osx-arm64/lib/libroyale_imgui.dylib
```

The ImGui shim includes cimgui symbols required by `Evergine.Bindings.Imgui` and project-owned `royale_imgui_*` C ABI entry points for backend lifetime, event forwarding, frame setup, and future draw-data submission. It intentionally uses SDL3 headers without linking against a separate SDL3 dylib, so the running client resolves SDL symbols through the SDL3-CS native library already copied beside `Royale.Client`.

After running the native ImGui build script, run restore again before no-restore .NET build or test commands because the deterministic third-party refresh removes generated `obj/` files under the ignored ImGui.Net checkout.

## Box3D Sample Validation

Validate the pinned upstream Box3D source from the repository root with:

```sh
sh thirdparty/fetch-box3d.sh
```

Then configure, build, and run the bounded upstream sample app from `thirdparty/repos/box3d`:

```sh
cmake --preset macos
cmake --build --preset macos-release
./build/bin/Release/samples --frames 3
```

A passing macOS ARM64 sample run exits with status `0` and reports `samples: 3 frames, 0 sokol errors`.

The upstream sample app opens a native graphics window. In sandboxed agent sessions, the configure/build steps may need host compiler/Xcode access, and the sample run may need host GUI access. Keep generated CMake, FetchContent, Xcode, and binary outputs inside the ignored `thirdparty/repos/box3d` tree.

Linux validation should use the matching upstream Linux preset once a task explicitly brings Linux x64 validation back into scope.

## Box3D Shared Library Build

Build the project-owned macOS ARM64 Box3D shared library from the repository root with:

```sh
sh thirdparty/build-box3d-macos.sh
```

The script refreshes the pinned upstream source with `thirdparty/fetch-box3d.sh`, configures a Release shared-library build with upstream samples, tests, benchmarks, docs, and profiling disabled, and installs the generated files into the ignored artifact directory:

```text
thirdparty/artifacts/box3d/osx-arm64/
```

The expected library output is:

```text
thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.dylib
```

Box3D native lifecycle tests on macOS ARM64 copy this artifact into the `Royale.Box3D.Tests` output as `libbox3d.dylib`. Build the artifact before running the solution tests on a fresh checkout or after cleaning ignored build outputs.

Linux and Windows Box3D shared-library builds are intentionally deferred until dedicated platform tasks define and validate those workflows.

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
