---
id: BUILD-004
title: Create native library resolver
track: BUILD
milestone: M1
createdAt: 2026-07-04T09:21:21.8531300Z
modifiedAt: 2026-07-05T19:06:18Z
---

Load the correct bundled SDL3 and Box3D native libraries for each supported runtime identifier.

## Notes

Implemented a shared `Royale.Native` resolver for macOS ARM64. It maps `SDL3`, `box3d`, `cimgui`, and `royale_imgui` imports to bundled files under `runtimes/osx-arm64/native/` and reports the RID plus expected path when a mapped library is missing.

The client copies SDL3, Box3D, and `libroyale_imgui.dylib` to the runtime-native layout. The server receives Box3D only and no SDL/ImGui native libraries. Box3D tests load `libbox3d.dylib` from `runtimes/osx-arm64/native/`.

Validation:

* `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1`
* `dotnet build Royale.slnx -m:1 --no-restore`
* `dotnet test Royale.slnx -m:1 --no-restore`
* `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-build -- --screenshot /tmp/royale-build-004.bmp --screenshot-after-frames 2`
