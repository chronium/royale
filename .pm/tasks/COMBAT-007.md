---
id: COMBAT-007
title: Add static training dummy diagnostics
track: COMBAT
milestone: M3
priority: medium
dependsOn:
- COMBAT-004
- UI-002
createdAt: 2026-07-06T12:58:02.3915820Z
modifiedAt: 2026-07-06T12:58:12.0360440Z
---

Add a static offline training dummy that can receive weapon damage through the combat pipeline and expose health plus recent damage history in an ImGui diagnostics window for validating weapon behavior.

## Intent

The training dummy is a development target for checking weapon behavior as combat becomes more refined. It should make damage, cadence, range, falloff, future hit regions, and future randomness observable without requiring a second real player.

## Requirements

- Add one static training dummy to the offline combat sandbox after basic health and damage exist.
- The dummy must be damaged through the same combat damage path used for players or future authoritative targets; do not mutate dummy health directly from input or UI shortcuts.
- Give the dummy simple health state and enough collision/query representation for hitscan shots to target it.
- Add an ImGui diagnostics window showing current health and recent damage history.
- Damage history entries should preserve fields useful for later tuning: tick or time, weapon id, raw damage, final applied damage, remaining health, hit distance, hit point, and optional placeholders for hit region, falloff multiplier, and random modifier when those systems exist.
- Keep the first version static. No AI, movement, animation, respawn UX, loot, networking, server replication, or final player HUD is required.
- Do not add an in-world health bar in this task. Health display stays in ImGui until the project has a game-facing text/UI path.

## Validation

- Tests cover dummy health initialization, damage application through the combat path, death or zero-health behavior if COMBAT-004 defines it, and damage-history ordering/capacity.
- A local interactive or screenshot smoke check shows the ImGui diagnostics window updating after dummy damage.
- `dotnet build Royale.slnx -m:1 --no-restore` passes.
- `dotnet test Royale.slnx -m:1 --no-restore` passes.
- PM project validation passes.

## Human Validation

Ask the project owner to play a short combat loop and validate that the dummy diagnostics make weapon damage and cadence understandable.

## Notes

- 2026-07-06 - Implemented a client-owned offline `training-dummy` fixture, not map-authored content, server-authoritative state, networking state, AI, final HUD, or respawn gameplay.
- The local offline player resolves rifle hitscan against static map collision and the dummy capsule target. Static collision still blocks farther dummy targets. Applied dummy damage goes through `DamageController.Apply()`; dead dummy hits remain queryable but do not append damage history under COMBAT-004 no-op rules.
- Damage history stores the 16 most recent applied entries newest-first with tick, weapon id, raw damage, applied damage, remaining health, hit distance, hit point, and nullable placeholders for hit region, falloff multiplier, and random modifier.
- Added an ImGui `Training Dummy` diagnostics window with health/alive state, recent damage history, and a diagnostics-only reset button that restores dummy health and clears history. This reset is not gameplay input and is not authoritative combat behavior.
- Added debug primitive capsule rendering for the dummy so F6/F7 modes expose the target location.
- Updated `architecture/physics-and-combat` with the offline dummy behavior, damage history, reset exception, and debug visualization.
- Validation: `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui.Net `NU1510` warning; `dotnet test Royale.slnx -m:1 --no-restore` passed with the same warning; `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-build -- --screenshot /tmp/royale-training-dummy.bmp --screenshot-after-frames 2` passed and the converted PNG showed the `Training Dummy` ImGui window with health, reset, and empty recent-damage state; PM project validation passed.