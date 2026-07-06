---
id: COMBAT-006
title: Add weapon feedback
track: COMBAT
milestone: M3
dependsOn:
- COMBAT-005
createdAt: 2026-07-04T09:21:58.8223690Z
modifiedAt: 2026-07-06T08:28:00.6782200Z
---

Add minimal recoil, muzzle indication, hit markers, and impact visualization.

## Completion Notes

- Added client-owned `WeaponFeedbackState` for local offline rifle shots. It records one transient shot with origin, tracer end, shot direction, hit type, target/static id metadata, optional damage result, and render-time lifetime.
- Feedback emits only when the existing rifle cadence path fires. Dead-player fixed updates do not emit feedback, and debug respawn clears active and last feedback.
- Added presentation-only recoil. The recoil offset is applied only while creating the render camera, decays by render-frame time, and does not mutate `PlayerLookState`, hitscan direction, cadence, or damage behavior.
- Added debug-line muzzle, tracer, and impact markers through `DebugSceneBuilder`, so they are visible only in existing debug primitive render modes.
- Added ImGui `Player` diagnostics text for last shot result, transient hit marker, hit id, applied damage, and feedback lifetime.
- Updated `architecture/physics-and-combat` with the COMBAT-006 client-only feedback contract.

## Follow-up Refinement

- Increased inspectable weapon feedback lifetime to `3.0` seconds so hit markers and tracer direction remain visible long enough to inspect.
- Added explicit shot direction metadata for debug feedback.
- Moved the debug muzzle marker `0.35m` forward from the camera-origin shot point. The muzzle marker is at the tracer start, not the ray end.
- Set muzzle marker size to `0.048m`; the impact marker remains at the ray end.
- The tracer starts at the forward-offset muzzle marker and remains visible for the same inspectable feedback lifetime.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed with the existing ImGui `NU1510` warning.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
