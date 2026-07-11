---
id: EDITOR-001
title: Extract the shared desktop platform layer
track: EDITOR
milestone: M6
priority: high
createdAt: 2026-07-11T18:46:37.9511440Z
modifiedAt: 2026-07-11T19:04:20.0794980Z
---

Move reusable SDL window, event, timing, input plumbing, and desktop application lifecycle behavior out of Royale.Client while preserving client behavior and keeping graphical dependencies out of the server.

## Notes

- 2026-07-11 19:04 UTC - Implementation completed for shared SDL desktop extraction. Added Royale.Platform and Royale.Platform.Tests; moved SDL window/relative-mouse ownership, InputState, and FixedUpdateAccumulator; added validated window/loop settings, frame/fixed time contracts, ISdlDesktopApplication, and SdlDesktopHost. Refactored the client to callbacks while retaining SDL GPU, ImGui, rendering, networking, gameplay, telemetry, and screenshots in Royale.Client. SdlWindow exposes a checked nint NativeHandle; no InternalsVisibleTo was added.

  Validation: `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` succeeded. `dotnet build Royale.slnx -m:1 --no-restore` succeeded with 0 warnings/errors. `dotnet test Royale.slnx -m:1 --no-restore` passed all 954 tests, including 22 Royale.Platform.Tests. Forbidden-boundary scan for Client, Content, Simulation, Protocol, Network, rendering, and ImGui references under src/tests Royale.Platform returned no matches. Native smoke `dotnet run --project src/Royale.Client/Royale.Client.csproj --no-restore --no-build -- --config config/client.development.json --offline --screenshot /tmp/editor-001-smoke.bmp --screenshot-after-frames 30` exited 0, logged SDL/window/GPU initialization and clean shutdown, and produced a valid rendered 1920x1080 screenshot.

  Wiki: updated architecture/editor with the implemented callback lifecycle and architecture/overview with solution/dependency boundaries.

  Owner validation remains required for hands-on window resizing, relative-mouse/input behavior, and platform feel. Task remains doing until that validation is confirmed.