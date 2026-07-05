---
id: PLATFORM-001
title: Open an SDL3 window
track: PLATFORM
milestone: M0
createdAt: 2026-07-04T09:21:26.6617060Z
modifiedAt: 2026-07-04T09:22:33.6204060Z
---

Create a resizable high-DPI SDL3 window with event handling, keyboard input, and relative mouse support.

## Implementation Notes

Added the first real `Royale.Client` runtime around SDL3:

- `SdlApplication` initializes SDL video, creates the window, polls events, updates input state, updates the diagnostic title, and shuts down SDL cleanly.
- `SdlWindow` wraps the native SDL window pointer, logical size, aspect ratio, title updates, and destruction.
- `RelativeMouseMode` toggles SDL window-relative mouse mode and cursor visibility.
- `InputState` tracks key and mouse down/pressed/released transitions plus accumulated per-frame mouse deltas.

Window behavior:

- Title starts as `Royale`.
- Initial size is `1280x720`.
- Window flags are SDL resizable plus high pixel density.
- Quit events and window close requests exit the loop.
- `F1` toggles relative mouse capture.
- `Escape` releases mouse capture when captured, otherwise exits.
- The title is periodically updated with basic FPS and mouse capture state.

The task stayed platform-only: no SDL GPU device, renderer, ImGui, gameplay simulation, or network behavior was added.

`Royale.Client` references the pinned fetched SDL3-CS source project at `thirdparty/repos/SDL3-CS/SDL3-CS/SDL3-CS.csproj`. The reference is constrained to `TargetFramework=net8.0`; the binding itself is restored with `CI_DONT_TARGET_ANDROID=1` for desktop-only work.

Added `Royale.Client.Tests` for SDL-independent input behavior and registered it in `Royale.slnx`.

Updated `AGENTS.md` and the `third-party-dependencies` wiki page with the SDL3-CS restore command and third-party MSBuild boundary files.

## Validation

- `sh -n thirdparty/fetch-all.sh thirdparty/fetch-sdl3-cs.sh thirdparty/fetch-box3d.sh thirdparty/fetch-imgui-net.sh`
- `sh thirdparty/fetch-sdl3-cs.sh` fetched pinned SDL3-CS after network escalation.
- `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1`
- `dotnet build Royale.slnx -m:1 --no-restore`
- `dotnet test Royale.slnx -m:1 --no-restore`
- `dotnet list src/Royale.Server/Royale.Server.csproj reference` confirms the server references only Simulation, Protocol, Content, and Box3D.
- `rg "SDL|Royale.Client|ImGui|Rendering" src/Royale.Server tests/Royale.Server.Tests -n` found no server-side SDL, client, ImGui, or rendering references.

Manual interactive window verification with `dotnet run --project src/Royale.Client --no-restore` was not performed in this non-interactive agent session. It should be run on a machine with display access to confirm the window opens, resizes, `F1` toggles capture, `Escape` release/quit behavior works, and the close button exits cleanly.