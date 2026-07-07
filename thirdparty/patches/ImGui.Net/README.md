# ImGui.Net Patches

Place ordered project-specific patches for `thirdparty/repos/ImGui.Net` in this directory.

## 0001-remove-unnecessary-unsafe-package-reference.patch

Removes the `System.Runtime.CompilerServices.Unsafe` package reference from
`Generator/Evergine.Bindings.Imgui/Evergine.Bindings.Imgui.csproj`.

The pinned binding project targets `net10.0`, and the .NET SDK reports this
package as unnecessary with warning `NU1510`. Removing the reference keeps the
binding build clean without changing Royale runtime code.
