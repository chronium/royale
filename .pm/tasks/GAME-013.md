---
id: GAME-013
title: Author the Kenney prototype combat arena
track: GAME
milestone: M6
priority: medium
dependsOn:
- GAME-012
createdAt: 2026-07-10T13:32:22.6275470Z
modifiedAt: 2026-07-10T14:01:28.2513230Z
---

Add prototype-arena as a second explicitly selectable 40×40 m map using the validated Kenney Prototype Kit environment set. Preserve graybox defaults, server-authoritative static collision, current map schema and packaging boundaries, retain a hidden primitive ground fallback, add 12 validated spawns and eight placeholder loot points, cover map loading/collision/raycast/spawn behavior with tests, capture normal and collision-debug overview screenshots, and document content/rendering and map behavior. Depends on GAME-012.

## Notes

- 2026-07-10 14:01 UTC - Authored `prototype-arena` as a second explicit 40×40 Kenney map with unchanged graybox defaults/bounds/safe-zone settings, 12 inward-facing spawn candidates, eight loot placeholders, 45 model instances across west crate yard/north raised platform/south doorway compound/east column courtyard/central crossroads, four model perimeter walls, a visible triangle-mesh floor, and one hidden primitive fallback ground. Added map, scene-batching, collider-count, raycast, doorway-opening, and all-spawn reservation tests. Added deterministic validation-only `--render-view` and `--hide-telemetry` launch controls without changing defaults. Updated architecture/content-and-rendering map documentation.
  
  Validation passed: `dotnet build Royale.slnx -m:1 --no-restore` (0 warnings/errors); `dotnet test Royale.slnx -m:1 --no-restore` (all suites: AssetPipeline 18, Box3D 54, Client 287, Content 5, Diagnostics 8, Gameplay 116, Native 8, Network 67, Protocol 70, Server 164, Simulation 82); Linux x64 Docker passed Box3D 54, Simulation 82, Server 164; explicit server smoke `dotnet run --project src/Royale.Server/Royale.Server.csproj --no-build --no-restore -- --map prototype-arena --run-ticks 1` passed. Captured and visually inspected 1920×1080 `/tmp/prototype-arena-normal.bmp` and `/tmp/prototype-arena-debug.bmp` (PNG inspection copies beside them). Project-owner validation is still requested for layout readability, traversal, stairs/slope feel, collision alignment, and combat sightlines.