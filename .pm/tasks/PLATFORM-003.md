---
id: PLATFORM-003
title: Establish the fixed-update loop
track: PLATFORM
milestone: M0
createdAt: 2026-07-04T09:21:26.8449280Z
modifiedAt: 2026-07-04T09:22:33.6208450Z
---

Separate event processing, fixed-rate simulation, variable-rate rendering, and bounded catch-up behavior.

## Implementation Notes

- Client loop now uses SDL performance counters for per-frame timing.
- Fixed update cadence is 60 Hz with catch-up capped at 4 ticks per rendered frame.
- Event polling and input frame reset happen once per rendered frame before fixed ticks.
- Rendering/presenting still happens once per rendered frame; the fixed update hook is intentionally empty until simulation work lands.
- Window title diagnostics show averaged FPS, fixed ticks in the most recent frame, total fixed tick count, and mouse capture state.
- Architecture wiki already documented the fixed-timestep loop and bounded catch-up model, so no wiki update was needed.

## Validation

- `dotnet build Royale.slnx -m:1 --no-restore` passed.
- `dotnet test Royale.slnx -m:1 --no-restore` passed.
- Launched the SDL client with `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-restore`; the client reached the running window loop and was then interrupted from the terminal to avoid leaving a live process.