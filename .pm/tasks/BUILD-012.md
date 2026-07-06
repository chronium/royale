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