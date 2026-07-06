---
id: PHYS-010
title: Package native binaries with the client and server
track: PHYS
milestone: M1
priority: medium
dependsOn:
- BUILD-004
- PHYS-009
createdAt: 2026-07-04T09:21:47.2706290Z
modifiedAt: 2026-07-06T05:38:14.2577760Z
---

Include the correct Box3D native binary in every produced client and server artifact.

## Notes

- 2026-07-06 05:38 UTC - Implemented Box3D native packaging for osx-arm64 and linux-x64 by centralizing runtime-native copy rules in Royale.Box3D. Removed the duplicate Box3D native copy rule from Royale.Box3D.Tests. Added thirdparty/build-box3d-linux.sh, linux-x64 resolver mapping, and deterministic resolver path tests for macOS ARM64, Linux x64, unsupported RIDs, and unsupported imports. Validation: dotnet build Royale.slnx -m:1 --no-restore passed locally on macOS; dotnet test Royale.slnx -m:1 --no-restore passed locally on macOS; Docker linux/amd64 validation with mcr.microsoft.com/dotnet/sdk:10.0 built thirdparty/artifacts/box3d/linux-x64/lib/libbox3d.so, restored, built, tested, and verified libbox3d.so under client, server, and Royale.Box3D.Tests runtime-native outputs. The server runtime output contains Box3D only for native libraries introduced by this task; no SDL, ImGui shim, shaders, textures, or other client-only assets were added to the server.