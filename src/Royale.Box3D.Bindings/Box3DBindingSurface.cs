using System.Runtime.InteropServices;

namespace Royale.Box3D.Bindings;

public static class Box3DBindingSurface
{
    public const string NativeLibraryName = "box3d";

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultWorldDef", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3WorldDef b3DefaultWorldDef();

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateWorld", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3WorldId b3CreateWorld(in B3WorldDef def);

    [DllImport(NativeLibraryName, EntryPoint = "b3DestroyWorld", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3DestroyWorld(B3WorldId worldId);

    [DllImport(NativeLibraryName, EntryPoint = "b3GetWorldCount", CallingConvention = CallingConvention.Cdecl)]
    public static extern int b3GetWorldCount();

    [DllImport(NativeLibraryName, EntryPoint = "b3GetMaxWorldCount", CallingConvention = CallingConvention.Cdecl)]
    public static extern int b3GetMaxWorldCount();

    [DllImport(NativeLibraryName, EntryPoint = "b3World_IsValid", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool b3World_IsValid(B3WorldId id);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_Step", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3World_Step(B3WorldId worldId, float timeStep, int subStepCount);
}
