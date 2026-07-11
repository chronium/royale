using System.Runtime.InteropServices;
using Royale.Native;

namespace Royale.Box3D.Bindings.Interop;

public static class Box3DBindingSurface
{
    public const string NativeLibraryName = "box3d";
    public const ulong B3DefaultMaskBits = ulong.MaxValue;

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

    [DllImport(NativeLibraryName, EntryPoint = "b3World_Draw", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3World_Draw(B3WorldId worldId, ref B3DebugDraw draw, ulong maskBits);

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultDebugDraw", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3DebugDraw b3DefaultDebugDraw();

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultQueryFilter", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3QueryFilter b3DefaultQueryFilter();

    [DllImport(NativeLibraryName, EntryPoint = "b3World_OverlapAABB", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3TreeStats b3World_OverlapAABB(
        B3WorldId worldId,
        B3Aabb aabb,
        B3QueryFilter filter,
        [MarshalAs(UnmanagedType.FunctionPtr)] B3OverlapResultFcn fcn,
        nint context);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_OverlapShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3TreeStats b3World_OverlapShape(
        B3WorldId worldId,
        B3Pos origin,
        in B3ShapeProxy proxy,
        B3QueryFilter filter,
        [MarshalAs(UnmanagedType.FunctionPtr)] B3OverlapResultFcn fcn,
        nint context);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_CastRay", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3TreeStats b3World_CastRay(
        B3WorldId worldId,
        B3Pos origin,
        B3Vec3 translation,
        B3QueryFilter filter,
        [MarshalAs(UnmanagedType.FunctionPtr)] B3CastResultFcn fcn,
        nint context);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_CastRayClosest", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3RayResult b3World_CastRayClosest(
        B3WorldId worldId,
        B3Pos origin,
        B3Vec3 translation,
        B3QueryFilter filter);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_CastShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3TreeStats b3World_CastShape(
        B3WorldId worldId,
        B3Pos origin,
        in B3ShapeProxy proxy,
        B3Vec3 translation,
        B3QueryFilter filter,
        [MarshalAs(UnmanagedType.FunctionPtr)] B3CastResultFcn fcn,
        nint context);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_CastMover", CallingConvention = CallingConvention.Cdecl)]
    public static extern float b3World_CastMover(
        B3WorldId worldId,
        B3Pos origin,
        in B3Capsule mover,
        B3Vec3 translation,
        B3QueryFilter filter,
        [MarshalAs(UnmanagedType.FunctionPtr)] B3MoverFilterFcn? fcn,
        nint context);

    [DllImport(NativeLibraryName, EntryPoint = "b3World_CollideMover", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3World_CollideMover(
        B3WorldId worldId,
        B3Pos origin,
        in B3Capsule mover,
        B3QueryFilter filter,
        [MarshalAs(UnmanagedType.FunctionPtr)] B3PlaneResultFcn fcn,
        nint context);

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultBodyDef", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BodyDef b3DefaultBodyDef();

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BodyId b3CreateBody(B3WorldId worldId, in B3BodyDef def);

    [DllImport(NativeLibraryName, EntryPoint = "b3DestroyBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3DestroyBody(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_IsValid", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool b3Body_IsValid(B3BodyId id);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetType", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BodyType b3Body_GetType(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_SetType", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3Body_SetType(B3BodyId bodyId, B3BodyType type);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetPosition", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3Pos b3Body_GetPosition(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetRotation", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3Quat b3Body_GetRotation(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetTransform", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3WorldTransform b3Body_GetTransform(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_SetTransform", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3Body_SetTransform(B3BodyId bodyId, B3Pos position, B3Quat rotation);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_GetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3Vec3 b3Body_GetLinearVelocity(B3BodyId bodyId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Body_SetLinearVelocity", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3Body_SetLinearVelocity(B3BodyId bodyId, B3Vec3 linearVelocity);

    [DllImport(NativeLibraryName, EntryPoint = "b3DefaultShapeDef", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3ShapeDef b3DefaultShapeDef();

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateCapsuleShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3ShapeId b3CreateCapsuleShape(B3BodyId bodyId, in B3ShapeDef def, in B3Capsule capsule);

    [DllImport(NativeLibraryName, EntryPoint = "b3MakeBoxHull", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BoxHull b3MakeBoxHull(float hx, float hy, float hz);

    [DllImport(NativeLibraryName, EntryPoint = "b3MakeCubeHull", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BoxHull b3MakeCubeHull(float halfWidth);

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateHull", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe B3HullData* b3CreateHull(B3Vec3* points, int pointCount, int maxVertexCount);

    [DllImport(NativeLibraryName, EntryPoint = "b3DestroyHull", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void b3DestroyHull(B3HullData* hull);

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateHullShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3ShapeId b3CreateHullShape(B3BodyId bodyId, in B3ShapeDef def, in B3HullData hull);

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateMesh", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe B3MeshData* b3CreateMesh(
        in B3MeshDef def,
        int* degenerateTriangleIndices,
        int degenerateCapacity);

    [DllImport(NativeLibraryName, EntryPoint = "b3DestroyMesh", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void b3DestroyMesh(B3MeshData* mesh);

    [DllImport(NativeLibraryName, EntryPoint = "b3GetHeight", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int b3GetHeight(B3MeshData* mesh);

    [DllImport(NativeLibraryName, EntryPoint = "b3CreateMeshShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe B3ShapeId b3CreateMeshShape(
        B3BodyId bodyId,
        in B3ShapeDef def,
        B3MeshData* mesh,
        B3Vec3 scale);

    [DllImport(NativeLibraryName, EntryPoint = "b3DestroyShape", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3DestroyShape(B3ShapeId shapeId, [MarshalAs(UnmanagedType.I1)] bool updateBodyMass);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_IsValid", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool b3Shape_IsValid(B3ShapeId id);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_GetType", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3ShapeType b3Shape_GetType(B3ShapeId shapeId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_GetBody", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3BodyId b3Shape_GetBody(B3ShapeId shapeId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_GetWorld", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3WorldId b3Shape_GetWorld(B3ShapeId shapeId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_IsSensor", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool b3Shape_IsSensor(B3ShapeId shapeId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_GetFilter", CallingConvention = CallingConvention.Cdecl)]
    public static extern B3Filter b3Shape_GetFilter(B3ShapeId shapeId);

    [DllImport(NativeLibraryName, EntryPoint = "b3Shape_SetFilter", CallingConvention = CallingConvention.Cdecl)]
    public static extern void b3Shape_SetFilter(
        B3ShapeId shapeId,
        B3Filter filter,
        [MarshalAs(UnmanagedType.I1)] bool invokeContacts);
}
