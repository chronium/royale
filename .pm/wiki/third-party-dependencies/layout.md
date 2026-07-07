---
title: Third-Party Dependency Layout
createdAt: 2026-07-05T16:14:33.2369430Z
modifiedAt: 2026-07-07T04:41:59.3266630Z
---

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
  build-box3d-macos.sh
  build-box3d-linux.sh
  build-imgui-macos.sh
  build-blurgtext-macos.sh
  fetch-sdl3-cs.sh
  fetch-box3d.sh
  fetch-imgui-net.sh
  fetch-blurgtext.sh
  fetch-simplemesh.sh
  fetch-wattlescript.sh
  royale_imgui/          # committed project-owned C ABI shim source
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
    blurgtext/
      README.md
    SimpleMesh/
      README.md
    wattlescript/
      README.md
```

The separation is intentional:

* `thirdparty/repos/` contains ignored cloned repositories.
* `thirdparty/patches/` contains committed project patches and placeholder README files.
* `thirdparty/royale_imgui/` contains the committed project-owned native shim source that is built together with the ignored pinned ImGui.Net checkout.
* `thirdparty/build/` contains ignored temporary build output.
* `thirdparty/artifacts/` contains ignored generated binaries or packages.
* `thirdparty/Directory.Packages.props` prevents root Central Package Management from overriding dependency-owned package versions.
* `thirdparty/Directory.Build.props` records third-party build defaults that apply when a fetched dependency does not provide a nearer build props file.

## Ignore Rules

`thirdparty/.gitignore` ignores cloned repositories and generated artifacts while allowing scripts, docs, version pins, project-owned shim source, and patches to be committed.

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

## Build Artifacts

Native build outputs should go under ignored directories such as:

```text
thirdparty/build/
thirdparty/artifacts/
```

The current Box3D macOS ARM64 shared-library workflow uses:

```text
thirdparty/build/box3d/osx-arm64/
thirdparty/artifacts/box3d/osx-arm64/
```

The current Box3D Linux x64 shared-library workflow uses:

```text
thirdparty/build/box3d/linux-x64/
thirdparty/artifacts/box3d/linux-x64/
```

The current ImGui macOS ARM64 shared-library workflow uses:

```text
thirdparty/build/imgui/osx-arm64/
thirdparty/artifacts/imgui/osx-arm64/lib/libroyale_imgui.dylib
```

The current BlurgText macOS ARM64 shared-library workflow uses:

```text
thirdparty/build/blurgtext/osx-arm64/
thirdparty/artifacts/blurgtext/osx-arm64/lib/libblurgtext.dylib
```

WattleScript is currently fetched as managed interpreter source only and has no project-owned native build artifact layout.

Project packaging tasks may copy selected final native binaries into project-controlled runtime or packaging directories later, but generated third-party build directories should remain ignored.

## Runtime Native Layout

Managed projects load bundled native libraries from the standard `.NET` runtime-native layout under each build output:

```text
runtimes/osx-arm64/native/libSDL3.dylib
runtimes/osx-arm64/native/libbox3d.dylib
runtimes/osx-arm64/native/libroyale_imgui.dylib
runtimes/osx-arm64/native/libblurgtext.dylib
runtimes/linux-x64/native/libbox3d.so
```

`Royale.Native` owns the shared import-name resolver for this layout. The current resolver mappings are:

* `osx-arm64` `SDL3` -> `libSDL3.dylib`
* `osx-arm64` `box3d` -> `libbox3d.dylib`
* `osx-arm64` `cimgui` -> `libroyale_imgui.dylib`
* `osx-arm64` `royale_imgui` -> `libroyale_imgui.dylib`
* `osx-arm64` `libblurgtext` -> `libblurgtext.dylib`
* `linux-x64` `box3d` -> `libbox3d.so`

Box3D runtime-native copy rules are centralized in `Royale.Box3D`, so clients, servers, and tests receive Box3D through their project reference. The client output includes SDL3, Box3D, the ImGui shim, and BlurgText. The server output includes Box3D only and must remain free of SDL, ImGui, BlurgText, textures, shaders, and UI native libraries. WattleScript remains a future automated gameplay test-host dependency and must not be copied into client or server runtime outputs.