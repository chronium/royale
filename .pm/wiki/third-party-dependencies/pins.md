---
title: Third-Party Dependency Pins
createdAt: 2026-07-05T16:15:06.4182160Z
modifiedAt: 2026-07-12T18:32:29.5471590Z
---

## Version Pins

Pinned revisions live in `thirdparty/versions.env`.

Current pins:

| Dependency | Repository | Commit | Purpose |
| --- | --- | --- | --- |
| SDL3-CS | `https://github.com/ppy/SDL3-CS` | `a0a5276a874c0c48db705696ab7e2adc8b5db0a1` | C# bindings and native availability for SDL3. |
| box3d | `https://github.com/erincatto/box3d` | `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f` | Physics library source for future project-specific native builds and bindings. |
| ImGui.Net | `https://github.com/EvergineTeam/ImGui.Net` | `1f97beecfc9b83e1549e9782757cf85b1777cb9d` | C# ImGui bindings for client development UI. Patched to remove the unnecessary `System.Runtime.CompilerServices.Unsafe` package reference that caused SDK warning `NU1510`. |
| BlurgText | `https://github.com/CallumDev/blurgtext` | `ea49c33b27ad55cc811dc8be4c9829ed4367d936` | Game-facing text outside ImGui. |
| SimpleMesh | `https://github.com/CallumDev/SimpleMesh` | `9f46341e35fa5876fbea7b96bd021bc3abd7842d` | Managed mesh import source staged for future client rendering work. |
| WattleScript | `https://github.com/WattleScript/wattlescript` | `b8ccc1930733c25c8a25e6087fc29a4c555562fe` | Interpreter source staged for automated gameplay test orchestration. |
| LiteNetLib | `https://github.com/RevenantX/LiteNetLib` | `37cbf5ab608a4dbd0e491c528a0c14c1e09f1cba` | Managed UDP networking source staged for `NET-001` transport work. |

Use full commit SHAs, not branch names, tags, or floating references.

Native SDL3 is not pinned separately at this stage. Until platform packaging tasks prove a different requirement, SDL3 native availability is expected to come through the selected SDL3-CS source.

SimpleMesh retains its existing pinned revision. `GAME-012` adds `thirdparty/patches/SimpleMesh/0001-support-unsigned-byte-gltf-indices.patch`, a focused managed-code fix that accepts glTF `UNSIGNED_BYTE` (`5121`) index accessors as permitted by glTF 2.0 and used by the Kenney Prototype Kit environment models. No dependency pin, package, runtime boundary, or native build step changes.

Managed package pins live centrally in `Directory.Packages.props`. `StbImageWriteSharp 1.16.7` encodes PNG screenshots and thumbnail caches, and `StbImageSharp 2.30.15` decodes cached PNGs. Both are .NET Standard 2.0 C# ports of Sean Barrett's stb single-header libraries, are used only by `Royale.Rendering`, and retain the upstream public-domain/MIT dual-license dedication. They introduce no server, simulation, protocol, or content dependency.