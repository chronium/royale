using System.Runtime.InteropServices;

namespace Royale.Editor.Platform;

internal static class ImGuiEditorNative
{
    private const string LibraryName = "royale_imgui";

    public static void ClearActiveId() => ClearActiveIdNative();

    [DllImport(LibraryName, EntryPoint = "igClearActiveID", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ClearActiveIdNative();
}
