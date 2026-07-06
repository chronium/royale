---
title: Third-Party Dependency Workflow
createdAt: 2026-07-05T16:15:06.4438470Z
modifiedAt: 2026-07-06T18:53:06.8279800Z
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
sh thirdparty/fetch-blurgtext.sh
sh thirdparty/fetch-simplemesh.sh
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

`fetch-imgui-net.sh` initializes the pinned native submodule graph required by Evergine's generated binding surface: cimgui, Dear ImGui, cimplot/implot, cimnodes/imnodes, and cimguizmo/ImGuizmo.

`fetch-blurgtext.sh` initializes the upstream submodules needed by BlurgText's committed source tree: `deps/libraqm`, `deps/SheenBidi`, `deps/libunibreak`, `deps/plutosvg`, and nested `deps/plutosvg/plutovg`.

`fetch-simplemesh.sh` has no submodule or native build steps at the pinned revision.

## Restore and Build Notes

SDL3-CS is consumed from the fetched source project at `thirdparty/repos/SDL3-CS/SDL3-CS/SDL3-CS.csproj`.

ImGui.Net is consumed from the fetched source project at `thirdparty/repos/ImGui.Net/Generator/Evergine.Bindings.Imgui/Evergine.Bindings.Imgui.csproj`.

BlurgText is consumed by `Royale.Client` from the fetched managed project at `thirdparty/repos/blurgtext/dotnet/BlurgText/BlurgText.csproj` with `TargetFramework=net8.0`. This is client/rendering-only; the dedicated server must not reference BlurgText or receive Blurg native artifacts.

SimpleMesh is fetched to `thirdparty/repos/SimpleMesh` as managed-only source. It is Apache-2.0 licensed, targets `net8.0`, supports OBJ, Collada, and embedded-buffer glTF/glb import, and imports Y-up geometry. `RENDER-010` owns adding any future project reference or mesh loading/rendering code.

For a fresh checkout after fetching SDL3-CS, ImGui.Net, BlurgText, or SimpleMesh, restore the solution with the binding's desktop-target property:

```sh
dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1
```

This avoids requiring Android workloads for the SDL3-CS Android target during desktop client work. After restore has produced assets files, the normal no-restore build and test commands remain:

```sh
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

When a client project consumes SDL3-CS from source by project reference, it must explicitly copy the runtime-native SDL library from `thirdparty/repos/SDL3-CS/native/<rid>/` into the client output. Project references build the managed binding but do not automatically place the native package asset beside the consuming executable.

`Royale.Client` currently copies SDL3, the project-owned ImGui shim, and BlurgText for macOS ARM64 into `runtimes/osx-arm64/native/`:

```text
runtimes/osx-arm64/native/libSDL3.dylib
runtimes/osx-arm64/native/libroyale_imgui.dylib
runtimes/osx-arm64/native/libblurgtext.dylib
```

Additional SDL satellite libraries such as SDL3_image, SDL3_mixer, or SDL3_ttf should be copied only when a task introduces a concrete dependency on them.

## ImGui Native Shim Build

Build the project-owned macOS ARM64 ImGui shared library from the repository root with:

```sh
sh thirdparty/build-imgui-macos.sh
```

The script refreshes the pinned ImGui.Net source, verifies SDL3 development headers are available through `pkg-config sdl3`, compiles cimgui plus Dear ImGui's SDL3 platform backend and SDL_GPU renderer backend, and installs the generated library into:

```text
thirdparty/artifacts/imgui/osx-arm64/lib/libroyale_imgui.dylib
```

The ImGui shim includes cimgui symbols required by `Evergine.Bindings.Imgui` and project-owned `royale_imgui_*` C ABI entry points for backend lifetime, event forwarding, frame setup, and future draw-data submission. It intentionally uses SDL3 headers without linking against a separate SDL3 dylib, so the running client resolves SDL symbols through the SDL3-CS native library copied into `runtimes/osx-arm64/native/`.

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

Build the project-owned Linux x64 Box3D shared library from a Linux x64 environment with:

```sh
sh thirdparty/build-box3d-linux.sh
```

The scripts refresh the pinned upstream source with `thirdparty/fetch-box3d.sh`, configure a Release shared-library build with upstream samples, tests, benchmarks, docs, and profiling disabled, and install generated files into ignored artifact directories:

```text
thirdparty/artifacts/box3d/osx-arm64/
thirdparty/artifacts/box3d/linux-x64/
```

The expected upstream library outputs are:

```text
thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.0.1.0.dylib
thirdparty/artifacts/box3d/linux-x64/lib/libbox3d.so
```

Managed build outputs copy these artifacts under the canonical resolver filenames:

```text
runtimes/osx-arm64/native/libbox3d.dylib
runtimes/linux-x64/native/libbox3d.so
```

Box3D runtime-native copy rules live in `Royale.Box3D`, so clients, servers, and Box3D tests receive the native library through their project reference. The server receives Box3D only; SDL, ImGui, shaders, textures, and other client-only assets must stay out of the server output.

For Linux x64 validation from an Apple Silicon host, use OrbStack Docker with an amd64 .NET SDK container:

```sh
docker run --rm --platform linux/amd64 -v "$PWD:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'sh thirdparty/build-box3d-linux.sh && dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1 && dotnet build Royale.slnx -m:1 --no-restore && dotnet test Royale.slnx -m:1 --no-restore'
```

The SDK image may need CMake and C/C++ build tools installed before running `build-box3d-linux.sh`. Build the relevant Box3D artifact before running solution tests on a fresh checkout or after cleaning ignored build outputs.

Windows Box3D shared-library builds are intentionally deferred until a dedicated platform task defines and validates that workflow.

## BlurgText Native Build Shape

Build the project-owned macOS ARM64 BlurgText shared library from the repository root with:

```sh
sh thirdparty/build-blurgtext-macos.sh
```

The script refreshes the pinned upstream source with `thirdparty/fetch-blurgtext.sh`, configures the upstream CMake project for Release macOS ARM64 with `BT_BUILD_DEMO=OFF`, builds the `blurgtext` shared-library target, and copies the generated dylib into:

```text
thirdparty/artifacts/blurgtext/osx-arm64/lib/libblurgtext.dylib
```

`Royale.Client` copies that artifact to:

```text
runtimes/osx-arm64/native/libblurgtext.dylib
```

`NativeLibraryResolver` maps BlurgText's managed import name `libblurgtext` to `libblurgtext.dylib` for `osx-arm64`. The BlurgText dependency remains client/rendering-only; the dedicated server must not reference BlurgText, SDL GPU, textures, font assets, or UI code.

The pinned upstream BlurgText tree still provides the native build entry point at `thirdparty/repos/blurgtext/CMakeLists.txt`, and the upstream .NET native package projects remain under `thirdparty/repos/blurgtext/dotnet/BlurgText.Native.<rid>/`.

Linux x64 and Windows BlurgText native build/copy support are intentionally deferred to dedicated platform tasks. The expected upstream native library names from the pinned packaging metadata remain:

```text
libblurgtext.dylib   # osx-arm64 and osx-x64
libblurgtext.so      # linux-x64
```

BlurgText is MIT licensed. Distribution or packaging work must also carry the upstream notices called out by BlurgText's README, including Harfbuzz, SheenBidi, libraqm, and the FreeType credit notice.

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

No project-specific patches are currently required for SDL3-CS, box3d, ImGui.Net, BlurgText, or SimpleMesh.

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
