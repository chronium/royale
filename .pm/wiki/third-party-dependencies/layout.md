---
title: Third-Party Dependency Layout
createdAt: 2026-07-05T16:14:33.2369430Z
modifiedAt: 2026-07-05T19:06:18Z
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
  build-imgui-macos.sh
  fetch-sdl3-cs.sh
  fetch-box3d.sh
  fetch-imgui-net.sh
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

The current ImGui macOS ARM64 shared-library workflow uses:

```text
thirdparty/build/imgui/osx-arm64/
thirdparty/artifacts/imgui/osx-arm64/lib/libroyale_imgui.dylib
```

Project packaging tasks may copy selected final native binaries into project-controlled runtime or packaging directories later, but generated third-party build directories should remain ignored.

## Runtime Native Layout

Managed projects load bundled native libraries from the standard `.NET` runtime-native layout under each build output:

```text
runtimes/osx-arm64/native/libSDL3.dylib
runtimes/osx-arm64/native/libbox3d.dylib
runtimes/osx-arm64/native/libroyale_imgui.dylib
```

`Royale.Native` owns the shared import-name resolver for this layout. BUILD-004 intentionally supports only macOS ARM64 mappings:

* `SDL3` -> `libSDL3.dylib`
* `box3d` -> `libbox3d.dylib`
* `cimgui` -> `libroyale_imgui.dylib`
* `royale_imgui` -> `libroyale_imgui.dylib`

The client output includes SDL3, Box3D, and the ImGui shim. The server output includes Box3D only and must remain free of SDL and ImGui native libraries.
