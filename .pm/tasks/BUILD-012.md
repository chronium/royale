---
id: BUILD-012
title: Add SimpleMesh third-party dependency
track: BUILD
milestone: M1
priority: medium
createdAt: 2026-07-06T16:50:20.7698030Z
modifiedAt: 2026-07-06T16:50:32.5791980Z
---

Add SimpleMesh as a pinned managed third-party source dependency using the existing thirdparty workflow. Pin repository `https://github.com/CallumDev/SimpleMesh` at commit `9f46341e35fa5876fbea7b96bd021bc3abd7842d`. Add `SIMPLEMESH_REPO` and `SIMPLEMESH_COMMIT` to `thirdparty/versions.env`, create `thirdparty/fetch-simplemesh.sh`, wire it into `thirdparty/fetch-all.sh`, create `thirdparty/patches/SimpleMesh/README.md`, and update third-party documentation and wiki pages. Note that SimpleMesh is Apache-2.0 licensed, managed-only at this pin, targets `net8.0`, supports OBJ, Collada, and embedded-buffer glTF/glb, imports Y-up geometry, and does not require a native build artifact.

## Completion Notes

- Added SimpleMesh pin `9f46341e35fa5876fbea7b96bd021bc3abd7842d` from `https://github.com/CallumDev/SimpleMesh` to `thirdparty/versions.env`.
- Added `thirdparty/fetch-simplemesh.sh`, wired it after BlurgText in `thirdparty/fetch-all.sh`, and added the `thirdparty/patches/SimpleMesh/README.md` placeholder. No SimpleMesh submodules, native build steps, project references, solution entries, or mesh loading code were added.
- Updated `thirdparty/README.md` and PM wiki pages `third-party-dependencies/overview`, `third-party-dependencies/pins`, `third-party-dependencies/workflow`, and `third-party-dependencies/layout`.
- Validation passed: `sh thirdparty/fetch-simplemesh.sh`, `sh thirdparty/fetch-all.sh`, `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1`, `dotnet build Royale.slnx -m:1 --no-restore`, `dotnet test Royale.slnx -m:1 --no-restore`, and PM `validate_project`.
- Restore/build/test reported the existing ImGui.Net `NU1510` warning for `System.Runtime.CompilerServices.Unsafe`; no errors.