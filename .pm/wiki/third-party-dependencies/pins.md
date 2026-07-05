---
title: Third-Party Dependency Pins
createdAt: 2026-07-05T16:15:06.4182160Z
modifiedAt: 2026-07-05T16:15:06.4182160Z
---

## Version Pins

Pinned revisions live in `thirdparty/versions.env`.

Current pins:

| Dependency | Repository | Commit | Purpose |
| --- | --- | --- | --- |
| SDL3-CS | `https://github.com/ppy/SDL3-CS` | `a0a5276a874c0c48db705696ab7e2adc8b5db0a1` | C# bindings and native availability for SDL3. |
| box3d | `https://github.com/erincatto/box3d` | `540ea387b0c02bf714fbfdcc8fb88c039c35fe6f` | Physics library source for future project-specific native builds and bindings. |
| ImGui.Net | `https://github.com/EvergineTeam/ImGui.Net` | `1f97beecfc9b83e1549e9782757cf85b1777cb9d` | C# ImGui bindings for client development UI. |

Use full commit SHAs, not branch names, tags, or floating references.

Native SDL3 is not pinned separately at this stage. Until platform packaging tasks prove a different requirement, SDL3 native availability is expected to come through the selected SDL3-CS source.