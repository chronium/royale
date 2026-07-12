using System.Runtime.InteropServices;
using Evergine.Bindings.Imgui;

namespace Royale.Rendering.UI;

public static unsafe class ImGuiDockBuilder
{
    private const string LibraryName = "royale_imgui";
    public static void RemoveNode(uint id) => DockBuilderRemoveNode(id);
    public static uint AddDockSpace(uint id) => DockBuilderAddNode(id, (ImGuiDockNodeFlags)(1 << 10));
    public static uint Split(uint id, ImGuiDir direction, float ratio, out uint remainder)
    {
        uint split; uint opposite;
        DockBuilderSplitNode(id, direction, ratio, &split, &opposite);
        remainder = opposite;
        return split;
    }
    public static void DockWindow(string name, uint id) => DockBuilderDockWindow(name, id);
    public static void SetNodeSize(uint id, Evergine.Mathematics.Vector2 size) => DockBuilderSetNodeSize(id, size);
    public static void Finish(uint id) => DockBuilderFinish(id);

    [DllImport(LibraryName, EntryPoint = "igDockBuilderRemoveNode", CallingConvention = CallingConvention.Cdecl)] private static extern void DockBuilderRemoveNode(uint id);
    [DllImport(LibraryName, EntryPoint = "igDockBuilderAddNode", CallingConvention = CallingConvention.Cdecl)] private static extern uint DockBuilderAddNode(uint id, ImGuiDockNodeFlags flags);
    [DllImport(LibraryName, EntryPoint = "igDockBuilderSplitNode", CallingConvention = CallingConvention.Cdecl)] private static extern uint DockBuilderSplitNode(uint id, ImGuiDir direction, float ratio, uint* split, uint* remainder);
    [DllImport(LibraryName, EntryPoint = "igDockBuilderDockWindow", CallingConvention = CallingConvention.Cdecl)] private static extern void DockBuilderDockWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string name, uint id);
    [DllImport(LibraryName, EntryPoint = "igDockBuilderSetNodeSize", CallingConvention = CallingConvention.Cdecl)] private static extern void DockBuilderSetNodeSize(uint id, Evergine.Mathematics.Vector2 size);
    [DllImport(LibraryName, EntryPoint = "igDockBuilderFinish", CallingConvention = CallingConvention.Cdecl)] private static extern void DockBuilderFinish(uint id);
}
