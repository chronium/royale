# Third-Party Dependencies

This directory contains the committed dependency-management files for native and binding source dependencies. It does not contain cloned third-party repositories or generated build outputs.

## Policy

- Do not add Git submodules for third-party source.
- Do not commit cloned repositories, build directories, native artifacts, logs, or temporary files.
- Keep dependency revisions pinned to full commit SHAs in `versions.env`.
- Keep project-specific third-party changes as ordered patch files under `patches/<dependency>/`.
- Ask before changing a dependency pin, adding a dependency, or introducing a non-obvious patch.

## Pinned Dependencies

| Dependency | Repository | Commit | Purpose |
| --- | --- | --- | --- |
| SDL3-CS | `https://github.com/ppy/SDL3-CS` | `a0a5276a874c0c48db705696ab7e2adc8b5db0a1` | C# bindings and native availability for SDL3. |
| box3d | `https://github.com/erincatto/box3d` | `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f` | Physics library source for future project-specific native builds and bindings. |
| ImGui.Net | `https://github.com/EvergineTeam/ImGui.Net` | `1f97beecfc9b83e1549e9782757cf85b1777cb9d` | C# ImGui bindings for client development UI. |
| BlurgText | `https://github.com/CallumDev/blurgtext` | `ea49c33b27ad55cc811dc8be4c9829ed4367d936` | Game-facing text outside ImGui. |
| SimpleMesh | `https://github.com/CallumDev/SimpleMesh` | `9f46341e35fa5876fbea7b96bd021bc3abd7842d` | Managed mesh import source staged for future client rendering work. |
| WattleScript | `https://github.com/WattleScript/wattlescript` | `b8ccc1930733c25c8a25e6087fc29a4c555562fe` | Interpreter source staged for automated gameplay test orchestration. |
| LiteNetLib | `https://github.com/RevenantX/LiteNetLib` | `37cbf5ab608a4dbd0e491c528a0c14c1e09f1cba` | Managed UDP networking source staged for `NET-001` transport work. |

Native SDL3 is not pinned separately yet. Until platform packaging tasks prove a different requirement, SDL3 native availability is expected to come through the selected SDL3-CS source.

## Layout

```text
thirdparty/
  versions.env
  fetch-all.sh
  fetch-sdl3-cs.sh
  fetch-box3d.sh
  fetch-imgui-net.sh
  fetch-blurgtext.sh
  fetch-simplemesh.sh
  fetch-wattlescript.sh
  fetch-litenetlib.sh
  build-blurgtext-macos.sh
  repos/                 # ignored clones created by fetch scripts
  build/                 # ignored native build output
  artifacts/             # ignored generated packages or binaries
  patches/
    SDL3-CS/
    box3d/
    ImGui.Net/
    blurgtext/
    SimpleMesh/
    wattlescript/
    LiteNetLib/
```

## Fetching Source

Fetch every dependency:

```sh
sh thirdparty/fetch-all.sh
```

Fetch one dependency:

```sh
sh thirdparty/fetch-sdl3-cs.sh
sh thirdparty/fetch-box3d.sh
sh thirdparty/fetch-imgui-net.sh
sh thirdparty/fetch-blurgtext.sh
sh thirdparty/fetch-simplemesh.sh
sh thirdparty/fetch-wattlescript.sh
sh thirdparty/fetch-litenetlib.sh
```

Each script initializes or reuses an ignored repository under `thirdparty/repos/<dependency>`, fetches the pinned commit with depth 1, checks out detached `FETCH_HEAD`, resets and cleans the working tree, then applies any `*.patch` files from the matching patch directory.

`fetch-blurgtext.sh` also initializes the upstream submodules needed by BlurgText's committed source tree, including `deps/libraqm`, `deps/SheenBidi`, `deps/libunibreak`, `deps/plutosvg`, and nested `deps/plutosvg/plutovg`.

SimpleMesh has no project patches, submodules, or native build steps at the pinned revision. It is Apache-2.0 licensed, managed-only, targets `net8.0`, supports OBJ, Collada, and embedded-buffer glTF/glb import, and imports Y-up geometry. `RENDER-010` owns adding any future project reference or mesh loading/rendering code.

WattleScript has no project patches, submodules, or native build steps at the pinned revision. It is BSD 3-Clause licensed and contains string-library parts derived from KopiLua under the upstream license notice. It is fetched as interpreter source for automated gameplay test orchestration only. `TEST-001` owns adding any future test host, scenario API, script execution, smoke tests, or project references; WattleScript must not become a runtime dependency of the client, server, simulation, or protocol projects.

LiteNetLib has no project patches, submodules, or native build steps at the pinned revision. It is fetched as managed UDP networking source only and is staged for `NET-001` transport work; this pin does not add runtime project references or change protocol, client, server, or transport APIs. The pinned project targets `net8.0;netstandard2.1` and declares MIT package metadata.

After fetching, LiteNetLib's managed project is expected at:

```text
thirdparty/repos/LiteNetLib/LiteNetLib/LiteNetLib.csproj
```

After fetching, WattleScript's interpreter project is expected at:

```text
thirdparty/repos/wattlescript/src/WattleScript.Interpreter/WattleScript.Interpreter.csproj
```

After fetching, BlurgText's managed project is expected at:

```text
thirdparty/repos/blurgtext/dotnet/BlurgText/BlurgText.csproj
```

BlurgText's native build entry point is the upstream CMake project at:

```text
thirdparty/repos/blurgtext/CMakeLists.txt
```

The upstream CMake project defines the shared-library target `blurgtext` and the pinned .NET native package metadata expects `libblurgtext.dylib` for macOS RIDs and `libblurgtext.so` for Linux x64.

Build the project-owned macOS ARM64 BlurgText shared library with:

```sh
sh thirdparty/build-blurgtext-macos.sh
```

The script installs:

```text
thirdparty/artifacts/blurgtext/osx-arm64/lib/libblurgtext.dylib
```

`Royale.Client` copies that artifact to `runtimes/osx-arm64/native/libblurgtext.dylib` and resolves BlurgText's managed import name `libblurgtext` through `Royale.Native`. Linux x64 and Windows BlurgText native build/copy support remain deferred to dedicated platform tasks.

BlurgText is MIT licensed. Distribution or packaging work must also carry the upstream notices called out by BlurgText's README, including Harfbuzz, SheenBidi, libraqm, and FreeType credit.

## Patches

Patch directories are committed even when empty so the dependency-specific patch locations are stable.

Generate focused patches from the fetched dependency working tree:

```sh
git -C thirdparty/repos/SDL3-CS diff --binary > thirdparty/patches/SDL3-CS/0001-description.patch
```

Use ordered filenames such as `0001-description.patch`. After changing a patch or pin, rerun the relevant fetch script to verify a clean checkout plus patch application succeeds.

## Updating a Dependency

1. Ask before changing the pinned revision.
2. Update the full commit SHA in `versions.env`.
3. Run the dependency fetch script.
4. Re-apply and refresh patches only when necessary.
5. Build and test the affected project areas.
6. Commit only scripts, docs, pin changes, and patch files.
