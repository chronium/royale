---
id: PLATFORM-007
title: Add deterministic freecam launch overrides
track: PLATFORM
milestone: M2
priority: medium
dependsOn:
- PLATFORM-005
createdAt: 2026-07-06T19:14:19.4748080Z
modifiedAt: 2026-07-06T19:14:23.5380820Z
---

Add launch arguments for deterministic validation captures: choose initial camera mode, start in freecam when requested, and override the freecam position plus look-at target without mutating authoritative gameplay state.

## Implementation Notes

Accepted camera launch arguments:

- `--camera-mode gameplay|freecam`
- `--camera-position x,y,z`
- `--camera-look-at x,y,z`

Camera vectors are parsed with invariant culture as exactly three finite comma-separated floats. Position and look-at overrides require `--camera-mode freecam`; gameplay startup remains the default when no camera arguments are supplied. The overrides are applied only to client presentation freecam state and do not mutate local gameplay/player state.

Validation command used for the Kenney crate capture:

```sh
dotnet run --project src/Royale.Client/Royale.Client.csproj -p:CI_DONT_TARGET_ANDROID=1 -- --offline --map graybox --camera-mode freecam --camera-position 4,2.2,3 --camera-look-at 1.75,0.7,-1.35 --screenshot /tmp/royale-crate.bmp --screenshot-after-frames 5
```

Automated validation passed with:

```sh
dotnet build Royale.slnx -m:1 --no-restore
dotnet test Royale.slnx -m:1 --no-restore
```

The crate screenshot was visually checked after converting `/tmp/royale-crate.bmp` to `/tmp/royale-crate.png`; it frames the crate with the current default debug overlays/wireframes visible. Documentation was updated in `README.md` and `architecture/content-and-rendering`.