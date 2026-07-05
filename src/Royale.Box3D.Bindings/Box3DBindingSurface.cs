using System.Runtime.InteropServices;
using Royale.Native;

namespace Royale.Box3D.Bindings;

public static class Box3DBindingSurface
{
    public const string NativeLibraryName = "box3d";

    static Box3DBindingSurface()
    {
        NativeLibraryResolver.ConfigureForAssembly(typeof(Box3DBindingSurface).Assembly);
    }

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

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultBodyDef", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BodyDef b3DefaultBodyDef();

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BodyId b3CreateBody(B3WorldId worldId, in B3BodyDef def);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_IsValid", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool b3Body_IsValid(B3BodyId id);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetPosition", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3Pos b3Body_GetPosition(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetRotation", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3Quat b3Body_GetRotation(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultShapeDef", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3ShapeDef b3DefaultShapeDef();

    [DllImport(NativeLibraryName, EntryPoint = "b3MakeBoxHull", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BoxHull b3MakeBoxHull(float hx, float hy, float hz);

    [DllImport(NativeLibraryName, EntryPoint = "b3MakeCubeHull", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BoxHull b3MakeCubeHull(float halfWidth);

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateHullShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3ShapeId b3CreateHullShape(B3BodyId bodyId, in B3ShapeDef def, in B3HullData hull);
}
