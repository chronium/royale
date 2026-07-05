---
title: Third-Party Dependency Layout
createdAt: 2026-07-05T16:14:33.2369430Z
modifiedAt: 2026-07-05T16:14:33.2369430Z
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

## Build Artifacts

Native build outputs should go under ignored directories such as:

```text
thirdparty/build/
thirdparty/artifacts/
```

Project packaging tasks may copy selected final native binaries into project-controlled runtime or packaging directories later, but generated third-party build directories should remain ignored.