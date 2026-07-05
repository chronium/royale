---
id: PHYS-002
title: Produce shared Box3D libraries
track: PHYS
milestone: M0
createdAt: 2026-07-04T09:21:46.5385610Z
modifiedAt: 2026-07-04T09:22:33.6215650Z
---

Create reproducible native builds for macOS, Linux, and Windows shared-library formats.

## Notes

- Scope for this task completion is macOS ARM64 only.
- Linux and Windows shared-library outputs are deferred to later platform-specific work.
- Added `thirdparty/build-box3d-macos.sh` to refresh the pinned Box3D source and install a Release shared-library build to `thirdparty/artifacts/box3d/osx-arm64/`.
- Validation command:

```sh
sh thirdparty/build-box3d-macos.sh
```

- Expected output:

```text
thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.dylib
```

- Binary verification commands:

```sh
file thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.dylib
otool -L thirdparty/artifacts/box3d/osx-arm64/lib/libbox3d.dylib
```

- Verification result:
  - `sh thirdparty/build-box3d-macos.sh` passed on macOS ARM64 after network escalation for the pinned Box3D fetch.
  - `file` reported `Mach-O 64-bit dynamically linked shared library arm64`.
  - `otool -L` reported `@rpath/libbox3d.0.dylib` and `/usr/lib/libSystem.B.dylib`.
  - `thirdparty/build/`, `thirdparty/artifacts/`, and `thirdparty/repos/` remain ignored generated output.
  - Existing .NET build and test commands were not run because no tracked .NET files changed.
