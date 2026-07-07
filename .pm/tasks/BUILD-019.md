---
id: BUILD-019
title: Remove ImGui.Net NU1510 build warning
track: BUILD
milestone: M0
createdAt: 2026-07-07T08:27:43.3438080Z
modifiedAt: 2026-07-07T08:27:43.3438080Z
---

Stop the recurring NU1510 warning from the vendored ImGui.Net binding build without changing client runtime behavior. Prefer a focused third-party patch if the warning originates in the pinned source checkout.

## Implementation Notes

- Added `thirdparty/patches/ImGui.Net/0001-remove-unnecessary-unsafe-package-reference.patch`.
- The patch removes the `System.Runtime.CompilerServices.Unsafe` package reference from `Generator/Evergine.Bindings.Imgui/Evergine.Bindings.Imgui.csproj` in the pinned ImGui.Net source checkout.
- Updated `thirdparty/patches/ImGui.Net/README.md`, `thirdparty/README.md`, and wiki page `third-party-dependencies/pins` to document the patch.
- No Royale runtime code, project references, or ImGui integration behavior changed.

## Validation Notes

- `sh thirdparty/fetch-imgui-net.sh` completed and applied the patch cleanly after resetting the ignored ImGui.Net checkout.
- `dotnet restore Royale.slnx -p:CI_DONT_TARGET_ANDROID=1` passed.
- `dotnet build Royale.slnx -m:1 --no-restore` passed with `0 Warning(s)` and `0 Error(s)`.
- `dotnet test Royale.slnx -m:1 --no-restore` passed with 480 tests.