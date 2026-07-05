---
id: PHYS-001
title: Build unmodified Box3D samples
track: PHYS
milestone: M0
createdAt: 2026-07-04T09:21:46.4506740Z
modifiedAt: 2026-07-04T09:22:33.6214460Z
---

Build and run the upstream Box3D samples on macOS ARM64 and Linux x64 before introducing bindings.

## Validation Notes

Validated the pinned upstream Box3D source on macOS ARM64 only. Linux x64 validation is intentionally deferred for now by project-owner direction.

Machine/tooling summary:

- Host: Darwin `25.5.0` on `arm64`
- CMake: `4.3.0`
- Xcode: `26.6` (`17F113`)
- Box3D commit: `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f`

Commands run from the repository root unless noted:

```sh
sh thirdparty/fetch-box3d.sh
```

Commands run from `thirdparty/repos/box3d`:

```sh
cmake --preset macos
cmake --build --preset macos-release
./build/bin/Release/samples --frames 3
```

Results:

- Fetch checked out pinned upstream Box3D commit `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f`.
- Configure succeeded with AppleClang on Darwin arm64.
- Release build succeeded and produced `build/bin/Release/samples`.
- Bounded sample run exited `0` with output: `samples: 3 frames, 0 sokol errors`.

Notes:

- The first sandboxed configure/build attempts were blocked by host compiler/Xcode filesystem access; rerunning the same upstream commands with approved host access succeeded.
- The first sandboxed sample launch hung during macOS GUI startup; rerunning the same bounded sample command with approved host GUI access succeeded.
- No project-specific Box3D patches were introduced.
- No .NET project files changed, so no .NET build or test was required.